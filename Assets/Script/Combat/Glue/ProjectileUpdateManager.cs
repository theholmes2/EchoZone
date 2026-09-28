using System.Collections.Generic;
using UnityEngine;

namespace EchoZone.Combat.Glue
{
    /// <summary>활성 투사체의 수동 물리 업데이트 순서만 관리하는 작은 전용 Manager입니다.</summary>
    public sealed class ProjectileUpdateManager : MonoBehaviour
    {
        /// <summary>instance 값을 저장합니다.</summary>
        private static ProjectileUpdateManager instance;
        /// <summary>projectiles 값을 저장합니다.</summary>
        private readonly List<ProjectileNetworkGlue> projectiles = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        /// <summary>CreateRuntimeInstance 작업을 수행합니다.</summary>
        private static void CreateRuntimeInstance()
        {
            EnsureInstance();
        }

        /// <summary>Register 작업을 수행합니다.</summary>
        public static void Register(ProjectileNetworkGlue projectile)
        {
            EnsureInstance();
            if (projectile != null && !instance.projectiles.Contains(projectile))
            {
                instance.projectiles.Add(projectile);
            }
        }

        /// <summary>Unregister 작업을 수행합니다.</summary>
        public static void Unregister(ProjectileNetworkGlue projectile)
        {
            if (instance != null)
            {
                instance.projectiles.Remove(projectile);
            }
        }

        /// <summary>FixedUpdate 작업을 수행합니다.</summary>
        private void FixedUpdate()
        {
            ProjectileNetworkPoolGlue.Instance?.EnsureInitialized();
            for (int index = projectiles.Count - 1; index >= 0; index--)
            {
                ProjectileNetworkGlue projectile = projectiles[index];
                if (projectile == null)
                {
                    projectiles.RemoveAt(index);
                    continue;
                }

                projectile.ManualFixedUpdate(Time.fixedDeltaTime);
            }
        }

        /// <summary>OnDestroy 작업을 수행합니다.</summary>
        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>EnsureInstance 작업을 수행합니다.</summary>
        private static void EnsureInstance()
        {
            if (instance != null)
            {
                return;
            }

            instance = FindFirstObjectByType<ProjectileUpdateManager>();
            if (instance != null)
            {
                return;
            }

            GameObject managerObject = new(nameof(ProjectileUpdateManager));
            instance = managerObject.AddComponent<ProjectileUpdateManager>();
            DontDestroyOnLoad(managerObject);
        }
    }
}
