using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>탈출 판정과 클라우드 지갑 연결의 조절값입니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Heist/Extraction Config")]
    public sealed class ExtractionConfig : ScriptableObject
    {
        /// <summary>기존 마이그레이션 모듈에 추가한 지갑 API의 모듈 이름입니다.</summary>
        public string ModuleName = "HostMigrationModule";
        /// <summary>확정 잔액 조회 함수입니다.</summary>
        public string LoadFunction = "LoadWallet";
        /// <summary>서버 승인 탈출 저장 함수입니다.</summary>
        public string SettleFunction = "SettleEscape";
        /// <summary>플레이어가 머물러야 할 탈출 지점 반경입니다.</summary>
        [Min(0.5f)] public float PlayerRadius = 2f;
        /// <summary>함께 탈출할 소유 펫이 지점에서 떨어질 수 있는 최대 거리입니다.</summary>
        [Min(1f)] public float PetRadius = 6f;
        /// <summary>범위 안에 연속으로 머무를 시간입니다. 벗어나면 처음부터 다시 계산합니다.</summary>
        [Min(0.1f)] public float HoldSeconds = 3f;
        /// <summary>클라우드 실패 후 같은 요청을 재시도하는 간격입니다.</summary>
        [Min(1f)] public float RetrySeconds = 5f;
        /// <summary>탈출 지점까지 벽으로 가로막혔는지 검사할 높이입니다.</summary>
        [Min(0.1f)] public float CheckHeight = 0.8f;
        /// <summary>탈출 지점과 플레이어 사이를 가로막는 환경 레이어입니다.</summary>
        public LayerMask BlockingLayers = ~0;
    }
}
