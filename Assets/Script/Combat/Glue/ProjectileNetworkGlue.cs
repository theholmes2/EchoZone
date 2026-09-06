using EchoZone.Combat.Projectile;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Combat.Glue
{
    /// <summary>서버 투사체 시뮬레이션 결과를 Unity Transform과 네트워크 생명주기에 연결하는 Glue입니다.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class ProjectileNetworkGlue : NetworkBehaviour
    {
        /// <summary>서버 투사체 시뮬레이션에 전달할 속도·수명·피해 데이터입니다.</summary>
        [SerializeField] private WeaponFireConfig config;

        /// <summary>Unity와 네트워크를 모르는 투사체 위치·수명 계산 Brick입니다.</summary>
        private readonly ProjectileSimulationBrick simulationBrick = new();

        public override void OnNetworkSpawn()
        {
            ProjectileUpdateManager.Register(this);
        }

        public override void OnNetworkDespawn()
        {
            ProjectileUpdateManager.Unregister(this);
        }

        /// <summary>서버가 투사체 생성 직후 시작 위치와 방향을 전달합니다.</summary>
        public void InitializeServer(Vector3 spawnPosition, Vector3 fireDirection)
        {
            if (!IsServer)
            {
                return;
            }

            simulationBrick.Initialize(config, spawnPosition, fireDirection);
            transform.position = simulationBrick.Position;
        }

        /// <summary>중앙 투사체 관리자가 서버 물리 단계에서 호출하는 갱신 진입점입니다.</summary>
        public void ManualFixedUpdate(float fixedDeltaTime)
        {
            if (!IsServer || !simulationBrick.IsActive)
            {
                return;
            }

            simulationBrick.ManualFixedUpdate(fixedDeltaTime);
            transform.position = simulationBrick.Position;

            if (!simulationBrick.IsActive && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>서버 충돌 판정이 호출할 투사체 종료 진입점입니다.</summary>
        public void HandleServerHit(Collider target)
        {
            if (!IsServer)
            {
                return;
            }

            simulationBrick.Stop();
            if (NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }
    }
}
