using EchoZone.Player.Input;
using EchoZone.Player.Movement;
using EchoZone.Player.Network;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoZone.Editor
{
    [InitializeOnLoad]
    public static class PlayerNetworkMovementSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string ConfigFolderPath = "Assets/Data";
        private const string PlayerConfigFolderPath = "Assets/Data/Player";
        private const string ConfigPath = "Assets/Data/Player/PlayerMovementConfig.asset";

        static PlayerNetworkMovementSetup()
        {
            if (!AssetDatabase.IsAssetImportWorkerProcess())
            {
                EditorApplication.delayCall += SetupIfNeeded;
            }
        }

        [MenuItem("EchoZone/Setup Player Network Movement")]
        public static void Setup()
        {
            PlayerMovementConfig config = LoadOrCreateConfig();
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                Rigidbody body = prefabRoot.GetComponent<Rigidbody>();
                PlayerInputReader inputReader = GetOrAdd<PlayerInputReader>(prefabRoot);
                PlayerMovementMotor movementMotor = GetOrAdd<PlayerMovementMotor>(prefabRoot);
                PlayerNetworkMovementGlue networkGlue = GetOrAdd<PlayerNetworkMovementGlue>(prefabRoot);
                GetOrAdd<NetworkRigidbody>(prefabRoot);

                inputReader.Configure(inputActions, "Player", "Move");
                movementMotor.Configure(body, config);
                networkGlue.Configure(inputReader, movementMotor);

                EditorUtility.SetDirty(inputReader);
                EditorUtility.SetDirty(movementMotor);
                EditorUtility.SetDirty(networkGlue);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("EchoZone Player network movement setup completed.");
        }

        private static void SetupIfNeeded()
        {
            EditorApplication.delayCall -= SetupIfNeeded;

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null || playerPrefab.GetComponent<PlayerNetworkMovementGlue>() != null)
            {
                return;
            }

            Setup();
        }

        private static PlayerMovementConfig LoadOrCreateConfig()
        {
            PlayerMovementConfig config = AssetDatabase.LoadAssetAtPath<PlayerMovementConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            EnsureFolder(ConfigFolderPath, "Assets", "Data");
            EnsureFolder(PlayerConfigFolderPath, ConfigFolderPath, "Player");

            config = ScriptableObject.CreateInstance<PlayerMovementConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        private static void EnsureFolder(string fullPath, string parentPath, string folderName)
        {
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parentPath, folderName);
            }
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }
    }
}
