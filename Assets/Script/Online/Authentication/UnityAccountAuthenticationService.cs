using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace EchoZone.Online.Authentication
{
    /// <summary>
    /// 계정 인증 Brick과 Unity Username/Password 인증 API를 연결하는 Service입니다.
    /// 익명 또는 저장된 세션으로 자동 로그인하지 않습니다.
    /// </summary>
    public sealed class UnityAccountAuthenticationService
    {
        private readonly AccountAuthenticationBrick authenticationBrick = new();
        private bool hasPreparedExplicitLogin;

        /// <summary>이전 자격 증명 정리까지 끝나 명시적 계정 입력을 받을 수 있는지 나타냅니다.</summary>
        public bool IsPreparedForLogin => hasPreparedExplicitLogin && IsInitialized;

        /// <summary>로그인 화면이 표시할 계정 인증 상태와 오류를 제공합니다.</summary>
        public AccountAuthenticationBrick AuthenticationBrick => authenticationBrick;

        /// <summary>Unity Gaming Services가 계정 요청을 받을 준비를 마쳤는지 나타냅니다.</summary>
        public bool IsInitialized =>
            UnityServices.State == ServicesInitializationState.Initialized;

        /// <summary>아이디와 비밀번호로 인증된 플레이어가 있는지 나타냅니다.</summary>
        public bool IsSignedIn =>
            IsPreparedForLogin && authenticationBrick.State == AccountAuthenticationState.SignedIn &&
            AuthenticationService.Instance.IsSignedIn;

        /// <summary>인증 완료 후 Relay와 개인 Cloud 데이터의 계정 키로 사용할 Player ID입니다.</summary>
        public string PlayerId =>
            IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

        /// <summary>로그인 화면이 표시할 마지막 입력 검증 또는 SDK 실패 이유입니다.</summary>
        public string LastErrorMessage => authenticationBrick.LastErrorMessage;

        /// <summary>
        /// UGS만 초기화하고 이전 세션 자격 증명을 지워 매 실행의 첫 온라인 진입에 로그인을 요구합니다.
        /// 앱 시작 과정에서 한 번만 호출해야 합니다.
        /// </summary>
        public async Task<bool> InitializeForExplicitLoginAsync()
        {
            if (hasPreparedExplicitLogin)
            {
                return true;
            }

            try
            {
                if (!IsInitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                AuthenticationService.Instance.SignOut(clearCredentials: true);
                hasPreparedExplicitLogin = true;
                authenticationBrick.MarkReady();
                return true;
            }
            catch (Exception exception)
            {
                authenticationBrick.Fail(exception.Message);
                return false;
            }
        }

        /// <summary>검증된 아이디와 비밀번호로 기존 Unity 계정에 로그인합니다.</summary>
        public async Task<bool> SignInAsync(string username, string password, Func<bool> isCurrent = null)
        {
            if (!EnsurePrepared()) return false;
            if (!authenticationBrick.TryBeginSignIn(
                    username,
                    password,
                    out string normalizedUsername))
            {
                return false;
            }

            return await ExecuteAuthenticationAsync(
                () => AuthenticationService.Instance
                    .SignInWithUsernamePasswordAsync(normalizedUsername, password), isCurrent);
        }

        /// <summary>검증된 아이디와 비밀번호로 Unity 계정을 만들고 즉시 로그인합니다.</summary>
        public async Task<bool> SignUpAsync(string username, string password, Func<bool> isCurrent = null)
        {
            if (!EnsurePrepared()) return false;
            if (!authenticationBrick.TryBeginSignUp(
                    username,
                    password,
                    out string normalizedUsername))
            {
                return false;
            }

            return await ExecuteAuthenticationAsync(
                () => AuthenticationService.Instance
                    .SignUpWithUsernamePasswordAsync(normalizedUsername, password), isCurrent);
        }

        /// <summary>현재 계정과 저장된 세션 자격 증명을 지우고 로그인 화면으로 돌아갑니다.</summary>
        public void SignOut()
        {
            if (IsInitialized)
            {
                AuthenticationService.Instance.SignOut(clearCredentials: true);
            }

            authenticationBrick.ResetForLogin();
        }

        /// <summary>초기화 실패 상태를 재시도 가능 상태로 오인해 SDK를 호출하지 않도록 차단합니다.</summary>
        private bool EnsurePrepared()
        {
            if (IsPreparedForLogin) return true;
            authenticationBrick.Fail("계정 서비스 준비가 끝나지 않았습니다. 초기화 실패 시 게임을 다시 실행하세요.");
            return false;
        }

        private async Task<bool> ExecuteAuthenticationAsync(Func<Task> authenticationRequest, Func<bool> isCurrent)
        {
            try
            {
                await authenticationRequest();
                if (isCurrent != null && !isCurrent())
                {
                    AuthenticationService.Instance.SignOut(clearCredentials: true);
                    authenticationBrick.Fail("종료한 인증 요청입니다. 다시 입력해 주세요.");
                    return false;
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    authenticationBrick.Fail("계정 인증 결과를 확인하지 못했습니다.");
                    return false;
                }

                authenticationBrick.CompleteAuthentication();
                return true;
            }
            catch (Exception exception)
            {
                authenticationBrick.Fail(exception.Message);
                return false;
            }
        }
    }
}
