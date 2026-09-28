using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Enemy
{
    /// <summary>서버 AI 갱신과 각 피어의 경찰 View 갱신 순서를 관리합니다.</summary>
    public sealed class EnemyUpdateManager : MonoBehaviour
    {
        /// <summary>현재 피어에서 매 프레임 갱신할 활성 경찰 목록입니다.</summary>
        private readonly List<PoliceEnemyBrainGlue> activeEnemies = new();

        /// <summary>스폰된 경찰을 중앙 업데이트 목록에 등록합니다.</summary>
        public void Register(PoliceEnemyBrainGlue enemy)
        {
            if (enemy == null || activeEnemies.Contains(enemy))
            {
                return;
            }

            activeEnemies.Add(enemy);
        }

        /// <summary>디스폰된 경찰을 중앙 업데이트 목록에서 제거합니다.</summary>
        public void Unregister(PoliceEnemyBrainGlue enemy)
        {
            if (enemy != null)
            {
                activeEnemies.Remove(enemy);
            }
        }

        /// <summary>서버에서만 AI를 갱신하고 모든 피어에서 이동 결과를 View로 전달합니다.</summary>
        public void ManualUpdate(float serverTime, float deltaTime)
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                PoliceEnemyBrainGlue enemy = activeEnemies[i];
                if (enemy == null || !enemy.IsSpawned)
                {
                    activeEnemies.RemoveAt(i);
                    continue;
                }

                if (IsServerActive())
                {
                    enemy.ManualUpdate(serverTime, deltaTime);
                }
                enemy.ManualUpdateView();
            }
        }

        /// <summary>현재 실행 환경이 권위 있는 서버인지 확인합니다.</summary>
        private static bool IsServerActive()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }
    }
}
