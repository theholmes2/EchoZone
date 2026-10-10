using UnityEngine;

namespace EchoZone.Enemy
{
    /// <summary>Physics를 모르는 시야각·발견 게이지·마지막 목격 위치 계산 Brick입니다.</summary>
    public sealed class PolicePerceptionBrick
    {
        /// <summary>플레이어를 확정 발견하기 전까지 누적되는 0~1 게이지입니다.</summary>
        private float detectionProgress;

        /// <summary>플레이어가 시야에서 사라지기 직전에 확인된 마지막 위치입니다.</summary>
        private Vector3 lastKnownPosition;

        /// <summary>유효한 마지막 목격 위치가 기록되어 있는지 나타냅니다.</summary>
        private bool hasLastKnownPosition;

        /// <summary>플레이어를 확정 발견하기 전까지 누적되는 0~1 게이지입니다.</summary>
        public float DetectionProgress => detectionProgress;
        /// <summary>플레이어가 시야에서 사라지기 직전에 확인된 마지막 위치입니다.</summary>
        public Vector3 LastKnownPosition => lastKnownPosition;
        /// <summary>유효한 마지막 목격 위치가 기록되어 있는지 나타냅니다.</summary>
        public bool HasLastKnownPosition => hasLastKnownPosition;
        /// <summary>발견 게이지와 벽 너머 마지막 목격 위치를 복원합니다.</summary>
        public void Restore(float progress, bool hasPosition, Vector3 position)
        { detectionProgress = Mathf.Clamp01(progress); hasLastKnownPosition = hasPosition; lastKnownPosition = position; }

        /// <summary>재사용 시 이전 발견 게이지와 목격 위치를 제거합니다.</summary>
        public void Reset()
        {
            detectionProgress = 0f;
            lastKnownPosition = Vector3.zero;
            hasLastKnownPosition = false;
        }

        /// <summary>대상이 거리와 시야각 안에 있는지 순수 벡터 계산으로 판정합니다.</summary>
        public bool IsInsideSight(Vector3 observerPosition, Vector3 observerForward, Vector3 targetPosition, float sightAngle, float sightDistance)
        {
            Vector3 startPos = new Vector3(observerPosition.x, 0f, observerPosition.z);
            Vector3 endPos = new Vector3(targetPosition.x, 0f, targetPosition.z);

            Vector3 toTargetOffset = endPos - startPos;

            float distanceSquared = toTargetOffset.sqrMagnitude;
            float sightDistanceSquared = sightDistance * sightDistance;

            if (distanceSquared > sightDistanceSquared)
            {
                return false;
            }

            if (distanceSquared <= 0.0001f)
            {
                return true;
            }

            Vector3 forwardDir = new Vector3(observerForward.x, 0f, observerForward.z).normalized;
            Vector3 targetDir = toTargetOffset.normalized;

            float dot = Vector3.Dot(forwardDir, targetDir);

            float cosHalfAngle = Mathf.Cos(sightAngle * 0.5f * Mathf.Deg2Rad);

            return dot >= cosHalfAngle;
        }

        /// <summary>플레이어가 충분히 움직이며 시야 거리 비율로 정한 발소리 반경 안에 있는지 판정합니다.</summary>
        public bool CanHearMovement(
            Vector3 observerPosition,
            Vector3 targetPosition,
            Vector3 targetVelocity,
            float sightDistance,
            float hearingDistanceRatio,
            float minimumMovementSpeed)
        {
            Vector2 movement = new(targetVelocity.x, targetVelocity.z);
            float safeMinimumSpeed = Mathf.Max(0f, minimumMovementSpeed);
            if (movement.sqrMagnitude <= safeMinimumSpeed * safeMinimumSpeed) return false;

            Vector2 offset = new(targetPosition.x - observerPosition.x, targetPosition.z - observerPosition.z);
            float hearingDistance = Mathf.Max(0f, sightDistance) * Mathf.Clamp01(hearingDistanceRatio);
            return offset.sqrMagnitude <= hearingDistance * hearingDistance;
        }

        /// <summary>LOS 결과에 따라 발견 게이지를 증가 또는 감소시킵니다.</summary>
        public void ManualUpdateDetection(bool hasLineOfSight, float deltaTime, PoliceEnemyConfig config)
        {
            if (config == null) return;

            if (hasLineOfSight)
            {
                detectionProgress += config.DetectionGainPerSecond * deltaTime;
            }
            else
            {
                detectionProgress -= config.DetectionLossPerSecond * deltaTime;
            }
            detectionProgress = Mathf.Clamp01(detectionProgress);
        }

        /// <summary>현재 보이는 대상의 위치를 마지막 목격 위치로 갱신합니다.</summary>
        public void RememberTargetPosition(Vector3 targetPosition)
        {
            lastKnownPosition = targetPosition;

             hasLastKnownPosition = true;
        }

        /// <summary>수색 종료 후 저장된 마지막 목격 위치를 폐기합니다.</summary>
        public void ForgetLastKnownPosition()
        {
            hasLastKnownPosition = false;
        }
    }
}
