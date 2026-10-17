using System.Threading.Tasks;
using UnityEngine;
using EchoZone.Online.Loading;

namespace EchoZone.Online.Authentication
{
    /// <summary>
    /// 실행당 하나의 계정 Service를 공유하며 시작 시에만 명시적 로그인 준비를 수행합니다.
    /// </summary>
    public sealed class UnityAuthenticationGlue : MonoBehaviour
    {
        private static UnityAccountAuthenticationService authenticationService;
        private static Task<bool> initializationTask;

        /// <summary>UI·Relay가 공유하는 유일한 계정 인증 Service입니다. 조회 자체로 초기화나 로그아웃하지 않습니다.</summary>
        public UnityAccountAuthenticationService Service => authenticationService ??= new UnityAccountAuthenticationService();

        /// <summary>기존 Relay의 PlayerId 접근 연결을 유지하는 동일 Service 참조입니다.</summary>
        public UnityAccountAuthenticationService AuthenticationService => Service;

        /// <summary>도메인 리로드를 끈 Editor에서도 새 실행은 반드시 로그인부터 시작합니다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewExecution()
        {
            authenticationService = null;
            initializationTask = null;
        }

        /// <summary>중복 Glue 또는 씬 재진입에도 최초 작업 하나만 시작하며 실패는 Service의 메시지로 전달합니다.</summary>
        private void Start()
        {
            if (initializationTask == null) initializationTask = InitializeWithLoadingAsync();
        }

        /// <summary>실패한 준비만 명시적으로 재시도하며 로그인된 계정은 초기화하지 않습니다.</summary>
        public Task RetryInitializationAsync()
        {
            if (Service.IsSignedIn || initializationTask != null && !initializationTask.IsCompleted) return Task.CompletedTask;
            initializationTask = InitializeWithLoadingAsync();
            return initializationTask;
        }

        private async Task<bool> InitializeWithLoadingAsync()
        {
            var loading = GetComponent<LoadingOverlayGlue>();
            long id = loading != null ? loading.Begin(LoadingOperationKind.Initialization, null,
                () => Task.CompletedTask, RetryInitializationAsync) : 0;
            var task = Service.InitializeForExplicitLoginAsync();
            loading?.Track(task);
            bool result = await task;
            if (this == null || loading != null && !loading.State.Accepts(id)) return false;
            if (result) { loading?.Complete(id, 0); loading?.Succeed(id); }
            else loading?.Fail(id, Service.LastErrorMessage);
            return result;
        }
    }
}
