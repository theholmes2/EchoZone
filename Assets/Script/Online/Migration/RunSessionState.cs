using System;

namespace EchoZone.Online.Migration
{
    /// <summary>현재 게임 Run의 고유 ID와 마지막 발급 Snapshot 버전을 관리하는 순수 런타임 Brick입니다.</summary>
    public sealed class RunSessionState
    {
        /// <summary>현재 진행 중인 Run의 고유 식별자입니다.</summary>
        public string RunId { get; private set; } = string.Empty;

        /// <summary>마지막으로 발급한 Snapshot 버전입니다.</summary>
        public long CurrentSnapshotVersion { get; private set; }

        /// <summary>현재 수집 가능한 Run이 시작되었는지 나타냅니다.</summary>
        public bool HasActiveRun => !string.IsNullOrEmpty(RunId);

        /// <summary>새 고유 ID와 0번 버전으로 새로운 Run을 시작합니다.</summary>
        /// <returns>새로 생성한 RunId입니다.</returns>
        public string StartNewRun()
        {
            RunId = Guid.NewGuid().ToString("N");
            CurrentSnapshotVersion = 0;
            return RunId;
        }

        /// <summary>Host Migration으로 전달받은 기존 Run 식별자와 버전을 이어받습니다.</summary>
        /// <param name="runId">복원할 기존 Run의 고유 식별자입니다.</param>
        /// <param name="snapshotVersion">복원한 마지막 Snapshot 버전입니다.</param>
        /// <returns>전달값이 유효하여 기존 Run을 적용했으면 <see langword="true"/>입니다.</returns>
        public bool TryRestoreRun(string runId, long snapshotVersion)
        {
            if (string.IsNullOrWhiteSpace(runId) || snapshotVersion < 0)
            {
                return false;
            }

            RunId = runId;
            CurrentSnapshotVersion = snapshotVersion;
            return true;
        }

        /// <summary>다음 Snapshot을 최신 상태로 구분할 증가 버전을 발급합니다.</summary>
        /// <returns>1 증가한 새 Snapshot 버전이며 Run이 없으면 0입니다.</returns>
        public long IssueNextSnapshotVersion()
        {
            if (!HasActiveRun)
            {
                return 0;
            }

            CurrentSnapshotVersion++;
            return CurrentSnapshotVersion;
        }
    }
}
