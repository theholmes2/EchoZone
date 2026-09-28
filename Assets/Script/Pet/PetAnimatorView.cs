using UnityEngine;

namespace EchoZone.Pet
{
    /// <summary>동물 걷기와 임시 피격 표시만 처리하며 네트워크와 체력을 알지 못합니다.</summary>
    public sealed class PetAnimatorView : MonoBehaviour
    {
        /// <summary>원본 강아지 Animator입니다.</summary>
        [SerializeField] private Animator animator;
        /// <summary>Collider와 분리하여 변형할 외형 루트입니다.</summary>
        [SerializeField] private Transform visual;
        /// <summary>임시 연출 설정입니다.</summary>
        [SerializeField] private PetLifeConfig config;
        /// <summary>원본 외형 크기입니다.</summary>
        private Vector3 initialScale;
        /// <summary>원본 외형 회전입니다.</summary>
        private Quaternion initialRotation;
        /// <summary>피격 연출 잔여 시간입니다.</summary>
        private float hitRemaining;

        /// <summary>외형의 원래 자세를 한 번 저장합니다.</summary>
        private void Awake()
        {
            if (visual == null) return;
            initialScale = visual.localScale;
            initialRotation = visual.localRotation;
        }

        /// <summary>네트워크 생성 시 이전 표시 상태를 제거합니다.</summary>
        public void ResetView()
        {
            hitRemaining = 0f;
            if (visual != null) { visual.localScale = initialScale; visual.localRotation = initialRotation; }
            if (animator != null) { animator.enabled = true; animator.Rebind(); SetMoving(false); }
        }

        /// <summary>관측 이동량에 맞는 원본 걷기 파라미터를 전달합니다.</summary>
        public void SetMoving(bool moving)
        {
            if (animator == null) return;
            animator.SetFloat("Vert", moving ? 1f : 0f);
            animator.SetFloat("State", 0f);
        }

        /// <summary>실제 체력 감소를 관찰했을 때 임시 움찔 표시를 시작합니다.</summary>
        public void PlayHit()
        {
            if (config != null) hitRemaining = config.HitSeconds;
        }

        /// <summary>기존 수동 갱신에서 표시 시간만 진행합니다.</summary>
        public void ManualUpdate(float deltaTime)
        {
            if (visual == null || config == null) return;
            hitRemaining = Mathf.Max(0f, hitRemaining - deltaTime);
            float pulse = Mathf.Sin(Mathf.Clamp01(hitRemaining / Mathf.Max(0.01f, config.HitSeconds)) * Mathf.PI);
            visual.localScale = initialScale * (1f - pulse * config.HitSquash);
        }
    }
}
