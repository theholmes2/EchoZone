using EchoZone.Enemy;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Combat.Glue
{
    /// <summary>공통 보관 Brick을 총알의 서버 대여와 NGO 클라이언트 생성·반환에 연결합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileNetworkPoolGlue : MonoBehaviour, INetworkPrefabInstanceHandler
    {
        /// <summary>등록 프리팹과 보관 한도입니다.</summary>
        [SerializeField] private ProjectilePoolConfig config;
        /// <summary>중복 반환을 막는 기존 순수 풀입니다.</summary>
        private readonly ReusePoolBrick<NetworkObject> pool = new();
        /// <summary>현재 핸들러가 등록된 NGO 관리자입니다.</summary>
        private NetworkManager registeredManager;
        /// <summary>초기 생성 중 활성화 순서를 제어할 비활성 부모입니다.</summary>
        private Transform inactiveRoot;
        /// <summary>발사 Glue가 사용할 씬의 풀입니다.</summary>
        public static ProjectileNetworkPoolGlue Instance { get; private set; }
        /// <summary>실제로 재사용 가능한 유휴 총알 수입니다.</summary>
        public int InactiveCount => pool.Count;

        /// <summary>접속 전에 서버·클라이언트 모두의 풀을 공개합니다.</summary>
        private void Awake() { Instance = this; EnsureInitialized(); }
        /// <summary>NetworkManager의 Awake 순서가 늦었을 때 등록을 다시 시도합니다.</summary>
        private void Start() => EnsureInitialized();

        /// <summary>기존 투사체 관리자에서도 접속 전에 등록 상태를 보장합니다.</summary>
        public bool EnsureInitialized()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || config == null || config.ProjectilePrefab == null) return false;
            if (registeredManager == manager) return true;
            if (registeredManager != null) registeredManager.PrefabHandler.RemoveHandler(config.ProjectilePrefab);
            ClearInactive();
            if (!manager.PrefabHandler.AddHandler(config.ProjectilePrefab, this))
            {
                Debug.LogError("Projectile prefab already has another handler.", this);
                return false;
            }
            registeredManager = manager;
            return true;
        }

        /// <summary>발사 설정의 프리팹이 이 풀의 종류와 일치하는지 검사합니다.</summary>
        public bool Supports(GameObject prefab) => config != null && config.ProjectilePrefab == prefab;

        /// <summary>서버와 원격 클라이언트가 동일한 초기화 경로로 총알을 빌립니다.</summary>
        public NetworkObject Rent(Vector3 position, Quaternion rotation)
        {
            if (!EnsureInitialized()) return null;
            NetworkObject instance = null;
            while (pool.TryTake(out var candidate))
                if (candidate != null) { instance = candidate; break; }
            if (instance == null)
            {
                if (inactiveRoot == null)
                {
                    var holder = new GameObject("ProjectilePool_Inactive");
                    holder.transform.SetParent(transform, false);
                    holder.SetActive(false);
                    inactiveRoot = holder.transform;
                }
                instance = Instantiate(config.ProjectilePrefab, inactiveRoot).GetComponent<NetworkObject>();
                instance.gameObject.SetActive(false);
            }
            instance.transform.SetParent(null, true);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.GetComponent<ProjectileNetworkGlue>().ResetForReuse();
            instance.gameObject.SetActive(true);
            return instance;
        }

        /// <summary>클라이언트가 받은 Spawn 메시지를 로컬 풀 대여로 치환합니다.</summary>
        NetworkObject INetworkPrefabInstanceHandler.Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            => Rent(position, rotation);

        /// <summary>모든 피어의 NGO 파괴 요청을 유휴 보관으로 치환하고 초과분만 파괴합니다.</summary>
        void INetworkPrefabInstanceHandler.Destroy(NetworkObject instance)
        {
            if (instance == null || pool.Contains(instance)) return;
            instance.GetComponent<ProjectileNetworkGlue>()?.ResetForReuse();
            instance.gameObject.SetActive(false);
            if (!pool.TryReturn(instance, config.MaximumRetainedCount)) Destroy(instance.gameObject);
        }

        /// <summary>관리자 교체와 씬 종료 시 유휴 총알을 실제 파괴합니다.</summary>
        private void ClearInactive()
        {
            while (pool.TryTake(out var instance))
                if (instance != null) Destroy(instance.gameObject);
        }

        /// <summary>핸들러와 정적 참조를 해제합니다.</summary>
        private void OnDestroy()
        {
            if (registeredManager != null && config != null) registeredManager.PrefabHandler.RemoveHandler(config.ProjectilePrefab);
            ClearInactive();
            if (Instance == this) Instance = null;
        }
    }
}
