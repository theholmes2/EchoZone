using UnityEngine;

namespace EchoZone.Enemy
{
    /// <summary>경찰의 순찰·인지·추격·전투·수색 규칙에 필요한 기획 데이터를 보관합니다.</summary>
    [CreateAssetMenu(fileName = "PoliceEnemyConfig", menuName = "EchoZone/Enemy/Police Enemy Config")]
    public sealed class PoliceEnemyConfig : ScriptableObject
    {
        /// <summary>경찰 목적지 사이의 최소 간격입니다.</summary>
        [Min(0.1f)] public float DestinationSpacing = 1.1f;
        /// <summary>원래 목적지 주위 대체 목적지 반경입니다.</summary>
        [Min(0.1f)] public float DestinationSpread = 1.5f;
        /// <summary>대체 목적지의 원형 후보 개수입니다.</summary>
        [Min(4)] public int DestinationCandidates = 12;
        /// <summary>NavMesh 목적지 투영 허용 거리입니다.</summary>
        [Min(0.1f)] public float DestinationSampleRadius = 0.5f;
        /// <summary>경로를 다시 요청하는 최소 간격입니다.</summary>
        [Min(0.05f)] public float RepathSeconds = 0.35f;
        /// <summary>이동 정체 후 대체 위치를 다시 선택할 시간입니다.</summary>
        [Min(1f)] public float StuckSeconds = 4f;
        /// <summary>정체 타이머를 초기화할 이동 거리입니다.</summary>
        [Min(0.01f)] public float ProgressDistance = 0.2f;
        [Header("Presentation")]
        /// <summary>서버가 경찰을 생성할 때 사용할 네트워크 프리팹입니다.</summary>
        [SerializeField] private GameObject policePrefab;
        /// <summary>플레이어와 같은 화면상 키로 맞출 경찰 외형의 목표 높이입니다.</summary>
        [SerializeField, Min(0.01f)] private float visualHeight = 2.25f;
        /// <summary>경찰의 물리 및 NavMesh 캡슐 높이입니다.</summary>
        [SerializeField, Min(0.01f)] private float collisionHeight = 2f;
        /// <summary>경찰의 물리 및 NavMesh 캡슐 반경입니다.</summary>
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.5f;

        [Header("Patrol")]
        /// <summary>순찰 상태에서의 초당 이동 거리입니다.</summary>
        [SerializeField, Min(0f)] private float patrolMoveSpeed = 2.5f;
        /// <summary>순찰 지점에 도착했다고 인정할 거리입니다.</summary>
        [SerializeField, Min(0f)] private float patrolPointArrivalDistance = 0.35f;
        /// <summary>각 순찰 지점에 도착한 뒤 머무는 시간입니다.</summary>
        [SerializeField, Min(0f)] private float patrolWaitSeconds = 1f;

        [Header("Perception")]
        /// <summary>경찰 정면을 중심으로 플레이어를 감지할 전체 시야각입니다.</summary>
        [SerializeField, Range(1f, 179f)] private float sightAngle = 100f;
        /// <summary>플레이어를 감지할 수 있는 최대 거리입니다.</summary>
        [SerializeField, Min(0f)] private float sightDistance = 14f;
        /// <summary>LOS Ray를 발사할 경찰 기준점의 높이입니다.</summary>
        [SerializeField, Min(0f)] private float sightOriginHeight = 1.2f;
        /// <summary>플레이어가 보일 때 초당 증가하는 발견 게이지입니다.</summary>
        [SerializeField, Min(0f)] private float detectionGainPerSecond = 0.8f;
        /// <summary>플레이어가 보이지 않을 때 초당 감소하는 발견 게이지입니다.</summary>
        [SerializeField, Min(0f)] private float detectionLossPerSecond = 0.45f;
        /// <summary>플레이어를 확정 발견하는 게이지 기준입니다.</summary>
        [SerializeField, Range(0f, 1f)] private float detectionThreshold = 1f;
        /// <summary>경찰이 감지 대상으로 검색할 레이어입니다.</summary>
        [SerializeField] private LayerMask targetLayerMask = ~0;
        /// <summary>경찰과 대상 사이의 시야를 차단할 레이어입니다.</summary>
        [SerializeField] private LayerMask sightBlockingLayerMask = ~0;

        [Header("Chase And Search")]
        /// <summary>추격 상태에서의 초당 이동 거리입니다.</summary>
        [SerializeField, Min(0f)] private float chaseMoveSpeed = 4.5f;
        /// <summary>마지막 목격 위치에 도착했다고 인정할 거리입니다.</summary>
        [SerializeField, Min(0f)] private float lastKnownPositionArrivalDistance = 0.75f;
        /// <summary>마지막 목격 위치에서 대상을 찾을 최대 시간입니다.</summary>
        [SerializeField, Min(0f)] private float searchDurationSeconds = 5f;

        [Header("Ranged Combat")]
        /// <summary>후퇴를 시작할 최소 전투 거리입니다.</summary>
        [SerializeField, Min(0f)] private float minimumAttackDistance = 5f;
        /// <summary>경찰이 선회하며 유지하려는 전투 거리입니다.</summary>
        [SerializeField, Min(0f)] private float preferredAttackDistance = 8f;
        /// <summary>총기 공격을 허용할 최대 거리입니다.</summary>
        [SerializeField, Min(0f)] private float maximumAttackDistance = 11f;
        /// <summary>대상을 중심으로 선회할 때의 초당 이동 거리입니다.</summary>
        [SerializeField, Min(0f)] private float combatOrbitSpeed = 2.5f;
        /// <summary>전투 중 좌우 선회 방향을 반대로 바꿀 시간 간격입니다.</summary>
        [SerializeField, Min(0.1f)] private float combatOrbitSwitchSeconds = 2f;
        /// <summary>경찰의 연속된 두 발사 사이에 필요한 시간입니다.</summary>
        [SerializeField, Min(0f)] private float fireIntervalSeconds = 0.35f;

        /// <summary>서버가 경찰을 생성할 때 사용할 네트워크 프리팹입니다.</summary>
        public GameObject PolicePrefab => policePrefab;
        /// <summary>경찰 외형의 목표 높이입니다.</summary>
        public float VisualHeight => visualHeight;
        /// <summary>경찰 캡슐의 높이입니다.</summary>
        public float CollisionHeight => collisionHeight;
        /// <summary>경찰 캡슐의 반경입니다.</summary>
        public float CollisionRadius => collisionRadius;
        /// <summary>순찰 상태에서의 초당 이동 거리입니다.</summary>
        public float PatrolMoveSpeed => patrolMoveSpeed;
        /// <summary>순찰 지점에 도착했다고 인정할 거리입니다.</summary>
        public float PatrolPointArrivalDistance => patrolPointArrivalDistance;
        /// <summary>각 순찰 지점에 도착한 뒤 머무는 시간입니다.</summary>
        public float PatrolWaitSeconds => patrolWaitSeconds;
        /// <summary>경찰 정면을 중심으로 플레이어를 감지할 전체 시야각입니다.</summary>
        public float SightAngle => sightAngle;
        /// <summary>플레이어를 감지할 수 있는 최대 거리입니다.</summary>
        public float SightDistance => sightDistance;
        /// <summary>LOS Ray를 발사할 경찰 기준점의 높이입니다.</summary>
        public float SightOriginHeight => sightOriginHeight;
        /// <summary>플레이어가 보일 때 초당 증가하는 발견 게이지입니다.</summary>
        public float DetectionGainPerSecond => detectionGainPerSecond;
        /// <summary>플레이어가 보이지 않을 때 초당 감소하는 발견 게이지입니다.</summary>
        public float DetectionLossPerSecond => detectionLossPerSecond;
        /// <summary>플레이어를 확정 발견하는 게이지 기준입니다.</summary>
        public float DetectionThreshold => detectionThreshold;
        /// <summary>경찰이 감지 대상으로 검색할 레이어입니다.</summary>
        public LayerMask TargetLayerMask => targetLayerMask;
        /// <summary>경찰과 대상 사이의 시야를 차단할 레이어입니다.</summary>
        public LayerMask SightBlockingLayerMask => sightBlockingLayerMask;
        /// <summary>추격 상태에서의 초당 이동 거리입니다.</summary>
        public float ChaseMoveSpeed => chaseMoveSpeed;
        /// <summary>마지막 목격 위치에 도착했다고 인정할 거리입니다.</summary>
        public float LastKnownPositionArrivalDistance => lastKnownPositionArrivalDistance;
        /// <summary>마지막 목격 위치에서 대상을 찾을 최대 시간입니다.</summary>
        public float SearchDurationSeconds => searchDurationSeconds;
        /// <summary>후퇴를 시작할 최소 전투 거리입니다.</summary>
        public float MinimumAttackDistance => minimumAttackDistance;
        /// <summary>경찰이 선회하며 유지하려는 전투 거리입니다.</summary>
        public float PreferredAttackDistance => preferredAttackDistance;
        /// <summary>총기 공격을 허용할 최대 거리입니다.</summary>
        public float MaximumAttackDistance => maximumAttackDistance;
        /// <summary>대상을 중심으로 선회할 때의 초당 이동 거리입니다.</summary>
        public float CombatOrbitSpeed => combatOrbitSpeed;
        /// <summary>전투 중 좌우 선회 방향을 반대로 바꿀 시간 간격입니다.</summary>
        public float CombatOrbitSwitchSeconds => combatOrbitSwitchSeconds;
        /// <summary>경찰의 연속된 두 발사 사이에 필요한 시간입니다.</summary>
        public float FireIntervalSeconds => fireIntervalSeconds;
    }
}
