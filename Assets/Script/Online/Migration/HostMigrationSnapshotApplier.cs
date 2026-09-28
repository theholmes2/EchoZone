using System;
using System.Collections.Generic;
using EchoZone.Online.Reconnect;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Online.Migration
{
    /// <summary>검증된 Snapshot을 현재 새 Host의 서버 권한 Brick에 적용하는 Glue입니다.</summary>
    public sealed class HostMigrationSnapshotApplier
    {
        /// <summary>플레이어 캐시와 현재 월드 아이템에 Snapshot을 적용합니다.</summary>
        public bool Apply(HostMigrationSnapshot snapshot)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (snapshot == null || networkManager == null || !networkManager.IsServer)
            {
                return false;
            }

            NetworkPlayerSessionCacheGlue cache =
                NetworkPlayerSessionCacheGlue.Instance;
            if (SessionWorldMigrationGlue.AlreadyApplied(snapshot)) return true;
            if (snapshot.World != null) SessionWorldSnapshotValidator.Validate(snapshot.World);
            cache?.PrepareHostMigration(snapshot.Players);

            ApplyToConnectedPlayers(snapshot.Players);
            ApplyWorldItems(snapshot.WorldItems);
            SessionWorldMigrationGlue.Restore(snapshot);
            return true;
        }

        /// <summary>이미 새 Host에 Spawn된 플레이어가 있으면 즉시 상태를 교체합니다.</summary>
        private static void ApplyToConnectedPlayers(
            IReadOnlyList<HostMigrationPlayerSnapshot> players)
        {
            PlayerSessionCacheReporter[] reporters =
                UnityEngine.Object.FindObjectsByType<PlayerSessionCacheReporter>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            for (int i = 0; i < reporters.Length; i++)
            {
                PlayerSessionCacheReporter reporter = reporters[i];
                for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
                {
                    HostMigrationPlayerSnapshot player = players[playerIndex];
                    if (player != null &&
                        string.Equals(
                            reporter.PlayerId,
                            player.PlayerId,
                            StringComparison.Ordinal))
                    {
                        reporter.ApplyMigrationSnapshot(player.State);
                        break;
                    }
                }
            }
        }

        /// <summary>안정적인 WorldItemId가 일치하는 씬 아이템의 잔여 수량을 복원합니다.</summary>
        private static void ApplyWorldItems(
            IReadOnlyList<WorldItemMigrationSnapshot> worldItems)
        {
            WorldItemMigrationSource[] sources =
                UnityEngine.Object.FindObjectsByType<WorldItemMigrationSource>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Dictionary<string, WorldItemMigrationSource> sourcesById = new();

            for (int i = 0; i < sources.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(sources[i].WorldItemId))
                {
                    sourcesById[sources[i].WorldItemId] = sources[i];
                }
            }

            for (int i = 0; i < worldItems.Count; i++)
            {
                WorldItemMigrationSnapshot item = worldItems[i];
                if (item == null ||
                    !sourcesById.TryGetValue(item.WorldItemId, out WorldItemMigrationSource source))
                {
                    continue;
                }

                source.ApplySnapshot(item);
            }
        }
    }
}
