using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace EchoZone.Pet
{
    /// <summary>시작 펫 한 마리를 지급하고 주인 사망·퇴장 시 소속 펫 전부를 대기시킵니다.</summary>
    public sealed class PlayerPetGlue : NetworkBehaviour
    {
        /// <summary>등록된 네트워크 강아지 프리팹입니다.</summary>
        [SerializeField] private PetFollowGlue petPrefab;
        /// <summary>마이그레이션에도 동일한 등록 프리팹을 사용합니다.</summary>
        public PetFollowGlue PetPrefab => petPrefab;
        /// <summary>생성 위치와 NavMesh 검색 설정입니다.</summary>
        [SerializeField] private PetFollowConfig config;
        /// <summary>주인 사망을 즉시 통지받을 체력입니다.</summary>
        private PlayerStats stats;

        /// <summary>플레이어가 생성된 뒤 서버에서 펫을 한 번 생성합니다.</summary>
        protected override void OnNetworkPostSpawn()
        {
            if (!IsServer) return;
            stats = GetComponent<PlayerStats>();
            if (stats != null) stats.AddHealthChangedListener(HandleHealthChanged);
            EnsureStarterServer();
        }

        /// <summary>월드가 PlayerObject보다 늦게 스폰된 경우 기존 중앙 루프에서 최초 지급을 재시도합니다.</summary>
        public void EnsureStarterServer()
        {
            if (!IsServer || !IsSpawned) return;
            if (petPrefab == null || config == null) return;
            if (EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring) return;
            var world = EchoZone.Heist.HeistWorldGlue.Instance;
            if (world == null || !world.TryGrantStarter(EchoZone.Heist.HeistWorldGlue.PlayerIdentity(OwnerClientId))) return;
            Vector3 position = transform.position - Vector3.forward * config.FollowDistance;
            var sourceAgent = petPrefab.GetComponent<NavMeshAgent>();
            var filter = new NavMeshQueryFilter { agentTypeID = sourceAgent.agentTypeID, areaMask = sourceAgent.areaMask };
            if (NavMesh.SamplePosition(position, out var hit, config.SampleRadius, filter)) position = hit.position;
            var pet = Instantiate(petPrefab, position, Quaternion.identity);
            pet.NetworkObject.Spawn();
            pet.GetComponent<PetStateGlue>().AssignOwnerServer(NetworkObject);
        }

        /// <summary>체력 0 통지에서 여러 소속 펫의 추종을 동시에 해제합니다.</summary>
        private void HandleHealthChanged(int health)
        {
            if (health <= 0 && IsServer) PetUpdateManager.ReleaseOwned(NetworkObject);
        }

        /// <summary>퇴장한 주인의 펫은 월드에 남기고 이벤트만 해제합니다.</summary>
        public override void OnNetworkDespawn()
        {
            if (stats != null) stats.RemoveHealthChangedListener(HandleHealthChanged);
            if (IsServer && NetworkManager != null && !NetworkManager.ShutdownInProgress)
                PetUpdateManager.ReleaseOwned(NetworkObject);
        }
    }
}
