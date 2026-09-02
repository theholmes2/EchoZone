using System;
using System.Threading.Tasks;
using EchoZone.Online.Migration;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Multiplayer;

namespace EchoZone.Online.Relay
{
    /// <summary>
    /// Multiplayer Services를 통해 Relay 기반 Session을 생성하거나 참가하는 Brick입니다.
    /// 인증 실행 시점과 UI 표시는 담당하지 않습니다.
    /// </summary>
    public sealed class UnityRelaySessionService
    {
        private const string RunIdPropertyKey = "echozone_run_id";
        /// <summary>현재 플레이어가 생성하거나 참가한 Session입니다.</summary>
        private ISession activeSession;

        /// <summary>연결이 끊긴 뒤에도 재접속에 사용할 마지막 Session ID입니다.</summary>
        private string lastSessionId = string.Empty;

        /// <summary>각 로컬 Session handle에 설치할 NGO Snapshot 변환기입니다.</summary>
        private HostMigrationSessionDataHandler migrationDataHandler;

        /// <summary>마이그레이션 데이터 업로드 간격입니다.</summary>
        private TimeSpan migrationUploadInterval = TimeSpan.FromSeconds(5);

        /// <summary>마이그레이션 데이터 처리 제한 시간입니다.</summary>
        private TimeSpan migrationDataTimeout = TimeSpan.FromSeconds(5);

        public event Action<string> SessionHostChanged;
        public event Action SessionMigrated;
        public event Action<string> MigrationFailed;

        /// <summary>로비를 떠나지 않고 서버 상태와 이벤트 구독 연결을 갱신합니다.</summary>
        public async Task<bool> RefreshSessionForRecoveryAsync()
        {
            try
            {
                if (activeSession == null)
                    throw new InvalidOperationException("No Session available for recovery.");
                await activeSession.RefreshAsync();
                await activeSession.ReconnectAsync();
                LastErrorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>마지막 Session 요청에서 발생한 오류 메시지입니다.</summary>
        public string LastErrorMessage { get; private set; } = string.Empty;

        /// <summary>현재 생성하거나 참가한 Relay Session이 있는지 나타냅니다.</summary>
        public bool HasActiveSession => activeSession != null;

        /// <summary>현재 인증 플레이어가 Session이 확정한 Host인지 나타냅니다.</summary>
        public bool IsHost => activeSession != null && activeSession.IsHost;

        /// <summary>현재 Session의 고유 ID입니다.</summary>
        public string SessionId =>
            activeSession != null ? activeSession.Id : string.Empty;

        /// <summary>다른 플레이어가 참가할 때 사용할 짧은 Join Code입니다.</summary>
        public string JoinCode =>
            activeSession != null ? activeSession.Code : string.Empty;

        /// <summary>동일 Session의 모든 참가자가 읽을 수 있는 현재 게임 Run ID입니다.</summary>
        public string RunId =>
            activeSession != null &&
            activeSession.Properties.TryGetValue(
                RunIdPropertyKey,
                out SessionProperty property)
                ? property.Value
                : string.Empty;

        /// <summary>최초 Host가 Cloud Snapshot 조회 키인 Run ID를 Session에 게시합니다.</summary>
        public async Task<bool> PublishRunIdAsync(string runId)
        {
            if (activeSession == null ||
                !activeSession.IsHost ||
                string.IsNullOrWhiteSpace(runId))
            {
                return false;
            }

            try
            {
                IHostSession hostSession = activeSession.AsHost();
                hostSession.SetProperty(
                    RunIdPropertyKey,
                    new SessionProperty(
                        runId,
                        VisibilityPropertyOptions.Member));
                await hostSession.SavePropertiesAsync();
                return true;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>Host와 참가자 모두에게 동일한 Session 마이그레이션 규약을 설치합니다.</summary>
        public void ConfigureHostMigration(
            HostMigrationSessionDataHandler dataHandler,
            RelaySessionConfig config)
        {
            migrationDataHandler = dataHandler;
            if (config == null)
            {
                return;
            }

            migrationUploadInterval = TimeSpan.FromSeconds(
                config.MigrationSnapshotIntervalSeconds);
            migrationDataTimeout = TimeSpan.FromSeconds(
                config.MigrationDataTimeoutSeconds);
        }

        /// <summary>
        /// 전달받은 설정으로 Relay 네트워크를 사용하는 Host Session을 생성합니다.
        /// </summary>
        /// <param name="config">최대 참가 인원을 제공하는 Session 설정 데이터입니다.</param>
        /// <returns>Host Session 생성과 NGO Host 시작이 완료되었으면 <see langword="true"/>입니다.</returns>
        public async Task<bool> CreateHostSessionAsync(
            RelaySessionConfig config)
        {
            LastErrorMessage = string.Empty;

            if (config == null)
            {
                LastErrorMessage = "RelaySessionConfig is missing.";
                return false;
            }

            if (activeSession != null)
            {
                LastErrorMessage = "Leave the previous Session before creating a new one.";
                return false;
            }

            try
            {
                SessionOptions options = new SessionOptions
                {
                    MaxPlayers = config.MaxPlayers
                }.WithRelayNetwork();

                if (migrationDataHandler != null)
                {
                    options.WithHostMigration(
                        migrationDataHandler,
                        migrationUploadInterval,
                        migrationDataTimeout);
                }

                ReplaceActiveSession(
                    await MultiplayerService.Instance.CreateSessionAsync(options));

                lastSessionId = activeSession != null
                    ? activeSession.Id
                    : string.Empty;

                return activeSession != null;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Join Code로 기존 Relay Session에 참가하고 NGO Client 연결을 시작합니다.
        /// </summary>
        /// <param name="joinCode">Host가 전달한 Session Join Code입니다.</param>
        /// <returns>Session 참가와 NGO Client 연결이 완료되었으면 <see langword="true"/>입니다.</returns>
        public async Task<bool> JoinSessionAsync(string joinCode)
        {
            LastErrorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                LastErrorMessage = "Join Code is missing.";
                return false;
            }

            if (activeSession != null)
            {
                LastErrorMessage = "Leave the previous Session before joining again.";
                return false;
            }

            try
            {
                string normalizedJoinCode =
                    joinCode.Trim().ToUpperInvariant();

                await LeaveMatchingMembershipAsync(normalizedJoinCode);

                ReplaceActiveSession(await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(
                        normalizedJoinCode,
                        CreateJoinOptions()));

                lastSessionId = activeSession != null
                    ? activeSession.Id
                    : string.Empty;

                return activeSession != null;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 수동 입장 대상과 코드가 일치하는 로비에서 본인의 남은 참가 기록만 정리합니다.
        /// 티켓은 건드리지 않으며 다른 방과 다른 참가자는 퇴장시키지 않습니다.
        /// 조회/퇴장 실패 시 예외를 전달하여 신규 Join을 진행하지 않습니다.
        /// </summary>
        private static async Task LeaveMatchingMembershipAsync(string normalizedJoinCode)
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            if (string.IsNullOrWhiteSpace(playerId))
                throw new InvalidOperationException("Authenticated player ID is missing.");

            var lobbyIds = await LobbyService.Instance.GetJoinedLobbiesAsync();
            foreach (string lobbyId in lobbyIds)
            {
                Unity.Services.Lobbies.Models.Lobby lobby;
                try
                {
                    lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);
                }
                catch (LobbyServiceException exception)
                    when (exception.Reason == LobbyExceptionReason.LobbyNotFound)
                {
                    // 목록 조회 직후 삭제된 방은 정리 대상이 아닙니다.
                    continue;
                }

                if (!string.Equals(lobby.LobbyCode, normalizedJoinCode,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                await LobbyService.Instance.RemovePlayerAsync(lobby.Id, playerId);
                return;
            }
        }

        /// <summary>
        /// 마지막 Session ID를 사용해 끊긴 Lobby를 재접속하거나,
        /// Lobby가 유지된 상태라면 기존 참가 상태를 정리한 뒤 NGO Client를 다시 연결합니다.
        /// </summary>
        /// <returns>같은 Session 참가와 NGO Client 재시작이 완료되었으면 <see langword="true"/>입니다.</returns>
        public async Task<bool> ReconnectSessionAsync()
        {
            LastErrorMessage = string.Empty;

            if (string.IsNullOrEmpty(lastSessionId))
            {
                LastErrorMessage = "Previous Session ID is missing.";
                return false;
            }

            try
            {
                if (!await LeaveCurrentSessionAsync())
                {
                    return false;
                }

                ReplaceActiveSession(await MultiplayerService.Instance
                    .JoinSessionByIdAsync(lastSessionId, CreateJoinOptions()));

                return activeSession != null;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>남아 있는 SDK Session을 정상 정리합니다. 정리 실패를 참가 성공으로 숨기지 않습니다.</summary>
        public async Task<bool> LeaveCurrentSessionAsync()
        {
            if (activeSession == null)
            {
                return true;
            }

            try
            {
                if (activeSession.IsMember && activeSession.State != SessionState.Deleted)
                {
                    await activeSession.LeaveAsync();
                }
                ReplaceActiveSession(null);
                return true;
            }
            catch (Exception exception)
            {
                LastErrorMessage = $"Previous Session cleanup failed: {exception.Message}";
                return false;
            }
        }

        /// <summary>개발 테스트에서 현재 Host가 Session을 떠나 Host 선출을 발생시킵니다.</summary>
        public async Task<bool> LeaveHostForMigrationTestAsync()
        {
            if (activeSession == null || !activeSession.IsHost)
            {
                return false;
            }

            try
            {
                await activeSession.LeaveAsync();
                ReplaceActiveSession(null);
                return true;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                return false;
            }
        }

        /// <summary>일반 참가에도 동일한 Host Migration 데이터 핸들러를 설치합니다.</summary>
        private JoinSessionOptions CreateJoinOptions()
        {
            JoinSessionOptions options = new();
            if (migrationDataHandler != null)
            {
                options.WithHostMigration(
                    migrationDataHandler,
                    migrationUploadInterval,
                    migrationDataTimeout);
            }

            return options;
        }

        private void ReplaceActiveSession(ISession nextSession)
        {
            if (activeSession != null)
            {
                activeSession.SessionHostChanged -= HandleSessionHostChanged;
                activeSession.SessionMigrated -= HandleSessionMigrated;
                if (activeSession.Network != null)
                    activeSession.Network.MigrationFailed -= HandleMigrationFailed;
            }

            activeSession = nextSession;
            if (activeSession != null)
            {
                activeSession.SessionHostChanged += HandleSessionHostChanged;
                activeSession.SessionMigrated += HandleSessionMigrated;
                if (activeSession.Network != null)
                    activeSession.Network.MigrationFailed += HandleMigrationFailed;
            }
        }

        private void HandleSessionHostChanged(string newHostPlayerId)
        {
            SessionHostChanged?.Invoke(newHostPlayerId);
        }

        private void HandleSessionMigrated()
        {
            SessionMigrated?.Invoke();
        }

        private void HandleMigrationFailed(SessionError error)
        {
            LastErrorMessage = error.ToString();
            MigrationFailed?.Invoke(LastErrorMessage);
        }
    }
}
