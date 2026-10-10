using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Enemy
{
    /// <summary>Unity Update를 서버의 적 스폰·AI 및 각 피어의 View 갱신에 연결합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyRuntimeUpdateGlue : MonoBehaviour
    {
        /// <summary>경찰 생성 규칙을 수동 갱신할 작은 전용 Manager입니다.</summary>
        [SerializeField] private EnemySpawnManager spawnManager;

        /// <summary>활성 경찰 AI를 등록 순서에 따라 갱신할 작은 전용 Manager입니다.</summary>
        [SerializeField] private EnemyUpdateManager updateManager;
        /// <summary>모든 피어에서 접속 전 프리팹 풀 핸들러를 준비합니다.</summary>
        private EnemyNetworkPoolGlue pool;
        /// <summary>이전 프레임에 서버 세션을 운영했는지 나타냅니다.</summary>
        private bool wasServer;
        /// <summary>같은 씬 루프에서 갱신할 로컬 범죄 HUD입니다.</summary>
        [SerializeField] private EchoZone.Heist.HeistHudGlue heistHud;

        /// <summary>스폰 이후 AI 판단 순서를 매 프레임 한 번 실행합니다.</summary>
        private void Update()
        {
            heistHud?.ManualUpdate();
            if (pool == null) pool = GetComponent<EnemyNetworkPoolGlue>();
            pool?.EnsureInitialized();
            NetworkManager networkManager = NetworkManager.Singleton;
            bool serverActive = networkManager != null && networkManager.IsListening && networkManager.IsServer;
            if (wasServer && !serverActive) spawnManager?.ResetSession();
            wasServer = serverActive;
            if (networkManager == null || !networkManager.IsListening)
            {
                return;
            }

            float serverTime = (float)networkManager.ServerTime.TimeAsFloat;
            if (EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring) return;
            if (networkManager.IsServer)
            {
                EchoZone.Online.Migration.SessionWorldMigrationGlue.ApplyPendingPlayers();
                foreach (var client in networkManager.ConnectedClientsList)
                    client.PlayerObject?.GetComponent<EchoZone.Pet.PlayerPetGlue>()?.EnsureStarterServer();
                EchoZone.Heist.HeistWorldGlue.Instance?.ManualUpdateServer(networkManager.ServerTime.Time);
                spawnManager?.ManualUpdate(serverTime);
                EchoZone.Equipment.LootWorldGlue.Instance?.ManualUpdate(serverTime);
            }
            updateManager?.ManualUpdate(serverTime, Time.deltaTime);
            EchoZone.Pet.PetUpdateManager.ManualUpdate(Time.deltaTime);
        }
    }
}
