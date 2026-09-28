using System;
using System.Threading.Tasks;
using EchoZone.Online.Relay;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Online.Migration
{
    /// <summary>서버 Snapshot 수집·JSON 변환·Cloud 통신 Brick을 순서대로 연결하는 Glue입니다.</summary>
    public sealed class HostMigrationCloudCheckpointGlue : MonoBehaviour
    {
        [SerializeField] private HostMigrationSnapshotCollector collector;
        [SerializeField] private HostMigrationCloudConfig config;
        [SerializeField] private RelaySessionGlue relaySessionGlue;

        /// <summary>serializer 값을 저장합니다.</summary>
        private readonly HostMigrationSnapshotJsonSerializer serializer = new();
        /// <summary>cloudService 값을 저장합니다.</summary>
        private readonly HostMigrationCloudCheckpointService cloudService = new();

        /// <summary>Cloud 저장 작업이 실행 중인지 나타냅니다.</summary>
        public bool IsSaving { get; private set; }

        /// <summary>automaticSavingEnabled 값을 저장합니다.</summary>
        private bool automaticSavingEnabled;

        /// <summary>Session 참가 이후 설정 간격으로 현재 Host만 Cloud 체크포인트를 저장합니다.</summary>
        public void StartAutomaticSaving(float intervalSeconds)
        {
            if (automaticSavingEnabled)
            {
                return;
            }

            automaticSavingEnabled = true;
            _ = RunAutomaticSavingAsync(Mathf.Max(1f, intervalSeconds));
        }

        /// <summary>OnDestroy 작업을 수행합니다.</summary>
        private void OnDestroy()
        {
            automaticSavingEnabled = false;
        }

        /// <summary>RunAutomaticSavingAsync 작업을 수행합니다.</summary>
        private async Task RunAutomaticSavingAsync(float intervalSeconds)
        {
            int delayMilliseconds = Mathf.RoundToInt(intervalSeconds * 1000f);
            while (automaticSavingEnabled)
            {
                await Task.Delay(delayMilliseconds);
                if (this == null || !automaticSavingEnabled)
                {
                    return;
                }

                NetworkManager networkManager = NetworkManager.Singleton;
                if (networkManager != null && networkManager.IsServer)
                {
                    await SaveCurrentCheckpointAsync();
                }
            }
        }

        /// <summary>현재 서버 상태를 수집하여 Cloud Save Game Data 저장을 요청합니다.</summary>
        /// <returns>수집, 변환, Cloud Code 저장이 모두 성공했으면 <see langword="true"/>입니다.</returns>
        public async Task<bool> SaveCurrentCheckpointAsync()
        {
            if (IsSaving ||
                collector == null ||
                config == null ||
                relaySessionGlue == null ||
                string.IsNullOrWhiteSpace(relaySessionGlue.SessionId))
            {
                return false;
            }

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return false;
            }

            if (!collector.TryCollect(out HostMigrationSnapshot snapshot))
            {
                Debug.LogError("Host Migration Snapshot collection failed.", this);
                return false;
            }

            string snapshotJson = serializer.Serialize(snapshot);
            int snapshotBytes = System.Text.Encoding.UTF8.GetByteCount(snapshotJson);
            if (snapshotBytes > config.MaximumSnapshotBytes)
            {
                Debug.LogError($"Checkpoint exceeds configured size limit: {snapshotBytes}/{config.MaximumSnapshotBytes} bytes.", this);
                return false;
            }
            IsSaving = true;
            try
            {
                bool saved = await cloudService.SaveAsync(
                    config,
                    relaySessionGlue.SessionId,
                    snapshot,
                    snapshotJson);

                if (!saved)
                {
                    Debug.LogError(
                        $"Cloud checkpoint save failed: {cloudService.LastErrorMessage}",
                        this);
                    return false;
                }

                EchoZone.Online.OnlineDebugLog.Info(
                    $"Cloud checkpoint saved. RunId: {snapshot.RunId}, Version: {snapshot.SnapshotVersion}",
                    this);
                return true;
            }
            finally
            {
                IsSaving = false;
            }
        }

        /// <summary>Cloud에서 최신 Snapshot을 받아 도메인 데이터로 복원합니다.</summary>
        /// <param name="runId">불러올 게임 실행의 고유 식별자입니다.</param>
        /// <param name="snapshot">검증과 역직렬화가 끝난 Snapshot입니다.</param>
        /// <returns>Cloud 조회와 JSON 변환이 성공했으면 <see langword="true"/>입니다.</returns>
        public async Task<(bool Succeeded, HostMigrationSnapshot Snapshot)>
            LoadLatestCheckpointAsync(string runId)
        {
            if (config == null ||
                relaySessionGlue == null ||
                string.IsNullOrWhiteSpace(relaySessionGlue.SessionId))
            {
                return (false, null);
            }

            CloudCheckpointLoadResponse response =
                await cloudService.LoadLatestAsync(
                    config,
                    relaySessionGlue.SessionId,
                    runId);

            if (response == null ||
                !response.Found ||
                !serializer.TryDeserialize(
                    response.SnapshotJson,
                    out HostMigrationSnapshot snapshot) ||
                snapshot.RunId != runId ||
                snapshot.SnapshotVersion != response.SnapshotVersion)
            {
                return (false, null);
            }

            return (true, snapshot);
        }

        /// <summary>Client가 Cloud 저장 함수를 직접 호출해도 서버 권한 검사에서 거부되는지 시험합니다.</summary>
        /// <returns>Cloud Code 호출과 결과 기록이 끝날 때 완료되는 작업입니다.</returns>
        public async Task TestUnauthorizedClientSaveAsync()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null ||
                !networkManager.IsClient ||
                networkManager.IsHost ||
                config == null ||
                relaySessionGlue == null ||
                string.IsNullOrWhiteSpace(relaySessionGlue.SessionId))
            {
                return;
            }

            HostMigrationSnapshot unauthorizedSnapshot = new(
                "unauthorized-client-test",
                1,
                Array.Empty<HostMigrationPlayerSnapshot>(),
                Array.Empty<WorldItemMigrationSnapshot>());
            string snapshotJson = serializer.Serialize(unauthorizedSnapshot);
            bool saved = await cloudService.SaveAsync(
                config,
                relaySessionGlue.SessionId,
                unauthorizedSnapshot,
                snapshotJson);

            if (saved)
            {
                Debug.LogError(
                    "Cloud authorization test failed: Client save was accepted.",
                    this);
                return;
            }

            EchoZone.Online.OnlineDebugLog.Info(
                $"Cloud authorization test passed: Client save was rejected. {cloudService.LastErrorMessage}",
                this);
        }
    }
}
