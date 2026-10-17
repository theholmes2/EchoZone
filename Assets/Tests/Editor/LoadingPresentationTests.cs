using EchoZone.Online.Loading;
using NUnit.Framework;

/// <summary>표시 보간이 실제 완료 판정이나 실패·재시도 경계를 넘지 않는지 검증합니다.</summary>
public sealed class LoadingPresentationTests
{
    /// <summary>응답이 끝난 로그인 실패는 로딩을 닫아 다시 입력할 수 있게 합니다.</summary>
    [Test] public void FinishedLoginFailureReturnsToInput()
    {
        var obj = new UnityEngine.GameObject();
        try
        {
            var glue = obj.AddComponent<LoadingOverlayGlue>();
            long id = glue.Begin(LoadingOperationKind.SignIn, null, null, null);
            glue.Track(System.Threading.Tasks.Task.CompletedTask);
            glue.FinishAuthenticationFailure(id, "로그인 실패");
            Assert.IsTrue(glue.State.Failed);
            Assert.IsTrue(glue.CanStartOperation);
            Assert.IsFalse(glue.State.Accepts(id));
        }
        finally { UnityEngine.Object.DestroyImmediate(obj); }
    }

    /// <summary>진행 중 SDK 요청이나 방 참가 실패를 계정 입력 복귀로 잘못 해제하지 않습니다.</summary>
    [Test] public void AuthenticationReturnDoesNotBypassPendingOrSessionWork()
    {
        var obj = new UnityEngine.GameObject();
        var pending = new System.Threading.Tasks.TaskCompletionSource<bool>();
        try
        {
            var glue = obj.AddComponent<LoadingOverlayGlue>();
            long id = glue.Begin(LoadingOperationKind.SignIn, null, null, null);
            glue.Track(pending.Task);
            glue.FinishAuthenticationFailure(id, "실패");
            Assert.IsTrue(glue.IsBlocking);
            Assert.IsFalse(glue.State.Failed);
            pending.SetResult(false);
            glue.FinishAuthenticationFailure(id, "실패");
            Assert.IsTrue(glue.CanStartOperation);
            id = glue.Begin(LoadingOperationKind.Join, null, null, null);
            glue.FinishAuthenticationFailure(id, "실패");
            Assert.IsTrue(glue.IsBlocking);
            Assert.IsFalse(glue.State.Failed);
        }
        finally { pending.TrySetResult(false); UnityEngine.Object.DestroyImmediate(obj); }
    }

    /// <summary>목표 변경은 즉시 점프하지 않고 정해진 시간 내 도달합니다.</summary>
    [Test] public void StepBlendsWithoutJump()
    {
        var b = new LoadingPresentationBrick();
        b.Tick(0, .6, false, false, .4, .3); Assert.AreEqual(0, b.Progress);
        b.Tick(.2, .6, false, false, .4, .3); Assert.AreEqual(.3, b.Progress, .0001);
        b.Tick(.2, .6, false, false, .4, .3); Assert.AreEqual(.6, b.Progress, .0001);
    }
    /// <summary>보간 중 새 목표가 도착해도 현재 값에서 이어가고 후퇴하지 않습니다.</summary>
    [Test] public void RetargetUsesCurrentValue()
    {
        var b = new LoadingPresentationBrick(); b.Tick(.2, .3, false, false, .4, .3);
        double before = b.Progress;
        b.Tick(0, .8, false, false, .4, .3); Assert.AreEqual(before, b.Progress);
        b.Tick(.4, .8, false, false, .4, .3); Assert.AreEqual(.8, b.Progress);
        b.Tick(.4, .1, false, false, .4, .3); Assert.AreEqual(.8, b.Progress);
    }
    /// <summary>최종 성공은 90%로 점프하지 않고 현재 표시에서 100%로 보간합니다.</summary>
    [Test] public void SuccessBlendsFromCurrentValue()
    {
        var b = new LoadingPresentationBrick(); b.Tick(.2, .3, false, false, .4, .3);
        double before = b.Progress;
        b.Tick(0, .9, true, false, .4, .3); Assert.AreEqual(before, b.Progress);
        b.Tick(.15, .9, true, false, .4, .3); Assert.AreEqual((before + 1) / 2, b.Progress, .0001);
        b.Tick(.15, 1, true, false, .4, .3); Assert.AreEqual(1, b.Progress);
    }
    /// <summary>대기 시간이 길어도 승인 목표와 성공 전 상한을 넘지 않습니다.</summary>
    [Test] public void WaitingDoesNotInventProgress()
    {
        var b = new LoadingPresentationBrick(); b.Tick(999, 0, false, false, .4, .3);
        Assert.AreEqual(0, b.Progress);
        b.Tick(999, 1, false, false, .4, .3); Assert.AreEqual(.9, b.Progress);
    }
    /// <summary>실패는 즉시 정지하고 재시작은 이전 보간을 남기지 않습니다.</summary>
    [Test] public void FailureFreezesAndResetClears()
    {
        var b = new LoadingPresentationBrick(); b.Tick(.2, .6, false, false, .4, .3);
        double before = b.Progress;
        b.Tick(99, 1, true, true, .4, .3); Assert.AreEqual(before, b.Progress);
        b.Reset(); b.Tick(1, 0, false, false, .4, .3); Assert.AreEqual(0, b.Progress);
    }
}
