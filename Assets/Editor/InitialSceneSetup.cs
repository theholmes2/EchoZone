using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoZone.Editor
{
    [InitializeOnLoad]
    public static class InitialSceneSetup
    {
        private const string PlayerObjectName = "Player";
        private const string FloorObjectName = "Floor";
        private const int PlayerLayer = 8;
        private const int FloorLayer = 9;

        static InitialSceneSetup()
        {
            EditorApplication.delayCall += SetupOnce;
        }

        [MenuItem("EchoZone/Setup Initial Player And Floor")]
        public static void Setup()
        {
            EnsureLayer(PlayerLayer, "Player");
            EnsureLayer(FloorLayer, "Floor");
            EnsureTag("Floor");

            GameObject floor = GameObject.Find(FloorObjectName);
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = FloorObjectName;
                floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                floor.transform.localScale = new Vector3(2f, 1f, 2f);
            }

            floor.layer = FloorLayer;
            floor.tag = "Floor";

            GameObject player = GameObject.Find(PlayerObjectName);
            if (player == null)
            {
                player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                player.name = PlayerObjectName;
                player.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.identity);
            }

            player.layer = PlayerLayer;
            player.tag = "Player";

            if (player.GetComponent<Rigidbody>() == null)
            {
                Rigidbody rigidbody = player.AddComponent<Rigidbody>();
                rigidbody.mass = 1f;
                rigidbody.useGravity = true;
                rigidbody.isKinematic = false;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("EchoZone initial scene setup completed: Player and Floor are ready.");
        }

        private static void SetupOnce()
        {
            EditorApplication.delayCall -= SetupOnce;

            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                GameObject.Find(PlayerObjectName) != null ||
                GameObject.Find(FloorObjectName) != null)
            {
                return;
            }

            Setup();
        }

        private static void EnsureLayer(int index, string layerName)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layer = layers.GetArrayElementAtIndex(index);

            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
            }
        }

        private static void EnsureTag(string tagName)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty tags = tagManager.FindProperty("tags");

            for (int i = 0; i < tags.arraySize; i++)
            {
                if (tags.GetArrayElementAtIndex(i).stringValue == tagName)
                {
                    return;
                }
            }

            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tagName;
            tagManager.ApplyModifiedProperties();
        }
    }
}
