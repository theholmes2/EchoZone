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
    private const string HistoryKey = "play_history_v1";
    private const int HistoryConflictAttempts = 3;

    /// <summary>인증된 본인의 접수 완료 요청을 재개한 뒤 최신 완료 전적만 반환합니다. 수치 입력은 받지 않습니다.</summary>
    [CloudCodeFunction("GetOwnPlayHistory")]
    public async Task<CompletedPlayRecord[]> GetOwnPlayHistory(IExecutionContext context, IGameApiClient api)
    {
        if (string.IsNullOrWhiteSpace(context.PlayerId)) throw new UnauthorizedAccessException();
        await Resume(context, api, context.PlayerId);
        var (history, _) = await ReadHistory(context, api, context.PlayerId);
        return PlayHistoryBrick.Recent(history);
    }

    /// <summary>지급이 끝난 영수증의 고정 본문만 목록으로 옮깁니다. 금액은 다시 지급하지 않습니다.</summary>
    private static async Task<WalletRecord> FlushPlayHistory(IExecutionContext context, IGameApiClient api, string playerId)
    {
        for (int attempt = 0; ; attempt++)
        {
            // 돈 지급 쓰기와 분리하여 반드시 최신 지갑/토큰을 다시 읽습니다.
            var (wallet, walletLock) = await Read(context, api, playerId);
            if (wallet == null) return new WalletRecord();
            var pending = wallet.Receipts.Where(r => r.State == "Committed" &&
                !r.PlayRecordSaved && !string.IsNullOrEmpty(r.Request.RoundId)).ToArray();
            if (pending.Length == 0) return wallet;
            if (string.IsNullOrEmpty(walletLock)) throw new InvalidOperationException("History requires wallet write lock.");
            var (history, historyLock) = await ReadHistory(context, api, playerId);
            PlayHistoryBrick.StageCommitted(wallet, history, playerId);
            string walletJson = SerializeWallet(wallet);
            try
            {
                // Protected batch는 원자적입니다. 최초 history 생성도 기존 지갑 CAS에 묶어
                // 늦은 무조건 초기화가 최신 목록을 덮어쓰지 못하게 합니다.
                await api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken, context.ProjectId,
                    playerId, new SetItemBatchBody(new List<SetItemBody> {
                        new SetItemBody(Key, walletJson, walletLock),
                        new SetItemBody(HistoryKey, JsonSerializer.Serialize(history), historyLock!) }));
                return wallet;
            }
            catch (ApiException error) when (error.Response?.StatusCode == HttpStatusCode.Conflict && attempt + 1 < HistoryConflictAttempts)
            {
                // 다른 확정 기록을 덮지 않고 다음 시도에서 두 항목을 다시 읽습니다.
            }
        }
    }

    /// <summary>표시 목록만 읽으며 누락된 목록을 무조건 빈 값으로 저장하지 않습니다.</summary>
    private static async Task<(PlayHistoryData, string?)> ReadHistory(IExecutionContext context, IGameApiClient api, string playerId)
    {
        var response = await api.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken,
            context.ProjectId, playerId, new List<string> { HistoryKey });
        var item = response.Data.Results.FirstOrDefault(i => i.Key == HistoryKey);
        if (item == null) return (new PlayHistoryData(), null);
        var history = JsonSerializer.Deserialize<PlayHistoryData>(item.Value.ToString()!)
            ?? throw new InvalidOperationException("Invalid history data.");
        if (history.Records == null || history.Records.Any(r => r?.Summary == null || r.Summary.PlayerId != playerId))
            throw new InvalidOperationException("Invalid history ownership.");
        return (history, item.WriteLock);
    }
}
