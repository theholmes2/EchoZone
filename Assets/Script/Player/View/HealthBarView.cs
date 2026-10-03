using UnityEngine;

namespace EchoZone.Player.View
{
    /// <summary>
    /// PlayerStats의 현재 체력을 월드 공간 체력바로 표현하는 View입니다.
    /// 체력 계산이나 네트워크 동기화는 담당하지 않습니다.
    /// </summary>
    public sealed class HealthBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform fillRect;
        [SerializeField] private Vector3 localOffset = new(0f, 3.2f, 0f);

        /// <summary>playerStats 값을 저장합니다.</summary>
        private PlayerStats playerStats;
        /// <summary>targetCamera 값을 저장합니다.</summary>
        private Camera targetCamera;
        /// <summary>fullFillAnchorMaxX 값을 저장합니다.</summary>
        private float fullFillAnchorMaxX;
        /// <summary>체력 이벤트와 독립적으로 표시만 끄는 Canvas입니다.</summary>
        private Canvas displayCanvas;
        /// <summary>풀 재사용 시 배율이 누적되지 않도록 저장한 원본 크기입니다.</summary>
        private Vector3 initialScale;
        /// <summary>공용 프리팹 복사본에 모델별 표시 설정을 적용합니다.</summary>
        public void ApplyLayout(HealthBarDisplayConfig config)
        { transform.localPosition = config.LocalOffset; transform.localScale = initialScale * config.ScaleMultiplier; }
        /// <summary>체력 구독을 끊지 않고 Canvas만 제어합니다.</summary>
        public void SetVisible(bool visible) { requestedVisible = visible; RefreshVisibility(); }
        /// <summary>Glue가 합성한 명시적 표시 상태입니다.</summary>
        private bool requestedVisible = true;
        /// <summary>현재 모델의 Renderer 숨김도 기존 LateUpdate에서 함께 확인합니다.</summary>
        private Renderer[] bodyRenderers;
        /// <summary>명시적 숨김과 전체 외형 숨김 중 하나라도 있으면 UI를 감춥니다.</summary>
        private void RefreshVisibility()
        {
            bool bodyVisible = bodyRenderers == null || bodyRenderers.Length == 0;
            if (bodyRenderers != null)
                foreach (var r in bodyRenderers)
                    if (r != null && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy) { bodyVisible = true; break; }
            if (displayCanvas != null) displayCanvas.enabled = requestedVisible && bodyVisible;
        }
        /// <summary>프리팹에 설정된 최대 체력바 너비를 저장합니다.</summary>
        private void Awake()
        {
            displayCanvas = GetComponentInChildren<Canvas>(true);
            initialScale = transform.localScale;
            transform.localPosition = localOffset;

            if (fillRect != null)
            {
                fullFillAnchorMaxX = fillRect.anchorMax.x;
            }
        }

        /// <summary>표시할 플레이어 상태를 연결하고 현재 체력으로 즉시 갱신합니다.</summary>
        /// <param name="source">체력을 읽을 플레이어 상태 Brick입니다.</param>
        public void Initialize(PlayerStats source)
        {
            if (playerStats != null)
            {
                playerStats.RemoveHealthChangedListener(HandleHealthChanged);
            }

            playerStats = source;
            bodyRenderers = source != null ? source.GetComponentsInChildren<Renderer>(true) : null;

            if (playerStats == null)
            {
                SetNormalizedHealth(0f);
                return;
            }

            playerStats.AddHealthChangedListener(HandleHealthChanged);
            HandleHealthChanged(playerStats.CurrentHealth);
        }

        /// <summary>연결된 플레이어 상태의 이벤트 구독을 해제합니다.</summary>
        private void OnDestroy()
        {
            if (playerStats != null)
            {
                playerStats.RemoveHealthChangedListener(HandleHealthChanged);
            }
        }

        /// <summary>매 프레임 체력바가 현재 카메라와 같은 방향을 바라보게 합니다.</summary>
        private void LateUpdate()
        {
            RefreshVisibility();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                transform.rotation = targetCamera.transform.rotation;
            }
        }

        /// <summary>변경된 현재 체력을 0~1 비율로 변환합니다.</summary>
        /// <param name="currentHealth">PlayerStats가 전달한 현재 체력입니다.</param>
        private void HandleHealthChanged(int currentHealth)
        {
            float normalizedHealth = playerStats != null && playerStats.MaxHealth > 0
                ? (float)currentHealth / playerStats.MaxHealth
                : 0f;

            SetNormalizedHealth(normalizedHealth);
        }

        /// <summary>정규화된 체력에 맞춰 채움 영역의 가로 길이를 변경합니다.</summary>
        /// <param name="normalizedHealth">0에서 1 사이의 체력 비율입니다.</param>
        private void SetNormalizedHealth(float normalizedHealth)
        {
            if (fillRect == null)
            {
                return;
            }

            Vector2 anchorMax = fillRect.anchorMax;
            anchorMax.x = Mathf.Lerp(
                fillRect.anchorMin.x,
                fullFillAnchorMaxX,
                Mathf.Clamp01(normalizedHealth));
            fillRect.anchorMax = anchorMax;
        }
    }
}
