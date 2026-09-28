using EchoZone.Player.View;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace EchoZone.Enemy
{
    /// <summary>순수 보관 Brick을 NGO 프리팹 생성·반환 및 Unity 객체 수명과 연결합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyNetworkPoolGlue : MonoBehaviour, INetworkPrefabInstanceHandler
    {
        /// <summary>풀링할 경찰 프리팹 데이터입니다.</summary>
        [SerializeField] private PoliceEnemyConfig policeConfig;
        /// <summary>시체 유지시간과 보관 수 설정입니다.</summary>
        [SerializeField] private EnemyPoolConfig poolConfig;
        /// <summary>비활성 객체의 중복 반환을 방지하는 독립 보관 Brick입니다.</summary>
        private readonly ReusePoolBrick<NetworkObject> pool = new();
        /// <summary>프리팹 핸들러를 등록한 네트워크 관리자입니다.</summary>
        private NetworkManager registeredManager;
        /// <summary>최초 생성 시 OnEnable이 너무 일찍 호출되지 않도록 하는 비활성 부모입니다.</summary>
        private Transform inactiveRoot;
        /// <summary>현재 피어의 유휴 객체 수입니다.</summary>
        public int InactiveCount => pool.Count;
        /// <summary>서버에서 사망 후 유지할 시간입니다.</summary>
        public float CorpseLifetimeSeconds => poolConfig.CorpseLifetimeSeconds;

        /// <summary>씬 준비 시 연결을 시도합니다. 생성 순서가 늦으면 기존 런타임 Glue가 재시도합니다.</summary>
        private void Start() => EnsureInitialized();

        /// <summary>Client가 스폰 메시지를 받기 전에 핸들러를 등록합니다.</summary>
        public bool EnsureInitialized()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || policeConfig == null || policeConfig.PolicePrefab == null || poolConfig == null) return false;
            if (registeredManager == manager) return true;
            if (registeredManager != null) registeredManager.PrefabHandler.RemoveHandler(policeConfig.PolicePrefab);
            ClearInactive();
            if (!manager.PrefabHandler.AddHandler(policeConfig.PolicePrefab, this))
            {
                Debug.LogError("Police prefab already has a different pool handler.", this);
                return false;
            }
            registeredManager = manager;
            return true;
        }

        /// <summary>서버 스포너와 Client NGO 핸들러가 공통으로 사용할 대여 경로입니다.</summary>
        public NetworkObject Rent(Vector3 position, Quaternion rotation)
        {
            if (!EnsureInitialized()) return null;
            NetworkObject instance = null;
            while (pool.TryTake(out var candidate))
            {
                if (candidate != null) { instance = candidate; break; }
            }
            if (instance == null)
            {
                if (inactiveRoot == null)
                {
                    var holder = new GameObject("PolicePool_Inactive");
                    holder.transform.SetParent(transform, false);
                    holder.SetActive(false);
                    inactiveRoot = holder.transform;
                }
                instance = UnityEngine.Object.Instantiate(policeConfig.PolicePrefab, inactiveRoot).GetComponent<NetworkObject>();
                instance.gameObject.SetActive(false);
            }
            instance.transform.SetParent(null, true);
            instance.transform.SetPositionAndRotation(position, rotation);
            var agent = instance.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            instance.gameObject.SetActive(true);
            var stats = instance.GetComponent<PlayerStats>();
            stats.SetDamageBlocked(false);
            stats.ResetToMaximum();
            instance.GetComponentInChildren<CharacterAnimatorView>(true)?.ResetForSpawn();
            instance.GetComponent<PoliceDeadEventGlue>().ConfigurePool(this);
            return instance;
        }

        /// <summary>원격 피어의 NGO 스폰을 로컬 풀 대여에 연결합니다.</summary>
        NetworkObject INetworkPrefabInstanceHandler.Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            => Rent(position, rotation);

        /// <summary>서버와 Client의 NGO 파괴 요청을 비활성 보관으로 치환합니다.</summary>
        void INetworkPrefabInstanceHandler.Destroy(NetworkObject instance)
        {
            if (instance == null || pool.Contains(instance)) return;
            instance.gameObject.SetActive(false);
            if (!pool.TryReturn(instance, poolConfig.MaximumRetainedCount))
                UnityEngine.Object.Destroy(instance.gameObject);
        }

        /// <summary>서버만 반환을 승인하며 모든 피어에 디스폰을 전달합니다.</summary>
        public void DespawnServer(NetworkObject instance)
        {
            if (registeredManager != null && registeredManager.IsServer && instance != null && instance.IsSpawned)
                instance.Despawn(true);
        }

        /// <summary>씬 또는 관리자가 사라질 때 비활성 객체를 실제로 정리합니다.</summary>
        private void ClearInactive()
        {
            while (pool.TryTake(out var instance))
                if (instance != null) UnityEngine.Object.Destroy(instance.gameObject);
        }

        /// <summary>등록과 보관 객체를 해제합니다.</summary>
        private void OnDestroy()
        {
            if (registeredManager != null && policeConfig != null)
                registeredManager.PrefabHandler.RemoveHandler(policeConfig.PolicePrefab);
            ClearInactive();
        }
    }
}
