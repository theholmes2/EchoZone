using System;
using System.Threading.Tasks;
using EchoZone.Heist;
using EchoZone.Online.Loading;
using EchoZone.Online.Migration;
using EchoZone.Online.Reconnect;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Online.Relay
{
    public sealed partial class RelaySessionGlue
    {
        private LoadingOverlayGlue loading;
        private long loadingId;
        private int onlineGeneration;
        private bool acceptSessionEvents;
        private bool migrationRestorePending;
        private bool returningToTitle;
        private bool localTitle;
        private Task leaveRequest;
        private Task admissionTask;
        /// <summary>타이틀 복귀 후 이전 UI 콜백을 거절할 온라인 작업 세대입니다.</summary>
        public int OnlineGeneration => onlineGeneration;
        /// <summary>늦은 연결 콜백보다 우선하는 명시적 타이틀 복귀 상태입니다.</summary>
        public bool IsAtLocalTitle => localTitle;

        private void Awake() => loading = GetComponent<LoadingOverlayGlue>();
        private bool IsOnlineCurrent(int generation) => this != null && generation == onlineGeneration && !localTitle;

        private void InvalidateOnlineFlow()
        {
            onlineGeneration++; recoveryGeneration++;
            acceptSessionEvents = false;
            IsMigratingHost = false;
            ClientConnectionState = RelayClientConnectionState.ReconnectFailed;
            relaySessionService.InvalidatePendingRequests();
        }

        /// <summary>SDK가 타이틀 복귀 뒤 늦게 NGO를 시작해도 로컬 게임 화면으로 되돌아가지 않게 합니다.</summary>
        public void ManualLoadingUpdate()
        {
            if (!localTitle) return;
            var manager = NetworkManager.Singleton;
            if (manager != null && !manager.ShutdownInProgress && (manager.IsListening || manager.IsClient || manager.IsServer)) manager.Shutdown();
        }

        private async Task<bool> RunAdmissionAsync(bool create, string code)
        {
            if (!HasAuthenticatedAccount() || returningToTitle || admissionTask != null && !admissionTask.IsCompleted ||
                loading != null && !loading.CanStartOperation || IsReconnecting || IsMigratingHost) return false;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient) return false;
            leaveRequest = null;
            localTitle = false; acceptSessionEvents = true;
            int operation = ++onlineGeneration;
            long id = loading != null ? loading.Begin(create ? LoadingOperationKind.Create : LoadingOperationKind.Join,
                InvalidateOnlineFlow, ReturnToTitleAsync,
                () => RetryAdmissionAsync(create, code)) : 0;
            loadingId = id;
            var task = ExecuteAdmissionAsync(create, code, operation, id);
            admissionTask = task; loading?.Track(task);
            return await task;
        }

        private async Task RetryAdmissionAsync(bool create, string code)
        {
            if (!create && relaySessionService.HasActiveSession && !relaySessionService.IsHost)
            {
                // Session 참가까지 성공했다면 멤버십과 티켓을 유지하고 기존 NGO 복구 진입점을 사용합니다.
                StartRecoveryLoading(false);
                return;
            }
            await ReturnToTitleAsync();
            if (this != null) await RunAdmissionAsync(create, code);
        }

        private async Task<bool> ExecuteAdmissionAsync(bool create, string code, int operation, long id)
        {
            try
            {
                bool result = create ? await CreateHostSessionCoreAsync(operation) : await JoinSessionCoreAsync(code, operation);
                if (!IsOnlineCurrent(operation)) return false;
                if (!result)
                {
                    string reason = !string.IsNullOrEmpty(RecoveryStatus) ? RecoveryStatus : relaySessionService.LastErrorMessage;
                    FailLoading(string.IsNullOrEmpty(reason) ? "입장 준비 실패 · 설정과 연결 상태를 확인하세요." : reason);
                    return false;
                }
                return await FinishLoadingWhenReadyAsync(operation, id);
            }
            catch (Exception)
            {
                if (IsOnlineCurrent(operation)) FailLoading("온라인 입장 처리 중 오류가 발생했습니다. 현재 단계의 연결을 확인하세요.");
                return false;
            }
            finally { if (this != null && localTitle) ManualLoadingUpdate(); }
        }

        private async Task<bool> FinishLoadingWhenReadyAsync(int operation, long id)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + RestoreTimeout;
            string session = SessionId;
            var manager = NetworkManager.Singleton;
            while (IsOnlineCurrent(operation) && manager != null && manager == NetworkManager.Singleton && session == SessionId)
            {
                var player = manager.LocalClient?.PlayerObject;
                var world = HeistWorldGlue.Instance;
                bool playerReady = manager.IsServer && !manager.IsClient ||
                    player != null && player.IsSpawned && player.GetComponent<PlayerWalletGlue>() is { IsWalletLoaded: true };
                if (playerReady && world != null && world.IsOnlineWorldReady && !SessionWorldMigrationGlue.IsRestoring)
                {
                    loading?.Complete(id, 2); loading?.Succeed(id); return true;
                }
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                { FailLoading("플레이어/월드 복원: 준비 제한 시간을 초과했습니다."); return false; }
                await Task.Yield();
            }
            return false;
        }

        private float RestoreTimeout => loading != null && loading.Config != null ? loading.Config.RestoreTimeoutSeconds : config.ConnectionShutdownTimeoutSeconds;
        private float LeaveTimeout => loading != null && loading.Config != null ? loading.Config.LeaveTimeoutSeconds : config != null ? config.ConnectionShutdownTimeoutSeconds : 5f;

        private void FailLoading(string reason)
        {
            loading?.Fail(loadingId, reason);
        }

        private void StartRecoveryLoading(bool explicitTest)
        {
            localTitle = false; acceptSessionEvents = true;
            loadingId = loading != null ? loading.Begin(LoadingOperationKind.Reconnect, InvalidateOnlineFlow,
                ReturnToTitleAsync, RetryRecoveryAsync, true) : 0;
            var task = explicitTest ? ReconnectClientAsync() : RecoverSessionAsync();
            loading?.Track(task);
        }

        private void BeginMigrationLoading()
        {
            if (IsMigratingHost) return;
            loadingId = loading != null ? loading.Begin(LoadingOperationKind.Migration, InvalidateOnlineFlow,
                ReturnToTitleAsync, RetryRecoveryAsync, true) : 0;
        }

        private async Task RetryRecoveryAsync()
        {
            if (this == null || returningToTitle) return;
            localTitle = false; acceptSessionEvents = true;
            int operation = ++onlineGeneration;
            loadingId = loading != null ? loading.Begin(LoadingOperationKind.Migration, InvalidateOnlineFlow,
                ReturnToTitleAsync, RetryRecoveryAsync) : 0;
            var task = RetryRecoveryCoreAsync(operation);
            loading?.Track(task);
            await task;
        }

        private async Task RetryRecoveryCoreAsync(int operation)
        {
            if (!await relaySessionService.RefreshSessionForRecoveryAsync())
            { if (IsOnlineCurrent(operation)) FailLoading("세션 조회 실패 · 네트워크와 현재 방의 존재 여부를 확인하세요."); return; }
            if (!IsOnlineCurrent(operation)) return;
            loading?.Complete(loadingId, 0);
            if (IsMigratingHost || migrationRestorePending) return;
            if (relaySessionService.IsHost && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
                await RestoreMigratedSessionAsync();
            else await RecoverSessionAsync();
        }

        /// <summary>계정·지갑·영수증은 유지하고 NGO·로컬 Session·임시 티켓만 제한 시간 안에 정리합니다.</summary>
        public async Task ReturnToTitleAsync()
        {
            if (returningToTitle) return;
            returningToTitle = true; localTitle = true;
            InvalidateOnlineFlow();
            joinedAsClient = false; explicitReconnectTest = false; useInvalidTicketForTest = false;
            IsMigratingHost = false; ClientConnectionState = RelayClientConnectionState.Disconnected;
            pendingMigrationSnapshot = null; RecoveryStatus = string.Empty;
            cloudCheckpointGlue?.StopAutomaticSaving();
            var manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientConnectedCallback -= HandleProtectedClientConnected;
                SetServerPlayerDamageBlocked(false); IsPostMigrationProtected = false;
                manager.Shutdown();
            }
            try
            {
                leaveRequest ??= relaySessionService.LeaveCurrentSessionAsync();
                loading?.Track(leaveRequest);
                await Task.WhenAny(leaveRequest, Task.Delay(Mathf.CeilToInt(LeaveTimeout * 1000)));
                relaySessionService.DetachLocalSession();
                ReconnectTicketMemoryStore.Reset();
                SessionWorldMigrationGlue.Reset();
            }
            finally { if (this != null) { returningToTitle = false; ManualLoadingUpdate(); } }
        }
    }
}
