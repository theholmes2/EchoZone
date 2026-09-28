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
        /// <summary>Moving 값을 저장합니다.</summary>
        private static readonly int Moving = Animator.StringToHash("Moving");
        /// <summary>오른팔 반동 재생 전환에 사용하는 Trigger입니다.</summary>
        private static readonly int Shoot = Animator.StringToHash("Shoot");
        /// <summary>오른팔 조준 자세 유지에 사용하는 Bool입니다.</summary>
        private static readonly int IsAiming = Animator.StringToHash("IsAiming");
        /// <summary>현재 남아 있는 로컬 조준 연출 시간입니다.</summary>
        private float aimTimeRemaining;
        /// <summary>사망 중 다른 애니메이션과 회전이 자세를 덮어쓰지 않도록 차단합니다.</summary>
        private bool isDead;
        /// <summary>전신 사망 상태를 선택하는 Animator 파라미터입니다.</summary>
        private static readonly int Dead = Animator.StringToHash("IsDead");

        /// <summary>풀 재사용 시 이전 뼈 자세·사망·상체 반동을 초기 상태로 되돌립니다.</summary>
        public void ResetForSpawn()
        {
            isDead = false;
            aimTimeRemaining = 0f;
            if (animator == null) return;
            animator.Rebind();
            animator.SetBool(Dead, false);
            animator.SetBool(Moving, false);
            ResetFireAnimation();
            int layer = animator.GetLayerIndex("RightArmShoot");
            if (layer >= 0) animator.SetLayerWeight(layer, 1f);
            animator.Update(0f);
        }

        /// <summary>체력에서 관찰한 사망 상태를 표시하며 상체 사격 레이어를 중단합니다.</summary>
        public void SetDead(bool dead)
        {
            if (animator == null || isDead == dead) return;
            isDead = dead;
            ResetFireAnimation();
            animator.SetBool(Moving, false);
            animator.SetBool(Dead, dead);
            int layer = animator.GetLayerIndex("RightArmShoot");
            if (layer >= 0) animator.SetLayerWeight(layer, dead ? 0f : 1f);
        }

        /// <summary>승인된 발사 한 건의 반동을 재생하고 조준 유지 시간을 갱신합니다.</summary>
        public void PlayFireAnimation()
        {
            if (isDead || animator == null || config == null) return;
            aimTimeRemaining = config.AimHoldSeconds;
            animator.SetBool(IsAiming, true);
            animator.ResetTrigger(Shoot);
            animator.SetTrigger(Shoot);
        }

        /// <summary>기존 중앙 호출 순서에서 조준 연출 시간을 진행합니다.</summary>
        public void ManualUpdate(float deltaTime)
        {
            if (animator == null) return;
            aimTimeRemaining = Mathf.Max(0f, aimTimeRemaining - deltaTime);
            if (aimTimeRemaining <= 0f) animator.SetBool(IsAiming, false);
        }

        /// <summary>비활성화·디스폰 시 이전 발사 연출 상태를 제거합니다.</summary>
        public void ResetFireAnimation()
        {
            aimTimeRemaining = 0f;
            if (animator == null) return;
            animator.ResetTrigger(Shoot);
            animator.SetBool(IsAiming, false);
        }

        /// <summary>Configure 작업을 수행합니다.</summary>
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
            if (isDead || aimDirection.sqrMagnitude <= 0.0001f)
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
            if (isDead || animator == null || config == null) return;

            bool moving = movementDelta.sqrMagnitude > config.MovementThreshold * config.MovementThreshold;
            animator.SetBool(Moving, moving);
        }
       
    }
}
