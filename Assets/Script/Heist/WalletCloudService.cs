using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

namespace EchoZone.Heist
{
    /// <summary>Cloud Code 응답입니다. 무기·방어구는 아직 포함하지 않습니다.</summary>
    public sealed class WalletCloudRecord
    {
        /// <summary>마지막 확정 잔액입니다.</summary>
        public long Balance;
        /// <summary>계정 저장 버전입니다.</summary>
        public long Revision;
        /// <summary>마지막 탈출의 출처 세션이며 재입장 제한에는 사용하지 않습니다.</summary>
        public string LastSessionId;
        /// <summary>재전송을 구분할 마지막 탈출 정산 ID입니다.</summary>
        public string LastSettlementId;
        /// <summary>클라우드 서버 저장 UTC입니다.</summary>
        public string SavedAtUtc;
    }

    /// <summary>금액 계산 없이 호스트의 요청을 Cloud Code에 전달하는 서비스입니다.</summary>
    public sealed class WalletCloudService
    {
        /// <summary>서버가 확인한 계정 ID로 기준 잔액을 읽습니다.</summary>
        public Task<WalletCloudRecord> Load(ExtractionConfig config, string session, string player) =>
            CloudCodeService.Instance.CallModuleEndpointAsync<WalletCloudRecord>(config.ModuleName, config.LoadFunction,
                new Dictionary<string, object> { { "sessionId", session }, { "playerId", player } });

        /// <summary>재시도에도 동일 세션·기준 버전·잔액을 보냅니다.</summary>
        public Task<WalletCloudRecord> Settle(ExtractionConfig config, string session, string player, long revision, long balance, string settlementId) =>
            CloudCodeService.Instance.CallModuleEndpointAsync<WalletCloudRecord>(config.ModuleName, config.SettleFunction,
                new Dictionary<string, object> { { "sessionId", session }, { "playerId", player },
                    { "expectedRevision", revision }, { "balance", balance }, { "settlementId", settlementId } });
    }
}
