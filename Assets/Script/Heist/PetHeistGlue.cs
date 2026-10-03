using EchoZone.Pet;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace EchoZone.Heist
{
    /// <summary>펫의 건물 작업 표시 단계입니다.</summary>
    public enum PetHeistPhase : byte { None, Approaching, Stealing }
    /// <summary>소속 펫의 출입구 이동·실내 절도·장물 운반을 기존 이동 Glue와 연결합니다.</summary>
    [RequireComponent(typeof(PetStateGlue), typeof(HeistInteriorView))]
    public sealed class PetHeistGlue : NetworkBehaviour
    {
        /// <summary>프리팹별 펫 등급입니다. 실제 힘 수치는 공통 Config의 등급표에서 읽습니다.</summary>
        [SerializeField, Min(1)] private int grade = 1;
        /// <summary>스냅샷에 보존할 펫 등급입니다.</summary>
        public int Grade => grade;
        /// <summary>취소된 작업에는 돈을 지급하지 않고 완료된 운반량만 복원합니다.</summary>
        public void RestoreMigrationCargo(int amount, int restoredGrade)
        {
            if (!IsServer) return;
            CancelServer(); grade = Mathf.Max(1, restoredGrade); SetCargoServer(amount);
        }
        /// <summary>서버에서 진행 중인 건물 번호입니다.</summary>
        public int BuildingId => buildingId.Value;
        /// <summary>클라이언트의 건물별 작업 진행 표시를 위한 현재 대상 ID입니다.</summary>
        private readonly NetworkVariable<int> buildingId = new(-1);
        /// <summary>현재 적재량·등급을 적용한 서버 이동 속도 배율입니다.</summary>
        public float LoadSpeedMultiplier
        {
            get
            {
                var config = HeistWorldGlue.Instance != null ? HeistWorldGlue.Instance.Config : null;
                if (config == null) return 1f;
                var strengths = config.PetGradeStrength;
                float strength = strengths != null && strengths.Length > 0 ? strengths[Mathf.Clamp(grade - 1, 0, strengths.Length - 1)] : 1f;
                return PetLoadBrick.SpeedMultiplier(Cargo, config.PetCapacity, strength, config.MaximumLoadSlowdown);
            }
        }
        /// <summary>공개할 운반 현금입니다. 소유권이 바뀌어도 유지합니다.</summary>
        private readonly NetworkVariable<int> cargo = new();
        /// <summary>클라이언트의 내부 표시를 결정할 작업 단계입니다.</summary>
        private readonly NetworkVariable<PetHeistPhase> phase = new();
        /// <summary>현재 펫 작업의 서버 완료 시각입니다.</summary>
        private readonly NetworkVariable<double> finishAt = new();
        /// <summary>신고가 접수되어 경찰 회수를 기다리는지 나타냅니다.</summary>
        private readonly NetworkVariable<bool> reported = new();
        /// <summary>진행 중인 건물입니다. 서버에서만 사용합니다.</summary>
        private HeistBuildingSite site;
        /// <summary>현재 펫 소속 및 대기 상태입니다.</summary>
        private PetStateGlue state;
        /// <summary>기존 이동 제어입니다.</summary>
        private PetFollowGlue follow;
        /// <summary>NavMesh 도착 판정입니다.</summary>
        private NavMeshAgent agent;
        /// <summary>입장 표시입니다.</summary>
        private HeistInteriorView interior;
        /// <summary>현재 운반 중인 장물 총액입니다.</summary>
        public int Cargo => cargo.Value;
        /// <summary>다른 절도 작업을 배정할 수 없는지 나타냅니다.</summary>
        public bool IsBusy => phase.Value != PetHeistPhase.None;
        /// <summary>현재 건물 안에 들어간 상태입니다.</summary>
        public bool IsInside => phase.Value == PetHeistPhase.Stealing;
        /// <summary>작업 표시 단계입니다.</summary>
        public PetHeistPhase Phase => phase.Value;
        /// <summary>작업 완료 시각입니다.</summary>
        public double FinishAt => finishAt.Value;
        /// <summary>신고 접수 표시입니다.</summary>
        public bool Reported => reported.Value;
        /// <summary>경찰이 공격 대신 회수할 주인 없는 장물입니다.</summary>
        public bool IsAbandonedCargo => IsSpawned && Cargo > 0 && (state.State == PetBehaviourState.Waiting || state.IsCollectionWaiting);
        /// <summary>유예를 마친 수거 대기 펫은 장물이 없어도 회수할 수 있습니다.</summary>
        public bool CanBeRecovered => IsSpawned && state.AllowsPoliceRecovery && (Cargo > 0 || state.IsCollectionWaiting) &&
            !(HeistWorldGlue.Instance?.IsRetiredPet(state.PetId) ?? false) &&
            !(HeistWorldGlue.Instance?.IsSettlementPet(state.PetId) ?? false);
        /// <summary>기존 컴포넌트를 캐시합니다.</summary>
        private void Awake() { state = GetComponent<PetStateGlue>(); follow = GetComponent<PetFollowGlue>(); agent = GetComponent<NavMeshAgent>(); interior = GetComponent<HeistInteriorView>(); }
        /// <summary>늦게 참가한 피어도 내부 상태를 적용합니다.</summary>
        public override void OnNetworkSpawn() { phase.OnValueChanged += Changed; interior.SetInside(IsInside); }
        /// <summary>종료 시 시각 상태를 복구합니다.</summary>
        public override void OnNetworkDespawn() { phase.OnValueChanged -= Changed; interior.SetInside(false); }
        /// <summary>동기화된 단계에 맞춰 입장 표시를 적용합니다.</summary>
        private void Changed(PetHeistPhase before, PetHeistPhase after) => interior.SetInside(IsInside);
        /// <summary>서버 원장과 운반 금액을 맞춥니다.</summary>
        public void SetCargoServer(int amount) { if (IsServer && IsSpawned) cargo.Value = Mathf.Max(0, amount); }
        /// <summary>신고 예약 상태를 동기화합니다.</summary>
        public void SetReportedServer(bool value) { if (IsServer && IsSpawned) reported.Value = value; }
        /// <summary>주인·잔액·용량·완전 경로를 검증하고 출입구로 이동합니다.</summary>
        public bool TryStartServer(NetworkObject player, HeistBuildingSite target)
        {
            var world = HeistWorldGlue.Instance;
            if (!IsServer || !IsSpawned || world == null || target == null || !target.IsLootSite || IsBusy || !state.IsOwnedBy(player) ||
                world.IsRetiredPet(state.PetId) ||
                GetComponent<PlayerStats>().IsDead || world.Status(target.Id).Money <= 0 || Cargo >= world.Config.PetCapacity ||
                Vector3.Distance(transform.position, player.transform.position) > world.Config.PetCommandDistance || !agent.isOnNavMesh) return false;
            var path = new NavMeshPath();
            if (!agent.CalculatePath(target.Entrance, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            site = target; buildingId.Value = target.Id; phase.Value = PetHeistPhase.Approaching;
            finishAt.Value = NetworkManager.ServerTime.Time + world.Config.TravelTimeout;
            follow.MoveServer(site.Entrance, world.Config.WorkMoveSpeed, world.Config.ArrivalDistance);
            return true;
        }
        /// <summary>기존 펫 ManualUpdate에서 서버만 작업 시간을 진행합니다.</summary>
        public void ManualUpdateServer()
        {
            if (!IsServer || !IsBusy) return;
            var world = HeistWorldGlue.Instance;
            if (world == null || site == null || !state.TryGetOwner(out var owner) || !owner.IsSpawned || owner.GetComponent<PlayerStats>().IsDead || GetComponent<PlayerStats>().IsDead)
            { CancelServer(); return; }
            double now = NetworkManager.ServerTime.Time;
            if (phase.Value == PetHeistPhase.Approaching)
            {
                if (now >= finishAt.Value || !agent.isOnNavMesh) { CancelServer(); return; }
                if (!agent.pathPending && Vector3.Distance(transform.position, site.Entrance) <= world.Config.ArrivalDistance + 0.15f)
                { follow.StopServer(); phase.Value = PetHeistPhase.Stealing; finishAt.Value = now + world.Config.StealSeconds; world.TryCatchStealing(this); }
            }
            else if (now >= finishAt.Value)
            { world.CompleteTheft(this, site.Id, owner); CancelServer(); }
        }
        /// <summary>주인 사망·도주·실패 시 미완료 절도는 돈을 변경하지 않고 종료합니다.</summary>
        public void CancelServer()
        {
            if (!IsServer || !IsSpawned) return;
            phase.Value = PetHeistPhase.None; finishAt.Value = 0; site = null; buildingId.Value = -1;
            follow.StopServer(); follow.ResetFollowTarget();
        }
    }
}
