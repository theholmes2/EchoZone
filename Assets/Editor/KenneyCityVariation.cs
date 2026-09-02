using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class KenneyExpandedCityBuilder
{
    [MenuItem("EchoZone/Apply Random City Lots")]
    public static void ApplyCityVariation()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(ScenePath))return;
        var scene=SceneManager.GetSceneByPath(ScenePath);
        bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty){Debug.LogWarning("Save the city before applying lot variation.");return;}
        var previous=SceneManager.GetActiveScene();
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var staged=new List<GameObject>();var replaced=new List<GameObject>();bool committed=false;
        try
        {
            Transform city=null;
            foreach(var go in scene.GetRootGameObjects())if(go.name=="CITY_GRID_8x8")city=go.transform;
            if(city==null||city.Find("Random_Lots_Applied")!=null)return;
            data=JsonUtility.FromJson<Layout>(File.ReadAllText(DataPath));
            if(data.buildings==null||data.buildings.Length==0||data.propPacks.Length!=data.propModels.Length
                ||data.minimumBuildingScale<=0||data.minimumBuildingScale>1||data.propWidth>data.buildingWidth/3)
                throw new InvalidOperationException("Invalid city variation configuration.");
            const string backup="Assets/Scenes/Kenney_ExpandedCity_BeforeRandomLots.unity";
            if(!File.Exists(backup)&&!AssetDatabase.CopyAsset(ScenePath,backup))throw new IOException("City backup failed.");
            palettes.Clear();var random=new System.Random(data.variationSeed);
            int buildings=0,empty=0,props=0;
            foreach(Transform district in city)
            {
                if(!district.name.StartsWith("District_"))continue;
                var lots=new List<Vector3>();
                foreach(Transform child in district)
                {
                    if(child.name.StartsWith("building-"))
                    {
                        var renderers=child.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                        foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                        lots.Add(new Vector3(bounds.center.x,0,bounds.center.z));replaced.Add(child.gameObject);
                    }
                    else if(child.name=="Building footprint boundary")replaced.Add(child.gameObject);
                }
                var group=new GameObject("Randomized_Lots");group.transform.SetParent(district);staged.Add(group);
                foreach(var center in lots)
                {
                    if(random.NextDouble()<data.emptyLotChance)
                    {
                        empty++;
                        if(random.NextDouble()>=data.propLotChance)continue;
                        // Separated sub-cells keep props within the vacant lot, off all alleys.
                        for(int slot=0;slot<4;slot++)
                        {
                            if(random.NextDouble()>=data.propLotChance)continue;
                            int choice=random.Next(data.propModels.Length);
                            var pos=center+new Vector3((slot%2==0?-1:1)*data.buildingWidth/4,0,(slot<2?-1:1)*data.buildingWidth/4);
                            Model(group.transform,data.propPacks[choice],data.propModels[choice],pos,data.propWidth,random.Next(4)*90);props++;
                        }
                        continue;
                    }
                    float width=data.buildingWidth*Mathf.Lerp(data.minimumBuildingScale,1,(float)random.NextDouble());
                    float margin=(data.buildingWidth-width)/2;
                    var offset=new Vector3(((float)random.NextDouble()*2-1)*margin,0,((float)random.NextDouble()*2-1)*margin);
                    Model(group.transform,City,data.buildings[random.Next(data.buildings.Length)],center+offset,width,random.Next(4)*90);buildings++;
                }
            }
            foreach(var go in replaced)UnityEngine.Object.DestroyImmediate(go);
            var marker=new GameObject("Random_Lots_Applied");marker.transform.SetParent(city);committed=true;
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Scene save failed.");
            AssetDatabase.SaveAssets();
            foreach(var go in scene.GetRootGameObjects())if(go.TryGetComponent<Camera>(out var camera))Capture(camera,scene);
            string result=$"CITY_VARIATION_COMPLETE: buildings={buildings}, emptyLots={empty}, props={props}, seed={data.variationSeed}";
            File.WriteAllText("Docs/Previews/Kenney_CityVariation.done",result);Debug.Log(result);
        }
        catch
        {
            if(!committed)foreach(var go in staged)if(go!=null)UnityEngine.Object.DestroyImmediate(go);
            throw;
        }
        finally
        {
            if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(opened)EditorSceneManager.CloseScene(scene,true);
        }
    }
}
