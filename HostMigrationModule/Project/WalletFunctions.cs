using System.Text.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;
using EchoZone.Heist;

namespace HostMigrationModule;

/// <summary>현재 Session Host의 탈출 판정을 검증하여 Protected 계정 지갑을 저장합니다.</summary>
public sealed partial class WalletFunctions
{
    private const string Key = "extraction_wallet_v1";

    /// <summary>같은 방 참가자의 마지막 확정 지갑을 읽습니다. 읽기 실패를 잔액 0으로 처리하지 않습니다.</summary>
    [CloudCodeFunction("LoadWallet")]
    public async Task<WalletRecord> LoadWallet(IExecutionContext context, IGameApiClient api,
        string sessionId, string playerId)
    {
        await ValidateHost(context, api, sessionId, playerId);
        return await Resume(context, api, playerId);
    }

    /// <summary>현재 호스트가 승인한 불변 요청을 잔액과 같은 Protected 항목에 먼저 영속화합니다.</summary>
    [CloudCodeFunction("PrepareEscape")]
    public async Task<WalletRecord> PrepareEscape(IExecutionContext context, IGameApiClient api,
        string sessionId, string playerId, string requestJson)
    {
        await ValidateHost(context, api, sessionId, playerId);
        var request = JsonSerializer.Deserialize<SettlementRequest>(requestJson)
            ?? throw new ArgumentException("Settlement request required.");
        if (request.PlayerId != playerId || request.SessionId != sessionId)
            throw new UnauthorizedAccessException("Settlement origin mismatch.");
        var (stored, writeLock) = await Read(context, api, playerId);
        // 최초 키 동시 생성은 별도 단계입니다. 토큰 없는 신규 outbox 덮어쓰기는 허용하지 않습니다.
        if (stored == null) throw new InvalidOperationException("Wallet initialization required; manual reconciliation required.");
        SettlementJournalBrick.Prepare(stored, request, DateTime.UtcNow.ToString("O"));
        var receipt = stored.Receipts.Find(r => r.Request.SettlementId == request.SettlementId);
        if (receipt != null)
            return new WalletRecord { Balance = receipt.Request.Balance, Revision = checked(receipt.Request.ExpectedRevision + 1),
                LastSessionId = receipt.Request.SessionId, LastSettlementId = receipt.Request.SettlementId,
                SavedAtUtc = receipt.LastAttemptUtc, Receipts = new List<SettlementJournalEntry> { receipt } };
        await Write(context, api, playerId, stored, writeLock!);
        return stored;
    }

    /// <summary>원래 방이 사라져도 본인 계정에 이미 접수된 요청만 재개합니다. 금액 입력은 받지 않습니다.</summary>
    [CloudCodeFunction("ResumeOwnWallet")]
    public Task<WalletRecord> ResumeOwnWallet(IExecutionContext context, IGameApiClient api)
    {
        if (string.IsNullOrWhiteSpace(context.PlayerId)) throw new UnauthorizedAccessException();
        return Resume(context, api, context.PlayerId);
    }

    /// <summary>Cloud에 이미 기록된 Run 참가자의 요청은 미접속 상태여도 새 호스트가 검증합니다.</summary>
    [CloudCodeFunction("LoadRunWallet")]
    public async Task<WalletRecord> LoadRunWallet(IExecutionContext context, IGameApiClient api,
        string sessionId, string playerId, string runId)
    {
        var lobby = await api.Lobby.GetLobbyAsync(context, context.ServiceToken, sessionId, "cloud-code");
        if (lobby.Data.HostId != context.PlayerId) throw new UnauthorizedAccessException();
        var checkpoint = await HostMigrationFunctions.GetStoredCheckpoint(context, api, runId)
            ?? throw new InvalidOperationException("Run checkpoint required.");
        using var doc = JsonDocument.Parse(checkpoint.SnapshotJson);
        if (!doc.RootElement.TryGetProperty("world", out var world) || world.ValueKind == JsonValueKind.Null ||
            !world.GetProperty("wallets").EnumerateArray().Any(p => p.GetProperty("playerId").GetString() == playerId))
            throw new UnauthorizedAccessException("Player is not recorded in the Cloud run checkpoint.");
        return await Resume(context, api, playerId);
    }

    /// <summary>접수된 본문만 반영합니다. 응답 유실 시 다음 조회는 저장된 동일 영수증을 돌려줍니다.</summary>
    private static async Task<WalletRecord> Resume(IExecutionContext context, IGameApiClient api, string playerId)
    {
        var (stored, writeLock) = await Read(context, api, playerId);
        if (stored == null) return new WalletRecord();
        bool changed = false;
        foreach (var recovery in stored.Recoveries.Where(r => r.State == "Prepared"))
        {
            var checkpoint = await HostMigrationFunctions.GetStoredCheckpoint(context, api, recovery.RunId);
            if (checkpoint == null) continue;
            using var doc = JsonDocument.Parse(checkpoint.SnapshotJson);
            if (!doc.RootElement.GetProperty("world").TryGetProperty("retirementJournalJson", out var journal) || string.IsNullOrEmpty(journal.GetString())) continue;
            using var entries = JsonDocument.Parse(journal.GetString()!);
            if (!entries.RootElement.EnumerateArray().Any(r => r.GetProperty("SettlementId").GetString() == recovery.SettlementId)) continue;
            recovery.State = "CheckpointConfirmed"; changed = true;
        }
        if (stored.Pending != null && stored.Pending.State != "Conflict")
        { SettlementJournalBrick.Commit(stored, DateTime.UtcNow.ToString("O")); changed = true; }
        if (changed) await Write(context, api, playerId, stored, writeLock!);
        return stored;
    }

    /// <summary>회수 체크포인트 쓰기 전에 계정에 Run 검색 인덱스를 보존합니다. 감사비를 확정 잔액에 넣지 않습니다.</summary>
    internal static async Task IndexRetirements(IExecutionContext context, IGameApiClient api, string runId, string snapshotJson)
    {
        using var doc = JsonDocument.Parse(snapshotJson);
        if (!doc.RootElement.TryGetProperty("world", out var world) || world.ValueKind == JsonValueKind.Null ||
            !world.TryGetProperty("retirementJournalJson", out var journal) || string.IsNullOrEmpty(journal.GetString())) return;
        using var entries = JsonDocument.Parse(journal.GetString()!);
        string[] fields = { "SettlementId", "SessionId", "RunId", "PlayerId", "CustodianPlayerId", "PetId", "LedgerIds", "ReturnedAmounts", "ExpectedRevision", "Balance", "WalletLoaded", "Reward", "CreatedAtUtc" };
        foreach (var group in entries.RootElement.EnumerateArray().GroupBy(r => r.GetProperty("CustodianPlayerId").GetString()!))
        {
            var (wallet, token) = await Read(context, api, group.Key);
            if (wallet == null) throw new InvalidOperationException("Recovery custodian wallet initialization required; manual reconciliation required.");
            bool changed = false;
            foreach (var entry in group)
            {
                if (entry.GetProperty("RunId").GetString() != runId) throw new InvalidOperationException("Recovery run mismatch.");
                string id = entry.GetProperty("SettlementId").GetString()!;
                var payload = fields.ToDictionary(f => f, f => entry.GetProperty(f));
                string json = JsonSerializer.Serialize(payload);
                var existing = wallet.Recoveries.Find(r => r.SettlementId == id);
                if (existing != null)
                {
                    if (existing.PayloadJson != json || existing.RunId != runId) throw new InvalidOperationException("Recovery payload conflict.");
                    continue;
                }
                if (wallet.Recoveries.Count >= 128) throw new InvalidOperationException("Recovery archive required; manual reconciliation required.");
                wallet.Recoveries.Add(new RecoveryJournalEntry { SettlementId = id, RunId = runId, PayloadJson = json }); changed = true;
            }
            if (changed) await Write(context, api, group.Key, wallet, token!);
        }
    }

    /// <summary>잔액·미완료 큐·영수증은 하나의 CAS 항목으로 저장합니다. 크기 초과도 삭제 대신 보류합니다.</summary>
    private static async Task Write(IExecutionContext context, IGameApiClient api, string playerId, WalletRecord record, string writeLock)
    {
        string json = JsonSerializer.Serialize(record);
        if (System.Text.Encoding.UTF8.GetByteCount(json) > 200000)
            throw new InvalidOperationException("Receipt archive required; manual reconciliation required.");
        if (string.IsNullOrEmpty(writeLock)) throw new InvalidOperationException("Wallet write lock required.");
        await api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId,
            playerId, new SetItemBody(Key, json, writeLock));
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
        var current = stored ?? throw new InvalidOperationException("Prepare wallet before joining.");
        if (current.Pending != null || current.Receipts.Count > 0 || current.Recoveries.Count > 0)
            throw new InvalidOperationException("Use the durable PrepareEscape protocol for this wallet.");
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
        await Write(context, api, playerId, next, writeLock!);
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
        string json = item.Value.ToString()!;
        string? token = item.WriteLock;
        var record = JsonSerializer.Deserialize<WalletRecord>(json)
            ?? throw new InvalidOperationException("Invalid wallet data.");
        if (record.Balance < 0 || record.Revision < 0) throw new InvalidOperationException("Invalid wallet data.");
        if (record.Receipts == null || record.Recoveries == null || record.Receipts.Any(r => r?.Request == null)) throw new InvalidOperationException("Invalid receipt data.");
        if (record.Pending != null)
        {
            record.Pending.Request.Validate();
            if (record.Pending.Request.PlayerId != playerId) throw new InvalidOperationException("Pending settlement account mismatch.");
        }
        return (record, token);
    }
}

/// <summary>무기·방어구 정산을 나중에 확장할 버전이 있는 계정 진행도 봉투입니다.</summary>
public sealed class WalletRecord : SettlementWalletRecord
{
}
