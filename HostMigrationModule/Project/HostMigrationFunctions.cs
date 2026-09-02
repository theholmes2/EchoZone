using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;
using Unity.Services.Lobby.Model;

namespace HostMigrationModule;

/// <summary>Host Migration Checkpoint를 Cloud Save Private Game Data에 저장하고 조회합니다.</summary>
public sealed class HostMigrationFunctions
{
    private const string CheckpointKey = "checkpoint";
    private readonly ILogger<HostMigrationFunctions> logger;

    /// <summary>서버 함수의 로그 출력을 연결합니다.</summary>
    /// <param name="logger">Cloud Code가 제공하는 구조화 로그입니다.</param>
    public HostMigrationFunctions(ILogger<HostMigrationFunctions> logger)
    {
        this.logger = logger;
    }

    /// <summary>기존 버전보다 새로운 Host Migration Checkpoint만 저장합니다.</summary>
    /// <param name="context">호출자와 프로젝트의 인증 정보를 가진 실행 문맥입니다.</param>
    /// <param name="gameApiClient">Cloud Save를 호출할 서버 권한 API입니다.</param>
    /// <param name="sessionId">호출자의 Host 권한을 검사할 Lobby 기반 Session ID입니다.</param>
    /// <param name="runId">게임 실행을 구분하는 Custom Item ID입니다.</param>
    /// <param name="snapshotVersion">Host가 발급한 단조 증가 버전입니다.</param>
    /// <param name="snapshotJson">전체 플레이어·월드 상태 JSON입니다.</param>
    /// <returns>저장 승인 여부와 서버가 판단한 버전입니다.</returns>
    [CloudCodeFunction("SaveCheckpoint")]
    public async Task<CheckpointSaveResponse> SaveCheckpoint(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string sessionId,
        string runId,
        long snapshotVersion,
        string snapshotJson)
    {
        await EnsureCurrentSessionHost(context, gameApiClient, sessionId);
        ValidateArguments(runId, snapshotVersion, snapshotJson);
        ValidateSnapshotIdentity(runId, snapshotVersion, snapshotJson);

        StoredCheckpoint? current = await GetStoredCheckpoint(
            context,
            gameApiClient,
            runId);

        if (current != null && snapshotVersion <= current.SnapshotVersion)
        {
            return new CheckpointSaveResponse(
                false,
                current.SnapshotVersion,
                "The checkpoint is not newer than the stored version.");
        }

        StoredCheckpoint next = new(
            snapshotVersion,
            snapshotJson,
            context.PlayerId ?? string.Empty);
        string storedJson = JsonSerializer.Serialize(next);

        try
        {
            SetItemBody requestBody = current == null
                ? new SetItemBody(CheckpointKey, storedJson)
                : new SetItemBody(
                    CheckpointKey,
                    storedJson,
                    current.WriteLock!);

            await gameApiClient.CloudSaveData.SetPrivateCustomItemAsync(
                context,
                context.ServiceToken,
                context.ProjectId,
                runId,
                requestBody);
        }
        catch (ApiException exception)
            when (exception.Response.StatusCode == HttpStatusCode.Conflict)
        {
            logger.LogWarning(
                "Checkpoint write conflict. RunId: {RunId}, Version: {Version}",
                runId,
                snapshotVersion);
            return new CheckpointSaveResponse(
                false,
                current?.SnapshotVersion ?? 0,
                "Another checkpoint was stored first. Collect a new version and retry.");
        }

        logger.LogInformation(
            "Checkpoint saved. RunId: {RunId}, Version: {Version}, PlayerId: {PlayerId}",
            runId,
            snapshotVersion,
            context.PlayerId);
        return new CheckpointSaveResponse(true, snapshotVersion, string.Empty);
    }

    /// <summary>RunId에 저장된 최신 Host Migration Checkpoint를 조회합니다.</summary>
    /// <param name="context">호출자와 프로젝트의 인증 정보를 가진 실행 문맥입니다.</param>
    /// <param name="gameApiClient">Cloud Save를 호출할 서버 권한 API입니다.</param>
    /// <param name="sessionId">호출자의 Host 권한을 검사할 Lobby 기반 Session ID입니다.</param>
    /// <param name="runId">조회할 게임 실행의 Custom Item ID입니다.</param>
    /// <returns>발견 여부와 최신 Snapshot JSON입니다.</returns>
    [CloudCodeFunction("LoadLatestCheckpoint")]
    public async Task<CheckpointLoadResponse> LoadLatestCheckpoint(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string sessionId,
        string runId)
    {
        await EnsureCurrentSessionHost(context, gameApiClient, sessionId);

        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("RunId is required.", nameof(runId));
        }

        StoredCheckpoint? current = await GetStoredCheckpoint(
            context,
            gameApiClient,
            runId);

        return current == null
            ? new CheckpointLoadResponse(false, 0, string.Empty)
            : new CheckpointLoadResponse(
                true,
                current.SnapshotVersion,
                current.SnapshotJson);
    }

    /// <summary>Cloud Code 호출자가 현재 Multiplayer Session의 Host인지 서버에서 검사합니다.</summary>
    /// <param name="context">Cloud Code가 검증한 호출자 PlayerId를 가진 실행 문맥입니다.</param>
    /// <param name="gameApiClient">Lobby 상태를 서버 권한으로 조회할 API입니다.</param>
    /// <param name="sessionId">검사할 Lobby 기반 Multiplayer Session ID입니다.</param>
    /// <returns>Host 검사가 끝날 때 완료되는 작업입니다.</returns>
    private static async Task EnsureCurrentSessionHost(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("SessionId is required.", nameof(sessionId));
        }

        ApiResponse<Lobby> response = await gameApiClient.Lobby.GetLobbyAsync(
            context,
            context.ServiceToken,
            sessionId,
            "cloud-code");

        if (!string.Equals(
                response.Data.HostId,
                context.PlayerId,
                StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "Only the current session Host can access migration checkpoints.");
        }
    }

    /// <summary>Private Game Data에서 현재 Checkpoint와 write lock을 함께 읽습니다.</summary>
    private static async Task<StoredCheckpoint?> GetStoredCheckpoint(
        IExecutionContext context,
        IGameApiClient gameApiClient,
        string runId)
    {
        try
        {
            var result = await gameApiClient.CloudSaveData
                .GetPrivateCustomItemsAsync(
                    context,
                    context.ServiceToken,
                    context.ProjectId,
                    runId,
                    new List<string> { CheckpointKey });

            Item? item = result.Data.Results.FirstOrDefault();
            if (item?.Value == null)
            {
                return null;
            }

            StoredCheckpoint? checkpoint = JsonSerializer.Deserialize<StoredCheckpoint>(
                item.Value.ToString() ?? string.Empty);
            if (checkpoint != null)
            {
                checkpoint.WriteLock = item.WriteLock;
            }

            return checkpoint;
        }
        catch (ApiException exception)
            when (exception.Response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>저장 함수의 필수 인수를 검사합니다.</summary>
    private static void ValidateArguments(
        string runId,
        long snapshotVersion,
        string snapshotJson)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("RunId is required.", nameof(runId));
        }

        if (snapshotVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshotVersion));
        }

        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            throw new ArgumentException(
                "Snapshot JSON is required.",
                nameof(snapshotJson));
        }
    }

    /// <summary>요청 인수와 JSON 내부의 RunId·Version이 같은지 검사합니다.</summary>
    private static void ValidateSnapshotIdentity(
        string runId,
        long snapshotVersion,
        string snapshotJson)
    {
        using JsonDocument document = JsonDocument.Parse(snapshotJson);
        JsonElement root = document.RootElement;
        string? embeddedRunId = root.GetProperty("runId").GetString();
        long embeddedVersion = root.GetProperty("snapshotVersion").GetInt64();

        if (!string.Equals(runId, embeddedRunId, StringComparison.Ordinal) ||
            snapshotVersion != embeddedVersion)
        {
            throw new ArgumentException(
                "Snapshot identity does not match the request arguments.");
        }
    }
}

/// <summary>Cloud Code Module에 UGS 서버 API 의존성을 등록합니다.</summary>
public sealed class ModuleConfig : ICloudCodeSetup
{
    /// <summary>Cloud Save 호출에 사용할 Game API Client를 등록합니다.</summary>
    /// <param name="config">Cloud Code Module 구성 객체입니다.</param>
    public void Setup(ICloudCodeConfig config)
    {
        config.Dependencies.AddSingleton(GameApiClient.Create());
    }
}

/// <summary>Private Game Data에 한 값으로 저장되는 Checkpoint 봉투입니다.</summary>
public sealed class StoredCheckpoint
{
    public StoredCheckpoint(
        long snapshotVersion,
        string snapshotJson,
        string savedByPlayerId)
    {
        SnapshotVersion = snapshotVersion;
        SnapshotJson = snapshotJson;
        SavedByPlayerId = savedByPlayerId;
    }

    public long SnapshotVersion { get; set; }
    public string SnapshotJson { get; set; }
    public string SavedByPlayerId { get; set; }
    public string? WriteLock { get; set; }
}

/// <summary>클라이언트에 반환할 Checkpoint 저장 결과입니다.</summary>
public sealed record CheckpointSaveResponse(
    bool Saved,
    long SnapshotVersion,
    string Message);

/// <summary>클라이언트에 반환할 최신 Checkpoint 조회 결과입니다.</summary>
public sealed record CheckpointLoadResponse(
    bool Found,
    long SnapshotVersion,
    string SnapshotJson);
