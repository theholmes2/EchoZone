using Combat.Glue;
using Combat.View;
using EchoZone.Combat.Glue;
using UnityEditor;
using UnityEngine;

/// <summary>총구 섬광 View 프리팹을 만들고 네트워크 플레이어의 기존 Muzzle에 조립합니다.</summary>
public static class MuzzleFlashLightSetup
{
    private const string EffectPrefabPath = "Assets/Prefabs/Combat/MuzzleFlashLight.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player/KenneyNetworkPlayer.prefab";

    [InitializeOnLoadMethod]
    private static void QueueSetup()
    {
        // 이미 구성한 프리팹의 수동 위치와 계층은 컴파일 시 덮어쓰지 않습니다.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(EffectPrefabPath) == null)
        {
            EditorApplication.delayCall += Apply;
        }
    }

    [MenuItem("EchoZone/Combat/Build Muzzle Flash Light")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        GameObject effectPrefab = BuildEffectPrefab();
        ConnectPlayerPrefab(effectPrefab);
        AssetDatabase.SaveAssets();
        Debug.Log("MUZZLE_FLASH_LIGHT_READY: local Point Light View connected to approved fire event.");
    }

    private static GameObject BuildEffectPrefab()
    {
        GameObject root = new("MuzzleFlashLight");
        try
        {
            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.48f, 0.12f);
            light.intensity = 8f;
            light.range = 5f;
            light.shadows = LightShadows.Soft;
            light.enabled = false;

            MuzzleFlashEffectBrick effect = root.AddComponent<MuzzleFlashEffectBrick>();
            SerializedObject serializedEffect = new(effect);
            serializedEffect.FindProperty("muzzleLight").objectReferenceValue = light;
            serializedEffect.FindProperty("lightDuration").floatValue = 0.05f;
            serializedEffect.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, EffectPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ConnectPlayerPrefab(GameObject effectPrefab)
    {
        GameObject playerRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            NetworkWeaponFireGlue weaponFire = playerRoot.GetComponent<NetworkWeaponFireGlue>();
            SerializedObject serializedWeapon = new(weaponFire);
            Transform muzzle = serializedWeapon.FindProperty("muzzle").objectReferenceValue as Transform;
            if (weaponFire == null || muzzle == null || effectPrefab == null)
            {
                throw new MissingReferenceException("Network weapon, Muzzle, or muzzle flash prefab is missing.");
            }

            Transform existing = muzzle.Find(effectPrefab.name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject effectObject = (GameObject)PrefabUtility.InstantiatePrefab(
                effectPrefab,
                playerRoot.scene);
            effectObject.transform.SetParent(muzzle, false);
            effectObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            MuzzleFlashEffectBrick effect = effectObject.GetComponent<MuzzleFlashEffectBrick>();
            WeaponFireEventBinderGlue binder = playerRoot.GetComponent<WeaponFireEventBinderGlue>();
            if (binder == null)
            {
                binder = playerRoot.AddComponent<WeaponFireEventBinderGlue>();
            }

            SerializedObject serializedBinder = new(binder);
            serializedBinder.FindProperty("networkWeaponFireGlue").objectReferenceValue = weaponFire;
            serializedBinder.FindProperty("muzzleFlashEffectBrick").objectReferenceValue = effect;
            serializedBinder.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(playerRoot);
        }
    }
}
