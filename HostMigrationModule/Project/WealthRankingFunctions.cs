using System.Net;
using System.Text.Json;
using EchoZone.Heist;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace HostMigrationModule;

public sealed partial class WalletFunctions
{
    // Protected Player Data의 이 숫자 키에 descending index를 먼저 생성해야 합니다.
    private const string RankingBalanceKey = "wealth_balance_v1";
    private static readonly HttpApiClient RankingClient = new();

    /// <summary>서버 전용 숫자 인덱스로 확정 보유금 상위 20명을 조회합니다. 점수 입력을 받지 않습니다.</summary>
    [CloudCodeFunction("GetWealthRanking")]
    public async Task<WealthRankingResult> GetWealthRanking(IExecutionContext context, IGameApiClient api)
    {
        if (string.IsNullOrWhiteSpace(context.PlayerId)) throw new UnauthorizedAccessException();
        var own = await RefreshRankingIndex(context, api, context.PlayerId);
        var query = new QueryIndexBody(new List<FieldFilter> {
            new FieldFilter(RankingBalanceKey, 0L, FieldFilter.OpEnum.GE, false) },
            new List<string> { RankingBalanceKey }, 0, WealthRankingBrick.Limit);
        // 같은 SDK 인증·환경·오류 처리를 유지하고 문제 있는 응답 DTO 변환만 생략합니다.
        var options = new ApiRequestOptions(context, context.ServiceToken) { Data = query };
        options.PathParameters.Add("projectId", context.ProjectId);
        var response = await RankingClient.PostAsync("/v1/data/projects/{projectId}/players/protected/query",
            options, api.CloudSaveData.Configuration, default);
        var balances = ParseRankingResponse(response.RawContent ?? throw new InvalidOperationException("Ranking response missing."));
        return new WealthRankingResult { Entries = WealthRankingBrick.Build(balances, context.PlayerId), OwnBalance = own?.Balance };
    }

    /// <summary>성공한 검색 응답의 문자열 계정 ID와 정수 잔액만 읽고 잘못된 데이터는 빈 랭킹으로 숨기지 않습니다.</summary>
    internal static KeyValuePair<string, long>[] ParseRankingResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("results").EnumerateArray().Select(row =>
        {
            string id = row.GetProperty("id").GetString() ?? throw new InvalidOperationException("Ranking ID missing.");
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Ranking ID empty.");
            var item = row.GetProperty("data").EnumerateArray().Single(i => i.GetProperty("key").GetString() == RankingBalanceKey);
            long value = item.GetProperty("value").GetInt64();
            if (value < 0) throw new InvalidOperationException("Invalid ranking balance.");
            return new KeyValuePair<string, long>(id, value);
        }).ToArray();
    }

    /// <summary>기존 계정도 입장·랭킹 조회 시 최신 지갑 CAS와 묶어 숫자 인덱스를 갱신합니다. 조회 실패를 0원으로 만들지 않습니다.</summary>
    private static async Task<WalletRecord?> RefreshRankingIndex(IExecutionContext context, IGameApiClient api, string playerId)
    {
        for (int attempt = 0; ; attempt++)
        {
            var (wallet, token) = await Read(context, api, playerId);
            if (wallet == null) return null;
            try { await Write(context, api, playerId, wallet, token!); return wallet; }
            catch (ApiException error) when (error.Response?.StatusCode == HttpStatusCode.Conflict && attempt < 2) { }
        }
    }
}
