using UnityEngine;

namespace EchoZone.Combat.Aiming
{
    /// <summary>Unity 입력과 네트워크를 모르는 순수 조준 방향 계산 Brick입니다.</summary>
    public sealed class AimDirectionBrick
    {
        /// <summary>마지막으로 유효하게 계산된 수평 조준 방향입니다.</summary>
        private Vector3 currentAimDirection = Vector3.forward;

        /// <summary>현재 다른 시스템에 제공할 수 있는 읽기 전용 조준 방향입니다.</summary>
        public Vector3 CurrentAimDirection => currentAimDirection;

        /// <summary>플레이어 위치와 지면 조준점으로 수평 발사 방향을 계산합니다.</summary>
        public bool TryCalculateDirection(Vector3 origin, Vector3 targetPoint, out Vector3 direction)
        {
            Vector3 heading = targetPoint - origin;
            heading.y = 0f;

            if (heading.sqrMagnitude > 0.0001f)
            {
                direction = heading.normalized;
                currentAimDirection = direction;
                return true;
            }

            direction = currentAimDirection;
            return false;
        }

        /// <summary>명중 오차 각도를 적용한 최종 발사 방향을 계산합니다.</summary>
        public Vector3 ApplySpread(Vector3 direction, float spreadDegrees, float randomValue)
        {
            float finalSpreadAngle = randomValue * spreadDegrees;
            Quaternion spreadRotation = Quaternion.AngleAxis(finalSpreadAngle, Vector3.up);
            return spreadRotation * direction;
        }
    }
}
