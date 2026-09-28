using System;
using System.Collections.Generic;
using EchoZone.Online.Reconnect;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Online.Migration
{
    /// <summary>서버 원본 PlayerObject와 월드 아이템을 읽어 하나의 Host Migration 복사본으로 묶습니다.</summary>
    public sealed class HostMigrationSnapshotCollector : MonoBehaviour
    {
        /// <summary>runSessionState 값을 저장합니다.</summary>
        private readonly RunSessionState runSessionState = new();

        /// <summary>현재 Collector가 수집하는 Run의 고유 식별자입니다.</summary>
        public string RunId => runSessionState.RunId;

        /// <summary>마지막으로 수집한 Snapshot 버전입니다.</summary>
        public long CurrentSnapshotVersion =>
            runSessionState.CurrentSnapshotVersion;

        /// <summary>최초 Host가 새로운 Run ID와 버전 상태를 시작합니다.</summary>
        /// <returns>새로 생성한 RunId입니다.</returns>
        public string StartNewRun()
        {
            SessionWorldMigrationGlue.Reset();
            return runSessionState.StartNewRun();
        }

        /// <summary>새 Host가 기존 Snapshot의 Run ID와 버전을 이어받습니다.</summary>
        /// <param name="runId">복원할 기존 Run의 고유 식별자입니다.</param>
        /// <param name="snapshotVersion">복원한 마지막 Snapshot 버전입니다.</param>
        /// <returns>기존 Run 상태를 적용했으면 <see langword="true"/>입니다.</returns>
        public bool TryRestoreRun(string runId, long snapshotVersion)
        {
            return runSessionState.TryRestoreRun(runId, snapshotVersion);
        }

        /// <summary>현재 서버 권한 상태를 플레이어와 월드 아이템의 전체 복사본으로 수집합니다.</summary>
        /// <param name="snapshot">수집에 성공한 전체 Host Migration 복사본입니다.</param>
        /// <returns>현재 실행이 서버이고 복사본을 만들었으면 <see langword="true"/>입니다.</returns>
        public bool TryCollect(out HostMigrationSnapshot snapshot)
        {
            snapshot = null;
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.IsServer ||
                !runSessionState.HasActiveRun || SessionWorldMigrationGlue.IsRestoring)
            {
                return false;
            }

            List<HostMigrationPlayerSnapshot> players = CollectPlayers();
            SessionWorldMigrationGlue.IncludePendingPlayers(players);
            List<WorldItemMigrationSnapshot> worldItems = CollectWorldItems();
            long snapshotVersion =
                runSessionState.IssueNextSnapshotVersion();
            snapshot = new HostMigrationSnapshot(
                runSessionState.RunId,
                snapshotVersion,
                players,
                worldItems,
                SessionWorldMigrationGlue.Capture(runSessionState.RunId));
            return true;
        }

        /// <summary>서버에 존재하는 Player Reporter에서 인증 ID와 현재 상태를 수집합니다.</summary>
        private static List<HostMigrationPlayerSnapshot> CollectPlayers()
        {
            PlayerSessionCacheReporter[] reporters =
                FindObjectsByType<PlayerSessionCacheReporter>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

            Array.Sort(reporters, (left, right) =>
                string.CompareOrdinal(left.PlayerId, right.PlayerId));

            List<HostMigrationPlayerSnapshot> players = new(reporters.Length);
            for (int i = 0; i < reporters.Length; i++)
            {
                PlayerSessionCacheReporter reporter = reporters[i];
                PlayerSessionSnapshot state = reporter.CreateSnapshot();
                if (state == null || string.IsNullOrWhiteSpace(reporter.PlayerId))
                {
                    continue;
                }

                players.Add(new HostMigrationPlayerSnapshot(
                    reporter.PlayerId,
                    state));
            }

            return players;
        }

        /// <summary>활성 월드 아이템 Source에서 ID와 현재 수량을 수집합니다.</summary>
        private static List<WorldItemMigrationSnapshot> CollectWorldItems()
        {
            WorldItemMigrationSource[] sources =
                FindObjectsByType<WorldItemMigrationSource>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            Array.Sort(sources, (left, right) =>
                string.CompareOrdinal(left.WorldItemId, right.WorldItemId));

            List<WorldItemMigrationSnapshot> worldItems = new(sources.Length);
            for (int i = 0; i < sources.Length; i++)
            {
                WorldItemMigrationSnapshot itemSnapshot =
                    sources[i].CreateSnapshot();
                if (itemSnapshot != null)
                {
                    worldItems.Add(itemSnapshot);
                }
            }

            return worldItems;
        }
    }
}
