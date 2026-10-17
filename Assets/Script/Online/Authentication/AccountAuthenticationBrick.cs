using System;

namespace EchoZone.Online.Authentication
{
    /// <summary>계정 인증 화면이 현재 어떤 요청을 받을 수 있는지 나타냅니다.</summary>
    public enum AccountAuthenticationState
    {
        /// <summary>Unity Gaming Services가 아직 준비되지 않은 상태입니다.</summary>
        NotReady,

        /// <summary>로그인 또는 회원가입 요청을 받을 수 있는 상태입니다.</summary>
        Ready,

        /// <summary>입력한 계정으로 로그인을 처리하는 상태입니다.</summary>
        SigningIn,

        /// <summary>입력한 계정으로 회원가입을 처리하는 상태입니다.</summary>
        SigningUp,

        /// <summary>계정 인증을 마쳐 온라인 기능을 사용할 수 있는 상태입니다.</summary>
        SignedIn,

        /// <summary>마지막 준비 또는 인증 요청이 실패한 상태입니다.</summary>
        Failed
    }

    /// <summary>
    /// Unity SDK를 직접 호출하지 않고 계정 입력 규칙과 인증 요청 상태를 관리하는 Brick입니다.
    /// 비밀번호는 검증에만 사용하고 필드에 보관하지 않습니다.
    /// </summary>
    public sealed class AccountAuthenticationBrick
    {
        private const int MinimumUsernameLength = 3;
        private const int MaximumUsernameLength = 20;
        private const int MinimumPasswordLength = 8;
        private const int MaximumPasswordLength = 30;

        /// <summary>로그인 화면과 온라인 진입 차단이 참조할 현재 인증 단계입니다.</summary>
        public AccountAuthenticationState State { get; private set; } =
            AccountAuthenticationState.NotReady;

        /// <summary>사용자에게 표시할 마지막 입력 검증 또는 인증 실패 이유입니다.</summary>
        public string LastErrorMessage { get; private set; } = string.Empty;

        /// <summary>중복 클릭을 막아야 하는 인증 요청 처리 중 상태입니다.</summary>
        public bool IsBusy =>
            State == AccountAuthenticationState.SigningIn ||
            State == AccountAuthenticationState.SigningUp;

        /// <summary>UGS 준비가 끝나 새 계정 요청을 받을 수 있도록 상태를 엽니다.</summary>
        public void MarkReady()
        {
            if (IsBusy || State == AccountAuthenticationState.SignedIn)
            {
                return;
            }

            LastErrorMessage = string.Empty;
            State = AccountAuthenticationState.Ready;
        }

        /// <summary>로그인 입력을 검사하고 중복 요청을 막은 뒤 SDK 호출 상태로 전환합니다.</summary>
        public bool TryBeginSignIn(
            string username,
            string password,
            out string normalizedUsername)
        {
            return TryBegin(
                username,
                password,
                AccountAuthenticationState.SigningIn,
                out normalizedUsername);
        }

        /// <summary>회원가입 입력을 검사하고 중복 요청을 막은 뒤 SDK 호출 상태로 전환합니다.</summary>
        public bool TryBeginSignUp(
            string username,
            string password,
            out string normalizedUsername)
        {
            return TryBegin(
                username,
                password,
                AccountAuthenticationState.SigningUp,
                out normalizedUsername);
        }

        /// <summary>SDK가 계정 인증을 확정하면 온라인 진입이 가능한 상태로 닫습니다.</summary>
        public void CompleteAuthentication()
        {
            if (!IsBusy)
            {
                throw new InvalidOperationException(
                    "Authentication can only complete while a request is active.");
            }

            LastErrorMessage = string.Empty;
            State = AccountAuthenticationState.SignedIn;
        }

        /// <summary>준비 또는 인증 실패를 기록하고 같은 화면에서 다시 시도할 수 있게 합니다.</summary>
        public void Fail(string errorMessage)
        {
            LastErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
                ? "계정 인증에 실패했습니다."
                : errorMessage;
            State = AccountAuthenticationState.Failed;
        }

        /// <summary>로그아웃한 계정이 다른 계정으로 다시 인증할 수 있도록 입력 대기 상태로 돌아갑니다.</summary>
        public void ResetForLogin()
        {
            LastErrorMessage = string.Empty;
            State = AccountAuthenticationState.Ready;
        }

        private bool TryBegin(
            string username,
            string password,
            AccountAuthenticationState requestState,
            out string normalizedUsername)
        {
            normalizedUsername = username?.Trim() ?? string.Empty;

            if (State != AccountAuthenticationState.Ready &&
                State != AccountAuthenticationState.Failed)
            {
                LastErrorMessage = IsBusy
                    ? "계정 요청을 처리하고 있습니다."
                    : "계정 인증을 아직 시작할 수 없습니다.";
                return false;
            }

            if (!ValidateInput(username, password, requestState == AccountAuthenticationState.SigningUp))
            {
                return false;
            }

            State = requestState;
            return true;
        }

        /// <summary>로딩을 열기 전에 입력 오류만 확인합니다. 요청 상태를 시작하거나 비밀번호를 보관하지 않습니다.</summary>
        public bool ValidateInput(string username, string password, bool signUp)
        {
            if (State != AccountAuthenticationState.Ready && State != AccountAuthenticationState.Failed)
                return false;

            string normalizedUsername = username?.Trim() ?? string.Empty;
            if (normalizedUsername.Length == 0)
            {
                LastErrorMessage = "아이디를 입력해 주세요.";
                return false;
            }

            if (!IsValidUsername(normalizedUsername))
            {
                LastErrorMessage =
                    "아이디는 3~20자의 영문, 숫자, 마침표, 하이픈, @, 밑줄만 사용할 수 있습니다.";
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                LastErrorMessage = "비밀번호를 입력해 주세요.";
                return false;
            }

            if (signUp && !IsValidPassword(password))
            {
                LastErrorMessage =
                    "비밀번호는 8~30자이며 영문 대문자, 소문자, 숫자, 특수문자를 각각 포함해야 합니다.";
                return false;
            }

            LastErrorMessage = string.Empty;
            return true;
        }

        private static bool IsValidUsername(string username)
        {
            if (username.Length < MinimumUsernameLength ||
                username.Length > MaximumUsernameLength)
            {
                return false;
            }

            foreach (char character in username)
            {
                bool isAsciiLetter =
                    character >= 'A' && character <= 'Z' ||
                    character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';
                bool isAllowedSymbol =
                    character == '.' || character == '-' ||
                    character == '@' || character == '_';

                if (!isAsciiLetter && !isDigit && !isAllowedSymbol)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidPassword(string password)
        {
            if (string.IsNullOrEmpty(password) ||
                password.Length < MinimumPasswordLength ||
                password.Length > MaximumPasswordLength)
            {
                return false;
            }

            bool hasUppercase = false;
            bool hasLowercase = false;
            bool hasDigit = false;
            bool hasSymbol = false;

            foreach (char character in password)
            {
                hasUppercase |= char.IsUpper(character);
                hasLowercase |= char.IsLower(character);
                hasDigit |= char.IsDigit(character);
                hasSymbol |= !char.IsLetterOrDigit(character);
            }

            return hasUppercase && hasLowercase && hasDigit && hasSymbol;
        }
    }
}
