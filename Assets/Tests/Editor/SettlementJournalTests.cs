using System;
using EchoZone.Heist;
using Newtonsoft.Json;
using NUnit.Framework;

/// <summary>Cloud와 동일한 순수 정산 규칙을 직렬화 저장소 및 제어된 실패로 검증합니다. 실제 Cloud 호출 테스트는 아닙니다.</summary>
public sealed class SettlementJournalTests
{
    /// <summary>코인·탄약·총기 반환액이 각 정책대로 계산됩니다.</summary>
    [Test] public void ExtractionReturnsUseFaceValueBundleRatioAndRecordedPurchaseValue()
    {
        Assert.AreEqual(1500, ExtractionReturnBrick.CoinCredit(3, 500));
        Assert.AreEqual(15, ExtractionReturnBrick.AmmunitionCredit(30, 60, 30));
        Assert.AreEqual(300, ExtractionReturnBrick.WeaponCredit(300));
        Assert.AreEqual(0, ExtractionReturnBrick.WeaponCredit(0));
    }

    /// <summary>프로세스 메모리와 분리된 저장 결과를 흉내 냅니다.</summary>
    private sealed class Store
    {
        public string Json = JsonConvert.SerializeObject(new SettlementWalletRecord { Balance = 100, Revision = 2 });
        public SettlementWalletRecord Read() => JsonConvert.DeserializeObject<SettlementWalletRecord>(Json);
        public void Save(SettlementWalletRecord value, bool before = false, bool after = false)
        {
            if (before) throw new Exception("Injected pre-write failure");
            Json = JsonConvert.SerializeObject(value);
            if (after) throw new Exception("Injected lost response");
        }
    }

    /// <summary>동일 요청 비교에 사용하는 고정 시험 데이터입니다.</summary>
    private static SettlementRequest Request() => new SettlementRequest { PlayerId = "player", SessionId = "session", RunId = "run",
        SettlementId = "12345678901234567890123456789012", ExpectedRevision = 2, Balance = 150,
        PetIds = new[] { "pet" }, LedgerIds = new[] { "loot" }, Returns = new[] {
            new SettlementReturnItem { ItemId = "currency.coin.bronze", Quantity = 2, Credit = 100 } } };

    [Test] public void FailureBeforePrepareWriteLeavesBalanceAndQueueUnchanged()
    {
        var store = new Store(); var data = store.Read(); SettlementJournalBrick.Prepare(data, Request(), "utc");
        Assert.Throws<Exception>(() => store.Save(data, before: true));
        Assert.IsNull(store.Read().Pending); Assert.AreEqual(100, store.Read().Balance);
    }

    [Test] public void PreparedRequestSurvivesHostMemoryLossAndResume()
    {
        var store = new Store(); var data = store.Read(); SettlementJournalBrick.Prepare(data, Request(), "utc"); store.Save(data);
        var newHost = store.Read(); SettlementJournalBrick.Commit(newHost, "later"); store.Save(newHost);
        Assert.AreEqual(150, store.Read().Balance); Assert.AreEqual(3, store.Read().Revision);
        Assert.IsNull(store.Read().Pending); Assert.AreEqual("Committed", store.Read().Receipts[0].State);
    }

    [Test] public void LostCommitResponseReturnsReceiptWithoutSecondPayment()
    {
        var store = new Store(); var data = store.Read(); SettlementJournalBrick.Prepare(data, Request(), "utc"); store.Save(data);
        data = store.Read(); SettlementJournalBrick.Commit(data, "later");
        Assert.Throws<Exception>(() => store.Save(data, after: true));
        var retry = store.Read(); SettlementJournalBrick.Prepare(retry, Request(), "retry"); SettlementJournalBrick.Commit(retry, "retry");
        Assert.AreEqual(3, retry.Revision); Assert.AreEqual(150, retry.Balance); Assert.AreEqual(1, retry.Receipts.Count);
    }

    [Test] public void DuplicatePrepareKeepsOriginalCreationAndRequest()
    {
        var data = new Store().Read(); SettlementJournalBrick.Prepare(data, Request(), "first");
        SettlementJournalBrick.Prepare(data, Request(), "second"); Assert.AreEqual("first", data.Pending.CreatedAtUtc);
    }

    [TestCase("balance")] [TestCase("revision")] [TestCase("pet")] [TestCase("ledger")] [TestCase("player")] [TestCase("session")] [TestCase("return")]
    public void SameIdWithChangedPayloadIsRejected(string field)
    {
        var data = new Store().Read(); SettlementJournalBrick.Prepare(data, Request(), "utc");
        var changed = Request();
        switch (field)
        {
            case "balance": changed.Balance++; break;
            case "revision": changed.ExpectedRevision++; break;
            case "pet": changed.PetIds = new[] { "different" }; break;
            case "ledger": changed.LedgerIds = new[] { "different" }; break;
            case "player": changed.PlayerId = "different"; break;
            case "session": changed.SessionId = "different"; break;
            case "return": changed.Returns[0].Credit++; break;
        }
        Assert.Throws<InvalidOperationException>(() => SettlementJournalBrick.Prepare(data, changed, "retry"));
        SettlementJournalBrick.Commit(data, "done");
        Assert.Throws<InvalidOperationException>(() => SettlementJournalBrick.Prepare(data, changed, "retry"));
    }

    [Test] public void OldRevisionIsRejectedAndUnknownWriteHoldsPending()
    {
        var data = new Store().Read(); var request = Request(); request.ExpectedRevision = 1;
        Assert.Throws<InvalidOperationException>(() => SettlementJournalBrick.Prepare(data, request, "utc"));
        SettlementJournalBrick.Prepare(data, Request(), "utc"); data.Revision = 4; data.Balance = 999;
        SettlementJournalBrick.Commit(data, "later"); Assert.AreEqual("Conflict", data.Pending.State);
        Assert.AreEqual(999, data.Balance); Assert.AreEqual(4, data.Revision);
    }

    [Test] public void SnapshotRestoresExactRequestAndReceiptConfirmsIt()
    {
        var wallet = new WalletSessionBrick(); wallet.Load(100, 2); wallet.BeginEscape(50);
        var request = Request(); request.SettlementId = wallet.SettlementId; wallet.BindRequest(request);
        var restored = WalletSessionBrick.Restore(wallet.Export()); Assert.IsTrue(request.SamePayload(restored.Request));
        Assert.IsFalse(restored.Settled); restored.VerifyCloud(150, 3, request.SettlementId);
        Assert.IsTrue(restored.Settled); Assert.IsFalse(restored.Finalized);
        restored.MarkReturnsApplied(); restored.MarkFinalized();
        var completed = WalletSessionBrick.Restore(restored.Export());
        Assert.IsTrue(completed.ReturnsApplied); Assert.IsTrue(completed.Finalized);
    }

    [Test] public void CloudSuccessIsRequiredBeforeFinalization()
    {
        var wallet = new WalletSessionBrick(); wallet.Load(100, 2); wallet.BeginEscape(0);
        Assert.Throws<InvalidOperationException>(() => wallet.MarkFinalized());
    }

    [Test] public void LegacyRecordKeepsBalanceWithoutInventingQueue()
    {
        var data = JsonConvert.DeserializeObject<SettlementWalletRecord>("{\"Balance\":1032,\"Revision\":3,\"LastSettlementId\":\"old\"}");
        Assert.AreEqual(1032, data.Balance); Assert.AreEqual(3, data.Revision); Assert.IsNull(data.Pending); Assert.IsEmpty(data.Receipts);
    }

    [Test] public void BackoffIsBounded()
    {
        Assert.AreEqual(5, SettlementJournalBrick.RetryDelay(1, 5, 60));
        Assert.AreEqual(10, SettlementJournalBrick.RetryDelay(2, 5, 60));
        Assert.AreEqual(60, SettlementJournalBrick.RetryDelay(100, 5, 60));
    }
}
