using System;
using System.Threading.Tasks;
using EchoZone.Heist;
using NUnit.Framework;

/// <summary>실제 Cloud가 아닌 저장소 모사로 입장 전 준비·확인·중복 차단을 검증합니다.</summary>
public sealed class WalletPreparationTests
{
    [Test] public async Task MissingWalletCreatesZeroAndReadsAgain()
    {
        SettlementWalletRecord stored = null; int reads = 0, writes = 0;
        var result = await WalletPreparation.EnsureAsync(() => { reads++; return Task.FromResult(stored); },
            record => { writes++; stored = record; return Task.CompletedTask; });
        Assert.AreEqual(2, reads); Assert.AreEqual(1, writes); Assert.AreEqual(0, result.Balance);
        Assert.AreEqual(0, result.Revision); Assert.IsNull(result.Pending);
        Assert.IsEmpty(result.Receipts); Assert.IsEmpty(result.Recoveries);
    }
    [Test] public async Task ExistingWalletIsNeverWrittenOrReset()
    {
        var existing = new SettlementWalletRecord { Balance = 123, Revision = 7 };
        var result = await WalletPreparation.EnsureAsync(() => Task.FromResult(existing), _ => throw new Exception("Must not write"));
        Assert.AreSame(existing, result); Assert.AreEqual(123, result.Balance); Assert.AreEqual(7, result.Revision);
    }
    [Test] public void FailedReadNeverCreates()
    {
        Assert.ThrowsAsync<Exception>(async () => await WalletPreparation.EnsureAsync(
            () => throw new Exception("Read failed"), _ => throw new AssertionException("Must not create")));
    }
    [Test] public void FailedCreateDoesNotAdmit()
    {
        Assert.ThrowsAsync<Exception>(async () => await WalletPreparation.EnsureAsync(
            () => Task.FromResult<SettlementWalletRecord>(null), _ => throw new Exception("Timed out")));
    }
    [Test] public void UnconfirmedReadbackDoesNotAdmit()
    {
        Assert.ThrowsAsync<InvalidOperationException>(async () => await WalletPreparation.EnsureAsync(
            () => Task.FromResult<SettlementWalletRecord>(null), _ => Task.CompletedTask));
    }
    [Test] public void RapidRequestsBlockedUntilCompletionAndCooldown()
    {
        var gate = new WalletAdmissionGate(); Assert.IsTrue(gate.TryBegin(0));
        Assert.IsFalse(gate.TryBegin(1)); Assert.IsFalse(gate.TryBegin(999));
        gate.Complete(false, 999, 5); Assert.IsFalse(gate.TryBegin(1000));
        Assert.IsTrue(gate.TryBegin(1004)); gate.Complete(true, 1005, 5);
        Assert.IsTrue(gate.TryBegin(1005));
    }
    [Test] public async Task LostResponseRetryReadsExistingWithoutSecondCreate()
    {
        SettlementWalletRecord stored = null; int writes = 0;
        Assert.ThrowsAsync<Exception>(async () => await WalletPreparation.EnsureAsync(() => Task.FromResult(stored),
            record => { stored = record; writes++; throw new Exception("Lost response"); }));
        var result = await WalletPreparation.EnsureAsync(() => Task.FromResult(stored), _ => throw new AssertionException("Duplicate write"));
        Assert.AreEqual(1, writes); Assert.AreSame(stored, result);
    }
}
