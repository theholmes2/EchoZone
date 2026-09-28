using System;
using UnityEngine;

namespace EchoZone.CameraSystem
{
    /// <summary>
    /// 카메라의 위치·회전·줌 거리만 계산하는 독립 Brick입니다.
    /// 실제 <see cref="UnityEngine.Camera"/>, 플레이어 Transform, Input System을 참조하지 않습니다.
    /// 따라서 테스트에서는 순수 좌표만 전달하여 계산 결과를 검증할 수 있습니다.
    /// </summary>
    [Serializable]
    public sealed class GameplayCameraBrick
    {
        /// <summary>카메라 계산에 사용할 데이터입니다.</summary>
        private GameplayCameraConfig config;

        /// <summary>현재 부드럽게 보간된 카메라 위치입니다.</summary>
        private Vector3 currentPosition = Vector3.zero;

        /// <summary>현재 카메라 회전입니다.</summary>
        private Quaternion currentRotation = Quaternion.identity;

        /// <summary>현재 목표 지점과 카메라 사이의 거리입니다.</summary>
        private float currentDistance = 16f;

        /// <summary>초기 Pose 계산이 끝났는지 나타냅니다.</summary>
        private bool initialized = false;

        /// <summary>현재 부드럽게 보간된 카메라 위치입니다.</summary>
        public Vector3 CurrentPosition => currentPosition;
        /// <summary>현재 카메라 회전입니다.</summary>
        public Quaternion CurrentRotation => currentRotation;
        /// <summary>현재 목표 지점과 카메라 사이의 거리입니다.</summary>
        public float CurrentDistance => currentDistance;
        /// <summary>초기 Pose 계산이 끝났는지 나타냅니다.</summary>
        public bool IsInitialized => initialized;

        /// <summary>Brick이 사용할 설정 데이터를 연결합니다.</summary>
        /// <param name="cameraConfig">하드코딩 대신 사용할 카메라 설정 데이터입니다.</param>
        public void Configure(GameplayCameraConfig cameraConfig)
        {
            if (cameraConfig == null)
            {
                return;
            }

            config = cameraConfig;
            currentDistance = config.InitialDistance;
        }

        /// <summary>플레이어 위치를 기준으로 첫 프레임의 카메라 Pose를 즉시 계산합니다.</summary>
        /// <param name="targetPosition">카메라가 추적할 월드 좌표입니다.</param>
        public void Initialize(Vector3 targetPosition)
        {
            if (config == null)
            {
                return;
            }

            Vector3 lookTarget = targetPosition + config.TargetOffset;

            currentRotation = Quaternion.Euler(
                config.PitchDegrees,
                config.YawDegrees,
                0f);

            currentPosition =
                lookTarget - currentRotation * Vector3.forward * currentDistance;

            initialized = true;
        }

        /// <summary>마우스 휠 입력을 현재 줌 거리에 반영합니다.</summary>
        /// <param name="scrollDelta">입력 계층에서 정규화하여 전달한 휠 값입니다.</param>
        public void ApplyZoomInput(float scrollDelta)
        {
            if (config == null)
            {
                return;
            }

            currentDistance = Mathf.Clamp(
                currentDistance - scrollDelta * config.ZoomSensitivity,
                config.MinimumDistance,
                config.MaximumDistance);
        }

        /// <summary>목표 위치를 따라 다음 카메라 Pose를 계산합니다.</summary>
        /// <param name="targetPosition">현재 플레이어의 월드 좌표입니다.</param>
        /// <param name="deltaTime">호출 측이 관리하는 프레임 시간입니다.</param>
        public void ManualUpdate(Vector3 targetPosition, float deltaTime)
        {
            if (config == null || !initialized)
            {
                return;
            }

            Vector3 lookTarget = targetPosition + config.TargetOffset;
            Vector3 desiredPosition = lookTarget - currentRotation * Vector3.forward * currentDistance;
            float followT = 1f - Mathf.Exp(-config.FollowSharpness * Mathf.Max(0f, deltaTime));

            currentPosition = Vector3.Lerp(
                currentPosition,
                desiredPosition,
                followT);
        }
    }
}
