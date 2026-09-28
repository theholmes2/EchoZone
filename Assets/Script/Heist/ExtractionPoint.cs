using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>임시 탈출 지점입니다. 규칙이나 Update 없이 위치와 설정만 제공합니다.</summary>
    public sealed class ExtractionPoint : MonoBehaviour
    {
        /// <summary>서버 판정과 클라우드 연결에 공통으로 사용할 설정입니다.</summary>
        [SerializeField] private ExtractionConfig config;
        /// <summary>현재 씬의 임시 단일 탈출 지점입니다.</summary>
        public static ExtractionPoint Instance { get; private set; }
        /// <summary>탈출 설정 에셋입니다.</summary>
        public ExtractionConfig Config => config;
        /// <summary>씬이 활성화될 때 등록합니다.</summary>
        private void OnEnable() => Instance = this;
        /// <summary>씬 전환 시 이전 지점을 해제합니다.</summary>
        private void OnDisable() { if (Instance == this) Instance = null; }
    }
}
