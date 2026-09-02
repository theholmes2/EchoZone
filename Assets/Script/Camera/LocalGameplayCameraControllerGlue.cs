using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoZone.CameraSystem
{
    /// <summary>
    /// 로컬 화면 입력과 카메라 Glue를 연결하는 플레이 전용 프레임 관리자입니다.
    /// 네트워크 상태를 변경하지 않으며, 로컬 소유 플레이어가 연결된 카메라만 갱신합니다.
    /// </summary>
    public sealed class LocalGameplayCameraControllerGlue : MonoBehaviour
    {
        [SerializeField] private GameplayCameraGlue gameplayCameraGlue;

        /// <summary>현재 로컬 플레이어 창에 연결된 카메라 줌 입력 Action입니다.</summary>
        private InputAction zoomAction;

        /// <summary>씬 제작 도구가 플레이 카메라 Glue를 연결합니다.</summary>
        public void Configure(GameplayCameraGlue targetCameraGlue)
        {
            gameplayCameraGlue = targetCameraGlue;
        }

        /// <summary>이 로컬 플레이어 창의 휠 입력 Action을 생성하고 활성화합니다.</summary>
        private void OnEnable()
        {
            GameplayCameraConfig config = gameplayCameraGlue != null ? gameplayCameraGlue.Config : null;
            if (config == null || string.IsNullOrWhiteSpace(config.ZoomBindingPath))
            {
                return;
            }

            zoomAction = new InputAction(
                name: "CameraZoom",
                type: InputActionType.PassThrough,
                binding: config.ZoomBindingPath,
                expectedControlType: "Axis");
            zoomAction.Enable();
        }

        /// <summary>씬 종료 시 로컬 입력 Action을 해제합니다.</summary>
        private void OnDisable()
        {
            zoomAction?.Disable();
            zoomAction?.Dispose();
            zoomAction = null;
        }

        /// <summary>네트워크 이동이 끝난 뒤 추적과 줌을 한 번 갱신합니다.</summary>
        private void LateUpdate()
        {
            if (gameplayCameraGlue == null)
            {
                return;
            }

            float rawScroll = zoomAction?.ReadValue<float>() ?? 0f;
            float inputScale = gameplayCameraGlue.Config?.WheelInputScale ?? 0f;
            gameplayCameraGlue.ManualUpdate(rawScroll * inputScale, Time.deltaTime);
        }
    }
}
