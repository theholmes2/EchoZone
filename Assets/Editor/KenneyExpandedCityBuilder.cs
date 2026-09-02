using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// One-shot scene authoring only. Planning dimensions live in ExpandedCityLayout.json.
public static partial class KenneyExpandedCityBuilder
{
    const string ScenePath="Assets/Scenes/Kenney_ExpandedCity.unity";
    const string DataPath="Assets/Data/Maps/ExpandedCityLayout.json";
    const string Art="Assets/Art/Kenney/";
    const string Mats=Art+"ExpandedMaterials/";
    const string City="kenney_city-kit-commercial_2.1";
    const string Industry="kenney_city-kit-industrial_1.0";
    const string Factory="kenney_factory-kit_3.0";
    const string Cars="kenney_car-kit";
    [Serializable] public class Layout
    {
        public float mapWidth,mapDepth,alleyWidth,buildingWidth,roadWidth,plazaWidth,plazaDepth,barrierHeight,barrierThickness;
        public int mazeNodes,mazeSeed;
        public int districtColumns,districtRows;
        public int variationSeed;
        public float emptyLotChance,propLotChance,minimumBuildingScale,propWidth;
        public int heightMixSeed;
        public float tallBuildingRatio;
        public string[] buildings,standardBuildings,propPacks,propModels;
        public Vector3 commercialOrigin,industrialOrigin,warehouseOrigin,parkingOrigin;
    }
    static Layout data;
    static Transform root;
    static Material pavement,road,wall,paint;
    static readonly Dictionary<string,Material> palettes=new();

    [MenuItem("EchoZone/Expand City To Configured District Grid")]
    public static void ExpandCityGrid()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(ScenePath))return;
        var scene=SceneManager.GetSceneByPath(ScenePath);
        bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty){Debug.LogWarning("Save the map before expanding the city.");return;}
        var previous=SceneManager.GetActiveScene();
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        Transform oldRoot=null;
        try
        {
            foreach(var go in scene.GetRootGameObjects())
            {
                if(go.name=="CITY_GRID_8x8")return;
                if(go.name=="EXPANDED_CITY__STATIC_GEOMETRY_ONLY")oldRoot=go.transform;
            }
            if(oldRoot==null)throw new InvalidOperationException("Original map root missing.");
            data=JsonUtility.FromJson<Layout>(File.ReadAllText(DataPath));
            if(data.districtColumns!=8||data.districtRows!=8)throw new InvalidOperationException("Expected 8 by 8 districts.");
            const string backup="Assets/Scenes/Kenney_ExpandedCity_Before8x8.unity";
            if(!File.Exists(backup)&&!AssetDatabase.CopyAsset(ScenePath,backup))throw new IOException("Backup failed.");
            palettes.Clear();
            pavement=Mat("Concrete",new Color(.51f,.56f,.57f));road=Mat("Asphalt",new Color(.13f,.17f,.2f));
            wall=Mat("AlleyWalls",new Color(.39f,.43f,.45f));paint=Mat("Markings",new Color(.92f,.8f,.49f));
            root=new GameObject("CITY_GRID_8x8").transform;
            float block=(data.mazeNodes-1)*(data.buildingWidth+data.alleyWidth)+data.alleyWidth;
            float pitch=block+data.roadWidth;
            float extent=data.districtColumns*pitch+data.roadWidth;
            Box(root,"Ground",new Vector3(0,-.4f,0),new Vector3(extent,.8f,extent),Mat("Ground",new Color(.30f,.39f,.31f)));
            var streets=Group("00_Connected_Street_Grid");
            for(int i=0;i<=data.districtColumns;i++)
            {
                float axis=(i-data.districtColumns*.5f)*pitch;
                Box(streets,"North south road "+i,new Vector3(axis,.03f,0),new Vector3(data.roadWidth,.06f,extent),road);
                Box(streets,"East west road "+i,new Vector3(0,.03f,axis),new Vector3(extent,.06f,data.roadWidth),road);
            }
            Vector3 galleryCenter=Vector3.zero;
            for(int z=0;z<data.districtRows;z++)for(int x=0;x<data.districtColumns;x++)
            {
                Vector3 center=new Vector3((x-3.5f)*pitch,0,(z-3.5f)*pitch);
                var district=Group($"District_{x+1:00}_{z+1:00}");
                bool plaza=(x==3&&z==3)||((x+z*3)%11==0);
                if(!plaza)
                    Maze(district,center-new Vector3((block-data.alleyWidth)/2,0,(block-data.alleyWidth)/2),City,data.mazeSeed+x+z*data.districtColumns);
                else
                {
                    district.name+="_City_Plaza";
                    Box(district,"Plaza pavement",center+Vector3.up*.08f,new Vector3(block,.16f,block),pavement);
                    for(int b=0;b<4;b++)
                        Model(district,City,"building-"+(char)('a'+(x+z+b)%14),center+new Vector3(-block/2+data.buildingWidth/2+b*(data.buildingWidth+data.alleyWidth),0,block/2-data.buildingWidth/2),data.buildingWidth,180);
                    Model(district,Cars,"sedan",center+new Vector3(block/3,0,-block/3),4.5f,90);
                    if(x==3&&z==3)galleryCenter=center;
                }
            }
            var gallery=oldRoot.Find("60_Character_Gallery__Static_Candidates");
            if(gallery!=null){gallery.SetParent(root,true);gallery.position+=galleryCenter+new Vector3(0,0,65);}
            foreach(var go in scene.GetRootGameObjects())
            {
                if(go.name=="PLAYER__Offline_Map_Walkthrough")go.transform.position=galleryCenter+new Vector3(0,1.25f,12);
                if(go.TryGetComponent<Camera>(out var camera))
                {camera.transform.position=new Vector3(extent*.8f,extent,-extent);camera.transform.LookAt(Vector3.zero);camera.orthographicSize=extent*.65f;camera.farClipPlane=extent*5;}
            }
            UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            foreach(var go in scene.GetRootGameObjects())if(go.TryGetComponent<Camera>(out var camera))Capture(camera,scene);
            File.WriteAllText("Docs/Previews/Kenney_City8x8.done","64 city districts; previous scene preserved in Before8x8.");
            Debug.Log("CITY_8X8_COMPLETE: 64 districts, fantasy removed, player and gallery preserved.");
        }
        catch
        {
            if(oldRoot!=null&&root!=oldRoot&&root!=null)UnityEngine.Object.DestroyImmediate(root.gameObject);
            throw;
        }
        finally
        {
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(opened)EditorSceneManager.CloseScene(scene,true);
        }
    }

    [MenuItem("EchoZone/Apply Compact District Revision")]
    public static void CompactRevision()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetSceneByPath(ScenePath);
        bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty){Debug.LogWarning("Save the expanded map before compact revision.");return;}
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        var previous=SceneManager.GetActiveScene();SceneManager.SetActiveScene(scene);
        try
        {
            root=null;
            foreach(var obj in scene.GetRootGameObjects())if(obj.name=="EXPANDED_CITY__STATIC_GEOMETRY_ONLY")root=obj.transform;
            if(root==null)throw new InvalidOperationException("Map root missing.");
            if(root.Find("70_Fantasy_Town")!=null)return;
            string backup="Assets/Scenes/Kenney_ExpandedCity_BeforeCompact.unity";
            if(!File.Exists(backup)&&!AssetDatabase.CopyAsset(ScenePath,backup))throw new IOException("Backup failed.");
            data=JsonUtility.FromJson<Layout>(File.ReadAllText(DataPath));palettes.Clear();
            pavement=Mat("Concrete",new Color(.51f,.56f,.57f));road=Mat("Asphalt",new Color(.13f,.17f,.2f));
            root.Find("10_Commercial_2.4m_Maze").position=new Vector3(25,0,-13);
            root.Find("20_Industrial_2.4m_Maze").position=new Vector3(-16,0,-13);
            root.Find("30_Factory_And_Loading_Yard").position=new Vector3(-6,0,0);
            var ground=root.Find("Ground");ground.position=new Vector3(0,-.4f,-8);ground.localScale=new Vector3(data.mapWidth,.8f,data.mapDepth);
            var street=root.Find("00_Streets_And_Central_Plaza");
            street.Find("North south avenue").localScale=new Vector3(data.roadWidth,.06f,156);
            street.Find("North south avenue").position=new Vector3(0,.03f,-8);
            street.Find("East west avenue").localScale=new Vector3(180,.06f,data.roadWidth);
            street.Find("Open central court").localScale=new Vector3(24,.16f,44);
            street.Find("Open central court").position=new Vector3(0,.08f,-38);
            foreach(Transform t in street)if(t.name=="Road dash"&&(Mathf.Abs(t.position.x)>88||t.position.z>67))t.gameObject.SetActive(false);
            var parking=root.Find("40_Parking_And_Building_Rows");
            parking.Find("Parking apron").position=new Vector3(-45,.08f,-70);
            parking.Find("Parking apron").localScale=new Vector3(66,.16f,18);
            int cars=0,buildings=0,stripes=0;
            foreach(Transform t in parking)
            {
                if(t.name=="Parking apron")continue;
                if(t.name.StartsWith("building-")){t.position+=new Vector3((-74+buildings*11)-t.position.x,0,-84-t.position.z);buildings++;}
                else if(t.name=="Parking stripe"){t.position=new Vector3(-76+stripes*10,.18f,-70);stripes++;}
                else{t.position+=new Vector3((-71+cars*10)-t.position.x,0,-70-t.position.z);cars++;}
            }
            foreach(Transform t in root.Find("50_Sparse_Plaza_Cover")){var p=t.position;p.x=Mathf.Clamp(p.x,-8,8);t.position=p;}
            root.Find("60_Character_Gallery__Static_Candidates").position=new Vector3(0,0,-4);
            foreach(Transform t in root.Find("30_Factory_And_Loading_Yard"))
                if(t.name=="machine"||t.name=="box-large"||t.name=="conveyor-long-sides")t.gameObject.SetActive(false);
            var town=Group("70_Fantasy_Town");
            Box(town,"Village paving",new Vector3(-44,.08f,-38),new Vector3(64,.16f,46),Mat("VillageStone",new Color(.53f,.48f,.39f)));
            const string fantasy="kenney_fantasy-town-kit_2.0";
            for(int x=0;x<3;x++)for(int z=0;z<3;z++)
            {
                if(x==1&&z==1)continue;
                Vector3 p=new Vector3(-66+x*21,0,-22-z*16);
                var house=new GameObject("Fantasy_House_"+x+"_"+z).transform;house.SetParent(town);
                var front=Model(house,fantasy,z%2==0?"wall-window-shutters":"wall-wood-window-shutters",p+new Vector3(0,0,-4),8,0);
                Bounds b=front.GetComponentInChildren<Renderer>().bounds;
                foreach(var r in front.GetComponentsInChildren<Renderer>())b.Encapsulate(r.bounds);
                Model(house,fantasy,"wall",p+new Vector3(0,0,4),8,180);
                Model(house,fantasy,"wall",p+new Vector3(-4,0,0),8,90);
                Model(house,fantasy,"wall-door",p+new Vector3(4,0,0),8,270);
                Model(house,fantasy,"roof-high-point",p+Vector3.up*(b.max.y-.2f),9,0);
            }
            Model(town,fantasy,"fountain-round",new Vector3(-45,0,-38),5,0);
            Model(town,fantasy,"cart",new Vector3(-19,0,-38),3,90);
            Model(town,fantasy,"lantern",new Vector3(-73,0,-34),1,0);
            var factory=Group("80_Factory_Machines_And_Assembly_Lines");
            for(int row=0;row<3;row++)for(int col=0;col<5;col++)
            {
                Vector3 p=new Vector3(22+col*13,0,-19-row*17);
                Model(factory,Factory,col%2==0?"machine-fortified":"machine-window",p,4,90);
                Model(factory,Factory,"conveyor-long-sides",p+new Vector3(0,0,-6),6,0);
                if(col%2==0)Model(factory,Factory,"box-small",p+new Vector3(4,0,-4),1,0);
            }
            foreach(var obj in scene.GetRootGameObjects())if(obj.TryGetComponent<Camera>(out var camera))camera.orthographicSize=110;
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            foreach(var obj in scene.GetRootGameObjects())if(obj.TryGetComponent<Camera>(out var camera))Capture(camera,scene);
            File.WriteAllText("Docs/Previews/Kenney_CompactCity.done","Compact revision complete; BeforeCompact scene is the backup.");
            Debug.Log("COMPACT_CITY_REVISION_COMPLETE");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }

    [MenuItem("EchoZone/Refresh Expanded City Preview")]
    public static void RefreshPreview()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetSceneByPath(ScenePath);
        bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            foreach(var go in scene.GetRootGameObjects())
                if(go.TryGetComponent<Camera>(out var camera))Capture(camera,scene);
        }
        finally { if(opened)EditorSceneManager.CloseScene(scene,true); }
    }

    [MenuItem("EchoZone/Create Expanded Kenney City (New Scene Only)")]
    public static void Build()
    {
        if(File.Exists(ScenePath)) throw new InvalidOperationException("Scene exists; not overwriting user edits.");
        data=JsonUtility.FromJson<Layout>(File.ReadAllText(DataPath));
        if(data.alleyWidth<=2 || data.mazeNodes<2) throw new InvalidOperationException("Invalid layout dimensions.");
        Directory.CreateDirectory(Mats);
        palettes.Clear();
        AssetDatabase.Refresh();
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            root=new GameObject("EXPANDED_CITY__STATIC_GEOMETRY_ONLY").transform;
            pavement=Mat("Concrete",new Color(.51f,.56f,.57f));
            road=Mat("Asphalt",new Color(.13f,.17f,.2f));
            wall=Mat("AlleyWalls",new Color(.39f,.43f,.45f));
            paint=Mat("Markings",new Color(.92f,.8f,.49f));
            var ground=Mat("Ground",new Color(.30f,.39f,.31f));
            Box(root,"Ground",new Vector3(0,-.4f,0),new Vector3(data.mapWidth,.8f,data.mapDepth),ground);
            var streets=Group("00_Streets_And_Central_Plaza");
            Box(streets,"North south avenue",new Vector3(0,.03f,0),new Vector3(data.roadWidth,.06f,data.mapDepth-6),road);
            Box(streets,"East west avenue",new Vector3(0,.03f,-8),new Vector3(data.mapWidth-6,.06f,data.roadWidth),road);
            Box(streets,"Open central court",new Vector3(0,.08f,-39),new Vector3(data.plazaWidth,.16f,data.plazaDepth),pavement);
            for(int i=-17;i<=17;i++)
            {
                if(i< -3 || i>0) Box(streets,"Road dash",new Vector3(0,.075f,i*5),new Vector3(.15f,.02f,2),paint,false);
                if(Mathf.Abs(i)>2) Box(streets,"Road dash",new Vector3(i*6,.075f,-8),new Vector3(2,.02f,.15f),paint,false);
            }
            Maze(Group("10_Commercial_2.4m_Maze"),data.commercialOrigin,City,data.mazeSeed);
            Maze(Group("20_Industrial_2.4m_Maze"),data.industrialOrigin,Industry,data.mazeSeed+1);
            var warehouses=Group("30_Factory_And_Loading_Yard");
            Box(warehouses,"Factory pavement",new Vector3(58,.08f,-48),new Vector3(76,.16f,66),pavement);
            for(int i=0;i<5;i++)
                Model(warehouses,Industry,"building-"+(char)('a'+i*2),data.warehouseOrigin+new Vector3(i*15,0,0),12,180);
            for(int i=0;i<4;i++)
            {
                Model(warehouses,Factory,"machine",new Vector3(32+i*14,0,-41),5,90);
                Model(warehouses,Factory,"box-large",new Vector3(32+i*14,0,-31),2,0);
                Model(warehouses,Factory,"conveyor-long-sides",new Vector3(32+i*14,0,-51),6,0);
            }
            Model(warehouses,Cars,"truck",new Vector3(61,0,-23),6,90);
            Model(warehouses,Industry,"chimney-large",new Vector3(96,0,-76),4,0);
            var parking=Group("40_Parking_And_Building_Rows");
            Box(parking,"Parking apron",data.parkingOrigin+Vector3.up*.08f,new Vector3(64,.16f,37),road);
            string[] vehicles={"sedan","taxi","van","suv","delivery","police"};
            for(int i=0;i<6;i++)
            {
                Model(parking,Cars,vehicles[i],data.parkingOrigin+new Vector3(-24+i*9,0,3),4.5f,0);
                Box(parking,"Parking stripe",data.parkingOrigin+new Vector3(-28+i*9,.18f,3),new Vector3(.1f,.02f,7),paint,false);
                Model(parking,City,"building-"+(char)('a'+i),new Vector3(-98+i*15,0,-81),11,0);
            }
            var plaza=Group("50_Sparse_Plaza_Cover");
            Model(plaza,Cars,"van",new Vector3(-15,0,-47),4.5f,25);
            Model(plaza,Factory,"box-large",new Vector3(15,0,-30),2,0);
            Model(plaza,Factory,"box-wide",new Vector3(17,0,-32),2,20);
            var markers=Group("90_Reference_Markers_No_Gameplay");
            for(int i=0;i<2;i++)
            {
                var capsule=GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name="1m_wide_2m_tall_Person_Reference_"+i;
                capsule.transform.SetParent(markers);
                capsule.transform.position=new Vector3(-.55f+i*1.1f,1,-38);
                capsule.GetComponent<Renderer>().sharedMaterial=paint;
                UnityEngine.Object.DestroyImmediate(capsule.GetComponent<Collider>());
            }
            var light=new GameObject("Sun").AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=1.1f; light.shadows=LightShadows.Soft;
            light.transform.rotation=Quaternion.Euler(50,-30,0);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.49f,.55f,.61f);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag="MainCamera"; camera.transform.position=new Vector3(155,190,-210);
            camera.transform.LookAt(Vector3.zero); camera.orthographic=true; camera.orthographicSize=130;
            camera.farClipPlane=700; camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.55f,.65f,.69f);
            AddCharacterGallery();
            EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.SaveAssets();
            Capture(camera,scene);
            Debug.Log("EXPANDED_CITY_CREATED: "+ScenePath+"; maze corridors="+data.alleyWidth+"m; nodes per district="+data.mazeNodes*data.mazeNodes);
        }
        finally
        {
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene,true);
        }
    }

    static void Maze(Transform parent,Vector3 origin,string pack,int seed)
    {
        int n=data.mazeNodes;
        float step=data.buildingWidth+data.alleyWidth;
        float size=(n-1)*step+data.alleyWidth;
        Box(parent,"Walkable alley district",origin+new Vector3((n-1)*step/2,.08f,(n-1)*step/2),new Vector3(size,.16f,size),pavement);
        for(int x=0;x<n-1;x++)for(int z=0;z<n-1;z++)
        {
            // Exact footprint boxes define clear corridor width despite decorative model silhouettes.
            Vector3 pos=origin+new Vector3((x+.5f)*step,0,(z+.5f)*step);
            Model(parent,pack,"building-"+(char)('a'+(x+z*3)%14),pos,data.buildingWidth,(x+z)%4*90);
            Box(parent,"Building footprint boundary",pos+Vector3.up*.35f,new Vector3(data.buildingWidth,.7f,data.buildingWidth),wall);
        }
        var visited=new HashSet<int>(); var links=new HashSet<string>(); var stack=new Stack<int>();
        var random=new System.Random(seed); visited.Add(0);stack.Push(0);
        while(stack.Count>0)
        {
            int current=stack.Peek(),x=current%n,z=current/n;
            var next=new List<int>();
            if(x>0&&!visited.Contains(current-1))next.Add(current-1);
            if(x<n-1&&!visited.Contains(current+1))next.Add(current+1);
            if(z>0&&!visited.Contains(current-n))next.Add(current-n);
            if(z<n-1&&!visited.Contains(current+n))next.Add(current+n);
            if(next.Count==0){stack.Pop();continue;}
            int target=next[random.Next(next.Count)];links.Add(Key(current,target));visited.Add(target);stack.Push(target);
        }
        if(visited.Count!=n*n)throw new InvalidOperationException("Maze connectivity validation failed.");
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            int a=x+z*n;
            if(x<n-1&&!links.Contains(Key(a,a+1)))
                Box(parent,"Maze cross barrier",origin+new Vector3((x+.5f)*step,data.barrierHeight/2,z*step),new Vector3(data.barrierThickness,data.barrierHeight,data.alleyWidth),wall);
            if(z<n-1&&!links.Contains(Key(a,a+n)))
                Box(parent,"Maze cross barrier",origin+new Vector3(x*step,data.barrierHeight/2,(z+.5f)*step),new Vector3(data.alleyWidth,data.barrierHeight,data.barrierThickness),wall);
        }
        // Marked entrances connect the maze to surrounding open circulation.
        Box(parent,"South entry",origin+new Vector3(0,.19f,-2),new Vector3(data.alleyWidth,.02f,4),paint,false);
        Box(parent,"North exit",origin+new Vector3((n-1)*step,.19f,(n-1)*step+2),new Vector3(data.alleyWidth,.02f,4),paint,false);
    }
    static string Key(int a,int b)=>Math.Min(a,b)+":"+Math.Max(a,b);
    static Transform Group(string name){var g=new GameObject(name).transform;g.SetParent(root);return g;}
    static Material Mat(string name,Color color)
    {
        string path=Mats+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material!=null)return material;
        material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.1f);
        AssetDatabase.CreateAsset(material,path);return material;
    }
    static void Box(Transform parent,string name,Vector3 pos,Vector3 size,Material material,bool solid=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);
        go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
        if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static GameObject Model(Transform parent,string pack,string name,Vector3 pos,float width,float yaw,bool byHeight=false)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Art+pack+"/Models/FBX format/"+name+".fbx");
        if(asset==null)throw new InvalidOperationException("Missing asset "+pack+"/"+name);
        if(!palettes.TryGetValue(pack,out var material))
        {
            material=Mat(pack,Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+pack+"/Models/FBX format/Textures/colormap.png"));
            EditorUtility.SetDirty(material);palettes.Add(pack,material);
        }
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.name=name;go.transform.SetParent(parent);go.transform.rotation=Quaternion.Euler(0,yaw,0);
        var renderers=go.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;
        foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        go.transform.localScale*=width/(byHeight?bounds.size.y:Mathf.Max(bounds.size.x,bounds.size.z));
        bounds=renderers[0].bounds;
        foreach(var r in renderers){bounds.Encapsulate(r.bounds);var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)mats[i]=material;r.sharedMaterials=mats;}
        go.transform.position+=new Vector3(pos.x-bounds.center.x,pos.y+.2f-bounds.min.y,pos.z-bounds.center.z);
        if(!byHeight)foreach(var f in go.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh!=null)f.gameObject.AddComponent<MeshCollider>().sharedMesh=f.sharedMesh;
        return go;
    }

    static void AddCharacterGallery()
    {
        var group=Group("60_Character_Gallery__Static_Candidates");
        string[] names="character-female-a,character-female-c,character-female-e,character-male-a,character-male-c,character-male-e".Split(',');
        for(int row=0;row<2;row++)for(int i=0;i<6;i++)
        {
            string pack=row==0?"kenney_mini-characters":"kenney_blocky-characters_20";
            string name=row==0?names[i]:"character-"+(char)('a'+i);
            Vector3 pos=new Vector3(-18+i*7,.0f,-58-row*6);
            Box(group,"Display plinth",pos+Vector3.up*.12f,new Vector3(3,.24f,3),pavement);
            Model(group,pack,name,pos,2,180,true);
            var label=new GameObject((row==0?"Mini ":"Blocky ")+name);
            label.transform.SetParent(group);label.transform.position=pos+new Vector3(0,.3f,-2);
            label.transform.rotation=Quaternion.Euler(90,0,0);
            var text=label.AddComponent<TextMesh>();text.text=(row==0?"MINI ":"BLOCKY ")+(i+1);
            text.fontSize=48;text.characterSize=.12f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
        }
    }

    static void Capture(Camera camera,Scene scene)
    {
        // Isolate geometry from other scenes currently open in the editor.
        var layers=new Dictionary<GameObject,int>();
        foreach(var rootObject in scene.GetRootGameObjects())
            foreach(var t in rootObject.GetComponentsInChildren<Transform>(true))
            { layers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=30; }
        int oldMask=camera.cullingMask;
        camera.cullingMask=1<<30;
        camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
        var rt=new RenderTexture(1600,1200,24);var old=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var texture=new Texture2D(1600,1200,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1200),0,0);texture.Apply();
        Directory.CreateDirectory("Docs/Previews");File.WriteAllBytes("Docs/Previews/Kenney_ExpandedCity_Isolated.png",texture.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
        camera.cullingMask=oldMask;
        foreach(var pair in layers)pair.Key.layer=pair.Value;
    }
}
