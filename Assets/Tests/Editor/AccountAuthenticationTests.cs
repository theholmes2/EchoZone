using System.Threading.Tasks;
using EchoZone.Online.Authentication;
using EchoZone.Online.Relay;
using NUnit.Framework;
using UnityEngine;

/// <summary>실계정을 만들지 않고 계정 입력·중복 요청·미인증 진입 차단을 검증합니다.</summary>
public sealed class AccountAuthenticationTests
{
    /// <summary>빈 입력 항목을 통신 전에 안내하고 요청 처리 상태로 전환하지 않습니다.</summary>
    [TestCase("", "", "아이디")]
    [TestCase("   ", "Test123!", "아이디")]
    [TestCase("tester", "", "비밀번호")]
    [TestCase("tester", null, "비밀번호")]
    public void EmptyInputReportsFieldWithoutStarting(string username, string password, string field)
    {
        var brick = new AccountAuthenticationBrick(); brick.MarkReady();
        Assert.IsFalse(brick.ValidateInput(username, password, false));
        StringAssert.Contains(field, brick.LastErrorMessage);
        Assert.AreEqual(AccountAuthenticationState.Ready, brick.State);
    }

    /// <summary>로그인은 회원가입 복잡도 규칙으로 기존 자격 증명을 거부하지 않습니다.</summary>
    [Test] public void LoginDoesNotApplyRegistrationPasswordPolicy()
    {
        var brick = new AccountAuthenticationBrick(); brick.MarkReady();
        Assert.IsFalse(brick.ValidateInput("tester", "legacy", true));
        Assert.IsTrue(brick.ValidateInput("tester", "legacy", false));
        Assert.IsEmpty(brick.LastErrorMessage);
        Assert.IsFalse(brick.IsBusy);
        Assert.IsTrue(brick.TryBeginSignIn("tester", "legacy", out _));
    }

    /// <summary>사전 검사와 실제 요청이 같은 가입 규칙을 사용하고 비밀번호 공백을 제거하지 않습니다.</summary>
    [Test] public void PreflightAndRequestShareRulesWithoutTrimmingPassword()
    {
        var brick = new AccountAuthenticationBrick(); brick.MarkReady();
        string password = " " + new string('a', 26) + "A1! ";
        Assert.IsFalse(brick.ValidateInput("tester", password, true));
        Assert.IsFalse(brick.TryBeginSignUp("tester", password, out _));
        Assert.IsTrue(brick.ValidateInput("tester", "Test123!", true));
        Assert.IsFalse(brick.IsBusy);
        Assert.IsTrue(brick.TryBeginSignUp("tester", "Test123!", out _));
        Assert.IsFalse(brick.ValidateInput("", "", false));
        Assert.AreEqual(AccountAuthenticationState.SigningUp, brick.State);
    }

    /// <summary>초기화 전에는 정상 형식의 입력도 요청하지 않습니다.</summary>
    [Test] public void NotReadyRejectsRequest()
    {
        var brick = new AccountAuthenticationBrick();
        Assert.IsFalse(brick.TryBeginSignIn("tester", "Test123!", out _));
        Assert.IsFalse(brick.IsBusy);
    }

    /// <summary>잘못된 아이디와 비밀번호는 통신 전에 메시지로 반환합니다.</summary>
    [TestCase("a", "Test123!")]
    [TestCase("tester", "short")]
    [TestCase("bad space", "Test123!")]
    public void InvalidInputIsLocal(string username, string password)
    {
        var brick = new AccountAuthenticationBrick(); brick.MarkReady();
        Assert.IsFalse(brick.TryBeginSignUp(username, password, out _));
        Assert.IsNotEmpty(brick.LastErrorMessage); Assert.IsFalse(brick.IsBusy);
    }

    /// <summary>로그인과 회원가입이 서로 중복되지 않고 완료 후에도 재요청을 막습니다.</summary>
    [Test] public void BusyAndCompletedBlockDuplicates()
    {
        var brick = new AccountAuthenticationBrick(); brick.MarkReady();
        Assert.IsTrue(brick.TryBeginSignIn(" tester ", "Test123!", out var normalized));
        Assert.AreEqual("tester", normalized);
        Assert.IsTrue(brick.IsBusy);
        Assert.IsFalse(brick.TryBeginSignUp("tester", "Test123!", out _));
        Assert.AreEqual(AccountAuthenticationState.SigningIn, brick.State);
        brick.CompleteAuthentication();
        Assert.IsFalse(brick.TryBeginSignIn("tester", "Test123!", out _));
        Assert.AreEqual(AccountAuthenticationState.SignedIn, brick.State);
    }

    /// <summary>실패 후 다시 입력하면 이전 오류를 지우고 요청을 시작합니다.</summary>
    [Test] public void FailureAllowsRetry()
    {
        var brick = new AccountAuthenticationBrick(); brick.MarkReady();
        brick.Fail("실패");
        Assert.IsTrue(brick.TryBeginSignUp("tester", "Test123!", out _));
        Assert.IsEmpty(brick.LastErrorMessage);
    }

    /// <summary>준비되지 않은 Service는 SDK 요청과 이전 세션 노출을 막습니다.</summary>
    [Test] public async Task ServiceRequiresPreparation()
    {
        var service = new UnityAccountAuthenticationService();
        Assert.IsFalse(await service.SignInAsync("tester", "Test123!"));
        Assert.IsFalse(await service.SignUpAsync("tester", "Test123!"));
        Assert.IsFalse(service.IsSignedIn); Assert.IsEmpty(service.PlayerId);
    }

    /// <summary>Glue가 중복 배치되어도 같은 인증 Service를 반환합니다.</summary>
    [Test] public void GluesShareServiceAndMissingUIIsSafe()
    {
        var a = new GameObject(); var b = new GameObject();
        try
        {
            Assert.AreSame(a.AddComponent<UnityAuthenticationGlue>().Service, b.AddComponent<UnityAuthenticationGlue>().Service);
            Assert.DoesNotThrow(() => a.AddComponent<AccountLoginGlue>().ManualUpdate(false));
        }
        finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
    }

    /// <summary>UI를 우회해도 계정 연결 없는 Relay 생성·참가를 거부합니다.</summary>
    [Test] public async Task RelayRejectsUnauthenticatedAdmission()
    {
        var obj = new GameObject();
        try
        {
            var relay = obj.AddComponent<RelaySessionGlue>();
            Assert.IsFalse(await relay.CreateHostSessionAsync());
            Assert.IsFalse(await relay.JoinSessionAsync("ABC123"));
            Assert.IsNotEmpty(relay.AdmissionError);
        }
        finally { Object.DestroyImmediate(obj); }
    }
}
