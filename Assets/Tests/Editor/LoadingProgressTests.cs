using EchoZone.Online.Loading;
using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>온라인 성공을 추측하지 않는 진행률과 세대 무효화 규칙을 검증합니다.</summary>
public sealed class LoadingProgressTests
{
    /// <summary>모든 단계가 끝나도 실제 성공 전에는 90%까지만 표시합니다.</summary>
    [Test] public void CompletedStagesWaitAtNinety()
    {
        var b = new LoadingProgressBrick(); long id = b.Begin(LoadingOperationKind.Join, new double[] { 1, 3 });
        b.Complete(id, 0); Assert.AreEqual(.225, b.Progress, .0001);
        b.Complete(id, 1); b.Tick(999, .3); Assert.AreEqual(.9, b.Progress);
        Assert.IsFalse(b.Succeeded);
    }
    /// <summary>실제 최종 성공 이후에만 마지막 10%를 보간합니다.</summary>
    [Test] public void OnlyRealSuccessReachesHundred()
    {
        var b = new LoadingProgressBrick(); long id = b.Begin(LoadingOperationKind.Join, new double[] { 1 });
        Assert.IsFalse(b.Succeed(id)); b.Complete(id, 0); Assert.IsTrue(b.Succeed(id));
        b.Tick(.15, .3); Assert.AreEqual(.95, b.Progress, .0001);
        b.Tick(.15, .3); Assert.AreEqual(1, b.Progress);
    }
    /// <summary>단계 완료 이벤트가 중복되어도 진행률을 중복 적립하지 않습니다.</summary>
    [Test] public void DuplicateStepIsIdempotent()
    {
        var b = new LoadingProgressBrick(); long id = b.Begin(LoadingOperationKind.Create, new double[] { 1, 1 });
        b.Complete(id, 0); b.Complete(id, 0); Assert.AreEqual(.45, b.Progress);
    }
    /// <summary>실패·타이틀 복귀로 무효화된 응답은 이후 작업에 영향을 주지 않습니다.</summary>
    [Test] public void RetryRejectsOldResponses()
    {
        var b = new LoadingProgressBrick(); long old = b.Begin(LoadingOperationKind.SignIn, new double[] { 1 });
        b.Fail(old, "실패"); Assert.IsFalse(b.Complete(old, 0));
        b.Invalidate(); long next = b.Begin(LoadingOperationKind.SignIn, new double[] { 1 });
        Assert.Greater(next, old); Assert.IsFalse(b.Fail(old, "늦은 실패"));
        Assert.IsFalse(b.Succeed(old)); Assert.AreEqual(0, b.Progress);
    }
    /// <summary>소유자가 허용하지 않은 버튼은 실패 상태에서도 잠겨 있습니다.</summary>
    [Test] public void FailedButtonsRequireOwnerPermission()
    {
        var b = new LoadingProgressBrick(); long id = b.Begin(LoadingOperationKind.Migration, new double[] { 1 });
        b.SetActions(true, true); Assert.IsFalse(b.CanRetry); Assert.IsFalse(b.CanReturn);
        b.Fail(id, "실패"); b.SetActions(false, true);
        Assert.IsFalse(b.CanRetry); Assert.IsTrue(b.CanReturn);
        b.Invalidate(); Assert.IsFalse(b.CanReturn);
    }
    /// <summary>짧은 작업은 지연 표시하고 실패는 즉시 보여줍니다.</summary>
    [Test] public void DelayDoesNotInventProgress()
    {
        var b = new LoadingProgressBrick(); long id = b.Begin(LoadingOperationKind.Initialization, new double[] { 1 });
        b.Tick(.1, .3); Assert.IsFalse(b.ShouldShow(.2)); Assert.AreEqual(0, b.Progress);
        b.Fail(id, "실패"); Assert.IsTrue(b.ShouldShow(.2));
    }
    /// <summary>순서가 다른 실제 완료 보고도 누락 단계 없이 최종 성공을 요구합니다.</summary>
    [Test] public void OutOfOrderCompletionStillRequiresAllSteps()
    {
        var b = new LoadingProgressBrick(); long id = b.Begin(LoadingOperationKind.Join, new double[] { 1, 2, 1 });
        b.Complete(id, 2); b.Complete(id, 0); Assert.IsFalse(b.Succeed(id));
        b.Complete(id, 1); Assert.IsTrue(b.Succeed(id));
    }

    /// <summary>실패 화면으로 돌아와도 끝나지 않은 SDK 요청과 병렬 재시도를 허용하지 않습니다.</summary>
    [Test] public async Task PendingSdkBlocksRetryAndLateCompletion()
    {
        var obj = new GameObject("LoadingTest");
        try
        {
            var overlay = obj.AddComponent<LoadingOverlayGlue>();
            var source = new TaskCompletionSource<bool>(); bool invalidated = false;
            long id = overlay.Begin(LoadingOperationKind.Join, () => invalidated = true,
                () => Task.CompletedTask, () => Task.CompletedTask);
            overlay.Track(source.Task);
            overlay.Fail(id, "통신 실패"); overlay.ManualUpdate(0);
            Assert.IsTrue(invalidated); Assert.IsFalse(overlay.State.CanRetry);
            Assert.IsTrue(overlay.State.CanReturn);
            source.SetResult(true); await source.Task;
            Assert.IsFalse(overlay.State.Complete(id, 0));
            overlay.ManualUpdate(0); Assert.IsTrue(overlay.State.CanRetry);
        }
        finally { Object.DestroyImmediate(obj); }
    }

    /// <summary>타이틀 복귀는 응답 대기와 분리되지만 새 작업은 이전 응답 정리 후에만 열립니다.</summary>
    [Test] public void TitleCanReturnWithoutUnlockingPendingSdk()
    {
        var obj = new GameObject("LoadingTitleTest");
        try
        {
            var overlay = obj.AddComponent<LoadingOverlayGlue>();
            var source = new TaskCompletionSource<bool>(); bool returned = false;
            long id = overlay.Begin(LoadingOperationKind.Join, null,
                () => { returned = true; return Task.CompletedTask; }, () => Task.CompletedTask);
            overlay.Track(source.Task); overlay.Fail(id, "통신 실패"); overlay.ManualUpdate(0);
            typeof(LoadingOverlayGlue).GetMethod("Return", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(overlay, null);
            Assert.IsTrue(returned); Assert.IsFalse(overlay.IsBlocking); Assert.IsFalse(overlay.CanStartOperation);
            source.SetResult(true); Assert.IsTrue(overlay.CanStartOperation);
        }
        finally { Object.DestroyImmediate(obj); }
    }
}
