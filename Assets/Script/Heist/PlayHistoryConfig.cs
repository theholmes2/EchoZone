using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>동일한 타이틀 목록 화면에서 표시할 서버 데이터 종류입니다.</summary>
    public enum TitleRecordKind { PlayHistory, WealthRanking }
    /// <summary>타이틀 전적의 Cloud 연결과 목록 표시 설정입니다. 보관 상한은 서버가 강제합니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Heist/Play History Config")]
    public sealed class PlayHistoryConfig : ScriptableObject
    {
        /// <summary>개인 회차 전적 또는 확정 보유금 랭킹 표시를 선택합니다.</summary>
        public TitleRecordKind Kind;
        /// <summary>기존 지갑과 같은 Cloud Code 모듈입니다.</summary>
        public string ModuleName = "HostMigrationModule";
        /// <summary>인증된 본인의 완료 기록만 반환할 함수입니다.</summary>
        public string QueryFunction = "GetOwnPlayHistory";
        /// <summary>페이지당 표시할 목록 행 개수입니다. 서버는 최근 20회까지만 조회합니다.</summary>
        [Range(1, 10)] public int RowsPerPage = 5;
        /// <summary>전적·랭킹 응답을 기다리는 동안 표시할 아이콘의 초당 회전 각도입니다.</summary>
        [Min(0)] public float SpinnerDegreesPerSecond = 240;
    }
}
