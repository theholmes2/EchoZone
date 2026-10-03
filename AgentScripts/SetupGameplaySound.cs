if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/Scenes/Kenney_City2x2_Online.unity" || scene.isDirty)
    throw new System.InvalidOperationException("Expected saved online city scene; preserve user changes.");
if (!AssetDatabase.IsValidFolder("Assets/Data/Audio")) AssetDatabase.CreateFolder("Assets/Data", "Audio");
const string configPath = "Assets/Data/Audio/GameplaySoundConfig.asset";
var config = AssetDatabase.LoadAssetAtPath<EchoZone.Audio.GameplaySoundConfig>(configPath);
if (config == null)
{
    config = ScriptableObject.CreateInstance<EchoZone.Audio.GameplaySoundConfig>(); config.Normalize();
    AssetDatabase.CreateAsset(config, configPath);
}
const string prefabPath = "Assets/Prefabs/GameplaySoundSystem.prefab";
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
if (prefab == null)
{
    var root = new GameObject("GameplaySoundSystem");
    try
    {
        var glue = root.AddComponent<EchoZone.Audio.GameplaySoundGlue>();
        var serialized = new SerializedObject(glue);
        serialized.FindProperty("config").objectReferenceValue = config;
        var fields = new[] { "uiSources", "weaponSources", "worldSources" };
        var names = new[] { "UI_2D", "Weapon_3D", "World_3D" };
        var counts = new[] { 4, 16, 24 };
        for (int channel = 0; channel < fields.Length; channel++)
        {
            var group = new GameObject(names[channel]); group.transform.SetParent(root.transform, false);
            var array = serialized.FindProperty(fields[channel]); array.arraySize = counts[channel];
            for (int i = 0; i < counts[channel]; i++)
            {
                var voice = new GameObject("Voice_" + i); voice.transform.SetParent(group.transform, false);
                var source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.spatialBlend = channel == 0 ? 0 : 1;
                source.dopplerLevel = 0; source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = config.MinDistance; source.maxDistance = config.MaxDistance;
                array.GetArrayElementAtIndex(i).objectReferenceValue = source;
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    }
    finally { UnityEngine.Object.DestroyImmediate(root); }
}
if (UnityEngine.Object.FindFirstObjectByType<EchoZone.Audio.GameplaySoundGlue>() == null) PrefabUtility.InstantiatePrefab(prefab, scene);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
AssetDatabase.SaveAssetIfDirty(config);
return "GameplaySoundSystem serialized: UI 4 / Weapon 16 / World 24; Config ready.";
