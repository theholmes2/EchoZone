using System.Text.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace HostMigrationModule;

/// <summary>현재 Session Host의 탈출 판정을 검증하여 Protected 계정 지갑을 저장합니다.</summary>
public sealed class WalletFunctions
{
    private const string Key = "extraction_wallet_v1";

    /// <summary>같은 방 참가자의 마지막 확정 지갑을 읽습니다. 읽기 실패를 잔액 0으로 처리하지 않습니다.</summary>
    [CloudCodeFunction("LoadWallet")]
    public async Task<WalletRecord> LoadWallet(IExecutionContext context, IGameApiClient api,
        string sessionId, string playerId)
    {
        await ValidateHost(context, api, sessionId, playerId);
        var (wallet, _) = await Read(context, api, playerId);
        return wallet ?? new WalletRecord();
    }

    /// <summary>같은 정산의 재전송은 재지급하지 않고, 오래된 버전의 덮어쓰기는 거절합니다.</summary>
    [CloudCodeFunction("SettleEscape")]
    public async Task<WalletRecord> SettleEscape(IExecutionContext context, IGameApiClient api,
        string sessionId, string playerId, long expectedRevision, long balance, string settlementId)
    {
        await ValidateHost(context, api, sessionId, playerId);
        if (!Guid.TryParseExact(settlementId, "N", out _))
            throw new ArgumentException("A valid extraction settlement ID is required.", nameof(settlementId));
        if (balance < 0 || balance > 1000000000000L || expectedRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(balance));
        var (stored, writeLock) = await Read(context, api, playerId);
        var current = stored ?? new WalletRecord();
        if (current.LastSettlementId == settlementId)
        {
            if (current.Balance != balance || current.LastSessionId != sessionId || current.Revision - 1 != expectedRevision)
                throw new InvalidOperationException("Settlement ID was already used with different data.");
            return current;
        }
        if (current.Revision != expectedRevision)
            throw new InvalidOperationException("Wallet revision conflict; newer progress must not be overwritten.");
        var next = new WalletRecord { Balance = balance, Revision = checked(current.Revision + 1),
            LastSessionId = sessionId, LastSettlementId = settlementId, SavedAtUtc = DateTime.UtcNow.ToString("O") };
        await api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId,
            playerId, stored == null ? new SetItemBody(Key, JsonSerializer.Serialize(next)) :
                new SetItemBody(Key, JsonSerializer.Serialize(next), writeLock!));
        return next;
    }

    /// <summary>호출자의 호스트 권한과 돈을 받을 계정의 로비 소속을 클라우드에서 확인합니다.</summary>
    private static async Task ValidateHost(IExecutionContext context, IGameApiClient api, string sessionId, string playerId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("Session and player are required.");
        var lobby = await api.Lobby.GetLobbyAsync(context, context.ServiceToken, sessionId, "cloud-code");
        if (lobby.Data.HostId != context.PlayerId || !lobby.Data.Players.Any(p => p.Id == playerId))
            throw new UnauthorizedAccessException("Current host and current session member required.");
    }

    /// <summary>지갑과 조건부 쓰기 토큰을 함께 읽습니다. 잘못된 데이터는 새 지갑으로 초기화하지 않습니다.</summary>
    private static async Task<(WalletRecord?, string?)> Read(IExecutionContext context, IGameApiClient api, string playerId)
    {
        var response = await api.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken,
            context.ProjectId, playerId, new List<string> { Key });
        var item = response.Data.Results.FirstOrDefault(i => i.Key == Key);
        if (item == null) return (null, null);
        var record = JsonSerializer.Deserialize<WalletRecord>(item.Value.ToString()!)
            ?? throw new InvalidOperationException("Invalid wallet data.");
        if (record.Balance < 0 || record.Revision < 0) throw new InvalidOperationException("Invalid wallet data.");
        return (record, item.WriteLock);
    }
}

/// <summary>무기·방어구 정산을 나중에 확장할 버전이 있는 계정 진행도 봉투입니다.</summary>
public sealed class WalletRecord
{
    /// <summary>마지막 성공한 탈출에서 확정한 개인 잔액입니다.</summary>
    public long Balance { get; set; }
    /// <summary>동시에 열린 다른 게임의 오래된 저장을 거절할 버전입니다.</summary>
    public long Revision { get; set; }
    /// <summary>마지막 탈출의 출처 세션입니다. 같은 방 재입장을 막지 않습니다.</summary>
    public string LastSessionId { get; set; } = "";
    /// <summary>같은 탈출 재전송을 구분합니다. 기존 저장 데이터에서는 빈 문자열로 시작합니다.</summary>
    public string LastSettlementId { get; set; } = "";
    /// <summary>클라우드 서버가 기록한 저장 UTC입니다.</summary>
    public string SavedAtUtc { get; set; } = "";
}
