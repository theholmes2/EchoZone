using UnityEngine;

namespace EchoZone.Online.Authentication
{
    /// <summary>
    /// Unity 씬의 시작 시점과 인증 Brick을 연결하는 Glue입니다.
    /// 인증 결과는 개발 단계에서 확인할 수 있도록 Console에 기록합니다.
    /// </summary>
    public sealed class UnityAuthenticationGlue : MonoBehaviour
    {
        /// <summary>UGS 초기화와 플레이어 인증을 담당하는 독립 Brick입니다.</summary>
        private readonly UnityAuthenticationService authenticationService = new();

        /// <summary>Relay 연결 Glue가 사용할 수 있는 인증 Brick입니다.</summary>
        public UnityAuthenticationService AuthenticationService => authenticationService;

        /// <summary>씬이 시작되면 UGS 초기화와 익명 로그인을 순서대로 요청합니다.</summary>
        private async void Start()
        {
            bool succeeded =
                await authenticationService.InitializeAndSignInAsync();

            if (!succeeded)
            {
                Debug.LogError(
                    $"Unity Authentication failed: {authenticationService.LastErrorMessage}",
                    this);
                return;
            }

            EchoZone.Online.OnlineDebugLog.Info(
                $"Unity Authentication succeeded. PlayerId: {authenticationService.PlayerId}",
                this);
        }
    }
}
