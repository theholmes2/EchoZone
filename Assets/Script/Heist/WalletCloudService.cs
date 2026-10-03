using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

namespace EchoZone.Heist
{
    /// <summary>Cloud Code 응답입니다. 무기·방어구는 아직 포함하지 않습니다.</summary>
    public sealed class WalletCloudRecord : SettlementWalletRecord
    {
    }

    /// <summary>금액 계산 없이 호스트의 요청을 Cloud Code에 전달하는 서비스입니다.</summary>
    public sealed class WalletCloudService
    {
        /// <summary>금액과 PlayerId 입력 없이 인증된 본인의 지갑만 입장 전에 준비합니다.</summary>
        public Task<WalletCloudRecord> EnsureOwn(RelaySessionConfig config) =>
            CloudCodeService.Instance.CallModuleEndpointAsync<WalletCloudRecord>(config.WalletModuleName,
                config.WalletAdmissionFunction, new Dictionary<string, object>());
        /// <summary>Cloud 체크포인트의 계정 포함 여부를 검증한 뒤 미접속 계정도 복구 전에 대조합니다.</summary>
        public Task<WalletCloudRecord> LoadForRun(ExtractionConfig config, string session, string player, string run) =>
            CloudCodeService.Instance.CallModuleEndpointAsync<WalletCloudRecord>(config.ModuleName, config.RecoveryFunction,
                new Dictionary<string, object> { { "sessionId", session }, { "playerId", player }, { "runId", run } });
        /// <summary>최초 승인한 본문을 Cloud 영속 outbox에 접수합니다.</summary>
        public Task<WalletCloudRecord> Prepare(ExtractionConfig config, string session, string player, SettlementRequest request) =>
            CloudCodeService.Instance.CallModuleEndpointAsync<WalletCloudRecord>(config.ModuleName, config.PrepareFunction,
                new Dictionary<string, object> { { "sessionId", session }, { "playerId", player },
                    { "requestJson", Newtonsoft.Json.JsonConvert.SerializeObject(request) } });
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
