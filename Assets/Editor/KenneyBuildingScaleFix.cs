using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class KenneyBuildingScaleFix
{
    const string ScenePath="Assets/Scenes/Kenney_ExpandedCity.unity";
    const string CharacterPath="Assets/Art/Kenney/kenney_mini-characters/Models/FBX format/character-male-a.fbx";
    const string Marker="Docs/Previews/Kenney_BuildingScaleFix.done";
    const string TrafficRepairMarker="Docs/Previews/Kenney_TrafficWidthRepair.done";
    const string VisualScaleMarker="Docs/Previews/Kenney_BuildingVisualScaleV2.done";
    const string VisualScaleV3Marker="Docs/Previews/Kenney_BuildingVisualScaleV3.done";
    const string RoadClearanceV4Marker="Docs/Previews/Kenney_RoadClearanceV4.done";
    const string LayoutPath="Assets/Data/Maps/ExpandedCityLayout.json";
    const string CarsPath="Assets/Art/Kenney/kenney_car-kit/Models/FBX format/";

    [System.Serializable]
    sealed class ScaleLayout
    {
        public int districtColumns,districtRows,mazeNodes,trafficSeed;
        public float alleyWidth,buildingWidth,roadWidth,referenceCharacterHeight,cityBuildingScaleMultiplier,modelGroundOffset;
        public float trafficSegmentChance,trafficVehicleScaleMultiplier,trafficVehicleWidth,trafficLaneClearance,trafficRoadShoulder;
        public string[] trafficModels;
    }

    [MenuItem("EchoZone/Repair Building Road Clearance From Bounds")]
    public static void RepairRoadClearanceFromBounds()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(ScenePath))return;
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty){Debug.LogWarning("Save the city before repairing road clearance.");return;}
        var previous=SceneManager.GetActiveScene();if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var city=FindRoot(scene,"CITY_GRID_8x8");if(city==null)return;
            var layout=JsonUtility.FromJson<ScaleLayout>(File.ReadAllText(LayoutPath));Validate(layout);
            const string backup="Assets/Scenes/Kenney_ExpandedCity_BeforeRoadClearanceV4.unity";
            if(!File.Exists(backup)&&!AssetDatabase.CopyAsset(ScenePath,backup))throw new IOException("Road clearance V4 backup failed.");
            var streets=city.transform.Find("00_Connected_Street_Grid");
            float roadWidth=streets.Find("North south road 0").localScale.x;
            float currentBlock=(layout.mazeNodes-1)*(layout.buildingWidth*2+layout.alleyWidth)+layout.alleyWidth;
            float currentPitch=currentBlock+roadWidth,maxHalfExtent=0;
            foreach(Transform district in city.transform)
            {
                if(!district.name.StartsWith("District_")||!TryDistrictIndex(district.name,out int x,out int z))continue;
                Vector3 center=new Vector3((x-3.5f)*currentPitch,0,(z-3.5f)*currentPitch);
                foreach(var root in district.GetComponentsInChildren<Transform>(true))
                {
                    if(!IsBuildingRoot(root.name))continue;
                    var renderers=root.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                    Bounds bounds=BoundsOf(renderers);
                    maxHalfExtent=Mathf.Max(maxHalfExtent,Mathf.Abs(bounds.min.x-center.x),Mathf.Abs(bounds.max.x-center.x),Mathf.Abs(bounds.min.z-center.z),Mathf.Abs(bounds.max.z-center.z));
                }
            }
            float requiredBlock=Mathf.Max(currentBlock,2*(maxHalfExtent+layout.alleyWidth*.5f));
            float requiredPitch=requiredBlock+roadWidth;
            foreach(Transform district in city.transform)
            {
                if(!district.name.StartsWith("District_")||!TryDistrictIndex(district.name,out int x,out int z))continue;
                district.position+=new Vector3((x-3.5f)*(requiredPitch-currentPitch),0,(z-3.5f)*(requiredPitch-currentPitch));
            }
            float extent=layout.districtColumns*requiredPitch+roadWidth;
            foreach(Transform road in streets)
            {
                int index=TrailingIndex(road.name);
                if(road.name.StartsWith("North south road"))
                {road.position=new Vector3((index-layout.districtColumns*.5f)*requiredPitch,road.position.y,0);road.localScale=new Vector3(roadWidth,road.localScale.y,extent);}
                else if(road.name.StartsWith("East west road"))
                {road.position=new Vector3(0,road.position.y,(index-layout.districtRows*.5f)*requiredPitch);road.localScale=new Vector3(extent,road.localScale.y,roadWidth);}
            }
            city.transform.Find("Ground").localScale=new Vector3(extent,city.transform.Find("Ground").localScale.y,extent);
            var oldTraffic=city.transform.Find("Road_Traffic_Props");if(oldTraffic!=null)Object.DestroyImmediate(oldTraffic.gameObject);
            int traffic=PlaceTraffic(city.transform,scene,layout,roadWidth,layout.trafficVehicleWidth,requiredBlock);
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Road clearance V4 save failed.");
            File.WriteAllText(RoadClearanceV4Marker,$"traffic={traffic}; oldBlock={currentBlock:F2}; requiredBlock={requiredBlock:F2}; measuredHalfExtent={maxHalfExtent:F2}");
            Debug.Log($"BUILDING_ROAD_CLEARANCE_V4: traffic={traffic}, oldBlock={currentBlock:F2}, requiredBlock={requiredBlock:F2}, measuredHalfExtent={maxHalfExtent:F2}");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }

    [MenuItem("EchoZone/Double Current Building Scale")]
    public static void DoubleCurrentBuildingScale()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(ScenePath))return;
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty){Debug.LogWarning("Save the city before doubling building scale.");return;}
        var previous=SceneManager.GetActiveScene();if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var city=FindRoot(scene,"CITY_GRID_8x8");if(city==null)return;
            var layout=JsonUtility.FromJson<ScaleLayout>(File.ReadAllText(LayoutPath));Validate(layout);
            const string backup="Assets/Scenes/Kenney_ExpandedCity_BeforeBuildingDoubleV3.unity";
            if(!File.Exists(backup)&&!AssetDatabase.CopyAsset(ScenePath,backup))throw new IOException("Building V3 backup failed.");
            float oldStep=layout.buildingWidth+layout.alleyWidth;
            float newStep=layout.buildingWidth*2+layout.alleyWidth;
            float oldBlock=(layout.mazeNodes-1)*oldStep+layout.alleyWidth;
            float newBlock=(layout.mazeNodes-1)*newStep+layout.alleyWidth;
            float currentRoadWidth=city.transform.Find("00_Connected_Street_Grid/North south road 0").localScale.x;
            ReflowDistrictContent(city.transform,layout,oldBlock,newBlock,currentRoadWidth);
            int count=0;
            foreach(var transform in city.GetComponentsInChildren<Transform>(true))
            {
                if(transform==city.transform||!IsBuildingRoot(transform.name))continue;
                var renderers=transform.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                Bounds before=BoundsOf(renderers);Vector3 center=before.center;
                transform.localScale*=2f;
                Bounds after=BoundsOf(renderers);
                transform.position+=new Vector3(center.x-after.center.x,layout.modelGroundOffset-after.min.y,center.z-after.center.z);count++;
            }
            var oldTraffic=city.transform.Find("Road_Traffic_Props");if(oldTraffic!=null)Object.DestroyImmediate(oldTraffic.gameObject);
            int traffic=PlaceTraffic(city.transform,scene,layout,currentRoadWidth,layout.trafficVehicleWidth,newBlock);
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Building V3 save failed.");
            File.WriteAllText(VisualScaleV3Marker,$"buildings={count}; traffic={traffic}; block={newBlock:F2}; currentScaleMultiplier={layout.cityBuildingScaleMultiplier:F2}");
            Debug.Log($"BUILDING_VISUAL_SCALE_V3: buildings={count}, traffic={traffic}, block={newBlock:F2}, multiplier={layout.cityBuildingScaleMultiplier:F2}");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }

    [MenuItem("EchoZone/Repair Building Visual Scale From Door")]
    public static void RepairBuildingVisualScale()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(ScenePath))return;
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty){Debug.LogWarning("Save the city before repairing building visual scale.");return;}
        var previous=SceneManager.GetActiveScene();if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var city=FindRoot(scene,"CITY_GRID_8x8");if(city==null)return;
            var layout=JsonUtility.FromJson<ScaleLayout>(File.ReadAllText(LayoutPath));Validate(layout);
            const string backup="Assets/Scenes/Kenney_ExpandedCity_BeforeBuildingVisualScaleV2.unity";
            if(!File.Exists(backup)&&!AssetDatabase.CopyAsset(ScenePath,backup))throw new IOException("Visual scale backup failed.");
            int count=0;
            foreach(var transform in city.GetComponentsInChildren<Transform>(true))
            {
                if(transform==city.transform||!IsBuildingRoot(transform.name))continue;
                var renderers=transform.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                Bounds before=BoundsOf(renderers);Vector3 center=before.center;
                transform.localScale*=layout.cityBuildingScaleMultiplier;
                Bounds after=BoundsOf(renderers);
                transform.position+=new Vector3(center.x-after.center.x,layout.modelGroundOffset-after.min.y,center.z-after.center.z);
                count++;
            }
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Visual scale save failed.");
            File.WriteAllText(VisualScaleMarker,$"buildings={count}; multiplier={layout.cityBuildingScaleMultiplier:F2}");
            Debug.Log($"BUILDING_VISUAL_SCALE_V2: buildings={count}, multiplier={layout.cityBuildingScaleMultiplier:F2}");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }

    [MenuItem("EchoZone/Repair Two Lane Traffic Width")]
    public static void RepairTrafficWidth()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(ScenePath))return;
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty){Debug.LogWarning("Save the city before repairing traffic width.");return;}
        var previous=SceneManager.GetActiveScene();if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var city=FindRoot(scene,"CITY_GRID_8x8");if(city==null)return;
            var layout=JsonUtility.FromJson<ScaleLayout>(File.ReadAllText(LayoutPath));Validate(layout);
            var streets=city.transform.Find("00_Connected_Street_Grid");
            float currentRoadWidth=streets.Find("North south road 0").localScale.x;
            float targetRoadWidth=2*(layout.trafficVehicleWidth+layout.trafficLaneClearance)+2*layout.trafficRoadShoulder;
            ReflowTwoLaneGrid(city.transform,layout,currentRoadWidth,targetRoadWidth);
            var traffic=city.transform.Find("Road_Traffic_Props");if(traffic!=null)Object.DestroyImmediate(traffic.gameObject);
            int count=PlaceTraffic(city.transform,scene,layout,targetRoadWidth,layout.trafficVehicleWidth);
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Traffic repair save failed.");
            File.WriteAllText(TrafficRepairMarker,$"traffic={count}; vehicleWidth={layout.trafficVehicleWidth:F2}; roadWidth={targetRoadWidth:F2}");
            Debug.Log($"TRAFFIC_WIDTH_REPAIRED: traffic={count}, vehicle={layout.trafficVehicleWidth:F2}, road={targetRoadWidth:F2}");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }

    [MenuItem("EchoZone/Fix City Building Scale From Character")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(ScenePath))return;
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty){Debug.LogWarning("Save the city before fixing building scale.");return;}
        var previous=SceneManager.GetActiveScene();if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var city=FindRoot(scene,"CITY_GRID_8x8");if(city==null||city.transform.Find("Building_Scale_Fixed")!=null)return;
            const string backup="Assets/Scenes/Kenney_ExpandedCity_BeforeBuildingScaleFix.unity";
            if(!File.Exists(backup)&&!AssetDatabase.CopyAsset(ScenePath,backup))throw new IOException("Scene backup failed.");
            var layout=JsonUtility.FromJson<ScaleLayout>(File.ReadAllText(LayoutPath));Validate(layout);
            float sourceCharacterHeight=MeasurePrefabHeight(CharacterPath,scene);
            float commonScale=layout.referenceCharacterHeight/sourceCharacterHeight;
            float vehicleWidth=layout.trafficVehicleWidth;
            float twoLaneRoadWidth=2*(vehicleWidth+layout.trafficLaneClearance)+2*layout.trafficRoadShoulder;
            ReflowTwoLaneGrid(city.transform,layout,layout.roadWidth,twoLaneRoadWidth);
            int count=0;
            foreach(var transform in city.GetComponentsInChildren<Transform>(true))
            {
                if(transform==city.transform||!IsBuildingRoot(transform.name))continue;
                var renderers=transform.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                Bounds before=BoundsOf(renderers);Vector3 intendedCenter=before.center;
                transform.localScale=Vector3.one*commonScale;
                Bounds after=BoundsOf(renderers);
                transform.position+=new Vector3(intendedCenter.x-after.center.x,layout.modelGroundOffset-after.min.y,intendedCenter.z-after.center.z);
                count++;
            }
            int traffic=PlaceTraffic(city.transform,scene,layout,twoLaneRoadWidth,vehicleWidth);
            var marker=new GameObject("Building_Scale_Fixed");marker.transform.SetParent(city.transform);
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Scene save failed.");
            File.WriteAllText(Marker,$"buildings={count}; traffic={traffic}; twoLaneRoadWidth={twoLaneRoadWidth:F2}; vehicleWidth={vehicleWidth:F2}; characterHeight={layout.referenceCharacterHeight}; commonScale={commonScale:F4}");
            Debug.Log($"BUILDING_SCALE_FIXED: buildings={count}, traffic={traffic}, road={twoLaneRoadWidth:F2}, vehicle={vehicleWidth:F2}, scale={commonScale:F4}");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }

    static GameObject FindRoot(Scene scene,string name)
    {foreach(var go in scene.GetRootGameObjects())if(go.name==name)return go;return null;}

    static bool IsBuildingRoot(string name)
    {return name.StartsWith("building-")||name.StartsWith("low-detail-building-");}

    static void ReflowDistrictContent(Transform city,ScaleLayout layout,float oldBlock,float newBlock,float roadWidth)
    {
        float oldPitch=oldBlock+roadWidth,newPitch=newBlock+roadWidth;
        foreach(Transform district in city)
        {
            if(!district.name.StartsWith("District_")||!TryDistrictIndex(district.name,out int x,out int z))continue;
            Vector3 oldCenter=new Vector3((x-3.5f)*oldPitch,0,(z-3.5f)*oldPitch);
            Vector3 newCenter=new Vector3((x-3.5f)*newPitch,0,(z-3.5f)*newPitch);
            float ratio=(newBlock-layout.alleyWidth)/(oldBlock-layout.alleyWidth);
            foreach(Transform child in district)
            {
                if(child.name=="Randomized_Lots")
                {
                    foreach(Transform lot in child)lot.position=newCenter+(lot.position-oldCenter)*ratio;
                    continue;
                }
                child.position=newCenter+(child.position-oldCenter)*ratio;
                if(child.name=="Walkable alley district"||child.name=="Plaza pavement")
                    child.localScale=new Vector3(newBlock,child.localScale.y,newBlock);
            }
        }
        var streets=city.Find("00_Connected_Street_Grid");
        float extent=layout.districtColumns*newPitch+roadWidth;
        foreach(Transform road in streets)
        {
            int index=TrailingIndex(road.name);
            if(road.name.StartsWith("North south road"))
            {road.position=new Vector3((index-layout.districtColumns*.5f)*newPitch,road.position.y,0);road.localScale=new Vector3(roadWidth,road.localScale.y,extent);}
            else if(road.name.StartsWith("East west road"))
            {road.position=new Vector3(0,road.position.y,(index-layout.districtRows*.5f)*newPitch);road.localScale=new Vector3(extent,road.localScale.y,roadWidth);}
        }
        var ground=city.Find("Ground");ground.localScale=new Vector3(extent,ground.localScale.y,extent);
    }

    static void Validate(ScaleLayout layout)
    {
        if(layout==null||layout.referenceCharacterHeight<=0||layout.cityBuildingScaleMultiplier<=0||layout.modelGroundOffset<0||layout.trafficModels==null||layout.trafficModels.Length==0
            ||layout.trafficSegmentChance<0||layout.trafficSegmentChance>1||layout.trafficVehicleScaleMultiplier<=0||layout.trafficVehicleWidth<=0
            ||layout.trafficLaneClearance<0||layout.trafficRoadShoulder<0)
            throw new System.InvalidOperationException("Invalid building and traffic scale data.");
    }

    static void ReflowTwoLaneGrid(Transform city,ScaleLayout layout,float oldRoadWidth,float roadWidth)
    {
        float block=(layout.mazeNodes-1)*(layout.buildingWidth+layout.alleyWidth)+layout.alleyWidth;
        float oldPitch=block+oldRoadWidth,newPitch=block+roadWidth;
        foreach(Transform child in city)
        {
            if(child.name.StartsWith("District_")&&TryDistrictIndex(child.name,out int x,out int z))
                child.position+=new Vector3((x-3.5f)*(newPitch-oldPitch),0,(z-3.5f)*(newPitch-oldPitch));
            else if(child.name=="Ground")child.localScale=new Vector3(layout.districtColumns*newPitch+roadWidth,child.localScale.y,layout.districtRows*newPitch+roadWidth);
            else if(child.name=="00_Connected_Street_Grid")
            {
                foreach(Transform road in child)
                {
                    int index=TrailingIndex(road.name);
                    if(road.name.StartsWith("North south road"))
                    {road.position=new Vector3((index-layout.districtColumns*.5f)*newPitch,road.position.y,0);road.localScale=new Vector3(roadWidth,road.localScale.y,layout.districtRows*newPitch+roadWidth);}
                    else if(road.name.StartsWith("East west road"))
                    {road.position=new Vector3(0,road.position.y,(index-layout.districtRows*.5f)*newPitch);road.localScale=new Vector3(layout.districtColumns*newPitch+roadWidth,road.localScale.y,roadWidth);}
                }
            }
        }
    }

    static bool TryDistrictIndex(string name,out int x,out int z)
    {
        x=z=0;string[] parts=name.Split('_');
        return parts.Length>=3&&int.TryParse(parts[1],out x)&&int.TryParse(parts[2],out z)&&(--x>=0)&&(--z>=0);
    }

    static int TrailingIndex(string name)
    {int split=name.LastIndexOf(' ');return int.Parse(name.Substring(split+1));}

    static int PlaceTraffic(Transform city,Scene scene,ScaleLayout layout,float roadWidth,float vehicleWidth,float blockOverride=0)
    {
        var group=new GameObject("Road_Traffic_Props").transform;group.SetParent(city);
        var random=new System.Random(layout.trafficSeed);
        float block=blockOverride>0?blockOverride:(layout.mazeNodes-1)*(layout.buildingWidth+layout.alleyWidth)+layout.alleyWidth;
        float pitch=block+roadWidth;
        float laneOffset=(vehicleWidth+layout.trafficLaneClearance)/2;
        int count=0;
        // Each candidate lies halfway between intersections, leaving crossings unobstructed.
        for(int road=0;road<=layout.districtColumns;road++)for(int segment=0;segment<layout.districtRows;segment++)
        {
            if(random.NextDouble()>layout.trafficSegmentChance)continue;
            float roadAxis=(road-layout.districtColumns*.5f)*pitch;
            float segmentAxis=(segment-(layout.districtRows-1)*.5f)*pitch;
            Vector3 position=new Vector3(roadAxis+(random.Next(2)==0?-1:1)*laneOffset,0,segmentAxis);
            AddTrafficCar(group,scene,layout,random,position,0);count++;
        }
        for(int road=0;road<=layout.districtRows;road++)for(int segment=0;segment<layout.districtColumns;segment++)
        {
            if(random.NextDouble()>layout.trafficSegmentChance)continue;
            float roadAxis=(road-layout.districtRows*.5f)*pitch;
            float segmentAxis=(segment-(layout.districtColumns-1)*.5f)*pitch;
            Vector3 position=new Vector3(segmentAxis,0,roadAxis+(random.Next(2)==0?-1:1)*laneOffset);
            AddTrafficCar(group,scene,layout,random,position,90);count++;
        }
        return count;
    }

    static void AddTrafficCar(Transform parent,Scene scene,ScaleLayout layout,System.Random random,Vector3 center,float roadYaw)
    {
        string model=layout.trafficModels[random.Next(layout.trafficModels.Length)];
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(CarsPath+model+".fbx");if(asset==null)throw new IOException("Traffic model missing: "+model);
        var car=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);car.name="Traffic_"+model;car.transform.SetParent(parent);
        car.transform.rotation=Quaternion.Euler(0,roadYaw+(random.Next(2)==0?0:180),0);
        car.transform.localScale=Vector3.one;
        float sourceWidth=MeasureInstanceWidth(car);
        car.transform.localScale=Vector3.one*(layout.trafficVehicleWidth/sourceWidth);
        Bounds bounds=BoundsOf(car.GetComponentsInChildren<Renderer>());
        car.transform.position+=new Vector3(center.x-bounds.center.x,layout.modelGroundOffset-bounds.min.y,center.z-bounds.center.z);
        foreach(var filter in car.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null&&filter.GetComponent<Collider>()==null)
            filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
    }

    static float MeasurePrefabHeight(string path,Scene scene)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new IOException("Character model missing.");
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);instance.hideFlags=HideFlags.HideAndDontSave;
        try{return BoundsOf(instance.GetComponentsInChildren<Renderer>()).size.y;}
        finally{Object.DestroyImmediate(instance);}
    }

    static float MeasureInstanceWidth(GameObject instance)
    {Bounds bounds=BoundsOf(instance.GetComponentsInChildren<Renderer>());return Mathf.Min(bounds.size.x,bounds.size.z);}

    static Bounds BoundsOf(Renderer[] renderers)
    {
        if(renderers==null||renderers.Length==0)throw new System.InvalidOperationException("Renderer missing.");
        Bounds bounds=renderers[0].bounds;for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);return bounds;
    }
}
