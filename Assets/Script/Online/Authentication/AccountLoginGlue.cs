using TMPro;
using UnityEngine;
using UnityEngine.UI;
using EchoZone.Online.Loading;

namespace EchoZone.Online.Authentication
{
    /// <summary>타이틀의 계정 입력을 공유 인증 Service에 전달하고 준비·처리·실패 상태를 표시합니다.</summary>
    public sealed class AccountLoginGlue : MonoBehaviour
    {
        /// <summary>실행 중 하나의 계정 인증을 소유하는 연결입니다.</summary>
        [SerializeField] private UnityAuthenticationGlue authenticationGlue;
        /// <summary>인증 전에만 보여 줄 계정 입력 패널입니다.</summary>
        [SerializeField] private GameObject loginPanel;
        /// <summary>사용자가 직접 입력하는 계정 아이디입니다.</summary>
        [SerializeField] private TMP_InputField usernameInput;
        /// <summary>요청 직후 내용을 지우는 마스킹된 비밀번호 입력창입니다.</summary>
        [SerializeField] private TMP_InputField passwordInput;
        /// <summary>기존 계정 인증을 요청하는 버튼입니다.</summary>
        [SerializeField] private Button signInButton;
        /// <summary>새 계정을 만들고 인증하는 버튼입니다.</summary>
        [SerializeField] private Button signUpButton;
        /// <summary>초기화·처리 중 상태와 검증 실패 이유를 표시합니다.</summary>
        [SerializeField] private TMP_Text messageText;
        private bool preserveSession;
        private LoadingOverlayGlue loading;

        private void Awake()
        {
            loading = GetComponent<LoadingOverlayGlue>();
            if (passwordInput != null)
            {
                passwordInput.contentType = TMP_InputField.ContentType.Password;
                passwordInput.SetTextWithoutNotify(string.Empty);
            }
            signInButton?.onClick.AddListener(SignIn);
            signUpButton?.onClick.AddListener(SignUp);
            ManualUpdate(false);
        }

        private void OnDestroy()
        {
            signInButton?.onClick.RemoveListener(SignIn);
            signUpButton?.onClick.RemoveListener(SignUp);
            if (passwordInput != null) passwordInput.SetTextWithoutNotify(string.Empty);
        }

        /// <summary>기존 타이틀 갱신에서만 호출하며 세션 연결·복구 중에는 로그인 화면을 열지 않습니다.</summary>
        public void ManualUpdate(bool sessionActive)
        {
            preserveSession = sessionActive;
            var service = authenticationGlue != null ? authenticationGlue.Service : null;
            bool signedIn = service != null && service.IsSignedIn;
            bool busy = service != null && service.AuthenticationBrick.IsBusy;
            bool ready = service != null && service.IsPreparedForLogin;
            loginPanel?.SetActive(!sessionActive && !signedIn);
            bool enabledInput = ready && !busy && !signedIn && !sessionActive && (loading == null || loading.CanStartOperation);
            if (usernameInput != null) usernameInput.interactable = enabledInput;
            if (passwordInput != null) passwordInput.interactable = enabledInput;
            if (signInButton != null) signInButton.interactable = enabledInput;
            if (signUpButton != null) signUpButton.interactable = enabledInput;
            if (messageText == null) return;
            messageText.text = service == null ? "계정 인증 연결이 없습니다."
                : busy ? "계정 요청을 처리하는 중입니다..."
                : !string.IsNullOrEmpty(service.LastErrorMessage) ? service.LastErrorMessage
                : !ready ? "계정 서비스를 준비하는 중입니다..."
                : "아이디와 비밀번호로 로그인하거나 회원가입하세요.";
        }

        private void SignIn() => RequestAuthentication(false);
        private void SignUp() => RequestAuthentication(true);

        private async void RequestAuthentication(bool signUp)
        {
            if (authenticationGlue == null || usernameInput == null || passwordInput == null || preserveSession) return;
            var service = authenticationGlue.Service;
            if (!service.IsPreparedForLogin || service.IsSignedIn || service.AuthenticationBrick.IsBusy || loading != null && !loading.CanStartOperation) return;
            if (!service.AuthenticationBrick.ValidateInput(usernameInput.text, passwordInput.text, signUp))
            {
                ManualUpdate(preserveSession);
                return;
            }
            long id = loading != null ? loading.Begin(signUp ? LoadingOperationKind.SignUp : LoadingOperationKind.SignIn,
                null, () => System.Threading.Tasks.Task.CompletedTask,
                () => { passwordInput?.ActivateInputField(); return System.Threading.Tasks.Task.CompletedTask; }) : 0;
            bool Current() => this != null && (loading == null || loading.State.Accepts(id));
            var request = signUp
                ? service.SignUpAsync(usernameInput.text, passwordInput.text, Current)
                : service.SignInAsync(usernameInput.text, passwordInput.text, Current);
            loading?.Track(request);
            passwordInput.SetTextWithoutNotify(string.Empty);
            ManualUpdate(preserveSession);
            bool result = await request;
            if (!Current()) return;
            if (result) { loading?.Complete(id, 0); loading?.Succeed(id); }
            else
            {
                service.AuthenticationBrick.Fail((signUp ? "회원가입 실패" : "로그인 실패") + "\n" + service.LastErrorMessage);
                loading?.FinishAuthenticationFailure(id, service.LastErrorMessage);
            }
            ManualUpdate(false);
        }
    }
}
