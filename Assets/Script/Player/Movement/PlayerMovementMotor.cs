using UnityEngine;

namespace EchoZone.Player.Movement
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerMovementMotor : MonoBehaviour
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private PlayerMovementConfig config;

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

        public void Configure(Rigidbody targetBody, PlayerMovementConfig movementConfig)
        {
            body = targetBody;
            config = movementConfig;
        }
    }
}
