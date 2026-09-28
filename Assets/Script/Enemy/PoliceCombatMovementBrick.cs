using UnityEngine;

namespace EchoZone.Enemy
{
    /// <summary>NavMesh와 총기 구현을 모르는 원거리 유지·선회 이동 방향 계산 Brick입니다.</summary>
    public sealed class PoliceCombatMovementBrick
    {
        /// <summary>대상과의 거리에 따라 접근·후퇴·선회 중 필요한 이동 방향을 계산합니다.</summary>
        public Vector3 CalculateDirection(Vector3 enemyPosition, Vector3 targetPosition, float orbitSign, PoliceEnemyConfig config)
        {
            if (config == null) return Vector3.zero;

            Vector3 enemyPosXZ = new Vector3(enemyPosition.x, 0f, enemyPosition.z);
            Vector3 targetPosXZ = new Vector3(targetPosition.x, 0f, targetPosition.z);

            Vector3 fromTargetOffset = enemyPosXZ - targetPosXZ;
            float currentDistance = fromTargetOffset.magnitude;

            if (currentDistance <= 0.0001f) return Vector3.zero;

            Vector3 toTargetDir = -fromTargetOffset.normalized; // 플레이어를 향하는 [앞쪽] 방향
            Vector3 awayFromTargetDir = fromTargetOffset.normalized; // 플레이어에게서 멀어지는 [뒤쪽] 방향

            Vector3 depthMoveVector = Vector3.zero;

            if (currentDistance < config.MinimumAttackDistance)
            {
                depthMoveVector = awayFromTargetDir;
            }
            else if (currentDistance > config.PreferredAttackDistance)
            {
                depthMoveVector = toTargetDir;
            }

            Vector3 lateralDir = Vector3.Cross(Vector3.up, toTargetDir).normalized;

            Vector3 orbitMoveVector = lateralDir * orbitSign;

            Vector3 finalDirection = depthMoveVector + orbitMoveVector;

            return finalDirection.sqrMagnitude > 0.0001f ? finalDirection.normalized : Vector3.zero;
        }

        /// <summary>현재 거리에서 총을 발사할 수 있는지 판정합니다.</summary>
        public bool IsInsideAttackRange(float targetDistance, PoliceEnemyConfig config)
        {
            if (config == null)
                return false;

            return targetDistance >= config.MinimumAttackDistance
                && targetDistance <= config.MaximumAttackDistance;

        }
    }
}
