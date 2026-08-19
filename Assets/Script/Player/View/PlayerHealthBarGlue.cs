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

        /// <summary>플레이어 초기화가 끝난 뒤 체력바를 자식으로 생성하고 상태를 연결합니다.</summary>
        private void Start()
        {
            if (healthBarPrefab == null)
            {
                Debug.LogError("HealthBarView 프리팹이 연결되지 않았습니다.", this);
                return;
            }

            PlayerStats playerStats = GetComponent<PlayerStats>();
            HealthBarView healthBarView = Instantiate(
                healthBarPrefab,
                transform,
                false);

            healthBarView.Initialize(playerStats);
        }
    }
}
