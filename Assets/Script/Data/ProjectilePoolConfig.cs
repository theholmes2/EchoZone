using UnityEngine;

namespace EchoZone.Combat
{
    /// <summary>총알 종류와 피어별 유휴 객체 보관 한도입니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Combat/Projectile Pool Config")]
    public sealed class ProjectilePoolConfig : ScriptableObject
    {
        /// <summary>서버와 클라이언트가 동일하게 풀 핸들러를 등록할 투사체입니다.</summary>
        public GameObject ProjectilePrefab;
        /// <summary>동시 발사 제한이 아니라 반환 후 보관할 최대 수입니다.</summary>
        [Min(1)] public int MaximumRetainedCount = 128;
    }
}
