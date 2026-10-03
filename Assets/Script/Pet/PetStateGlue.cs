using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace EchoZone.Pet
{
    /// <summary>서버 권한 펫 행동 상태입니다. 체력 0은 영구 사망이 아닌 도주 계기입니다.</summary>
    public enum PetBehaviourState : byte { Following, Fleeing, Waiting, CollectionWaiting }

    /// <summary>펫별 주인과 도주·대기·회복을 관리하며 이동은 Follow Glue에 위임합니다.</summary>
    [RequireComponent(typeof(PetFollowGlue), typeof(PlayerStats), typeof(PetEscapePlanner))]
    public sealed partial class PetStateGlue : NetworkBehaviour
    {
        /// <summary>카탈로그의 종류 ID입니다. 개체별 PetId와 별개이며 프리팹에서 지정합니다.</summary>
        [SerializeField] private string definitionId;
        /// <summary>복구용 종류 식별자입니다.</summary>
        public string DefinitionId => definitionId;
        /// <summary>카탈로그 연결 검증용 행동 설정입니다.</summary>
        public PetBehaviourConfig BehaviourConfig => config;
        /// <summary>마이그레이션과 회수 완료 기록에 사용할 펫 고정 식별자입니다.</summary>
        public string PetId { get; private set; } = System.Guid.NewGuid().ToString("N");
        /// <summary>이전 주인 신원입니다. 도주 및 재접속 대기에서도 보존합니다.</summary>
        private string ownerPlayerId;
        /// <summary>복원된 주인의 접속을 기다릴 때 인계를 막습니다.</summary>
        private bool awaitingOwner;
        /// <summary>도주·상호작용·회복 규칙입니다.</summary>
        [SerializeField] private PetBehaviourConfig config;
        /// <summary>게임상의 주인 참조입니다. NGO 소유권은 계속 서버에 둡니다.</summary>
        private readonly NetworkVariable<NetworkObjectReference> owner = new();
        /// <summary>모든 피어에 공개할 행동 상태입니다.</summary>
        private readonly NetworkVariable<PetBehaviourState> state = new(PetBehaviourState.Waiting);
        /// <summary>기존 추종과 외형 갱신입니다.</summary>
        private PetFollowGlue follow;
        /// <summary>공통 체력입니다.</summary>
        private PlayerStats stats;
        /// <summary>도주 계획기입니다.</summary>
        private PetEscapePlanner planner;
        /// <summary>서버 이동 Agent입니다.</summary>
        private NavMeshAgent agent;
        /// <summary>절도 작업 중 일반 추종을 양보할 작업 Glue입니다.</summary>
        private EchoZone.Heist.PetHeistGlue heist;
        /// <summary>도주 시작 때 저장한 원의 중심입니다.</summary>
        private Vector3 escapeCenter;
        /// <summary>마지막으로 승인한 도주 목적지입니다.</summary>
        private Vector3 escapeDestination;
        /// <summary>다음 위험 검사 시각입니다.</summary>
        private float nextCheck;
        /// <summary>도주 경로를 확보했는지 나타냅니다.</summary>
        private bool hasEscapeDestination;
        /// <summary>대기 회복의 소수 체력 누적치입니다.</summary>
        private float recovery;
        /// <summary>도주 후 대기한 펫만 새로운 경찰 접근에 재도주합니다.</summary>
        private bool watchThreats;
        /// <summary>경로 계산과 독립적으로 관리하는 도주 회차입니다.</summary>
        private readonly PetEscapeEpisodeBrick escapeEpisode = new();
        /// <summary>장물이 없어도 경찰에게 회수를 요청하는 최종 대기 상태입니다.</summary>
        public bool IsCollectionWaiting => State == PetBehaviourState.CollectionWaiting;
        /// <summary>도주 회차 중에는 한도와 유예를 모두 충족하기 전 경찰 회수를 막습니다.</summary>
        public bool AllowsPoliceRecovery => IsCollectionWaiting || (State == PetBehaviourState.Waiting && !escapeEpisode.Active);
        /// <summary>현재 네트워크 행동 상태입니다.</summary>
        public PetBehaviourState State => state.Value;
        /// <summary>안전 경로 부재를 Inspector/테스트에서 확인할 수 있습니다.</summary>
        public bool HasEscapeDestination => hasEscapeDestination;
        /// <summary>승인한 목적지입니다.</summary>
        public Vector3 EscapeDestination => escapeDestination;
        /// <summary>클라이언트에서도 사용할 대기 및 체력 조건입니다.</summary>
        public bool CanBeClaimed => IsSpawned && (State == PetBehaviourState.Waiting || IsCollectionWaiting) && stats != null && stats.CurrentHealth > 0 &&
            !(EchoZone.Heist.HeistWorldGlue.Instance?.IsRetiredPet(PetId) ?? false) &&
            !(EchoZone.Heist.HeistWorldGlue.Instance?.IsSettlementPet(PetId) ?? false);

        /// <summary>같은 펫의 연결을 준비합니다.</summary>
        private void Awake()
        {
            follow = GetComponent<PetFollowGlue>();
            stats = GetComponent<PlayerStats>();
            planner = GetComponent<PetEscapePlanner>();
            agent = GetComponent<NavMeshAgent>();
            heist = GetComponent<EchoZone.Heist.PetHeistGlue>();
        }

        /// <summary>주인이 없는 펫도 갱신 목록에 등록합니다.</summary>
        public override void OnNetworkSpawn() => PetUpdateManager.Register(this);
        /// <summary>디스폰 및 세션 종료 때 목록에서 제거합니다.</summary>
        public override void OnNetworkDespawn() => PetUpdateManager.Unregister(this);

        /// <summary>주인 참조를 현재 피어의 네트워크 객체로 해석합니다.</summary>
        public bool TryGetOwner(out NetworkObject value)
        {
            value = null;
            return State == PetBehaviourState.Following && owner.Value.TryGet(out value, NetworkManager);
        }
        /// <summary>플레이어 소속 관계를 비교합니다.</summary>
        public bool IsOwnedBy(NetworkObject value) => value != null && TryGetOwner(out var current) && current == value;

        /// <summary>최초 생성 및 검증된 상호작용 뒤 서버만 주인을 바꿉니다.</summary>
        public void AssignOwnerServer(NetworkObject player)
        {
            if (!IsServer || !IsSpawned || player == null || !player.IsSpawned || !player.IsPlayerObject) return;
            owner.Value = new NetworkObjectReference(player);
            ownerPlayerId = EchoZone.Heist.HeistWorldGlue.PlayerIdentity(player.OwnerClientId);
            awaitingOwner = false;
            state.Value = PetBehaviourState.Following;
            escapeEpisode.Reset();
            stats.SetDamageBlocked(false);
            watchThreats = hasEscapeDestination = false;
            recovery = 0f;
            follow.ResetFollowTarget();
            EchoZone.Heist.HeistWorldGlue.Instance?.NotifyAcquired(this, player);
        }

        /// <summary>주인 사망 또는 퇴장 시 그 자리에 멈추고 누구나 인계할 수 있게 합니다.</summary>
        public void WaitServer()
        {
            if (!IsServer || !IsSpawned) return;
            heist?.CancelServer();
            owner.Value = default;
            state.Value = PetBehaviourState.Waiting;
            ownerPlayerId = null; awaitingOwner = false;
            watchThreats = hasEscapeDestination = false;
            stats.SetDamageBlocked(false);
            follow.StopServer();
        }

        /// <summary>체력 소진 시 도주를 시작합니다. 도주 중에는 반복 피해를 막습니다.</summary>
        public void BeginEscapeServer()
        {
            if (!IsServer || !IsSpawned || config == null || State == PetBehaviourState.Fleeing || IsCollectionWaiting ||
                !escapeEpisode.TryBegin(config.MaximumEscapeCount, config.ReclaimGraceSeconds, config.EscapeFailureSeconds)) return;
            heist?.CancelServer();
            if (TryGetOwner(out var current)) escapeCenter = current.transform.position;
            else if (!watchThreats) escapeCenter = transform.position;
            owner.Value = default;
            state.Value = PetBehaviourState.Fleeing;
            stats.SetDamageBlocked(true);
            watchThreats = true;
            hasEscapeDestination = false;
            nextCheck = 0f;
            recovery = 0f;
            follow.StopServer();
        }

        /// <summary>중앙 목록에서 한 번 호출해 서버 행동과 피어별 표시를 갱신합니다.</summary>
        public void ManualUpdate(float deltaTime)
        {
            if (!IsSpawned || config == null) return;
            Transform target = null;
            if (IsServer)
            {
                if ((EchoZone.Heist.HeistWorldGlue.Instance?.IsRetiredPet(PetId) ?? false) ||
                    (EchoZone.Heist.HeistWorldGlue.Instance?.IsSettlementPet(PetId) ?? false))
                { follow.StopServer(); return; }
                escapeEpisode.Tick(deltaTime, State == PetBehaviourState.Fleeing);
                if (State == PetBehaviourState.Fleeing && escapeEpisode.Failed)
                { FinishFailedEscapeServer(); }
                if (State == PetBehaviourState.Following)
                {
                    if (awaitingOwner)
                    {
                        foreach (var client in NetworkManager.ConnectedClientsList)
                            if (client.PlayerObject != null && EchoZone.Heist.HeistWorldGlue.PlayerIdentity(client.ClientId) == ownerPlayerId)
                            { owner.Value = new NetworkObjectReference(client.PlayerObject); awaitingOwner = false; break; }
                    }
                    if (awaitingOwner) { follow.StopServer(); return; }
                    if (!TryGetOwner(out var player) || !player.IsSpawned ||
                        (player.TryGetComponent<PlayerStats>(out var ownerStats) && ownerStats.IsDead)) WaitServer();
                    else target = player.transform;
                }
                if (State == PetBehaviourState.Fleeing) TickEscape();
                if (State == PetBehaviourState.Waiting || IsCollectionWaiting) TickWaiting(deltaTime);
                heist?.ManualUpdateServer();
                if (!IsSpawned) return;
                if (heist != null && heist.IsBusy) target = null;
            }
            follow.ManualUpdate(target, deltaTime);
        }

        /// <summary>현재 위치에서 안전 목적지와 남은 경로를 주기적으로 검증합니다.</summary>
        private void TickEscape()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + Mathf.Max(0.1f, config.RecheckSeconds);
                planner.RefreshThreats(agent);
                bool arrived = hasEscapeDestination && PetEscapeBrick.Distance(transform.position, escapeDestination) <= config.ArrivalDistance + 0.1f;
                bool invalid = !hasEscapeDestination || !planner.IsSafe(escapeDestination) ||
                    (!arrived && !agent.pathPending && (agent.pathStatus != NavMeshPathStatus.PathComplete || !planner.IsEscapePathSafe(agent.path.corners)));
                if (invalid)
                {
                    hasEscapeDestination = planner.TryPlan(agent, escapeCenter, out escapeDestination);
                    if (hasEscapeDestination) follow.MoveServer(escapeDestination, config.EscapeSpeed, config.ArrivalDistance);
                    else follow.StopServer();
                }
            }
            if (hasEscapeDestination && !agent.pathPending &&
                PetEscapeBrick.Distance(transform.position, escapeDestination) <= config.ArrivalDistance + 0.1f)
            {
                planner.RefreshThreats(agent);
                if (!planner.IsSafe(transform.position)) { hasEscapeDestination = false; return; }
                state.Value = PetBehaviourState.Waiting;
                stats.SetDamageBlocked(false);
                follow.StopServer();
            }
        }

        /// <summary>도주 제한시간이 끝나면 현재 위치 가까운 유효 NavMesh로 보정하고 유예를 유지한 채 대기합니다.</summary>
        private void FinishFailedEscapeServer()
        {
            if (agent != null && agent.enabled && !agent.isOnNavMesh)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (NavMesh.SamplePosition(transform.position, out var hit, config.SampleRadius, filter)) agent.Warp(hit.position);
            }
            state.Value = PetBehaviourState.Waiting;
            hasEscapeDestination = false; stats.SetDamageBlocked(false); follow.StopServer();
        }

        /// <summary>정지 상태에서 소수 회복량을 누적하고 도주 후 은신 위치의 위험을 다시 검사합니다.</summary>
        private void TickWaiting(float deltaTime)
        {
            if (escapeEpisode.CanCollect(config.MaximumEscapeCount))
            { state.Value = PetBehaviourState.CollectionWaiting; watchThreats = false; follow.StopServer(); }
            if (!IsCollectionWaiting && !escapeEpisode.Failed && escapeEpisode.Count < config.MaximumEscapeCount && watchThreats && Time.time >= nextCheck)
            {
                nextCheck = Time.time + Mathf.Max(0.1f, config.RecheckSeconds);
                planner.RefreshThreats(agent);
                if (!planner.IsSafe(transform.position)) { BeginEscapeServer(); return; }
            }
            if (stats.CurrentHealth >= stats.MaxHealth) { recovery = 0f; return; }
            recovery += Mathf.Max(0f, deltaTime) * Mathf.Max(0f, config.RecoveryPerSecond);
            int amount = Mathf.FloorToInt(recovery);
            if (amount > 0) { recovery -= amount; stats.RestoreHealth(amount); }
        }

        /// <summary>서버에서 생존 플레이어·대기 상태·거리·벽 차단을 모두 검사한 뒤 인계합니다.</summary>
        public bool TryClaimServer(GameObject interactor)
        {
            if (!IsServer || !CanBeClaimed || config == null || interactor == null ||
                !interactor.TryGetComponent<NetworkObject>(out var player) || !player.IsPlayerObject || !player.IsSpawned ||
                !interactor.TryGetComponent<PlayerStats>(out var playerStats) || playerStats.IsDead ||
                Vector3.Distance(transform.position, player.transform.position) > config.ClaimDistance) return false;
            Vector3 start = player.transform.position + Vector3.up * config.ClaimRayHeight;
            Vector3 end = GetComponent<Collider>().bounds.center;
            foreach (var hit in Physics.RaycastAll(start, (end - start).normalized, Vector3.Distance(start, end), config.ClaimBlockingLayers, QueryTriggerInteraction.Ignore))
            {
                var obj = hit.collider.GetComponentInParent<NetworkObject>();
                if (obj != player && obj != NetworkObject) return false;
            }
            AssignOwnerServer(player);
            return true;
        }
    }
}
