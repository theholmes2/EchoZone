using UnityEngine;

namespace EchoZone.Player.Movement
{
    /// <summary>
    /// WASD의 2차원 입력을 카메라 화면 기준의 월드 이동 방향으로 변환하는 독립 Brick입니다.
    /// 실제 Camera나 Rigidbody를 직접 참조하지 않으므로 오프라인과 네트워크 이동에서 함께 사용할 수 있습니다.
    /// </summary>
    public sealed class CameraRelativeMovementBrick
    {
        /// <summary>
        /// 화면의 위쪽을 W, 오른쪽을 D로 느낄 수 있도록 카메라 축을 지면에 투영해 이동 방향을 만듭니다.
        /// </summary>
        /// <param name="moveInput">WASD 또는 스틱에서 읽은 2차원 입력입니다.</param>
        /// <param name="cameraForward">카메라가 바라보는 월드 전방 벡터입니다.</param>
        /// <param name="cameraRight">카메라의 월드 오른쪽 벡터입니다.</param>
        /// <returns>X에는 월드 X, Y에는 월드 Z가 들어 있는 이동 입력입니다.</returns>
        public Vector2 ConvertToWorldInput(
            Vector2 moveInput,
            Vector3 cameraForward,
            Vector3 cameraRight)
        {
            Vector2 flatForward = new(cameraForward.x, cameraForward.z);

            Vector2 flatRight = new(cameraRight.x, cameraRight.z);
            flatForward.Normalize();
            flatRight.Normalize();

            return Vector2.ClampMagnitude(flatRight * moveInput.x +flatForward * moveInput.y,1f);
            
        }
    }
}
