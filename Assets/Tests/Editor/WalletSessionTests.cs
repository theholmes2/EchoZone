using EchoZone.Heist;
using NUnit.Framework;

public sealed class WalletSessionTests
{
    [Test]
    public void EarlyRewardsSurviveLoadAndSpendingCannotOverdraw()
    {
        var wallet = new WalletSessionBrick(); wallet.Credit(50);
        Assert.IsFalse(wallet.TrySpend(1)); wallet.Load(100, 2);
        Assert.AreEqual(150, wallet.Balance); Assert.IsTrue(wallet.TrySpend(30));
        Assert.IsFalse(wallet.TrySpend(121)); Assert.AreEqual(120, wallet.Balance);
    }
    [Test]
    public void EscapeFreezesOneSettlementAndRejectsOtherResponses()
    {
        var wallet = new WalletSessionBrick(); wallet.Load(100, 2);
        Assert.IsTrue(wallet.BeginEscape(1000)); Assert.IsFalse(wallet.BeginEscape(1000));
        wallet.Credit(500); Assert.IsFalse(wallet.TrySpend(1)); Assert.AreEqual(1100, wallet.Balance);
        var id = wallet.SettlementId;
        Assert.IsFalse(wallet.BeginEscape(1000)); Assert.AreEqual(id, wallet.SettlementId);
        Assert.Throws<System.InvalidOperationException>(() => wallet.Confirm(1101, 3, id));
        Assert.Throws<System.InvalidOperationException>(() => wallet.Confirm(1100, 3, "other"));
        wallet.Confirm(1100, 3, id); Assert.IsTrue(wallet.Settled);
    }
    [Test]
    public void ExtractedCargoCannotReturnButStillLeavesTheftEvidence()
    {
        var ledger = new HeistLedgerBrick(); ledger.Record(1, 2, 3, 1000);
        Assert.AreEqual(1000, ledger.Extract(2)); Assert.AreEqual(0, ledger.Extract(2));
        Assert.AreEqual(0, ledger.Cargo(2)); Assert.IsTrue(ledger.Inspect(1));
        Assert.IsEmpty(ledger.Return(2, 9, .05f, out int reward)); Assert.AreEqual(0, reward);
    }
    [Test]
    public void ReentryLoadsConfirmedBalanceAndCreatesAnotherSettlement()
    {
        var first = new WalletSessionBrick(); first.Load(500, 1);
        first.BeginEscape(50); first.Confirm(550, 2, first.SettlementId);
        var next = new WalletSessionBrick(); next.Load(first.Balance, first.Revision);
        Assert.IsFalse(next.Escaping); Assert.IsFalse(next.Settled);
        next.Credit(20); Assert.IsTrue(next.TrySpend(10));
        Assert.IsTrue(next.BeginEscape(100));
        Assert.AreNotEqual(first.SettlementId, next.SettlementId);
        Assert.AreEqual(660, next.Balance);
        next.Confirm(660, 3, next.SettlementId);
        Assert.IsTrue(next.Settled);
    }
}
