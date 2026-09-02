using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class KenneyExpandedCityBuilder
{
    const string HeightMixMarker="Docs/Previews/Kenney_CityHeightMix.done";

    [MenuItem("EchoZone/Apply City High-Rise Ratio")]
    public static void ApplyHeightMix()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(ScenePath))return;
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty){Debug.LogWarning("Save the city before applying the height mix.");return;}
        var previous=SceneManager.GetActiveScene();if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            Transform city=null;foreach(var go in scene.GetRootGameObjects())if(go.name=="CITY_GRID_8x8")city=go.transform;
            if(city==null)return;
            data=JsonUtility.FromJson<Layout>(File.ReadAllText(DataPath));
            if(data.tallBuildingRatio<0||data.tallBuildingRatio>1||data.standardBuildings==null||data.standardBuildings.Length==0)
                throw new InvalidOperationException("Invalid city height mix data.");
            const string backup="Assets/Scenes/Kenney_ExpandedCity_BeforeHeightMix.unity";
            if(!File.Exists(backup)&&!AssetDatabase.CopyAsset(ScenePath,backup))throw new IOException("Height mix backup failed.");
            var all=new List<Transform>();var tall=new List<Transform>();
            foreach(var item in city.GetComponentsInChildren<Transform>(true))
            {
                if(!item.name.StartsWith("building-"))continue;
                all.Add(item);if(item.name.StartsWith("building-skyscraper-"))tall.Add(item);
            }
            int target=Mathf.RoundToInt(all.Count*data.tallBuildingRatio);
            var random=new System.Random(data.heightMixSeed);
            for(int i=tall.Count-1;i>0;i--){int j=random.Next(i+1);(tall[i],tall[j])=(tall[j],tall[i]);}
            int replaced=Mathf.Max(0,tall.Count-target);
            palettes.Clear();
            for(int i=0;i<replaced;i++)
            {
                var old=tall[i];var renderers=old.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                Bounds bounds=renderers[0].bounds;for(int r=1;r<renderers.Length;r++)bounds.Encapsulate(renderers[r].bounds);
                string model=data.standardBuildings[random.Next(data.standardBuildings.Length)];
                var replacement=Model(old.parent,City,model,new Vector3(bounds.center.x,0,bounds.center.z),Mathf.Max(bounds.size.x,bounds.size.z),old.eulerAngles.y);
                replacement.transform.SetSiblingIndex(old.GetSiblingIndex());UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Height mix scene save failed.");
            int remaining=tall.Count-replaced;
            File.WriteAllText(HeightMixMarker,$"total={all.Count}; tallBefore={tall.Count}; tallAfter={remaining}; targetRatio={data.tallBuildingRatio:F2}; replaced={replaced}");
            Debug.Log($"CITY_HEIGHT_MIX: total={all.Count}, tallBefore={tall.Count}, tallAfter={remaining}, ratio={(all.Count==0?0:(float)remaining/all.Count):P1}, replaced={replaced}");
        }
        finally{if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
}
