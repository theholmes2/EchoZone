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
        [SerializeField] private Vector3 localOffset = new(0f, 1.4f, 0f);

        private PlayerStats playerStats;
        private Camera targetCamera;
        private float fullFillAnchorMaxX;

        /// <summary>프리팹에 설정된 최대 체력바 너비를 저장합니다.</summary>
        private void Awake()
        {
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
