using UnityEngine;

namespace EchoZone.Player.View
{
    /// <summary>
    /// 플레이어의 PlayerStats Brick과 월드 공간 HealthBarView 프리팹을 연결하는 Glue입니다.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerHealthBarGlue : MonoBehaviour
    {
        /// <summary>사용자가 연결한 공용 체력바이며 개체당 한 번만 생성합니다.</summary>
        [SerializeField] private HealthBarView healthBarPrefab;
        /// <summary>미지정 시 기존 체력바 프리팹 크기와 위치를 유지합니다.</summary>
        [SerializeField] private HealthBarDisplayConfig displayConfig;
        /// <summary>내부·시야 숨김을 독립적으로 결합합니다.</summary>
        private readonly HealthBarVisibilityBrick visibility = new();

        /// <summary>체력 이벤트를 유지한 채 표시만 변경합니다.</summary>
        public void SetHidden(HealthBarHiddenReason reason, bool hidden)
        { visibility.Set(reason, hidden); if (healthBarView != null) healthBarView.SetVisible(visibility.Visible); }

        /// <summary>풀 반환 때 표시 사유를 제거하고 기존 체력바를 보존합니다.</summary>
        private void OnDisable() { visibility.Reset(); if (healthBarView != null) healthBarView.SetVisible(false); }

        /// <summary>풀 재활성화 때 중복 생성하지 않고 재사용할 체력바입니다.</summary>
        private HealthBarView healthBarView;

        /// <summary>체력바는 최초 한 번만 생성하고 재활성화 때 현재 체력을 다시 연결합니다.</summary>
        private void OnEnable()
        {
            if (healthBarPrefab == null)
            {
                Debug.LogError("HealthBarView 프리팹이 연결되지 않았습니다.", this);
                return;
            }

            PlayerStats playerStats = GetComponent<PlayerStats>();
            if (healthBarView == null) healthBarView = Instantiate(
                healthBarPrefab,
                transform,
                false);

            healthBarView.Initialize(playerStats);
            if (displayConfig != null) healthBarView.ApplyLayout(displayConfig);
            healthBarView.SetVisible(visibility.Visible);
        }
    }

}
