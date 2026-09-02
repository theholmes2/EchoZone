using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KenneyEntrancePreviewGenerator
{
    const string ModelRoot = "Assets/Art/Kenney/kenney_city-kit-commercial_2.1/Models/FBX format/";
    const string OutputRoot = "Docs/Previews/BuildingEntrances";
    static readonly string[] Models = {
        "building-a","building-b","building-c","building-d","building-e","building-f","building-g",
        "building-h","building-i","building-j","building-k","building-l","building-m","building-n",
        "building-skyscraper-a","building-skyscraper-b","building-skyscraper-c","building-skyscraper-d","building-skyscraper-e"
    };

    [MenuItem("EchoZone/Generate Building Entrance Turntables")]
    public static void Generate()
    {
        Directory.CreateDirectory(OutputRoot);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var cameraObject = new GameObject("Entrance Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>();
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f, .21f, .24f);
            camera.orthographic = true;
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 1000;

            AddLight(preview, new Vector3(40, 60, -30), 1.35f);
            AddLight(preview, new Vector3(-35, 30, 40), .75f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.7f, .72f, .75f);

            foreach (string modelName in Models)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot + modelName + ".fbx");
                if (asset == null) throw new InvalidOperationException("Missing " + modelName);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, preview);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                Bounds bounds = BoundsOf(instance.GetComponentsInChildren<Renderer>());
                instance.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                bounds = BoundsOf(instance.GetComponentsInChildren<Renderer>());

                var sheet = new Texture2D(1600, 480, TextureFormat.RGB24, false);
                Vector3[] cameraSides = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
                for (int view = 0; view < 4; view++)
                {
                    var image = Render(camera, bounds, cameraSides[view]);
                    sheet.SetPixels(view * 400, 0, 400, 480, image.GetPixels());
                    UnityEngine.Object.DestroyImmediate(image);
                }
                sheet.Apply();
                File.WriteAllBytes(Path.Combine(OutputRoot, modelName + "__PosZ_NegZ_PosX_NegX.png"), sheet.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(sheet);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        AssetDatabase.Refresh();
        Debug.Log("ENTRANCE_TURNTABLES_COMPLETE: " + Models.Length);
    }

    static void AddLight(Scene scene, Vector3 euler, float intensity)
    {
        var go = new GameObject("Preview Light");
        SceneManager.MoveGameObjectToScene(go, scene);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        go.transform.rotation = Quaternion.Euler(euler);
    }

    static Texture2D Render(Camera camera, Bounds bounds, Vector3 side)
    {
        float vertical = Mathf.Max(bounds.size.y, side.x == 0 ? bounds.size.x : bounds.size.z);
        camera.orthographicSize = vertical * .62f;
        Vector3 center = bounds.center;
        camera.transform.position = center + side * Mathf.Max(bounds.size.x, bounds.size.z) * 3;
        camera.transform.LookAt(center + Vector3.up * bounds.size.y * -.05f);
        var rt = new RenderTexture(400, 480, 24);
        camera.targetTexture = rt;
        camera.Render();
        var old = RenderTexture.active;
        RenderTexture.active = rt;
        var image = new Texture2D(400, 480, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 400, 480), 0, 0);
        image.Apply();
        RenderTexture.active = old;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt);
        return image;
    }

    static Bounds BoundsOf(Renderer[] renderers)
    {
        if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderer.");
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
