using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

namespace EchoZone.Heist
{
    /// <summary>전적 수치·타인 계정 입력 없이 본인의 완료 기록 조회만 연결합니다.</summary>
    public sealed class PlayHistoryService
    {
        /// <summary>금액 입력 없이 서버 확정 지갑의 상위 랭킹만 조회합니다.</summary>
        public Task<WealthRankingResult> LoadRanking(PlayHistoryConfig config) => CloudCodeService.Instance
            .CallModuleEndpointAsync<WealthRankingResult>(config.ModuleName, config.QueryFunction, new Dictionary<string, object>());
        /// <summary>서버에 이미 접수된 전적 재시도와 최신 목록 조회를 요청합니다.</summary>
        public Task<CompletedPlayRecord[]> Load(PlayHistoryConfig config) => CloudCodeService.Instance
            .CallModuleEndpointAsync<CompletedPlayRecord[]>(config.ModuleName, config.QueryFunction, new Dictionary<string, object>());
    }
}
