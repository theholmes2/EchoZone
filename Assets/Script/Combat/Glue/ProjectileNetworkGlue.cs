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
        /// <summary>발사 당시 승인된 피해량입니다.</summary>
        private float damage;
        /// <summary>자기 피격을 제외할 발사자입니다.</summary>
        private NetworkObject shooter;
        /// <summary>동일 총알의 중복 피해 적용을 막습니다.</summary>
        private bool hitConsumed;

        /// <summary>OnNetworkSpawn 작업을 수행합니다.</summary>
        public override void OnNetworkSpawn()
        {
            ResetForReuse();
            ProjectileUpdateManager.Register(this);
        }

        /// <summary>OnNetworkDespawn 작업을 수행합니다.</summary>
        public override void OnNetworkDespawn()
        {
            ProjectileUpdateManager.Unregister(this);
            ResetForReuse();
        }

        /// <summary>양쪽 피어에서 이전 발사의 상태와 궤적을 제거합니다. 대여 후 서버가 새 발사를 초기화합니다.</summary>
        public void ResetForReuse()
        {
            simulationBrick.Initialize(null, Vector3.zero, Vector3.zero);
            damage = 0f;
            shooter = null;
            hitConsumed = false;
            foreach (var trail in GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
        }

        /// <summary>서버가 투사체 생성 직후 시작 위치와 방향을 전달합니다.</summary>
        public void InitializeServer(Vector3 spawnPosition, Vector3 fireDirection, float shotDamage, NetworkObject source)
        {
            if (!IsServer)
            {
                return;
            }

            damage = float.IsFinite(shotDamage) ? Mathf.Max(0f, shotDamage) : 0f;
            shooter = source;
            hitConsumed = false;
            simulationBrick.Initialize(config, spawnPosition, fireDirection);
            transform.position = simulationBrick.Position;
        }

        /// <summary>중앙 투사체 관리자가 서버 물리 단계에서 호출하는 갱신 진입점입니다.</summary>
        public void ManualFixedUpdate(float fixedDeltaTime)
        {
            if (!IsServer || !IsSpawned || !simulationBrick.IsActive)
            {
                return;
            }

            Vector3 previousPosition = simulationBrick.Position;
            simulationBrick.ManualFixedUpdate(fixedDeltaTime);
            Collider hit = FindFirstHit(previousPosition, simulationBrick.Position);
            if (hit != null)
            {
                HandleServerHit(hit);
                return;
            }
            transform.position = simulationBrick.Position;

            if (!simulationBrick.IsActive && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>서버 충돌 판정이 호출할 투사체 종료 진입점입니다.</summary>
        public void HandleServerHit(Collider target)
        {
            if (!IsServer || !IsSpawned || hitConsumed || target == null || ShouldIgnore(target))
            {
                return;
            }

            hitConsumed = true;
            bool policeShot = shooter != null && shooter.GetComponent<EchoZone.Enemy.PoliceEnemyBrainGlue>() != null;
            var victim = target.GetComponentInParent<NetworkObject>();
            if (!policeShot || (EchoZone.Heist.HeistWorldGlue.Instance != null && EchoZone.Heist.HeistWorldGlue.Instance.CanPoliceAttack(victim)))
                target.GetComponentInParent<DamageReceiverGlue>()?.ApplyServerBulletDamage(damage, shooter);
            simulationBrick.Stop();
            if (NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>시작점 겹침과 이동 구간을 검사하여 가장 가까운 유효 차폐물을 반환합니다.</summary>
        private Collider FindFirstHit(Vector3 start, Vector3 end)
        {
            float radius = config.ProjectileHitRadius;
            foreach (Collider overlap in Physics.OverlapSphere(start, radius, config.ProjectileHitLayers, QueryTriggerInteraction.Ignore))
            {
                if (!ShouldIgnore(overlap)) return overlap;
            }

            Vector3 displacement = end - start;
            float distance = displacement.magnitude;
            if (distance <= 0f) return null;
            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.SphereCastAll(start, radius, displacement / distance,
                         distance, config.ProjectileHitLayers, QueryTriggerInteraction.Ignore))
            {
                if (ShouldIgnore(hit.collider) || hit.distance >= nearestDistance) continue;
                nearest = hit.collider;
                nearestDistance = hit.distance;
            }
            return nearest;
        }

        /// <summary>발사자와 총알 자신의 Collider 및 다른 투사체는 피격 대상에서 제외합니다.</summary>
        private bool ShouldIgnore(Collider target)
        {
            return target == null || target.GetComponentInParent<ProjectileNetworkGlue>() != null ||
                (shooter != null && target.transform.IsChildOf(shooter.transform));
        }
    }
}
