using UnityEngine;

namespace EchoZone.Player.View
{
    /// <summary>
    /// 플레이어의 PlayerStats Brick과 월드 공간 HealthBarView 프리팹을 연결하는 Glue입니다.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerHealthBarGlue : MonoBehaviour
    {
        [SerializeField] private HealthBarView healthBarPrefab;

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
        }
    }

}
