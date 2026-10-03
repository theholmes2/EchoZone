using UnityEngine;
namespace EchoZone.Player.View
{
    /// <summary>모델별 체력바 높이와 기본 프리팹 크기의 배율입니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/View/Health Bar Display")]
    public sealed class HealthBarDisplayConfig : ScriptableObject
    {
        /// <summary>캐릭터 루트 기준 체력바 위치입니다.</summary>
        public Vector3 LocalOffset = new(0, 2, 0);
        /// <summary>기본 체력바 크기에 곱할 값입니다.</summary>
        [Min(0.01f)] public float ScaleMultiplier = 0.7f;
    }
}
