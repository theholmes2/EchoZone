using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

namespace EchoZone.Online.Migration
{
    /// <summary>Cloud Code Module 호출만 담당하는 Host Migration Cloud 통신 Brick입니다.</summary>
    public sealed class HostMigrationCloudCheckpointService
    {
        /// <summary>마지막 Cloud Code 호출이 실패한 원인입니다.</summary>
        public string LastErrorMessage { get; private set; } = string.Empty;

        /// <summary>JSON Snapshot을 Cloud Code의 서버 권한 저장 함수로 전달합니다.</summary>
        /// <param name="config">Module과 함수 이름을 가진 접속 규약 데이터입니다.</param>
        /// <param name="sessionId">Cloud Code가 Host 권한을 검사할 Multiplayer Session ID입니다.</param>
        /// <param name="snapshot">저장할 Host Migration Snapshot입니다.</param>
        /// <param name="snapshotJson">Snapshot을 변환한 JSON 문자열입니다.</param>
        /// <returns>Cloud Code가 저장을 승인했으면 <see langword="true"/>입니다.</returns>
        public async Task<bool> SaveAsync(
            HostMigrationCloudConfig config,
            string sessionId,
            HostMigrationSnapshot snapshot,
            string snapshotJson)
        {
            LastErrorMessage = string.Empty;
            if (!HasValidConfig(config) ||
                string.IsNullOrWhiteSpace(sessionId) ||
                snapshot == null ||
                string.IsNullOrWhiteSpace(snapshotJson))
            {
                LastErrorMessage = "Cloud checkpoint save arguments are invalid.";
                return false;
            }

            try
            {
                Dictionary<string, object> arguments = new()
                {
                    ["sessionId"] = sessionId,
                    ["runId"] = snapshot.RunId,
                    ["snapshotVersion"] = snapshot.SnapshotVersion,
                    ["snapshotJson"] = snapshotJson
                };

                CloudCheckpointSaveResponse response =
                    await CloudCodeService.Instance
                        .CallModuleEndpointAsync<CloudCheckpointSaveResponse>(
                            config.ModuleName,
                            config.SaveFunctionName,
                            arguments);

                if (response == null || !response.Saved)
                {
                    LastErrorMessage = response?.Message ??
                        "Cloud Code rejected the checkpoint.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>RunId에 해당하는 최신 JSON Snapshot을 Cloud Code에서 조회합니다.</summary>
        /// <param name="config">Module과 함수 이름을 가진 접속 규약 데이터입니다.</param>
        /// <param name="sessionId">Cloud Code가 Host 권한을 검사할 Multiplayer Session ID입니다.</param>
        /// <param name="runId">조회할 게임 실행의 고유 식별자입니다.</param>
        /// <returns>조회 성공 여부와 JSON을 함께 가진 응답입니다.</returns>
        public async Task<CloudCheckpointLoadResponse> LoadLatestAsync(
            HostMigrationCloudConfig config,
            string sessionId,
            string runId)
        {
            LastErrorMessage = string.Empty;
            if (!HasValidConfig(config) ||
                string.IsNullOrWhiteSpace(sessionId) ||
                string.IsNullOrWhiteSpace(runId))
            {
                LastErrorMessage = "Cloud checkpoint load arguments are invalid.";
                return null;
            }

            try
            {
                Dictionary<string, object> arguments = new()
                {
                    ["sessionId"] = sessionId,
                    ["runId"] = runId
                };

                return await CloudCodeService.Instance
                    .CallModuleEndpointAsync<CloudCheckpointLoadResponse>(
                        config.ModuleName,
                        config.LoadFunctionName,
                        arguments);
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                return null;
            }
        }

        /// <summary>Cloud Code 접속 규약에 빈 값이 없는지 검사합니다.</summary>
        private static bool HasValidConfig(HostMigrationCloudConfig config)
        {
            return config != null &&
                   !string.IsNullOrWhiteSpace(config.ModuleName) &&
                   !string.IsNullOrWhiteSpace(config.SaveFunctionName) &&
                   !string.IsNullOrWhiteSpace(config.LoadFunctionName);
        }
    }

    /// <summary>Cloud Code 저장 함수가 반환하는 최소 결과입니다.</summary>
    [Serializable]
    public sealed class CloudCheckpointSaveResponse
    {
        /// <summary>Saved 값을 저장합니다.</summary>
        public bool Saved;
        /// <summary>SnapshotVersion 값을 저장합니다.</summary>
        public long SnapshotVersion;
        /// <summary>Message 값을 저장합니다.</summary>
        public string Message;
    }

    /// <summary>Cloud Code 조회 함수가 반환하는 최신 Snapshot 전송 결과입니다.</summary>
    [Serializable]
    public sealed class CloudCheckpointLoadResponse
    {
        /// <summary>Found 값을 저장합니다.</summary>
        public bool Found;
        /// <summary>SnapshotVersion 값을 저장합니다.</summary>
        public long SnapshotVersion;
        /// <summary>SnapshotJson 값을 저장합니다.</summary>
        public string SnapshotJson;
    }
}
