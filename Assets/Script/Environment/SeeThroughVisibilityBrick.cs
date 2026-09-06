using UnityEngine;

namespace EchoZone.Environment
{
    /// <summary>카메라·Physics·Renderer를 모르는 시스루 FOV 표본 계산 Brick입니다.</summary>
    public sealed class SeeThroughVisibilityBrick
    {
        /// <summary>FOV 내부의 한 방향을 수평 월드 방향으로 계산합니다.</summary>
        public Vector3 CalculateSampleDirection(
            Vector3 playerForward,
            float fieldOfViewAngle,
            int rayIndex,
            int rayCount)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(playerForward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude <= 0.0001f) flatForward = Vector3.forward;

            float ratio = rayCount <= 1 ? 0.5f : (float)rayIndex / (rayCount - 1);
            float signedAngle = Mathf.Lerp(-fieldOfViewAngle * 0.5f, fieldOfViewAngle * 0.5f, ratio);
            return Quaternion.AngleAxis(signedAngle, Vector3.up) * flatForward;
        }
    }
}
