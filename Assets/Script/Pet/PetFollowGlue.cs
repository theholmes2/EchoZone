using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

namespace EchoZone.Pet
{
    /// <summary>서버 NavMesh 추종과 모든 피어의 강아지 애니메이션을 연결합니다.</summary>
    [RequireComponent(typeof(NetworkObject), typeof(NavMeshAgent))]
    public sealed class PetFollowGlue : NetworkBehaviour
    {
        /// <summary>공통 추종 설정입니다.</summary>
        [SerializeField] private PetFollowConfig config;
        /// <summary>카탈로그와 프리팹의 설정 일치 검증에 사용합니다.</summary>
        public PetFollowConfig FollowConfig => config;
        /// <summary>걷기·피격·사망 표시를 분리한 동물 View입니다.</summary>
        [SerializeField] private PetAnimatorView view;
        /// <summary>돈 기능이 없는 임시 상자 외형입니다.</summary>
        [SerializeField] private Transform cargo;
        /// <summary>서버만 활성화하는 길찾기 컴포넌트입니다.</summary>
        private NavMeshAgent agent;
        /// <summary>장물 적재에 따른 이동 배율을 제공하는 기존 작업 Glue입니다.</summary>
        private EchoZone.Heist.PetHeistGlue heist;
        /// <summary>순수 추종 방향 계산기입니다.</summary>
        private readonly PetFollowBrick followBrick = new();
        /// <summary>다음 경로 요청 시각입니다.</summary>
        private float nextRepath;
        /// <summary>부활 순간 이동 감지용 직전 주인 위치입니다.</summary>
        private Vector3 previousOwnerPosition;
        /// <summary>주인 위치의 첫 관측 여부입니다.</summary>
        private bool hasOwnerPosition;
        /// <summary>네트워크 관측 이동으로 애니메이션을 재생할 직전 위치입니다.</summary>
        private Vector3 previousViewPosition;

        /// <summary>생성 시 서버만 Agent를 활성화합니다.</summary>
        public override void OnNetworkSpawn()
        {
            agent = GetComponent<NavMeshAgent>();
            heist = GetComponent<EchoZone.Heist.PetHeistGlue>();
            agent.enabled = IsServer;
            previousViewPosition = transform.position;
            hasOwnerPosition = false;
            nextRepath = 0f;
            if (config == null) return;
            if (cargo != null) cargo.localPosition = config.CargoOffset;
            if (!IsServer) return;
            agent.speed = config.MoveSpeed;
            agent.acceleration = config.Acceleration;
            agent.angularSpeed = config.AngularSpeed;
            agent.stoppingDistance = config.FollowDistance;
        }

        /// <summary>플레이어의 기존 ManualUpdate 경로가 각 피어에서 호출합니다.</summary>
        public void ManualUpdate(Transform owner, float deltaTime)
        {
            if (!IsSpawned || config == null) return;
            if (IsServer && owner != null) FollowServer(owner.position);
            Vector3 delta = transform.position - previousViewPosition;
            previousViewPosition = transform.position;
            delta.y = 0f;
            float speed = delta.magnitude / Mathf.Max(deltaTime, 0.0001f);
            view?.SetMoving(speed > config.AnimationMoveThreshold);
            view?.ManualUpdate(deltaTime);
        }

        /// <summary>서버 이동과 잔여 경로를 제거합니다.</summary>
        public void StopServer()
        {
            if (!IsServer || agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.isStopped = true;
        }

        /// <summary>검증된 도주 목적지로 이동합니다.</summary>
        public void MoveServer(Vector3 destination, float speed, float stoppingDistance)
        {
            if (!IsServer || agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            agent.speed = speed * (heist != null ? heist.LoadSpeedMultiplier : 1f);
            agent.stoppingDistance = stoppingDistance;
            agent.isStopped = false;
            agent.SetDestination(destination);
        }

        /// <summary>새 주인을 순간 이동으로 오인하지 않도록 이전 관측을 지웁니다.</summary>
        public void ResetFollowTarget()
        {
            hasOwnerPosition = false;
            nextRepath = 0f;
        }

        /// <summary>실제 이동만 따라가고 벽은 기존 NavMesh 경로로 우회합니다.</summary>
        private void FollowServer(Vector3 ownerPosition)
        {
            Vector3 displacement = hasOwnerPosition ? ownerPosition - previousOwnerPosition : Vector3.zero;
            bool ownerTeleported = hasOwnerPosition && displacement.magnitude > config.OwnerTeleportDistance;
            previousOwnerPosition = ownerPosition;
            hasOwnerPosition = true;
            if (!ownerTeleported) followBrick.ObserveMovement(displacement);
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if ((!agent.isOnNavMesh || ownerTeleported) &&
                NavMesh.SamplePosition(followBrick.GetSpawnPosition(ownerPosition, config.FollowDistance), out var spawn, config.SampleRadius, filter))
            {
                agent.Warp(spawn.position);
                GetComponent<NetworkTransform>()?.Teleport(transform.position, transform.rotation, transform.localScale);
                nextRepath = 0f;
            }
            if (!agent.isOnNavMesh || Time.time < nextRepath) return;
            agent.speed = config.MoveSpeed * (heist != null ? heist.LoadSpeedMultiplier : 1f);
            agent.stoppingDistance = config.FollowDistance;
            agent.isStopped = false;
            nextRepath = Time.time + config.RepathSeconds;
            if (NavMesh.SamplePosition(ownerPosition, out var destination, config.SampleRadius, filter))
                agent.SetDestination(destination.position);
        }
    }
}
