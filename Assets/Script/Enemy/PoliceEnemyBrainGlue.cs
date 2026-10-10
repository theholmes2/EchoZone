using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using EchoZone.Combat;
using EchoZone.Combat.Glue;
using EchoZone.Player.View;

namespace EchoZone.Enemy
{
    /// <summary>네트워크로 공유할 경찰의 장착 총기 종류입니다.</summary>
    public enum PoliceWeaponType : byte
    {
        Pistol,
        Rifle
    }

    /// <summary>서버의 Physics·NavMesh·대상 검색을 경찰의 순수 판단 Brick과 연결하는 Glue입니다.</summary>
    [DisallowMultipleComponent]
    public sealed partial class PoliceEnemyBrainGlue : NetworkBehaviour
    {
        /// <summary>풀 대여마다 새로 발급하고 복구 때 이어받는 경찰 ID입니다.</summary>
        public string PoliceId { get; private set; }
        /// <summary>경찰의 모든 행동 수치를 제공하는 기획 데이터입니다.</summary>
        [SerializeField] private PoliceEnemyConfig config;
        /// <summary>Brick이 결정한 목적지를 실제 이동으로 연결할 NavMesh Agent입니다.</summary>
        [SerializeField] private NavMeshAgent navigationAgent;
        /// <summary>플레이어 LOS 검사를 시작할 눈높이 Transform입니다.</summary>
        [SerializeField] private Transform sightOrigin;
        /// <summary>1→2→3→1 순서로 순회할 씬 순찰 지점입니다.</summary>
        [SerializeField] private Transform[] patrolPoints;
        /// <summary>전투 상태에서 서버 권한 투사체 발사를 요청할 총기 Glue입니다.</summary>
        [SerializeField] private NetworkWeaponFireGlue weaponFireGlue;
        /// <summary>권총 경찰에게 적용할 발사 규칙과 외형 데이터입니다.</summary>
        [SerializeField] private WeaponFireConfig pistolWeaponConfig;
        /// <summary>소총 경찰에게 적용할 발사 규칙과 외형 데이터입니다.</summary>
        [SerializeField] private WeaponFireConfig rifleWeaponConfig;

        /// <summary>순찰 순서와 대기시간을 계산하는 순수 Brick입니다.</summary>
        private readonly PolicePatrolBrick patrolBrick = new();
        /// <summary>시야각과 발견 게이지를 계산하는 순수 Brick입니다.</summary>
        private readonly PolicePerceptionBrick perceptionBrick = new();
        /// <summary>접근·후퇴·선회 방향을 계산하는 순수 Brick입니다.</summary>
        private readonly PoliceCombatMovementBrick combatMovementBrick = new();
        /// <summary>현재 서버가 실행할 경찰 행동 상태입니다.</summary>
        private PoliceEnemyState currentState = PoliceEnemyState.Patrol;
        /// <summary>현재 경찰이 발견하거나 추격 중인 플레이어입니다.</summary>
        private Transform currentTarget;
        /// <summary>다음 총기 발사를 허용할 서버 시각입니다.</summary>
        private float nextFireTime;
        /// <summary>현재 점사 묶음에서 요청한 발사 횟수입니다.</summary>
        private int currentBurstShotCount;
        /// <summary>현재 선회 방향으로, 1은 오른쪽이고 -1은 왼쪽입니다.</summary>
        private float orbitSign = 1f;
        /// <summary>다음으로 좌우 선회 방향을 반전할 서버 시각입니다.</summary>
        private float nextOrbitSwitchTime;
        /// <summary>순찰 지점에 도착해 대기 중인지 나타냅니다.</summary>
        private bool isWaitingAtPatrolPoint;
        /// <summary>이번 인지 갱신에서 현재 대상을 직접 볼 수 있었는지 나타냅니다.</summary>
        private bool canSeeCurrentTarget;
        /// <summary>현재 대상의 얼굴 확인이 완료됐는지 나타냅니다. 대상 변경·수색 종료 시 초기화합니다.</summary>
        private bool targetIdentified;
        /// <summary>현재 경찰이 소속된 경찰서의 스폰 그룹 인덱스입니다.</summary>
        private int stationIndex = -1;
        /// <summary>구역별 순찰 잔류 인원을 집계할 소속 경찰서 번호입니다.</summary>
        public int StationIndex => stationIndex;
        /// <summary>이동 결과를 애니메이션으로 표시하는 기존 View입니다.</summary>
        private CharacterAnimatorView animatorView;
        /// <summary>로컬 화면에서 직전 프레임에 관찰한 위치입니다.</summary>
        private Vector3 previousViewPosition;
        /// <summary>사망 체력 이벤트로 서버 AI 실행을 차단합니다.</summary>
        private bool deathBlocked;
        /// <summary>사망 후 반환 예약을 기존 갱신 순서에서 실행할 Glue입니다.</summary>
        private PoliceDeadEventGlue deathGlue;
        /// <summary>비전투 시간의 건물 검사·장물 회수 연결입니다.</summary>
        private EchoZone.Heist.PoliceHeistDutyGlue heistDuty;
        /// <summary>실제 서버 피격만 통지하는 기존 피해 수신 Glue입니다.</summary>
        private DamageReceiverGlue damageReceiver;
        /// <summary>피격으로 신원이 확인된 대상을 일반 근접 후보보다 우선합니다.</summary>
        private bool pursuingAttacker;
        /// <summary>승인한 분산 목적지와 경로 갱신 시각입니다.</summary>
        private Vector3 reservedDestination, requestedDestination, progressPosition;
        private bool hasReservedDestination;
        private float repathAt, stuckAt;
        /// <summary>외부 작업이 공유할 목적지 최소 간격입니다.</summary>
        public float DestinationSpacing => config.DestinationSpacing;
        /// <summary>작업·상태 전환과 풀 반환 때 위치 예약을 해제합니다.</summary>
        public void ReleaseDestination()
        {
            PoliceDestinationBrick.Shared.Release(NetworkObjectId);
            hasReservedDestination = false; repathAt = 0;
        }

        /// <summary>사망 시 경로·속도를 제거하고 이후 AI 실행을 차단합니다.</summary>
        public void SetDeathBlocked(bool blocked)
        {
            if (deathBlocked == blocked) return;
            deathBlocked = blocked;
            if (blocked) ReleaseDestination();
            if (!IsServer || navigationAgent == null || !navigationAgent.enabled || !navigationAgent.isOnNavMesh) return;
            if (blocked)
            {
                navigationAgent.ResetPath();
                navigationAgent.velocity = Vector3.zero;
            }
            navigationAgent.isStopped = blocked;
        }
        /// <summary>서버가 선택하고 모든 Client가 동일하게 적용할 장착 총기 종류입니다.</summary>
        private readonly NetworkVariable<PoliceWeaponType> equippedWeaponType = new(
            PoliceWeaponType.Rifle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>현재 서버가 실행할 경찰 행동 상태입니다.</summary>
        public PoliceEnemyState CurrentState => currentState;
        /// <summary>별도 배치된 경찰도 도주 계획의 위험 반경에 포함할 시야거리입니다.</summary>
        public float SightDistance => config != null ? config.SightDistance : 0f;
        /// <summary>주인 없는 장물을 볼 때에도 동일한 시야각을 사용합니다.</summary>
        public float SightAngle => config != null ? config.SightAngle : 0f;
        /// <summary>경찰서 외부에서 직접 지정한 순찰 경로도 제공합니다.</summary>
        public System.Collections.Generic.IReadOnlyList<Transform> PatrolPoints => patrolPoints;
        /// <summary>현재 플레이어를 발견한 정도를 0~1 범위로 제공합니다.</summary>
        public float DetectionProgress => perceptionBrick.DetectionProgress;

        /// <summary>서버가 생성된 경찰의 총기 종류를 선택합니다.</summary>
        public void ConfigureWeaponType(PoliceWeaponType weaponType)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            equippedWeaponType.Value = weaponType;
            ApplyWeaponType(weaponType);
        }

        /// <summary>생성된 경찰에게 소속 경찰서의 순찰 경로와 그룹 인덱스를 전달합니다.</summary>
        public void ConfigurePatrolRoute(Transform[] routePoints, int assignedStationIndex)
        {
            patrolPoints = routePoints;
            stationIndex = assignedStationIndex;
        }

        /// <summary>모든 피어에서 View 갱신을 등록하고 서버에서만 NavMesh 이동 수치를 초기화합니다.</summary>
        public override void OnNetworkSpawn()
        {
            lostPursuit.Reset(); pursuitStall = 0;
            pendingMigrationTarget = null;
            if (IsServer) PoliceId = System.Guid.NewGuid().ToString("N");
            deathGlue = GetComponent<PoliceDeadEventGlue>();
            heistDuty = GetComponent<EchoZone.Heist.PoliceHeistDutyGlue>();
            damageReceiver = GetComponent<DamageReceiverGlue>();
            if (IsServer && damageReceiver != null) damageReceiver.ServerDamageApplied += HandleServerDamage;
            pursuingAttacker = false;
            ReleaseDestination();
            deathBlocked = false;
            currentState = PoliceEnemyState.Patrol;
            currentTarget = null;
            targetIdentified = false;
            nextFireTime = nextOrbitSwitchTime = 0f;
            currentBurstShotCount = 0;
            orbitSign = 1f;
            isWaitingAtPatrolPoint = canSeeCurrentTarget = false;
            patrolBrick.Reset();
            perceptionBrick.Reset();
            equippedWeaponType.OnValueChanged += HandleWeaponTypeChanged;
            ApplyWeaponType(equippedWeaponType.Value);
            animatorView = GetComponentInChildren<CharacterAnimatorView>(true);
            previousViewPosition = transform.position;
            InitializePlayerCollisions();
            FindFirstObjectByType<EnemyUpdateManager>()?.Register(this);
            if (!IsServer)
            {
                if (navigationAgent != null)
                {
                    navigationAgent.enabled = false;
                }
                return;
            }

            if (navigationAgent != null && config != null)
            {
                navigationAgent.enabled = true;
                if (navigationAgent.isOnNavMesh)
                {
                    navigationAgent.Warp(transform.position);
                    navigationAgent.ResetPath();
                    navigationAgent.isStopped = false;
                    navigationAgent.velocity = Vector3.zero;
                }
                navigationAgent.speed = config.PatrolMoveSpeed;
                navigationAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                navigationAgent.avoidancePriority = 20 + (int)(NetworkObjectId % 60);
                navigationAgent.radius = config.CollisionRadius;
                navigationAgent.stoppingDistance = Mathf.Min(navigationAgent.stoppingDistance, config.PatrolPointArrivalDistance);
                for (int i = 0; patrolPoints != null && i < (int)(NetworkObjectId % (ulong)Mathf.Max(1, patrolPoints.Length)); i++) patrolBrick.AdvancePoint(patrolPoints.Length);
            }

        }

        /// <summary>각 피어의 갱신 목록에서 제거하고 서버에서만 스폰 수를 갱신합니다.</summary>
        public override void OnNetworkDespawn()
        {
            pendingMigrationTarget = null;
            ReleaseDestination();
            if (damageReceiver != null) damageReceiver.ServerDamageApplied -= HandleServerDamage;
            pursuingAttacker = false;
            equippedWeaponType.OnValueChanged -= HandleWeaponTypeChanged;
            ShutdownPlayerCollisions();
            FindFirstObjectByType<EnemyUpdateManager>()?.Unregister(this);
            animatorView?.UpdateMovementAnimation(Vector2.zero);
            if (IsServer)
            {
                FindFirstObjectByType<EnemySpawnManager>()?.NotifyDespawned(stationIndex);
            }
        }

        /// <summary>경찰 공격은 즉시 수배하고 피격 경찰만 발견 게이지 없이 대응합니다.</summary>
        private void HandleServerDamage(NetworkObject attacker, int appliedDamage)
        {
            if (!IsServer || !IsSpawned || appliedDamage <= 0 || attacker == null ||
                !attacker.IsSpawned || !attacker.IsPlayerObject) return;
            EchoZone.Heist.HeistWorldGlue.Instance?.WitnessPoliceCrimeServer(attacker);
            if (deathBlocked || config == null || GetComponent<PlayerStats>().IsDead) return;

            heistDuty?.CancelServer();
            currentTarget = attacker.transform;
            pursuingAttacker = true;
            targetIdentified = true;
            perceptionBrick.Reset();
            RememberGroundPosition(attacker.transform.position);
            lastAimPoint = attacker.transform.position;
            lostPursuit.Reset();
            float now = NetworkManager.ServerTime.TimeAsFloat;
            FaceCurrentTarget();
            canSeeCurrentTarget = EvaluatePerception();
            ChangeState(canSeeCurrentTarget && HorizontalDistance(transform.position, currentTarget.position) <= config.MaximumAttackDistance
                ? PoliceEnemyState.Combat : PoliceEnemyState.Chase, now);
        }

        /// <summary>네트워크 장착 총기 변경을 현재 경찰의 발사 Glue와 외형에 적용합니다.</summary>
        private void HandleWeaponTypeChanged(PoliceWeaponType previous, PoliceWeaponType current)
        {
            ApplyWeaponType(current);
        }

        /// <summary>총기 종류에 대응하는 Config를 발사 Glue에 전달합니다.</summary>
        private void ApplyWeaponType(PoliceWeaponType weaponType)
        {
            if (weaponFireGlue == null)
            {
                return;
            }

            WeaponFireConfig selectedConfig = weaponType == PoliceWeaponType.Pistol
                ? pistolWeaponConfig
                : rifleWeaponConfig;
            weaponFireGlue.ConfigureWeapon(selectedConfig);
            currentBurstShotCount = 0;
        }

        /// <summary>서버와 클라이언트가 각자 관찰한 수평 이동 변위를 기존 View에 전달합니다.</summary>
        public void ManualUpdateView()
        {
            ManualUpdatePlayerCollisions();
            Vector3 position = transform.position;
            Vector3 delta = position - previousViewPosition;
            previousViewPosition = position;
            animatorView?.UpdateMovementAnimation(new Vector2(delta.x, delta.z));
        }

        /// <summary>중앙 적 업데이트 관리자가 서버에서 정해진 순서로 호출할 진입점입니다.</summary>
        public void ManualUpdate(float serverTime, float deltaTime)
        {
            pursuitDelta = Mathf.Max(0, deltaTime);
            deathGlue?.ManualUpdate(serverTime);
            if (!IsSpawned) return;
            if (deathBlocked || !IsServer || config == null || navigationAgent == null || !navigationAgent.enabled)
            {
                if (IsServer && deathBlocked) heistDuty?.CancelServer();
                return;
            }

            KeepUpright();

            if (heistDuty != null && heistDuty.IsInside) { heistDuty.ManualUpdateServer(); return; }
            if (currentTarget != null && (EchoZone.Heist.HeistWorldGlue.Instance == null ||
                !EchoZone.Heist.HeistWorldGlue.Instance.CanPoliceAttack(currentTarget.GetComponent<NetworkObject>())))
            { currentTarget = null; targetIdentified = false; pursuingAttacker = false; perceptionBrick.Reset(); ChangeState(PoliceEnemyState.Patrol, serverTime); }

            canSeeCurrentTarget = EvaluatePerception();
            if (!canSeeCurrentTarget) EvaluateHearing();
            perceptionBrick.ManualUpdateDetection(canSeeCurrentTarget, Mathf.Max(0f, deltaTime), config);
            weaponFireGlue?.ManualUpdate(serverTime);

            if (canSeeCurrentTarget) heistDuty?.CancelServer();
            else if (currentTarget == null && !perceptionBrick.HasLastKnownPosition && heistDuty != null && heistDuty.ManualUpdateServer()) return;

            SelectState(serverTime);

            switch (currentState)
            {
                case PoliceEnemyState.Patrol:
                    TickPatrol(serverTime);
                    break;

                case PoliceEnemyState.Suspicious:
                    TickSuspicious();
                    break;

                case PoliceEnemyState.Chase:
                    TickChase();
                    break;

                case PoliceEnemyState.Combat:
                    TickCombat(serverTime);
                    break;

                case PoliceEnemyState.Search:
                    TickSearch(serverTime);
                    break;
            }
        }

        /// <summary>가장 적합한 플레이어 대상을 찾고 FOV와 첫 벽 LOS를 검사합니다.</summary>
        private bool EvaluatePerception()
        {
            Vector3 origin = sightOrigin != null
                ? sightOrigin.position
                : transform.position + Vector3.up * config.SightOriginHeight;

            Collider[] candidates = Physics.OverlapSphere(
                origin,
                config.SightDistance,
                config.TargetLayerMask,
                QueryTriggerInteraction.Ignore);

            Transform bestTarget = null;
            Vector3 bestAimPoint = default;
            float bestSqrDistance = float.PositiveInfinity;

            for (int i = 0; i < candidates.Length; i++)
            {
                Collider candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                NetworkObject playerObject = candidate.GetComponentInParent<NetworkObject>();
                if (playerObject == null || !playerObject.IsSpawned ||
                    (!playerObject.IsPlayerObject && playerObject.GetComponent<EchoZone.Pet.PetFollowGlue>() == null))
                {
                    continue;
                }

                Transform target = playerObject.transform;
                if (pursuingAttacker && currentTarget != null && target != currentTarget) continue;
                if (EchoZone.Heist.HeistWorldGlue.Instance == null || !EchoZone.Heist.HeistWorldGlue.Instance.CanPoliceAttack(playerObject))
                    continue;
                if (playerObject.TryGetComponent<PlayerStats>(out var targetStats) && targetStats.IsDead)
                    continue;
                if (playerObject.TryGetComponent<EchoZone.Pet.PetStateGlue>(out var petState) &&
                    petState.State == EchoZone.Pet.PetBehaviourState.Fleeing)
                    continue;
                Vector3 aimPoint = candidate.bounds.center;
                if (!perceptionBrick.IsInsideSight(
                        transform.position,
                        transform.forward,
                        aimPoint,
                        config.SightAngle,
                        config.SightDistance))
                {
                    continue;
                }

                Vector3 toTarget = aimPoint - origin;
                float targetDistance = toTarget.magnitude;
                if (targetDistance <= 0.0001f)
                {
                    bestTarget = target;
                    bestAimPoint = aimPoint;
                    break;
                }

                if (Physics.Raycast(
                        origin,
                        toTarget / targetDistance,
                        out RaycastHit hit,
                        targetDistance,
                        config.SightBlockingLayerMask,
                        QueryTriggerInteraction.Ignore))
                {
                    NetworkObject hitObject = hit.collider.GetComponentInParent<NetworkObject>();
                    if (hitObject != playerObject)
                    {
                        continue;
                    }
                }

                float sqrDistance = (target.position - transform.position).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestTarget = target;
                    bestAimPoint = aimPoint;
                }
            }

            if (bestTarget == null)
            {
                return false;
            }

            if (currentTarget != bestTarget) { perceptionBrick.Reset(); targetIdentified = false; }
            currentTarget = bestTarget;
            lastAimPoint = bestAimPoint;
            RememberGroundPosition(bestTarget.position);
            lostPursuit.Reset();
            return true;
        }

        /// <summary>시야 절반 거리에서 움직이는 공격 대상 플레이어의 발소리 위치를 기억해 추격을 시작합니다.</summary>
        private void EvaluateHearing()
        {
            Collider[] candidates = Physics.OverlapSphere(
                transform.position,
                config.SightDistance * config.HearingDistanceRatio,
                config.TargetLayerMask,
                QueryTriggerInteraction.Ignore);

            NetworkObject heardPlayer = null;
            float bestSqrDistance = float.PositiveInfinity;
            for (int i = 0; i < candidates.Length; i++)
            {
                NetworkObject player = candidates[i]?.GetComponentInParent<NetworkObject>();
                if (player == null || !player.IsSpawned || !player.IsPlayerObject ||
                    !player.TryGetComponent(out Rigidbody body) ||
                    (player.TryGetComponent<PlayerStats>(out var stats) && stats.IsDead) ||
                    EchoZone.Heist.HeistWorldGlue.Instance == null ||
                    !EchoZone.Heist.HeistWorldGlue.Instance.CanPoliceAttack(player))
                    continue;

                if (!perceptionBrick.CanHearMovement(
                        transform.position,
                        player.transform.position,
                        body.linearVelocity,
                        config.SightDistance,
                        config.HearingDistanceRatio,
                        config.HearingMovementSpeed))
                    continue;

                float sqrDistance = (player.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance) continue;
                bestSqrDistance = sqrDistance;
                heardPlayer = player;
            }

            if (heardPlayer == null) return;
            if (currentTarget != heardPlayer.transform) perceptionBrick.Reset();
            currentTarget = heardPlayer.transform;
            targetIdentified = true;
            RememberGroundPosition(heardPlayer.transform.position);
            lostPursuit.Reset();
        }

        /// <summary>현재 인지 결과와 거리 조건에 따라 다음 AI 상태를 결정합니다.</summary>
        private void SelectState(float serverTime)
        {
            if (canSeeCurrentTarget && currentTarget != null)
            {
                targetIdentified |= perceptionBrick.DetectionProgress >= 1f;
                if (!targetIdentified)
                {
                    ChangeState(PoliceEnemyState.Suspicious, serverTime);
                    return;
                }
                float targetDistance = HorizontalDistance(transform.position, currentTarget.position);
                PoliceEnemyState visibleState =
                    targetDistance <= config.MaximumAttackDistance
                        ? PoliceEnemyState.Combat
                        : PoliceEnemyState.Chase;
                ChangeState(visibleState, serverTime);
                return;
            }

            if (perceptionBrick.HasLastKnownPosition)
            {
                ChangeState(lostPursuit.Arrived ? PoliceEnemyState.Search : PoliceEnemyState.Chase, serverTime);
                return;
            }

            if (!perceptionBrick.HasLastKnownPosition)
            {
                ChangeState(PoliceEnemyState.Patrol, serverTime);
            }
        }

        /// <summary>설정된 지점들을 1→2→3→1 순서로 이동합니다.</summary>
        private void TickPatrol(float serverTime)
        {
            navigationAgent.speed = config.PatrolMoveSpeed;

            Transform patrolPoint = GetCurrentPatrolPoint();
            if (patrolPoint == null || !CanMoveOnNavMesh())
            {
                StopMoving();
                return;
            }

            if (patrolBrick.HasReachedPoint(
                    transform.position,
                    hasReservedDestination ? reservedDestination : patrolPoint.position,
                    config.PatrolPointArrivalDistance))
            {
                if (!isWaitingAtPatrolPoint)
                {
                    isWaitingAtPatrolPoint = true;
                    patrolBrick.BeginWait(serverTime, config.PatrolWaitSeconds);
                    StopMoving();
                    return;
                }

                if (!patrolBrick.IsWaitComplete(serverTime))
                {
                    StopMoving();
                    return;
                }

                patrolBrick.AdvancePoint(patrolPoints.Length);
                ReleaseDestination();
                isWaitingAtPatrolPoint = false;
                patrolPoint = GetCurrentPatrolPoint();
            }

            MoveTo(patrolPoint.position);
        }

        /// <summary>발견 게이지가 기준에 도달할 때까지 멈춰서 현재 대상을 주시합니다.</summary>
        private void TickSuspicious()
        {
            StopMoving();
            FaceCurrentTarget();
        }

        /// <summary>현재 보이는 플레이어를 추격합니다.</summary>
        private void TickChase()
        {
            if (!canSeeCurrentTarget && perceptionBrick.HasLastKnownPosition)
            { TickLostPursuit((float)NetworkManager.ServerTime.Time); return; }
            if (currentTarget == null)
            {
                StopMoving();
                return;
            }

            navigationAgent.speed = config.ChaseMoveSpeed;
            MoveTo(canSeeCurrentTarget ? currentTarget.position : perceptionBrick.LastKnownPosition);
        }

        /// <summary>사거리를 유지하며 대상을 중심으로 선회하고 발사를 요청합니다.</summary>
        private void TickCombat(float serverTime)
        {
            if (currentTarget == null)
            {
                StopMoving();
                return;
            }

            if (serverTime >= nextOrbitSwitchTime)
            {
                orbitSign = -orbitSign;
                nextOrbitSwitchTime = serverTime + config.CombatOrbitSwitchSeconds;
            }

            Vector3 direction = combatMovementBrick.CalculateDirection(
                transform.position,
                currentTarget.position,
                orbitSign,
                config);

            navigationAgent.speed = config.CombatOrbitSpeed;
            if (direction.sqrMagnitude > 0.0001f)
            {
                MoveTo(transform.position + direction * 2f);
            }
            else
            {
                StopMoving();
            }

            FaceCurrentTarget();

            float distance = HorizontalDistance(transform.position, currentTarget.position);
            if (weaponFireGlue != null &&
                combatMovementBrick.IsInsideAttackRange(distance, config) &&
                serverTime >= nextFireTime)
            {
                Vector3 fireDirection =
                    lastAimPoint - weaponFireGlue.MuzzlePosition;
                weaponFireGlue.RequestFire(fireDirection.normalized);
                currentBurstShotCount++;

                if (currentBurstShotCount >= weaponFireGlue.EnemyBurstShotCount)
                {
                    currentBurstShotCount = 0;
                    nextFireTime = serverTime + weaponFireGlue.EnemyBurstPauseSeconds;
                }
                else
                {
                    nextFireTime = serverTime + weaponFireGlue.FireIntervalSeconds;
                }
            }
        }

        /// <summary>마지막 목격 위치까지 이동한 뒤 제한시간 동안 주변을 수색합니다.</summary>
        private void TickSearch(float serverTime)
        {
            if (!perceptionBrick.HasLastKnownPosition)
            {
                ChangeState(PoliceEnemyState.Patrol, serverTime);
                return;
            }

            TickLostPursuit(serverTime);
        }

        /// <summary>상태 진입 시 한 번만 필요한 대기·수색 값을 초기화합니다.</summary>
        private void ChangeState(PoliceEnemyState nextState, float serverTime)
        {
            if (currentState == nextState)
            {
                return;
            }

            currentState = nextState;
            SetPlayerBlockingServer(nextState == PoliceEnemyState.Combat);
            ReleaseDestination();
            isWaitingAtPatrolPoint = false;

            if (nextState == PoliceEnemyState.Patrol) lostPursuit.Reset();

            if (nextState == PoliceEnemyState.Combat)
            {
                currentBurstShotCount = 0;
                nextOrbitSwitchTime = serverTime + config.CombatOrbitSwitchSeconds;
            }
        }

        /// <summary>현재 인덱스부터 유효한 순찰 지점을 찾아 반환합니다.</summary>
        private Transform GetCurrentPatrolPoint()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < patrolPoints.Length; i++)
            {
                int index = patrolBrick.CurrentPointIndex % patrolPoints.Length;
                Transform point = patrolPoints[index];
                if (point != null)
                {
                    return point;
                }
                patrolBrick.AdvancePoint(patrolPoints.Length);
            }

            return null;
        }

        /// <summary>Agent가 현재 NavMesh 위에서 목적지를 받을 수 있는지 확인합니다.</summary>
        private bool CanMoveOnNavMesh()
        {
            return navigationAgent != null && navigationAgent.enabled && navigationAgent.isOnNavMesh;
        }

        /// <summary>NavMesh Agent에 새 목적지를 안전하게 전달합니다.</summary>
        private void MoveTo(Vector3 destination)
        {
            if (!CanMoveOnNavMesh())
            {
                return;
            }

            float now = NetworkManager.ServerTime.TimeAsFloat;
            if (now < repathAt) return;
            repathAt = now + config.RepathSeconds;
            bool stuck = hasReservedDestination && now >= stuckAt &&
                Vector3.Distance(transform.position, reservedDestination) > config.LastKnownPositionArrivalDistance;
            if (!hasReservedDestination || Vector3.Distance(requestedDestination, destination) > config.DestinationSpacing || stuck)
            {
                ReleaseDestination();
                repathAt = now + config.RepathSeconds;
                var filter = new NavMeshQueryFilter { agentTypeID = navigationAgent.agentTypeID, areaMask = navigationAgent.areaMask };
                var path = new NavMeshPath();
                for (int i = stuck ? 1 : 0; i <= config.DestinationCandidates; i++)
                {
                    var point = PoliceDestinationBrick.Candidate(destination, i, config.DestinationCandidates, config.DestinationSpread, NetworkObjectId);
                    if (!NavMesh.SamplePosition(point, out var hit, config.DestinationSampleRadius, filter) ||
                        !navigationAgent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete ||
                        !PoliceDestinationBrick.Shared.TryClaim(NetworkObjectId, hit.position, config.DestinationSpacing)) continue;
                    requestedDestination = destination; reservedDestination = hit.position; hasReservedDestination = true;
                    progressPosition = transform.position; stuckAt = now + config.StuckSeconds; break;
                }
            }
            if (!hasReservedDestination) { StopMoving(); return; }
            if (Vector3.Distance(progressPosition, transform.position) >= config.ProgressDistance)
            { progressPosition = transform.position; stuckAt = now + config.StuckSeconds; }
            navigationAgent.isStopped = false;
            if (!navigationAgent.hasPath || Vector3.Distance(navigationAgent.destination, reservedDestination) > config.ProgressDistance)
                navigationAgent.SetDestination(reservedDestination);
        }

        /// <summary>현재 NavMesh 이동을 멈춥니다.</summary>
        private void StopMoving()
        {
            if (!CanMoveOnNavMesh())
            {
                return;
            }

            navigationAgent.isStopped = true;
            navigationAgent.ResetPath();
        }

        /// <summary>높이 변화 없이 현재 대상을 향해 회전합니다.</summary>
        private void FaceCurrentTarget()
        {
            if (currentTarget == null)
            {
                return;
            }

            Vector3 direction = currentTarget.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        /// <summary>물리 충돌이나 애니메이션 루트 회전이 경찰 본체를 기울여도 서버 권위 회전을 수직으로 복구합니다.</summary>
        private void KeepUpright()
        {
            Vector3 eulerAngles = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, eulerAngles.y, 0f);
        }

        /// <summary>두 위치 사이의 지면 기준 거리를 계산합니다.</summary>
        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
