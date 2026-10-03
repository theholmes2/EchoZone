using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>절도 가능한 출입구의 바닥 포탈 표시 데이터입니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Heist/Portal Config")]
    public sealed class HeistPortalConfig : ScriptableObject
    {
        /// <summary>모든 절도 포탈이 공유하는 금빛 머티리얼입니다.</summary>
        public Material SharedMaterial;
        /// <summary>탈출 지점과 같은 원판 메시의 크기입니다.</summary>
        public Vector3 Scale = new Vector3(2f, 0.03f, 2f);
        /// <summary>출입구 지면으로부터 띄울 높이입니다.</summary>
        public float HeightOffset = 0.06f;
    }
}
