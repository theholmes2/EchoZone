using System.Collections.Generic;
using UnityEngine;

namespace EchoZone.Combat.Glue
{
    /// <summary>활성 투사체의 수동 물리 업데이트 순서만 관리하는 작은 전용 Manager입니다.</summary>
    public sealed class ProjectileUpdateManager : MonoBehaviour
    {
        private static ProjectileUpdateManager instance;
        private readonly List<ProjectileNetworkGlue> projectiles = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateRuntimeInstance()
        {
            EnsureInstance();
        }

        public static void Register(ProjectileNetworkGlue projectile)
        {
            EnsureInstance();
            if (projectile != null && !instance.projectiles.Contains(projectile))
            {
                instance.projectiles.Add(projectile);
            }
        }

        public static void Unregister(ProjectileNetworkGlue projectile)
        {
            if (instance != null)
            {
                instance.projectiles.Remove(projectile);
            }
        }

        private void FixedUpdate()
        {
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

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

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
