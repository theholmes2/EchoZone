using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

namespace EchoZone.Online.Authentication
{
    /// <summary>
    /// Unity Gaming Services를 초기화하고 플레이어의 익명 인증 상태를 관리하는 Brick입니다.
    /// Relay, NGO, UI에는 의존하지 않습니다.
    /// </summary>
    public sealed class UnityAuthenticationService
    {
        /// <summary>마지막 인증 시도에서 발생한 오류 메시지입니다.</summary>
        public string LastErrorMessage { get; private set; } = string.Empty;

        /// <summary>Unity Gaming Services 초기화가 완료되었는지 나타냅니다.</summary>
        public bool IsInitialized =>
            UnityServices.State == ServicesInitializationState.Initialized;

        /// <summary>현재 플레이어가 Unity Authentication에 로그인되어 있는지 나타냅니다.</summary>
        public bool IsSignedIn =>
            IsInitialized && AuthenticationService.Instance.IsSignedIn;

        /// <summary>인증된 플레이어의 Unity Player ID입니다.</summary>
        public string PlayerId =>
            IsSignedIn ? AuthenticationService.Instance.PlayerId : string.Empty;

        /// <summary>
        /// Unity Gaming Services를 준비하고, 로그인되어 있지 않으면 익명 로그인을 요청합니다.
        /// </summary>
        /// <returns>인증이 완료되었으면 <see langword="true"/>입니다.</returns>
        public async Task<bool> InitializeAndSignInAsync()
        {
            LastErrorMessage = string.Empty;

            try
            {
                if (!IsInitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                return AuthenticationService.Instance.IsSignedIn;
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                return false;
            }
        }
    }
}
