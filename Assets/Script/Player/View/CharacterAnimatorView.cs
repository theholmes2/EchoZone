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

        /// <summary>
        /// 캐릭터의 시선(회전)을 즉시 지정된 방향으로 돌립니다.
        /// </summary>
        public void SetFacing(Vector3 aimDirection, float turnDegreesPerSecond, float deltaTime)
        {
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion facing = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
            transform.rotation = turnDegreesPerSecond > 0f
                ? Quaternion.RotateTowards(transform.rotation, facing, turnDegreesPerSecond * deltaTime)
                : facing;
        }

        /// <summary>
        /// 이동 변위를 기반으로 달리기/걷기 애니메이션 상태를 갱신합니다.
        /// </summary>
        public void UpdateMovementAnimation(Vector2 movementDelta)
        {
            if (animator == null || config == null) return;

            bool moving = movementDelta.sqrMagnitude > config.MovementThreshold * config.MovementThreshold;
            animator.SetBool(Moving, moving);
        }
       
    }
}
