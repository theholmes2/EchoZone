using UnityEngine;

namespace EchoZone.Pet
{
    /// <summary>펫의 임시 피격 연출 값을 보관합니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Pet/Life Config")]
    public sealed class PetLifeConfig : ScriptableObject
    {
        /// <summary>피격 움찔 연출 시간입니다.</summary>
        [Min(0.01f)] public float HitSeconds = 0.15f;
        /// <summary>피격 중 외형 축소 비율입니다. 충돌체 크기는 변경하지 않습니다.</summary>
        [Range(0f, 0.3f)] public float HitSquash = 0.12f;
    }
}
