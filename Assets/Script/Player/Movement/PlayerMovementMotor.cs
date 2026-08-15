using UnityEngine;

namespace EchoZone.Player.Movement
{
    /// <summary>
    /// 전달받은 이동 입력과 설정 데이터를 사용해 Rigidbody 이동을 시뮬레이션하는 Brick입니다.
    /// 입력 수집과 네트워크 전송은 담당하지 않습니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerMovementMotor : MonoBehaviour
    {
        /// <summary>실제로 이동시킬 플레이어 Rigidbody입니다.</summary>
        [SerializeField] private Rigidbody body;

        /// <summary>이동 속도 등 시뮬레이션 데이터를 제공하는 설정입니다.</summary>
        [SerializeField] private PlayerMovementConfig config;

        /// <summary>입력 방향을 기준으로 한 번의 물리 프레임 이동을 시뮬레이션합니다.</summary>
        /// <param name="input">정규화 전의 2차원 이동 입력입니다.</param>
        /// <param name="fixedDeltaTime">현재 물리 프레임의 시간 간격입니다.</param>
        public void SimulateMovement(Vector2 input, float fixedDeltaTime)
        {
            if (body == null || config == null)
            {
                return;
            }

            Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);
            Vector3 direction = new Vector3(clampedInput.x, 0f, clampedInput.y);
            Vector3 nextPosition = body.position + direction * config.MoveSpeed * fixedDeltaTime;

            body.MovePosition(nextPosition);
        }

        /// <summary>이동에 사용할 Rigidbody와 설정 데이터를 외부에서 연결합니다.</summary>
        /// <param name="targetBody">이동시킬 Rigidbody입니다.</param>
        /// <param name="movementConfig">이동 데이터를 제공할 설정입니다.</param>
        public void Configure(Rigidbody targetBody, PlayerMovementConfig movementConfig)
        {
            body = targetBody;
            config = movementConfig;
        }
    }
}
