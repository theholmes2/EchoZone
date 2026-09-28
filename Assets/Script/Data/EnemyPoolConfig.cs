using UnityEngine;

namespace EchoZone.Enemy
{
    /// <summary>경찰 시체 유지시간과 피어별 유휴 풀 한도를 보관합니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Enemy/Enemy Pool Config")]
    public sealed class EnemyPoolConfig : ScriptableObject
    {
        /// <summary>사망 판정 후 애니메이션과 시체를 보여줄 서버 시간입니다.</summary>
        [SerializeField, Min(0.4f)] private float corpseLifetimeSeconds = 2f;
        /// <summary>피어마다 보관할 비활성 경찰의 최대 수입니다.</summary>
        [SerializeField, Min(1)] private int maximumRetainedCount = 32;
        /// <summary>원본 사망 클립보다 짧게 설정되지 않도록 제한한 유지시간입니다.</summary>
        public float CorpseLifetimeSeconds => Mathf.Max(0.4f, corpseLifetimeSeconds);
        /// <summary>최대 유휴 보관 수입니다.</summary>
        public int MaximumRetainedCount => Mathf.Max(1, maximumRetainedCount);
    }
}
