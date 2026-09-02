using UnityEngine;

namespace EchoZone.Player.View
{
    /// <summary>
    /// 이동 데이터를 관찰해 Animator와 캐릭터 표시 방향만 갱신하는 View입니다.
    /// 이동 규칙, 입력, 네트워크 권한을 알지 못합니다.
    /// </summary>
    public sealed class CharacterAnimatorView : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterAnimationConfig config;
        private static readonly int Moving = Animator.StringToHash("Moving");

        public void Configure(Animator targetAnimator, CharacterAnimationConfig animationConfig)
        {
            animator = targetAnimator;
            config = animationConfig;
        }

        public void ManualUpdate(Vector2 movement, float deltaTime)
        {
            if (animator == null || config == null)
            {
                return;
            }

            bool moving = movement.sqrMagnitude > config.MovementThreshold * config.MovementThreshold;
            animator.SetBool(Moving, moving);
            if (!moving)
            {
                return;
            }

            Vector3 direction = new(movement.x, 0f, movement.y);
            Quaternion facing = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                facing,
                config.TurnDegreesPerSecond * deltaTime);
        }
    }
}
