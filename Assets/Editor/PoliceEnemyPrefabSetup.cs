using EchoZone.Enemy;
using EchoZone.Player.View;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Kenney character-male-c 외형으로 경찰 네트워크 적 프리팹을 만들고 관련 설정에 연결합니다.</summary>
public static class PoliceEnemyPrefabSetup
{
    private const string ModelPath = "Assets/Art/Kenney/kenney_mini-characters/Models/FBX format/character-male-c.fbx";
    private const string AnimatorControllerPath = "Assets/Art/Kenney/MiniCharacterPlayer.controller";
    private const string PrefabPath = "Assets/Prefabs/Enemy/KenneyPoliceNetworkEnemy.prefab";
    private const string PoliceConfigPath = "Assets/Data/Enemy/PoliceEnemyConfig.asset";
    private const string AnimationConfigPath = "Assets/Data/Player/CharacterAnimationConfig.asset";
    private const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";

    /// <summary>경찰 프리팹이 없는 경우에만 초기 생성하여 수동 장착과 설정을 보존합니다.</summary>
    [InitializeOnLoadMethod]
    private static void QueueSetup()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            EditorApplication.delayCall += Apply;
        }
    }

    /// <summary>경찰 프리팹을 생성하고 Config 및 NGO 프리팹 목록에 등록합니다.</summary>
    [MenuItem("EchoZone/Enemy/Build Kenney Police Enemy")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        PoliceEnemyConfig policeConfig = AssetDatabase.LoadAssetAtPath<PoliceEnemyConfig>(PoliceConfigPath);
        CharacterAnimationConfig animationConfig =
            AssetDatabase.LoadAssetAtPath<CharacterAnimationConfig>(AnimationConfigPath);
        RuntimeAnimatorController animatorController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorControllerPath);
        if (model == null || policeConfig == null || animationConfig == null || animatorController == null)
        {
            Debug.LogError("POLICE_ENEMY_PREFAB_FAILED: required model or config asset is missing.");
            return;
        }

        EnsureFolder("Assets/Prefabs/Enemy");
        GameObject prefab = BuildPrefab(model, policeConfig, animationConfig, animatorController);
        ConnectPoliceConfig(policeConfig, prefab);
        RegisterNetworkPrefab(prefab);
        AssetDatabase.SaveAssets();
        Debug.Log("POLICE_ENEMY_PREFAB_READY: character-male-c is connected to PoliceEnemyConfig and NGO.");
    }

    /// <summary>경찰 AI에 필요한 네트워크·충돌·NavMesh·표시 컴포넌트를 조립합니다.</summary>
    private static GameObject BuildPrefab(
        GameObject model,
        PoliceEnemyConfig policeConfig,
        CharacterAnimationConfig animationConfig,
        RuntimeAnimatorController animatorController)
    {
        GameObject root = new("KenneyPoliceNetworkEnemy");
        try
        {
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();

            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.radius = policeConfig.CollisionRadius;
            collider.height = policeConfig.CollisionHeight;
            collider.center = Vector3.up * (policeConfig.CollisionHeight * 0.5f);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = policeConfig.CollisionRadius;
            agent.height = policeConfig.CollisionHeight;
            agent.speed = policeConfig.PatrolMoveSpeed;
            agent.angularSpeed = 720f;
            agent.acceleration = 16f;

            GameObject view = (GameObject)PrefabUtility.InstantiatePrefab(model);
            view.name = "Character_View";
            view.transform.SetParent(root.transform, false);
            FitViewToCharacterHeight(view.transform, policeConfig.VisualHeight);

            Animator animator = view.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = view.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = animatorController;
            animator.applyRootMotion = false;

            CharacterAnimatorView animatorView = view.AddComponent<CharacterAnimatorView>();
            SerializedObject serializedAnimatorView = new(animatorView);
            serializedAnimatorView.FindProperty("animator").objectReferenceValue = animator;
            serializedAnimatorView.FindProperty("config").objectReferenceValue = animationConfig;
            serializedAnimatorView.ApplyModifiedPropertiesWithoutUndo();

            GameObject sightOriginObject = new("SightOrigin");
            sightOriginObject.transform.SetParent(root.transform, false);
            sightOriginObject.transform.localPosition = Vector3.up * policeConfig.SightOriginHeight;

            PoliceEnemyBrainGlue brain = root.AddComponent<PoliceEnemyBrainGlue>();
            SerializedObject serializedBrain = new(brain);
            serializedBrain.FindProperty("config").objectReferenceValue = policeConfig;
            serializedBrain.FindProperty("navigationAgent").objectReferenceValue = agent;
            serializedBrain.FindProperty("sightOrigin").objectReferenceValue = sightOriginObject.transform;
            serializedBrain.FindProperty("patrolPoints").arraySize = 0;
            serializedBrain.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>모델의 실제 Renderer 크기를 기준으로 발이 바닥에 닿고 키가 목표 높이가 되게 맞춥니다.</summary>
    private static void FitViewToCharacterHeight(Transform view, float targetHeight)
    {
        Renderer[] renderers = view.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        if (bounds.size.y <= 0.0001f)
        {
            return;
        }

        float scale = targetHeight / bounds.size.y;
        view.localScale = Vector3.one * scale;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        view.position += Vector3.up * (view.parent.position.y - bounds.min.y);
    }

    /// <summary>경찰 설정이 새 적 프리팹을 생성 대상으로 사용하게 연결합니다.</summary>
    private static void ConnectPoliceConfig(PoliceEnemyConfig policeConfig, GameObject prefab)
    {
        SerializedObject serializedConfig = new(policeConfig);
        serializedConfig.FindProperty("policePrefab").objectReferenceValue = prefab;
        serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(policeConfig);
    }

    /// <summary>서버가 생성한 경찰을 Client도 Spawn할 수 있도록 기본 NGO 프리팹 목록에 등록합니다.</summary>
    private static void RegisterNetworkPrefab(GameObject prefab)
    {
        Object networkPrefabs = AssetDatabase.LoadMainAssetAtPath(NetworkPrefabsPath);
        SerializedObject serializedList = new(networkPrefabs);
        SerializedProperty list = serializedList.FindProperty("List");

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == prefab)
            {
                return;
            }
        }

        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        SerializedProperty entry = list.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("Override").boolValue = false;
        entry.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("SourcePrefabToOverride").objectReferenceValue = null;
        entry.FindPropertyRelative("SourceHashToOverride").uintValue = 0;
        entry.FindPropertyRelative("OverridingTargetPrefab").objectReferenceValue = null;
        serializedList.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(networkPrefabs);
    }

    /// <summary>요청한 Assets 하위 폴더가 없으면 단계별로 생성합니다.</summary>
    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
