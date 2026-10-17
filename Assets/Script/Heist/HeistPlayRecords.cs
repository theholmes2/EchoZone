using System;
using EchoZone.Online.Migration;
using EchoZone.Online.Reconnect;
using EchoZone.Online.Relay;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Heist
{
    public sealed partial class HeistWorldGlue
    {
        /// <summary>현재 방의 계정 지갑에만 회차를 연결합니다. 개인의 이전 방 기록은 읽지 않습니다.</summary>
        public PlayRecordBrick PlayRecordFor(string playerId)
        {
            if (!IsServer || !IsSpawned || SessionWorldMigrationGlue.IsRestoring || string.IsNullOrEmpty(playerId)) return null;
            if (!wallets.TryGetValue(playerId, out var wallet)) wallet = Wallet(playerId);
            if (wallet == null || wallet.Escaping || wallet.Settled) return wallet?.PlayRecord;
            var collector = FindFirstObjectByType<HostMigrationSnapshotCollector>();
            var relay = FindFirstObjectByType<RelaySessionGlue>();
            if (collector == null || string.IsNullOrEmpty(collector.RunId) || relay == null || string.IsNullOrEmpty(relay.SessionId)) return null;
            return wallet.EnsurePlayRecord(playerId, string.IsNullOrEmpty(worldId) ? collector.RunId : worldId,
                collector.RunId, relay.SessionId, DateTime.UtcNow.ToString("O"));
        }

        /// <summary>승인된 연결 계정을 찾아 현재 회차만 반환합니다.</summary>
        public PlayRecordBrick PlayRecordFor(NetworkObject player)
        {
            if (player == null || !player.IsSpawned || !player.IsPlayerObject || NetworkPlayerSessionCacheGlue.Instance == null ||
                !NetworkPlayerSessionCacheGlue.Instance.TryGetPlayerId(player.OwnerClientId, out string id)) return null;
            return PlayRecordFor(id);
        }

        /// <summary>실제로 체력을 감소시킨 치명타만 집계합니다. 환경·자해·펫 사망은 처치에서 제외합니다.</summary>
        public void RecordLethalDamage(NetworkObject victim, NetworkObject source)
        {
            if (!IsServer || SessionWorldMigrationGlue.IsRestoring || victim == null || source == null || source == victim ||
                !source.IsPlayerObject || !source.IsSpawned || !victim.TryGetComponent<PlayerStats>(out var stats) || !stats.IsDead) return;
            if (!victim.IsPlayerObject && !victim.TryGetComponent<EchoZone.Enemy.PoliceEnemyBrainGlue>(out _)) return;
            PlayRecordFor(source)?.Kill(Guid.NewGuid().ToString("N"), victim.IsPlayerObject);
        }
    }
}
