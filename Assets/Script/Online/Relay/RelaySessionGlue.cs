using System.Threading.Tasks;
using System.Text;
using EchoZone.Online.Authentication;
using EchoZone.Online.Reconnect;
using EchoZone.Online.Migration;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Online.Relay
{
    /// <summary>Relay Client의 현재 연결 및 복구 진행 상태입니다.</summary>
    public enum RelayClientConnectionState
    {
        Disconnected,
        Connected,
        Reconnecting,
        ReconnectFailed
    }

    /// <summary>
    /// 플레이어 인증, Relay 설정 데이터, Relay Session Brick을 연결하는 Glue입니다.
    /// </summary>
    public sealed class RelaySessionGlue : MonoBehaviour
    {
        [SerializeField] private UnityAuthenticationGlue authenticationGlue;
        [SerializeField] private RelaySessionConfig config;
        [SerializeField] private HostMigrationSnapshotCollector migrationSnapshotCollector;
        [SerializeField] private HostMigrationCloudCheckpointGlue cloudCheckpointGlue;

        /// <summary>Relay Host Session 생성 로직을 담당하는 Brick입니다.</summary>
        private readonly UnityRelaySessionService relaySessionService = new();

        private readonly HostMigrationSnapshotApplier snapshotApplier = new();
        private HostMigrationSessionDataHandler migrationDataHandler;
        private bool explicitReconnectTest;
        private int recoveryGeneration;
        public string RecoveryStatus { get; private set; } = string.Empty;
        private HostMigrationSnapshot pendingMigrationSnapshot;

        /// <summary>현재 실행이 Relay Session에 참가한 Client인지 나타냅니다.</summary>
        private bool joinedAsClient;
        private bool useInvalidTicketForTest;

        /// <summary>연결 당시 NGO가 이 Client에 발급한 임시 Client ID입니다.</summary>
        private ulong connectedLocalClientId;

        /// <summary>현재 Host Session 생성 요청이 진행 중인지 나타냅니다.</summary>
        public bool IsCreatingHostSession { get; private set; }

        /// <summary>현재 Client Session 참가 요청이 진행 중인지 나타냅니다.</summary>
        public bool IsJoiningSession { get; private set; }

        /// <summary>재접속 반복 작업이 실행 중인지 나타냅니다.</summary>
        public bool IsReconnecting =>
            ClientConnectionState == RelayClientConnectionState.Reconnecting;

        /// <summary>Session Host 교체와 상태 복원이 진행 중인지 나타냅니다.</summary>
        public bool IsMigratingHost { get; private set; }

        /// <summary>복구 직후 서버 피해 차단 시간이 진행 중인지 나타냅니다.</summary>
        public bool IsPostMigrationProtected { get; private set; }

        /// <summary>Relay Client의 현재 연결 상태입니다.</summary>
        public RelayClientConnectionState ClientConnectionState { get; private set; } =
            RelayClientConnectionState.Disconnected;

        /// <summary>생성된 Session에 참가할 때 사용할 Join Code입니다.</summary>
        public string JoinCode => relaySessionService.JoinCode;

        /// <summary>Cloud Code가 Lobby 권한을 검사할 때 사용할 현재 Session ID입니다.</summary>
        public string SessionId => relaySessionService.SessionId;

        /// <summary>NGO의 로컬 Client 단절 알림을 구독합니다.</summary>
        private void Start()
        {
            migrationDataHandler = new HostMigrationSessionDataHandler(
                migrationSnapshotCollector,
                HandleMigrationDataReceived);
            relaySessionService.ConfigureHostMigration(
                migrationDataHandler,
                config);
            relaySessionService.SessionHostChanged += HandleSessionHostChanged;
            relaySessionService.SessionMigrated += HandleSessionMigrated;
            relaySessionService.MigrationFailed += HandleMigrationFailed;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback +=
                    HandleClientDisconnected;
            }
        }

        /// <summary>오브젝트가 해제될 때 NGO 단절 알림 구독을 제거합니다.</summary>
        private void OnDestroy()
        {
            recoveryGeneration++;
            relaySessionService.MigrationFailed -= HandleMigrationFailed;
            relaySessionService.SessionHostChanged -= HandleSessionHostChanged;
            relaySessionService.SessionMigrated -= HandleSessionMigrated;

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -=
                    HandleClientDisconnected;
            }
        }

        /// <summary>MPS가 새 Host를 확정하면 모든 참가자의 로딩 상태를 시작합니다.</summary>
        private void HandleSessionHostChanged(string newHostPlayerId)
        {
            recoveryGeneration++;
            IsMigratingHost = true;
            RecoveryStatus = "Host changed. Restoring the session...";
            ClientConnectionState = RelayClientConnectionState.Reconnecting;
            Debug.Log(
                $"Session Host changed. New Host PlayerId: {ShortenPlayerId(newHostPlayerId)}",
                this);
            _ = WatchMigrationAsync(recoveryGeneration);
        }

        private async Task WatchMigrationAsync(int generation)
        {
            await Task.Delay(Mathf.RoundToInt(config.SessionRecoveryTimeoutSeconds * 1000f));
            if (this != null && generation == recoveryGeneration && IsMigratingHost)
                HandleMigrationFailed("Migration completion timed out.");
        }

        private void HandleMigrationFailed(string reason)
        {
            recoveryGeneration++;
            IsMigratingHost = false;
            ClientConnectionState = RelayClientConnectionState.ReconnectFailed;
            RecoveryStatus = reason;
            Debug.LogError($"Host migration failed: {reason}", this);
        }

        /// <summary>MPS가 전달한 Session Snapshot을 새 Host 시작 전 복원 준비 상태로 보관합니다.</summary>
        private void HandleMigrationDataReceived(HostMigrationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            pendingMigrationSnapshot = snapshot;
            migrationSnapshotCollector?.TryRestoreRun(
                snapshot.RunId,
                snapshot.SnapshotVersion);
            NetworkPlayerSessionCacheGlue.Instance?.PrepareHostMigration(
                snapshot.Players);
        }

        /// <summary>새 Relay 네트워크가 시작되면 새 Host는 Cloud 최신본을 선택해 적용합니다.</summary>
        private async void HandleSessionMigrated()
        {
            int generation = recoveryGeneration;
            try
            {
                if (relaySessionService.IsHost)
                {
                    HostMigrationSnapshot snapshot = pendingMigrationSnapshot;
                    string runId = snapshot?.RunId;
                    if (string.IsNullOrWhiteSpace(runId))
                    {
                        runId = relaySessionService.RunId;
                    }

                    if (!string.IsNullOrWhiteSpace(runId) && cloudCheckpointGlue != null)
                    {
                        (bool succeeded, HostMigrationSnapshot cloudSnapshot) =
                            await cloudCheckpointGlue.LoadLatestCheckpointAsync(
                                runId);
                        if (this == null || generation != recoveryGeneration) return;
                        if (succeeded &&
                            cloudSnapshot != null &&
                            (snapshot == null ||
                             cloudSnapshot.SnapshotVersion >= snapshot.SnapshotVersion))
                        {
                            snapshot = cloudSnapshot;
                        }
                    }

                    if (snapshot == null || !snapshotApplier.Apply(snapshot))
                    {
                        HandleMigrationFailed("No applicable Snapshot.");
                        return;
                    }

                    migrationSnapshotCollector?.TryRestoreRun(
                        snapshot.RunId,
                        snapshot.SnapshotVersion);
                    Debug.Log(
                        $"Host migration Snapshot applied. RunId: {snapshot.RunId}, Version: {snapshot.SnapshotVersion}",
                        this);
                    _ = RunPostMigrationProtectionAsync();
                }

                ClientConnectionState = RelayClientConnectionState.Connected;
                IsMigratingHost = false;
                recoveryGeneration++;
                useInvalidTicketForTest = false;
                joinedAsClient = !relaySessionService.IsHost;
                connectedLocalClientId = NetworkManager.Singleton.LocalClientId;
                RecoveryStatus = string.Empty;
            }
            catch (System.Exception exception)
            {
                HandleMigrationFailed(exception.Message);
            }
        }

        /// <summary>설정 시간 동안 현재와 새로 접속한 서버 PlayerStats의 피해를 차단합니다.</summary>
        private async Task RunPostMigrationProtectionAsync()
        {
            float duration = config != null
                ? config.PostMigrationInvulnerabilitySeconds
                : 0f;
            if (duration <= 0f)
            {
                return;
            }

            IsPostMigrationProtected = true;
            SetServerPlayerDamageBlocked(true);
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback +=
                    HandleProtectedClientConnected;
            }

            await Task.Delay(Mathf.RoundToInt(duration * 1000f));

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -=
                    HandleProtectedClientConnected;
            }

            SetServerPlayerDamageBlocked(false);
            IsPostMigrationProtected = false;
            Debug.Log("Post-migration invulnerability ended.", this);
        }

        private async void HandleProtectedClientConnected(ulong clientId)
        {
            await Task.Yield();
            SetServerPlayerDamageBlocked(true);
        }

        private static void SetServerPlayerDamageBlocked(bool blocked)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            PlayerStats[] playerStats = FindObjectsByType<PlayerStats>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < playerStats.Length; i++)
            {
                playerStats[i].SetDamageBlocked(blocked);
            }
        }

        private static string ShortenPlayerId(string playerId)
        {
            const int visibleCharacters = 8;
            return string.IsNullOrEmpty(playerId) || playerId.Length <= visibleCharacters
                ? playerId
                : playerId.Substring(0, visibleCharacters);
        }

        /// <summary>
        /// 인증을 보장한 뒤 설정 데이터를 Relay Brick에 전달하여 Host Session을 생성합니다.
        /// </summary>
        /// <returns>Relay Host Session이 준비되었으면 <see langword="true"/>입니다.</returns>
        public async Task<bool> CreateHostSessionAsync()
        {
            if (IsCreatingHostSession || IsJoiningSession || IsReconnecting || IsMigratingHost ||
                (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient))
            {
                return false;
            }

            if (authenticationGlue == null || config == null)
            {
                Debug.LogError(
                    "RelaySessionGlue references are missing.",
                    this);
                return false;
            }

            IsCreatingHostSession = true;

            try
            {
                bool authenticated = await authenticationGlue
                    .AuthenticationService
                    .InitializeAndSignInAsync();

                if (!authenticated)
                {
                    Debug.LogError(
                        $"Unity Authentication failed: {authenticationGlue.AuthenticationService.LastErrorMessage}",
                        this);
                    return false;
                }

                if (!await PrepareManualConnectionAsync())
                {
                    return false;
                }
                ReconnectTicketMemoryStore.Reset();
                ConfigureConnectionIdentity();

                bool created =
                    await relaySessionService.CreateHostSessionAsync(config);

                if (!created || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
                {
                    Debug.LogError(
                        $"Relay Session creation failed: {relaySessionService.LastErrorMessage}",
                        this);
                    return false;
                }

                Debug.Log(
                    $"Relay Host Session created. Join Code: {relaySessionService.JoinCode}",
                    this);
                if (migrationSnapshotCollector != null)
                {
                    string runId = migrationSnapshotCollector.StartNewRun();
                    Debug.Log($"New Run started. RunId: {runId}", this);
                    if (!await relaySessionService.PublishRunIdAsync(runId))
                    {
                        Debug.LogError(
                            $"Run ID publish failed: {relaySessionService.LastErrorMessage}",
                            this);
                        return false;
                    }
                }

                joinedAsClient = false;
                cloudCheckpointGlue?.StartAutomaticSaving(
                    config.MigrationSnapshotIntervalSeconds);
                return true;
            }
            finally
            {
                IsCreatingHostSession = false;
            }
        }

        /// <summary>
        /// 인증을 보장한 뒤 Join Code를 Relay Brick에 전달하여 기존 Session에 참가합니다.
        /// </summary>
        /// <param name="joinCode">Host 화면에 표시된 Session Join Code입니다.</param>
        /// <returns>Relay Session 참가와 NGO Client 시작이 완료되었으면 <see langword="true"/>입니다.</returns>
        public async Task<bool> JoinSessionAsync(string joinCode)
        {
            if (IsCreatingHostSession || IsJoiningSession || IsReconnecting || IsMigratingHost ||
                (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient))
            {
                return false;
            }

            if (authenticationGlue == null || config == null || string.IsNullOrWhiteSpace(joinCode))
            {
                Debug.LogError(
                    "UnityAuthenticationGlue reference is missing.",
                    this);
                return false;
            }

            IsJoiningSession = true;

            try
            {
                bool authenticated = await authenticationGlue
                    .AuthenticationService
                    .InitializeAndSignInAsync();

                if (!authenticated)
                {
                    Debug.LogError(
                        $"Unity Authentication failed: {authenticationGlue.AuthenticationService.LastErrorMessage}",
                        this);
                    return false;
                }

                if (!await PrepareManualConnectionAsync())
                {
                    return false;
                }
                ReconnectTicketMemoryStore.PrepareForSession(joinCode);
                ConfigureConnectionIdentity();

                bool joined =
                    await relaySessionService.JoinSessionAsync(joinCode);

                if (!joined || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsConnectedClient)
                {
                    ClientConnectionState = RelayClientConnectionState.ReconnectFailed;
                    Debug.LogError(
                        $"Relay Session join failed: {relaySessionService.LastErrorMessage}",
                        this);
                    return false;
                }

                Debug.Log(
                    $"Relay Session joined. Session ID: {relaySessionService.SessionId}",
                    this);
                joinedAsClient = true;
                connectedLocalClientId = NetworkManager.Singleton.LocalClientId;
                ClientConnectionState = RelayClientConnectionState.Connected;
                cloudCheckpointGlue?.StartAutomaticSaving(
                    config != null ? config.MigrationSnapshotIntervalSeconds : 5f);
                return true;
            }
            finally
            {
                IsJoiningSession = false;
            }
        }

        /// <summary>
        /// 로컬 Relay Client의 예기치 않은 단절을 감지하면 자동 복구를 시작합니다.
        /// Host에서 다른 Client가 나간 경우에는 실행하지 않습니다.
        /// </summary>
        /// <param name="clientId">연결이 끊어진 NGO Client ID입니다.</param>
        private void HandleClientDisconnected(ulong clientId)
        {
            if (!joinedAsClient ||
                IsCreatingHostSession || IsJoiningSession || IsMigratingHost ||
                clientId != connectedLocalClientId ||
                IsReconnecting)
            {
                return;
            }

            ReconnectTicketMemoryStore.MarkDisconnected(
                Time.realtimeSinceStartupAsDouble,
                config != null ? config.ReconnectStateCacheSeconds : 0f);
            if (explicitReconnectTest)
            {
                explicitReconnectTest = false;
                _ = ReconnectClientAsync();
            }
            else
            {
                _ = RecoverSessionAsync();
            }
        }

        /// <summary>불명확한 단절에서 Session을 떠나지 않고 Unity의 선출 알림을 기다립니다.</summary>
        private async Task RecoverSessionAsync()
        {
            if (config == null)
            {
                HandleMigrationFailed("RelaySessionConfig is missing.");
                return;
            }
            int generation = ++recoveryGeneration;
            ClientConnectionState = RelayClientConnectionState.Reconnecting;
            double deadline = Time.realtimeSinceStartupAsDouble + config.SessionRecoveryTimeoutSeconds;
            double graceDeadline = Time.realtimeSinceStartupAsDouble + config.HostMigrationGraceSeconds;
            RecoveryStatus = "Connection lost. Keeping session membership...";
            Debug.Log("Session recovery started. Waiting for Unity host election; not leaving Lobby.", this);
            while (this != null && generation == recoveryGeneration)
            {
                await Task.Delay(Mathf.RoundToInt(config.SessionRecoveryPollSeconds * 1000f));
                if (this == null || generation != recoveryGeneration) return;
                bool cloudAvailable = await relaySessionService.RefreshSessionForRecoveryAsync();
                if (this == null || generation != recoveryGeneration) return;

                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.IsConnectedClient)
                {
                    connectedLocalClientId = manager.LocalClientId;
                    ClientConnectionState = RelayClientConnectionState.Connected;
                    RecoveryStatus = string.Empty;
                    Debug.Log("Session connection recovered without leaving Lobby.", this);
                    return;
                }

                RecoveryStatus = cloudAvailable
                    ? (Time.realtimeSinceStartupAsDouble < graceDeadline
                        ? "Cloud reachable. Waiting for connection recovery..."
                        : "Grace elapsed. Waiting for Unity host election (Dashboard controlled)...")
                    : "Cloud unavailable. Waiting for connectivity...";
                Debug.Log($"Session recovery: {RecoveryStatus} {relaySessionService.LastErrorMessage}", this);

                // NGO만 재시도한다. Session 핸들과 호스트 변경 구독은 유지한다.
                // 기존 Relay allocation이 만료됐으면 이 시도는 실패하고 선출 대기를 계속한다.
                if (cloudAvailable && manager != null && !manager.IsListening &&
                    !manager.ShutdownInProgress && !relaySessionService.IsHost)
                {
                    ConfigureConnectionIdentity();
                    manager.StartClient();
                }

                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    joinedAsClient = false;
                    ClientConnectionState = RelayClientConnectionState.ReconnectFailed;
                    if (manager != null) manager.Shutdown();
                    RecoveryStatus = "Session recovery timed out; check Lobby settings and connectivity.";
                    Debug.LogError(RecoveryStatus, this);
                    return;
                }
            }
        }

        /// <summary>
        /// 설정된 횟수와 간격에 따라 마지막 Relay Session으로 재접속을 시도합니다.
        /// </summary>
        /// <returns>재접속 반복 작업의 완료를 나타내는 Task입니다.</returns>
        private async Task ReconnectClientAsync()
        {
            if (config == null)
            {
                ClientConnectionState = RelayClientConnectionState.ReconnectFailed;
                Debug.LogError("RelaySessionConfig reference is missing.", this);
                return;
            }

            ClientConnectionState = RelayClientConnectionState.Reconnecting;

            for (int attempt = 1; attempt <= config.ReconnectMaxAttempts; attempt++)
            {
                int delayMilliseconds = Mathf.RoundToInt(
                    config.ReconnectRetryDelaySeconds * 1000f);
                await Task.Delay(delayMilliseconds);

                if (this == null)
                {
                    return;
                }

                if (IsMigratingHost)
                {
                    useInvalidTicketForTest = false;
                    return;
                }

                ConfigureConnectionIdentity();

                bool reconnected =
                    await relaySessionService.ReconnectSessionAsync();

                if (reconnected && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
                {
                    useInvalidTicketForTest = false;
                    connectedLocalClientId = NetworkManager.Singleton.LocalClientId;
                    ClientConnectionState = RelayClientConnectionState.Connected;
                    Debug.Log(
                        $"Relay Session reconnected on attempt {attempt}. Session ID: {relaySessionService.SessionId}",
                        this);
                    return;
                }

                Debug.LogWarning(
                    $"Relay reconnect attempt {attempt} failed: {relaySessionService.LastErrorMessage}",
                    this);
            }

            useInvalidTicketForTest = false;
            joinedAsClient = false;
            ClientConnectionState = RelayClientConnectionState.ReconnectFailed;
            Debug.LogError("Relay Session reconnect attempts exhausted.", this);
        }

        /// <summary>
        /// 재접속 흐름을 검증하기 위해 현재 Client의 NGO 연결을 강제로 종료합니다.
        /// Host에서는 실행하지 않습니다.
        /// </summary>
        public void ForceClientDisconnectForTest()
        {
            NetworkManager networkManager = NetworkManager.Singleton;

            if (!joinedAsClient ||
                networkManager == null ||
                !networkManager.IsClient ||
                networkManager.IsHost ||
                IsReconnecting)
            {
                return;
            }

            explicitReconnectTest = true;
            networkManager.Shutdown();
        }

        /// <summary>서버가 잘못된 재접속 티켓을 거부하는지 검증한 뒤 Client 연결을 종료합니다.</summary>
        public void ForceClientDisconnectWithInvalidTicketForTest()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (!joinedAsClient || IsReconnecting || IsMigratingHost ||
                networkManager == null || !networkManager.IsConnectedClient || networkManager.IsHost)
            {
                return;
            }

            // 실제 티켓을 훼손하지 않고 테스트 재시도 payload만 바꿉니다.
            useInvalidTicketForTest = true;
            ForceClientDisconnectForTest();
        }

        /// <summary>현재 Host가 정상적으로 Session을 떠나도록 요청해 마이그레이션을 시험합니다.</summary>
        public async Task ForceHostExitForMigrationTestAsync()
        {
            if (!relaySessionService.IsHost)
            {
                return;
            }

            bool left = await relaySessionService.LeaveHostForMigrationTestAsync();
            if (!left)
            {
                Debug.LogError(
                    $"Host migration test exit failed: {relaySessionService.LastErrorMessage}",
                    this);
            }
        }

        /// <summary>인증 PlayerId를 NGO 연결 승인 Payload에 기록합니다.</summary>
        private void ConfigureConnectionIdentity()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || authenticationGlue == null)
            {
                return;
            }

            ReconnectCredential credential = new(
                authenticationGlue.AuthenticationService.PlayerId,
                useInvalidTicketForTest
                    ? "invalid-reconnect-ticket"
                    : ReconnectTicketMemoryStore.GetTicket(Time.realtimeSinceStartupAsDouble));

            networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(
                credential.Serialize());
        }

        /// <summary>수동 참가/생성 전에 이전 Session과 NGO 연결을 순서대로 정리합니다.</summary>
        private async Task<bool> PrepareManualConnectionAsync()
        {
            recoveryGeneration++;
            explicitReconnectTest = false;
            IsMigratingHost = false;
            RecoveryStatus = string.Empty;
            joinedAsClient = false;
            useInvalidTicketForTest = false;
            ClientConnectionState = RelayClientConnectionState.Disconnected;
            if (!await relaySessionService.LeaveCurrentSessionAsync())
            {
                Debug.LogError(relaySessionService.LastErrorMessage, this);
                return false;
            }

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || config == null)
            {
                return false;
            }
            if (manager.IsListening || manager.IsClient || manager.IsServer)
            {
                manager.Shutdown();
            }

            double deadline = Time.realtimeSinceStartupAsDouble + config.ConnectionShutdownTimeoutSeconds;
            while (manager != null && manager.ShutdownInProgress)
            {
                if (this == null || Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    return false;
                }
                await Task.Yield();
            }
            return this != null && manager != null;
        }
    }
}
