using UnityEngine;

namespace EchoZone.Combat.Projectile
{
    /// <summary>Transform, Rigidbody, NetworkObject를 모르는 투사체 이동·수명 계산 Brick입니다.</summary>
    public sealed class ProjectileSimulationBrick
    {
        /// <summary>투사체 이동과 수명 계산에 사용할 기획 데이터입니다.</summary>
        private WeaponFireConfig config;
        /// <summary>순수 시뮬레이션이 계산한 현재 월드 위치입니다.</summary>
        private Vector3 position;
        /// <summary>투사체가 전진하는 정규화된 월드 방향입니다.</summary>
        private Vector3 direction;
        /// <summary>투사체 계산이 자동 종료되기까지 남은 시간입니다.</summary>
        private float remainingLifetime;
        /// <summary>투사체 시뮬레이션이 계속 진행되어야 하는지 나타냅니다.</summary>
        private bool isActive;

        /// <summary>계산된 현재 위치를 읽기 전용으로 제공합니다.</summary>
        public Vector3 Position => position;
        /// <summary>현재 진행 방향을 읽기 전용으로 제공합니다.</summary>
        public Vector3 Direction => direction;
        /// <summary>현재 활성 상태를 읽기 전용으로 제공합니다.</summary>
        public bool IsActive => isActive;

        /// <summary>서버가 승인한 생성 위치와 방향으로 투사체 상태를 초기화합니다.</summary>
        public void Initialize(WeaponFireConfig weaponConfig, Vector3 spawnPosition, Vector3 fireDirection)
        {
            config = weaponConfig;
            position = spawnPosition;
            direction = fireDirection.normalized;
            remainingLifetime = config != null ? config.ProjectileLifetimeSeconds : 0f;
            isActive = config != null && direction.sqrMagnitude > 0.0001f;
        }

        /// <summary>중앙 물리 업데이트가 전달한 시간만큼 다음 위치와 남은 수명을 계산합니다.</summary>
        public void ManualFixedUpdate(float fixedDeltaTime)
        {
            if (!isActive || config == null)
            {
                return;
            }

            position += direction * config.ProjectileSpeed * fixedDeltaTime;
            remainingLifetime -= fixedDeltaTime;

            if (remainingLifetime <= 0f)
            {
                isActive = false;
            }
        }

        /// <summary>충돌 또는 서버 판정으로 투사체 계산을 종료합니다.</summary>
        public void Stop()
        {
            isActive = false;
        }
    }
}
