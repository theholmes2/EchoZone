using EchoZone.Enemy;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace EchoZone.Heist
{
    /// <summary>기존 경찰 전투가 없을 때 건물 검사와 주인 없는 장물 회수를 수행합니다.</summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(HeistInteriorView))]
    public sealed class PoliceHeistDutyGlue : NetworkBehaviour
    {
        /// <summary>피어 공통 실내 검사 표시입니다.</summary>
        private readonly NetworkVariable<bool> inside = new();
        /// <summary>예약한 검사 건물입니다.</summary>
        private HeistBuildingSite site;
        /// <summary>회수하려는 펫입니다.</summary>
        private PetHeistGlue pet;
        /// <summary>범인의 신원을 모르는 상태에서 수색하는 도난 현장입니다.</summary>
        private HeistBuildingSite crimeSite;
        /// <summary>경찰별로 분산된 다음 수색 후보 번호입니다.</summary>
        private int searchPointIndex;
        /// <summary>현재 승인한 수색 지점과 도착 대기 종료 시각입니다.</summary>
        private Vector3 searchPoint;
        private double searchWaitUntil;
        /// <summary>현재 수색 지점이 완전 경로로 승인됐는지 나타냅니다.</summary>
        private bool hasSearchPoint;
        /// <summary>현장 수색 중인지 확인할 서버 진단 값입니다.</summary>
        public bool IsSearchingCrime => crimeSite != null;
        /// <summary>기존 서버 이동 Agent입니다.</summary>
        private NavMeshAgent agent;
        /// <summary>표시 전용 연결입니다.</summary>
        private HeistInteriorView interior;
        /// <summary>도달 여부 검사에서 재사용할 NavMesh 경로입니다.</summary>
        private NavMeshPath path;
        /// <summary>작업 탐색과 목적지 갱신 시각입니다.</summary>
        private double nextSearch, nextPath, finishAt, travelUntil;
        /// <summary>작업을 마친 뒤 복원할 원래 정지 거리입니다.</summary>
        private float initialStoppingDistance;
        /// <summary>이동 정체를 확인할 마지막 진행 위치입니다.</summary>
        private Vector3 progressPosition;
        /// <summary>진전이 없을 때 작업을 취소할 서버 시각입니다.</summary>
        private double stuckAt;
        /// <summary>새 검사에 배정할 수 있는 비전투 상태입니다.</summary>
        public bool CanAcceptInspection => IsSpawned && !GetComponent<PlayerStats>().IsDead && site == null && pet == null &&
            GetComponent<PoliceEnemyBrainGlue>().CurrentState == PoliceEnemyState.Patrol;
        /// <summary>현재 실내 검사 중인지 나타냅니다.</summary>
        public bool IsInside => inside.Value;
        /// <summary>테스트·표시에 사용할 현재 검사 건물 번호입니다.</summary>
        public int InspectionBuildingId => site != null ? site.Id : -1;
        /// <summary>Unity 참조를 준비합니다.</summary>
        private void Awake() { agent = GetComponent<NavMeshAgent>(); initialStoppingDistance = agent.stoppingDistance; interior = GetComponent<HeistInteriorView>(); path = new NavMeshPath(); }
        /// <summary>풀 재사용 시 지난 작업을 제거합니다.</summary>
        public override void OnNetworkSpawn()
        { site = null; pet = null; crimeSite = null; hasSearchPoint = false; searchPointIndex = (int)(NetworkObjectId % 1024); nextSearch = nextPath = 0; inside.OnValueChanged += Changed; if (IsServer) inside.Value = false; interior.SetInside(inside.Value); }
        /// <summary>반환 시 건물 예약과 내부 표시를 해제합니다.</summary>
        public override void OnNetworkDespawn() { CancelServer(); inside.OnValueChanged -= Changed; interior.SetInside(false); }
        /// <summary>검사 입퇴장을 각 피어에 표시합니다.</summary>
        private void Changed(bool before, bool after) => interior.SetInside(after);
        /// <summary>도달 불가능한 출입구를 작업으로 선택하지 않습니다.</summary>
        public bool CanNavigate(Vector3 point) => agent.enabled && agent.isOnNavMesh && agent.CalculatePath(point, path) && path.status == NavMeshPathStatus.PathComplete;
        /// <summary>전투·사망에 양보하며 진행 중 검사는 적발 없이 취소합니다.</summary>
        public void CancelServer()
        {
            if (!IsServer) return;
            if (site == null && pet == null && crimeSite == null && !inside.Value) return;
            if (NetworkManager != null && !NetworkManager.ShutdownInProgress) HeistWorldGlue.Instance?.FinishInspection(site, this, false);
            HeistWorldGlue.Instance?.ReleaseRecovery(this);
            PoliceDestinationBrick.Shared.Release(NetworkObjectId);
            site = null; pet = null; crimeSite = null; hasSearchPoint = false;
            if (IsSpawned) inside.Value = false;
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; agent.stoppingDistance = initialStoppingDistance; }
        }
        /// <summary>수배자를 보고 있지 않을 때 기존 AI 루프가 호출합니다. 작업 중이면 true입니다.</summary>
        public bool ManualUpdateServer()
        {
            var world = HeistWorldGlue.Instance;
            if (!IsServer || world == null || !agent.enabled || !agent.isOnNavMesh) return false;
            if (GetComponent<PlayerStats>().IsDead) { CancelServer(); return false; }
            var config = world.Config; double now = NetworkManager.ServerTime.Time;
            if (site == null && pet == null && !inside.Value && now >= nextSearch)
            {
                var inspection = world.ReserveInspection(this);
                if (inspection != null)
                {
                    crimeSite = null; hasSearchPoint = false; site = inspection;
                    travelUntil = now + config.TravelTimeout; nextPath = 0;
                    progressPosition = transform.position; stuckAt = now + config.WorkStuckSeconds;
                }
            }
            if (crimeSite != null)
            {
                if (world.Status(crimeSite.Id).SearchUntil <= now) { CancelServer(); return false; }
                return TickCrimeSearch(world, now);
            }
            if (site == null && pet == null)
            {
                if (now < nextSearch) return false;
                nextSearch = now + config.JobSearchInterval;
                float best = float.PositiveInfinity;
                foreach (var state in EchoZone.Pet.PetUpdateManager.Pets)
                {
                    if (state == null || !state.TryGetComponent<PetHeistGlue>(out var candidate) || !candidate.IsAbandonedCargo) continue;
                    float distance = (transform.position - candidate.transform.position).sqrMagnitude;
                    if (distance >= best || (!world.HasReport(candidate.NetworkObjectId) && !CanSee(candidate.transform.position)) || !CanNavigate(candidate.transform.position)) continue;
                    if (!world.TryReserveRecovery(candidate, this)) continue;
                    pet = candidate; best = distance;
                    break;
                }
                if (pet == null)
                {
                    float nearest = float.PositiveInfinity;
                    foreach (var candidate in world.Sites)
                    {
                        float distance = (candidate.Entrance - transform.position).sqrMagnitude;
                        if (world.Status(candidate.Id).SearchUntil > now && distance < nearest && CanNavigate(candidate.Entrance))
                        { crimeSite = candidate; nearest = distance; }
                    }
                    if (crimeSite != null) { hasSearchPoint = false; return TickCrimeSearch(world, now); }
                    site = world.ReserveInspection(this);
                }
                if (site == null && pet == null) return false;
                travelUntil = now + config.TravelTimeout; nextPath = 0;
                if (site == null) GetComponent<PoliceEnemyBrainGlue>().ReleaseDestination();
                progressPosition = transform.position; stuckAt = now + config.WorkStuckSeconds;
            }
            if (pet != null && !pet.IsAbandonedCargo) { CancelServer(); return false; }
            if (inside.Value)
            {
                if (now >= finishAt) { world.FinishInspection(site, this, true); site = null; inside.Value = false; agent.stoppingDistance = initialStoppingDistance; }
                return true;
            }
            if (Vector3.Distance(progressPosition, transform.position) >= config.WorkProgressDistance)
            { progressPosition = transform.position; stuckAt = now + config.WorkStuckSeconds; }
            if (now >= travelUntil || now >= stuckAt) { CancelServer(); nextSearch = now + config.InspectionRetrySeconds; return false; }
            Vector3 destination = site != null ? site.Entrance : pet.transform.position;
            float arrival = site != null ? config.ArrivalDistance : config.ConfiscationDistance;
            if (Vector3.Distance(transform.position, destination) <= arrival + 0.1f && !agent.pathPending)
            {
                agent.ResetPath(); agent.isStopped = true;
                if (site != null) { inside.Value = true; world.StartInspection(site, this); finishAt = now + config.InspectionSeconds; }
                else { world.Confiscate(this, pet); world.ReleaseRecovery(this); pet = null; agent.stoppingDistance = initialStoppingDistance; }
                return true;
            }
            if (now >= nextPath)
            { nextPath = now + config.JobSearchInterval; agent.speed = site != null ? config.InspectionRunSpeed : config.WorkMoveSpeed; agent.stoppingDistance = arrival; agent.isStopped = false; agent.SetDestination(destination); }
            return true;
        }
        /// <summary>범인을 추측하지 않고 도난 현장 주변 도달 가능한 지점을 순회합니다.</summary>
        private bool TickCrimeSearch(HeistWorldGlue world, double now)
        {
            var config = world.Config;
            if (!hasSearchPoint)
            {
                if (now < nextPath) return true;
                nextPath = now + config.JobSearchInterval;
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                int count = Mathf.Max(3, config.CrimeSearchPointCount);
                for (int i = 0; i < count; i++)
                {
                    var candidate = CrimeSearchBrick.Point(crimeSite.Entrance, config.CrimeSearchRadius, searchPointIndex++, count);
                    if (!NavMesh.SamplePosition(candidate, out var hit, config.CrimeSearchSampleRadius, filter) || !CanNavigate(hit.position)) continue;
                    if (!PoliceDestinationBrick.Shared.TryClaim(NetworkObjectId, hit.position, GetComponent<PoliceEnemyBrainGlue>().DestinationSpacing)) continue;
                    searchPoint = hit.position; hasSearchPoint = true; searchWaitUntil = 0;
                    travelUntil = now + config.TravelTimeout;
                    progressPosition = transform.position; stuckAt = now + config.WorkStuckSeconds;
                    agent.speed = config.WorkMoveSpeed; agent.stoppingDistance = config.ArrivalDistance;
                    agent.isStopped = false; agent.SetDestination(searchPoint); break;
                }
                return true;
            }
            if (Vector3.Distance(progressPosition, transform.position) >= config.WorkProgressDistance)
            { progressPosition = transform.position; stuckAt = now + config.WorkStuckSeconds; }
            if (now >= travelUntil || (searchWaitUntil == 0 && now >= stuckAt))
            { hasSearchPoint = false; PoliceDestinationBrick.Shared.Release(NetworkObjectId); agent.ResetPath(); return true; }
            if (!agent.pathPending && Vector3.Distance(transform.position, searchPoint) <= config.ArrivalDistance + 0.1f)
            {
                agent.isStopped = true;
                if (searchWaitUntil == 0) searchWaitUntil = now + config.CrimeSearchWaitSeconds;
                transform.Rotate(Vector3.up, agent.angularSpeed * Time.deltaTime);
                if (now >= searchWaitUntil) { hasSearchPoint = false; PoliceDestinationBrick.Shared.Release(NetworkObjectId); }
            }
            return true;
        }
        /// <summary>미신고 장물은 경찰의 실제 시야와 벽 차단을 통과해야 발견합니다.</summary>
        private bool CanSee(Vector3 target)
        {
            var brain = GetComponent<PoliceEnemyBrainGlue>();
            Vector3 delta = target - transform.position; delta.y = 0;
            if (delta.magnitude > brain.SightDistance || Vector3.Angle(transform.forward, delta) > brain.SightAngle * 0.5f) return false;
            Vector3 from = transform.position + Vector3.up;
            Vector3 to = target + Vector3.up;
            foreach (var hit in Physics.RaycastAll(from, (to - from).normalized, (to - from).magnitude, HeistWorldGlue.Instance.Config.BlockingLayers, QueryTriggerInteraction.Ignore))
            {
                var obj = hit.collider.GetComponentInParent<NetworkObject>();
                if (obj == NetworkObject || (obj != null && obj.GetComponent<PetHeistGlue>() != null)) continue;
                return false;
            }
            return true;
        }
    }
}
