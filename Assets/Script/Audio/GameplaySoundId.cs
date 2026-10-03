namespace EchoZone.Audio
{
    /// <summary>문자열 대신 사용하는 확정된 게임 효과음 식별자입니다.</summary>
    public enum GameplaySoundId
    {
        /// <summary>플레이어의 승인된 사격입니다.</summary>
        PlayerShot,
        /// <summary>경찰의 승인된 사격입니다.</summary>
        PoliceShot,
        /// <summary>펫 절도 명령이 승인되었습니다.</summary>
        TheftStarted,
        /// <summary>장물 이동이 확정되었습니다.</summary>
        TheftSucceeded,
        /// <summary>절도 거부·취소·적발입니다.</summary>
        TheftFailed,
        /// <summary>펫 획득·신고 요청이 승인되었습니다.</summary>
        RequestSucceeded,
        /// <summary>상호작용 요청이 거부되었습니다.</summary>
        RequestFailed,
        /// <summary>로컬 플레이어 수배가 시작되었습니다.</summary>
        WantedStarted,
        /// <summary>로컬 플레이어 수배가 해제되었습니다.</summary>
        WantedCleared,
        /// <summary>아이템이 실제 인벤토리로 이동했습니다.</summary>
        ItemAcquired,
        /// <summary>탈출 대기가 끝나 정산을 시작합니다.</summary>
        ExtractionStarted,
        /// <summary>정산과 최종 체크포인트가 확인되었습니다.</summary>
        SettlementSucceeded,
        /// <summary>정산 요청의 첫 실패입니다.</summary>
        SettlementFailed,
        /// <summary>감사비 또는 현상금이 지급되었습니다.</summary>
        RewardReceived
    }

    /// <summary>서로 다른 공간 설정을 섞지 않는 재생 채널입니다.</summary>
    public enum GameplaySoundChannel
    {
        /// <summary>개인 피드백 2D입니다.</summary>
        UI,
        /// <summary>플레이어 무기 3D입니다.</summary>
        Weapon,
        /// <summary>경찰·월드 3D입니다.</summary>
        World
    }
}
