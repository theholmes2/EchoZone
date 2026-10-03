using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>돈 건물 종류를 나타내는 정적 표시입니다. 금액·절도 판정이나 네트워크 권한을 갖지 않습니다.</summary>
    public sealed class HeistPortalView : MonoBehaviour
    {
        /// <summary>공유 머티리얼과 표시 크기를 제공하는 설정입니다.</summary>
        [SerializeField] private HeistPortalConfig config;
        /// <summary>원판 메시를 가진 표시 오브젝트입니다.</summary>
        [SerializeField] private Transform disc;
        /// <summary>런타임 시작 시 설정을 표시로 적용합니다.</summary>
        private void Awake() => ApplyConfiguration();
        /// <summary>인스턴스 머티리얼을 생성하지 않고 공유 설정을 적용합니다.</summary>
        public void ApplyConfiguration()
        {
            if (config == null || disc == null) return;
            disc.localPosition = Vector3.up * config.HeightOffset;
            disc.localScale = config.Scale;
            var renderer = disc.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = config.SharedMaterial;
        }
    }
}
