using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KenneyDoorConnectedSidewalkBuilder
{
    const string ScenePath = "Assets/Scenes/Kenney_ExpandedCity.unity";
    const string BackupPath = "Assets/Scenes/Kenney_ExpandedCity_BeforeDoorSidewalkV1.unity";
    const string CatalogPath = "Assets/Data/Maps/BuildingEntranceCatalog.json";
    const string TilePath = "Assets/Art/Kenney/kenney_city-kit-roads_2.1/Models/FBX format/tile-low.fbx";
    const string NetworkName = "SIDEWALK_NETWORK__KENNEY_ROADS";

    [Serializable] class Catalog { public float sidewalkWidth, doorApronLength, routeClearance, pathCellSize; public Entry[] entries; }
    [Serializable] class Entry { public string model; public Entrance[] entrances; }
    [Serializable] class Entrance { public string face; public float lateral, doorWidth; }

    [MenuItem("EchoZone/Rebuild Door Connected Sidewalks")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open Kenney_ExpandedCity first.");
        if (scene.isDirty && !EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save current scene.");
        if (!File.Exists(BackupPath) && !AssetDatabase.CopyAsset(ScenePath, BackupPath)) throw new IOException("Sidewalk backup failed.");

        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(CatalogPath));
        Validate(catalog);
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var entry in catalog.entries) entries[entry.model] = entry;
        var tile = AssetDatabase.LoadAssetAtPath<GameObject>(TilePath);
        if (tile == null) throw new InvalidOperationException("Kenney tile-low.fbx is missing.");

        Transform city = null;
        foreach (var root in scene.GetRootGameObjects()) if (root.name == "CITY_GRID_8x8") city = root.transform;
        if (city == null) throw new InvalidOperationException("CITY_GRID_8x8 was not found.");
        var old = city.Find(NetworkName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        var network = new GameObject(NetworkName).transform;
        network.SetParent(city, false);

        int districts = 0, entrances = 0, routes = 0, unreachable = 0, strips = 0;
        foreach (Transform district in city)
        {
            if (!district.name.StartsWith("District_", StringComparison.Ordinal)) continue;
            if (!TryDistrictBounds(district, out var walkBounds)) continue;
            var buildings = FindBuildings(district, entries);
            if (buildings.Count == 0) continue;
            var group = new GameObject("Door_Paths_" + district.name).transform;
            group.SetParent(network, false);
            float y = walkBounds.max.y + .04f;

            var obstacles = BuildObstacles(district, buildings, catalog.routeClearance);
            var field = BuildDistanceField(walkBounds, obstacles, catalog.pathCellSize, catalog.sidewalkWidth);
            var horizontal = new Dictionary<int, HashSet<int>>();
            var vertical = new Dictionary<int, HashSet<int>>();

            AddPerimeter(tile, group, walkBounds, y, catalog.sidewalkWidth);
            strips += 4;
            foreach (var building in buildings)
            {
                var entry = entries[building.name];
                Bounds localBounds = LocalBounds(building);
                foreach (var entrance in entry.entrances)
                {
                    entrances++;
                    Vector3 localFace = Face(entrance.face);
                    Vector3 localLateral = Mathf.Abs(localFace.z) > .5f ? Vector3.right : Vector3.forward;
                    float faceExtent = Mathf.Abs(localFace.x) > .5f ? localBounds.extents.x : localBounds.extents.z;
                    float lateralExtent = Mathf.Abs(localLateral.x) > .5f ? localBounds.extents.x : localBounds.extents.z;
                    Vector3 localDoor = localBounds.center + localFace * faceExtent + localLateral * (entrance.lateral * lateralExtent);
                    localDoor.y = localBounds.min.y;
                    Vector3 door = building.TransformPoint(localDoor);
                    Vector3 outward = building.TransformDirection(localFace); outward.y = 0; outward.Normalize();
                    Vector3 apronEnd = door + outward * catalog.doorApronLength;
                    int searchRadius = Mathf.Max(field.width, field.height);
                    if (!field.TryNearestPassable(apronEnd, searchRadius, out int sx, out int sz)) { unreachable++; continue; }
                    var path = field.Trace(sx, sz);
                    if (path.Count == 0) { unreachable++; continue; }
                    AddSegment(tile, group, "Door entrance apron", door, field.World(path[0].x, path[0].y), y + .012f, Mathf.Max(catalog.sidewalkWidth, entrance.doorWidth));
                    strips++;
                    for (int i = 1; i < path.Count; i++)
                    {
                        var a = path[i - 1]; var b = path[i];
                        if (a.y == b.y) AddEdge(horizontal, a.y, Mathf.Min(a.x, b.x));
                        else AddEdge(vertical, a.x, Mathf.Min(a.y, b.y));
                    }
                    routes++;
                }
            }
            strips += RenderEdges(tile, group, field, horizontal, vertical, y, catalog.sidewalkWidth);
            districts++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Door sidewalk scene save failed.");
        AssetDatabase.SaveAssets();
        Selection.activeTransform = network;
        Debug.Log($"DOOR_SIDEWALKS_COMPLETE: districts={districts}, catalogEntrances={entrances}, routes={routes}, unreachable={unreachable}, strips={strips}");
    }

    static void Validate(Catalog c)
    {
        if (c == null || c.entries == null || c.entries.Length == 0) throw new InvalidOperationException("Entrance catalog is empty.");
        if (c.sidewalkWidth <= 0 || c.doorApronLength <= 0 || c.pathCellSize <= 0) throw new InvalidOperationException("Entrance catalog dimensions are invalid.");
    }

    static List<Transform> FindBuildings(Transform district, Dictionary<string, Entry> entries)
    {
        var result = new List<Transform>();
        foreach (var t in district.GetComponentsInChildren<Transform>(true))
            if (entries.ContainsKey(t.name) && PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject) == t.gameObject) result.Add(t);
        return result;
    }

    static List<Rect> BuildObstacles(Transform district, List<Transform> buildings, float clearance)
    {
        var obstacles = new List<Rect>();
        foreach (var building in buildings)
        {
            Bounds b = WorldBounds(building.GetComponentsInChildren<Renderer>());
            obstacles.Add(Rect.MinMaxRect(b.min.x - clearance, b.min.z - clearance, b.max.x + clearance, b.max.z + clearance));
        }
        foreach (var t in district.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Maze cross barrier", StringComparison.Ordinal)) continue;
            var r = t.GetComponent<Renderer>(); if (r == null) continue;
            Bounds b = r.bounds;
            obstacles.Add(Rect.MinMaxRect(b.min.x - clearance, b.min.z - clearance, b.max.x + clearance, b.max.z + clearance));
        }
        return obstacles;
    }

    sealed class Field
    {
        public readonly Bounds bounds; public readonly float cell; public readonly int width, height; public readonly int[,] distance; public readonly bool[,] blocked;
        public Field(Bounds b, float c, int w, int h) { bounds = b; cell = c; width = w; height = h; distance = new int[w, h]; blocked = new bool[w, h]; }
        public Vector3 World(int x, int z) => new Vector3(bounds.min.x + (x + .5f) * cell, 0, bounds.min.z + (z + .5f) * cell);
        public bool TryNearestPassable(Vector3 world, int radius, out int rx, out int rz)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt((world.x - bounds.min.x) / cell), 0, width - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((world.z - bounds.min.z) / cell), 0, height - 1);
            for (int r = 0; r <= radius; r++) for (int z = -r; z <= r; z++) for (int x = -r; x <= r; x++)
            {
                if (Mathf.Abs(x) != r && Mathf.Abs(z) != r) continue;
                int nx = cx + x, nz = cz + z;
                if (nx >= 0 && nz >= 0 && nx < width && nz < height && !blocked[nx, nz] && distance[nx, nz] >= 0) { rx = nx; rz = nz; return true; }
            }
            rx = rz = -1; return false;
        }
        public List<Vector2Int> Trace(int x, int z)
        {
            var path = new List<Vector2Int>(); if (distance[x, z] < 0) return path;
            path.Add(new Vector2Int(x, z));
            while (distance[x, z] > 0 && path.Count < width * height)
            {
                int best = distance[x, z], bx = x, bz = z;
                Try(x - 1, z, ref best, ref bx, ref bz); Try(x + 1, z, ref best, ref bx, ref bz); Try(x, z - 1, ref best, ref bx, ref bz); Try(x, z + 1, ref best, ref bx, ref bz);
                if (bx == x && bz == z) break;
                x = bx; z = bz; path.Add(new Vector2Int(x, z));
            }
            return path;
        }
        void Try(int x, int z, ref int best, ref int bx, ref int bz) { if (x >= 0 && z >= 0 && x < width && z < height && distance[x, z] >= 0 && distance[x, z] < best) { best = distance[x, z]; bx = x; bz = z; } }
    }

    static Field BuildDistanceField(Bounds bounds, List<Rect> obstacles, float cell, float sidewalkWidth)
    {
        int w = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / cell)), h = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / cell));
        var f = new Field(bounds, cell, w, h);
        for (int x = 0; x < w; x++) for (int z = 0; z < h; z++)
        {
            f.distance[x, z] = -1; Vector3 p = f.World(x, z);
            foreach (var obstacle in obstacles) if (obstacle.Contains(new Vector2(p.x, p.z))) { f.blocked[x, z] = true; break; }
        }
        var queue = new Queue<Vector2Int>(); int margin = Mathf.Max(1, Mathf.CeilToInt(sidewalkWidth / cell));
        for (int x = 0; x < w; x++) for (int z = 0; z < h; z++) if (x < margin || z < margin || x >= w - margin || z >= h - margin) Seed(f, x, z, queue);
        int[] dx = { -1, 1, 0, 0 }, dz = { 0, 0, -1, 1 };
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            for (int i = 0; i < 4; i++) { int nx = p.x + dx[i], nz = p.y + dz[i]; if (nx < 0 || nz < 0 || nx >= w || nz >= h || f.blocked[nx, nz] || f.distance[nx, nz] >= 0) continue; f.distance[nx, nz] = f.distance[p.x, p.y] + 1; queue.Enqueue(new Vector2Int(nx, nz)); }
        }
        return f;
    }

    static void Seed(Field f, int x, int z, Queue<Vector2Int> q) { if (!f.blocked[x, z] && f.distance[x, z] < 0) { f.distance[x, z] = 0; q.Enqueue(new Vector2Int(x, z)); } }
    static void AddEdge(Dictionary<int, HashSet<int>> map, int line, int edge) { if (!map.TryGetValue(line, out var set)) map[line] = set = new HashSet<int>(); set.Add(edge); }

    static int RenderEdges(GameObject tile, Transform parent, Field f, Dictionary<int, HashSet<int>> horizontal, Dictionary<int, HashSet<int>> vertical, float y, float width)
    {
        int count = 0;
        foreach (var pair in horizontal) count += RenderRuns(tile, parent, f, pair.Key, pair.Value, true, y, width);
        foreach (var pair in vertical) count += RenderRuns(tile, parent, f, pair.Key, pair.Value, false, y, width);
        return count;
    }

    static int RenderRuns(GameObject tile, Transform parent, Field f, int line, HashSet<int> edges, bool horizontal, float y, float width)
    {
        var sorted = new List<int>(edges); sorted.Sort(); int count = 0;
        for (int i = 0; i < sorted.Count;)
        {
            int start = sorted[i], end = start; while (i + 1 < sorted.Count && sorted[i + 1] == end + 1) { i++; end++; }
            Vector3 a = horizontal ? f.World(start, line) : f.World(line, start);
            Vector3 b = horizontal ? f.World(end + 1, line) : f.World(line, end + 1);
            AddSegment(tile, parent, "Connected door route", a, b, y, width); count++; i++;
        }
        return count;
    }

    static void AddPerimeter(GameObject tile, Transform parent, Bounds b, float y, float width)
    {
        float i = width * .5f;
        AddTile(tile, parent, "West perimeter sidewalk", new Vector3(b.min.x + i, y, b.center.z), width, b.size.z, 0);
        AddTile(tile, parent, "East perimeter sidewalk", new Vector3(b.max.x - i, y, b.center.z), width, b.size.z, 0);
        AddTile(tile, parent, "South perimeter sidewalk", new Vector3(b.center.x, y + .006f, b.min.z + i), b.size.x, width, 0);
        AddTile(tile, parent, "North perimeter sidewalk", new Vector3(b.center.x, y + .006f, b.max.z - i), b.size.x, width, 0);
    }

    static void AddSegment(GameObject tile, Transform parent, string name, Vector3 a, Vector3 b, float y, float width)
    {
        Vector3 center = (a + b) * .5f; center.y = y;
        float dx = Mathf.Abs(b.x - a.x), dz = Mathf.Abs(b.z - a.z);
        AddTile(tile, parent, name, center, dx > dz ? dx + width : width, dz >= dx ? dz + width : width, 0);
    }

    static void AddTile(GameObject asset, Transform parent, string name, Vector3 center, float sizeX, float sizeZ, float yaw)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset); go.name = name; go.transform.SetParent(parent, true); go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var renderers = go.GetComponentsInChildren<Renderer>(); Bounds b = WorldBounds(renderers); var s = go.transform.localScale;
        s.x *= sizeX / Mathf.Max(.001f, b.size.x); s.z *= sizeZ / Mathf.Max(.001f, b.size.z); go.transform.localScale = s;
        b = WorldBounds(renderers); go.transform.position += center - new Vector3(b.center.x, b.min.y, b.center.z);
    }

    static bool TryDistrictBounds(Transform district, out Bounds bounds)
    {
        foreach (Transform child in district) if (child.name == "Walkable alley district" || child.name == "Plaza pavement") { var r = child.GetComponent<Renderer>(); if (r != null) { bounds = r.bounds; return true; } }
        bounds = default; return false;
    }

    static Bounds LocalBounds(Transform root)
    {
        bool initialized = false; Bounds result = default;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue; Bounds b = filter.sharedMesh.bounds;
            foreach (var corner in Corners(b)) { Vector3 p = root.InverseTransformPoint(filter.transform.TransformPoint(corner)); if (!initialized) { result = new Bounds(p, Vector3.zero); initialized = true; } else result.Encapsulate(p); }
        }
        if (!initialized) throw new InvalidOperationException("Building has no mesh: " + root.name); return result;
    }

    static IEnumerable<Vector3> Corners(Bounds b) { for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2) yield return b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)); }
    static Bounds WorldBounds(Renderer[] renderers) { if (renderers.Length == 0) throw new InvalidOperationException("Missing renderer."); Bounds b = renderers[0].bounds; for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds); return b; }
    static Vector3 Face(string face) { switch (face) { case "PosZ": return Vector3.forward; case "NegZ": return Vector3.back; case "PosX": return Vector3.right; case "NegX": return Vector3.left; default: throw new InvalidOperationException("Unknown entrance face " + face); } }
}
