using UnityEngine;

namespace EchoZone.CameraSystem
{
    /// <summary>
    /// 플레이어 Transform, 실제 Unity Camera, 계산 Brick을 서로 연결하는 Glue입니다.
    /// 좌표 계산 규칙은 소유하지 않고 Brick에 위임하며, 계산 결과를 Scene 오브젝트에 적용합니다.
    /// 자체 Update를 두지 않아 추후 중앙 업데이트 관리자가 호출 순서를 결정할 수 있습니다.
    /// </summary>
    public sealed class GameplayCameraGlue : MonoBehaviour
    {
        /// <summary>계산된 Pose를 실제 화면에 적용할 플레이 전용 카메라입니다.</summary>
        [SerializeField] private UnityEngine.Camera gameplayCamera;

        /// <summary>카메라가 따라갈 로컬 플레이어 Transform입니다.</summary>
        [SerializeField] private Transform followTarget;

        /// <summary>카메라 기획 수치를 제공하는 데이터 에셋입니다.</summary>
        [SerializeField] private GameplayCameraConfig config;

        /// <summary>Unity 오브젝트를 모르는 카메라 좌표 계산 Brick입니다.</summary>
        private readonly GameplayCameraBrick cameraBrick = new();

        /// <summary>입력 Glue가 원시 휠 값을 정규화할 때 사용할 읽기 전용 설정입니다.</summary>
        public GameplayCameraConfig Config => config;

        /// <summary>카메라 기준 이동을 계산할 때 참조할 실제 플레이 카메라 Transform입니다.</summary>
        public Transform CameraTransform => gameplayCamera != null ? gameplayCamera.transform : null;

        /// <summary>현재 카메라가 추적 중인 로컬 플레이어입니다.</summary>
        public Transform FollowTarget => followTarget;

        /// <summary>Scene에 배치된 카메라, 추적 대상, 설정 데이터를 Glue에 연결합니다.</summary>
        public void Configure(
            UnityEngine.Camera targetCamera,
            Transform target,
            GameplayCameraConfig cameraConfig)
        {
            if (targetCamera == null || target == null || cameraConfig == null)
            {
                return;
            }
            gameplayCamera = targetCamera;
            followTarget = target;
            config = cameraConfig;
            cameraBrick.Configure(config);
        }

        /// <summary>플레이 시작 또는 로컬 플레이어 변경 시 카메라를 즉시 초기화합니다.</summary>
        public void InitializeCamera()
        {
            
            if (gameplayCamera == null || followTarget == null || config == null)
            {
                return;
            }

            // Scene 직렬화로 참조가 연결된 경우에도 Brick은 별도 초기화가 필요합니다.
            cameraBrick.Configure(config);
            cameraBrick.Initialize(followTarget.position);
            ApplyPose();

        }

        /// <summary>로컬 소유권을 가진 네트워크 플레이어를 추적 대상으로 연결합니다.</summary>
        public void BindFollowTarget(Transform target)
        {
            if (target == null)
            {
                return;
            }

            followTarget = target;
            InitializeCamera();
        }

        /// <summary>지정한 플레이어가 현재 대상일 때만 카메라 연결을 해제합니다.</summary>
        public void UnbindFollowTarget(Transform target)
        {
            if (followTarget == target)
            {
                followTarget = null;
            }
        }

        /// <summary>
        /// 중앙 업데이트 관리자가 입력 수집 이후, 화면 렌더링 이전에 호출할 카메라 갱신 진입점입니다.
        /// </summary>
        /// <param name="zoomInput">입력 Brick에서 읽은 마우스 휠 값입니다.</param>
        /// <param name="deltaTime">중앙 업데이트 관리자가 전달한 프레임 시간입니다.</param>
        public void ManualUpdate(float zoomInput, float deltaTime)
        {
            if (followTarget == null)
            {
                return;
            }
            cameraBrick.ApplyZoomInput(zoomInput);
            cameraBrick.ManualUpdate(followTarget.position, deltaTime);
            ApplyPose();
        }

        /// <summary>Brick의 계산 결과를 실제 Unity Camera Transform에 적용합니다.</summary>
        private void ApplyPose()
        {
            if (gameplayCamera == null || config == null || !cameraBrick.IsInitialized)
            {
                return;
            }

            gameplayCamera.transform.position = cameraBrick.CurrentPosition;

            gameplayCamera.transform.rotation = cameraBrick.CurrentRotation;

            gameplayCamera.fieldOfView = config.FieldOfView;
        }
    }
}
