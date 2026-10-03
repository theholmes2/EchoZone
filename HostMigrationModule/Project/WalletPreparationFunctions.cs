using System.Text.Json;
using EchoZone.Heist;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace HostMigrationModule;

public sealed partial class WalletFunctions
{
    /// <summary>본인 지갑을 입장 전에 준비합니다. 최초 쓰기는 원자적 생성이 아니며 동일 계정 다중 접속은 지원하지 않습니다.</summary>
    [CloudCodeFunction("EnsureOwnWallet")]
    public async Task<WalletRecord> EnsureOwnWallet(IExecutionContext context, IGameApiClient api)
    {
        string? player = context.PlayerId;
        if (string.IsNullOrWhiteSpace(player)) throw new UnauthorizedAccessException();
        await WalletPreparation.EnsureAsync(
            async () => (await Read(context, api, player)).Item1!,
            empty => api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId,
                player, new SetItemBody(Key, JsonSerializer.Serialize(empty))));
        var result = await Resume(context, api, player);
        if (result.Pending != null || result.Recoveries.Any(r => r.State != "CheckpointConfirmed"))
            throw new InvalidOperationException("Wallet reconciliation required before joining.");
        return result;
    }
}
