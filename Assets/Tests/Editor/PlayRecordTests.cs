using System;
using System.Linq;
using EchoZone.Heist;
using Newtonsoft.Json;
using NUnit.Framework;

/// <summary>전적 누적·복원·완료 목록과 영수증 분리를 순수 데이터로 검증합니다.</summary>
public sealed class PlayRecordTests
{
    private const string StartUtc = "2026-10-14T00:00:00Z";
    private const string EndUtc = "2026-10-14T01:00:00Z";
    private static PlayRecordBrick Round(string player = "player") => PlayRecordBrick.Start(player, "world", "run", "session", StartUtc);
    private static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
    private static SettlementRequest Request(long revision, string player = "player")
    {
        var summary = Round(player).Freeze(50, EndUtc);
        return new SettlementRequest { PlayerId = player, SessionId = "session", RunId = "run", SettlementId = Guid.NewGuid().ToString("N"),
            ExpectedRevision = revision, Balance = revision + 100, PlayRecord = summary, RoundId = summary.RoundId, PlayRecordHash = summary.Fingerprint() };
    }

    [Test] public void ConfirmedActionsDeduplicateAndTrackSeparateMoneyKinds()
    {
        var round = Round();
        round.Theft("a", 1, 100); round.Theft("a", 1, 100); round.Theft("b", 1, 30); round.Theft("c", 2, 20);
        round.Kill("p", true); round.Kill("p", true); round.Kill("npc", false);
        round.Report("r"); round.Report("r"); round.Reward("b", 20, true); round.Reward("b", 20, true); round.Reward("r", 5, false);
        var r = round.Freeze(50, EndUtc);
        Assert.AreEqual(150, r.StolenMoney); Assert.AreEqual(2, r.RobbedBuildings);
        Assert.AreEqual(1, r.PlayerKills); Assert.AreEqual(1, r.PoliceKills); Assert.AreEqual(1, r.Reports);
        Assert.AreEqual(20, r.BountyMoney); Assert.AreEqual(5, r.ReportRewardMoney); Assert.AreEqual(50, r.ExtractedCargo);
        round.Theft("late", 3, 999); Assert.AreEqual(150, round.State.Summary.StolenMoney);
    }

    [Test] public void DeathRespawnReconnectAndRepeatedSnapshotKeepOneRound()
    {
        var wallet = new WalletSessionBrick(); var round = wallet.EnsurePlayRecord("player", "world", "run", "session", StartUtc);
        round.Health(true); round.Health(true); round.Theft("a", 1, 20);
        string id = round.State.Summary.RoundId;
        var restored = WalletSessionBrick.Restore(wallet.Export());
        restored = WalletSessionBrick.Restore(restored.Export());
        restored.PlayRecord.Health(true); restored.PlayRecord.Health(false); restored.PlayRecord.Health(true);
        restored.PlayRecord.Theft("a", 1, 20);
        Assert.AreEqual(id, restored.PlayRecord.State.Summary.RoundId); Assert.AreEqual(2, restored.PlayRecord.State.Summary.Deaths);
        Assert.AreEqual(20, restored.PlayRecord.State.Summary.StolenMoney);
    }

    [Test] public void OldSnapshotStartsOnlyNewEventsAndReentryGetsNewRound()
    {
        var old = WalletSessionBrick.Restore("{\"loaded\":true,\"balance\":12,\"revision\":3}");
        Assert.IsNull(old.PlayRecord);
        var first = old.EnsurePlayRecord("player", "world", "run", "session", StartUtc);
        Assert.AreEqual(0, first.State.Summary.StolenMoney);
        var next = new WalletSessionBrick().EnsurePlayRecord("player", "world", "run", "session", EndUtc);
        Assert.AreNotEqual(first.State.Summary.RoundId, next.State.Summary.RoundId);
    }

    [Test] public void DisconnectedRestoringAndFrozenTimeDoesNotIncrease()
    {
        var round = Round(); round.Tick(2, true); round.Tick(100, false); round.Tick(double.NaN, true);
        round.Freeze(0, EndUtc); round.Tick(50, true); Assert.AreEqual(2, round.State.Summary.PlayedSeconds);
    }

    [Test] public void FailedMoneySettlementDoesNotPublishHistory()
    {
        var wallet = new SettlementWalletRecord(); var history = new PlayHistoryData();
        SettlementJournalBrick.Prepare(wallet, Request(0), StartUtc);
        Assert.IsFalse(PlayHistoryBrick.StageCommitted(wallet, history, "player")); Assert.IsEmpty(history.Records);
    }

    [Test] public void HistoryRetryAfterMoneySuccessDoesNotPayAgainAndCompactsOnlySummary()
    {
        var wallet = new SettlementWalletRecord(); var request = Request(0);
        SettlementJournalBrick.Prepare(wallet, Copy(request), StartUtc); SettlementJournalBrick.Commit(wallet, EndUtc);
        var history = new PlayHistoryData(); var failedWallet = Copy(wallet); var failedHistory = Copy(history);
        PlayHistoryBrick.StageCommitted(failedWallet, failedHistory, "player"); // 저장 직전 실패: 복사본은 폐기
        Assert.IsNotNull(wallet.Receipts[0].Request.PlayRecord);
        PlayHistoryBrick.StageCommitted(wallet, history, "player");
        SettlementJournalBrick.Prepare(wallet, request, EndUtc); SettlementJournalBrick.Commit(wallet, EndUtc);
        Assert.IsFalse(PlayHistoryBrick.StageCommitted(wallet, history, "player"));
        Assert.AreEqual(100, wallet.Balance); Assert.AreEqual(1, wallet.Revision); Assert.AreEqual(1, history.Records.Count);
        Assert.IsNull(wallet.Receipts[0].Request.PlayRecord); Assert.IsTrue(wallet.Receipts[0].PlayRecordSaved);
        request.PlayRecord.Deaths++; request.PlayRecordHash = request.PlayRecord.Fingerprint();
        Assert.Throws<InvalidOperationException>(() => SettlementJournalBrick.Prepare(wallet, request, EndUtc));
    }

    [Test] public void RetentionIsPerAccountAndOldReplayCannotResurrectOrDeleteRecent()
    {
        var wallet = new SettlementWalletRecord(); var history = new PlayHistoryData(); var first = Request(0);
        for (int i = 0; i < 101; i++)
        {
            SettlementJournalBrick.Prepare(wallet, Copy(i == 0 ? first : Request(i)), StartUtc);
            SettlementJournalBrick.Commit(wallet, EndUtc); PlayHistoryBrick.StageCommitted(wallet, history, "player");
        }
        Assert.AreEqual(100, history.Records.Count); Assert.AreEqual(101, wallet.Receipts.Count);
        Assert.IsFalse(history.Records.Any(r => r.Summary.RoundId == first.RoundId));
        SettlementJournalBrick.Prepare(wallet, first, EndUtc);
        Assert.IsFalse(PlayHistoryBrick.StageCommitted(wallet, history, "player"));
        Assert.AreEqual(101, wallet.Revision); Assert.AreEqual(2, history.Records.Min(r => r.Sequence));
        var recent = PlayHistoryBrick.Recent(history); Assert.AreEqual(20, recent.Length); Assert.AreEqual(101, recent[0].Sequence);
        var other = new SettlementWalletRecord(); var otherHistory = new PlayHistoryData();
        SettlementJournalBrick.Prepare(other, Request(0, "other"), StartUtc); SettlementJournalBrick.Commit(other, EndUtc);
        PlayHistoryBrick.StageCommitted(other, otherHistory, "other");
        Assert.AreEqual(1, otherHistory.Records.Count); Assert.AreEqual(100, history.Records.Count);
    }

    [Test] public void NewSettlementCannotReuseRoundOrSubmitOnlyHash()
    {
        var wallet = new SettlementWalletRecord(); var request = Request(0);
        SettlementJournalBrick.Prepare(wallet, Copy(request), StartUtc); SettlementJournalBrick.Commit(wallet, EndUtc);
        request.SettlementId = Guid.NewGuid().ToString("N"); request.ExpectedRevision = 1;
        Assert.Throws<InvalidOperationException>(() => SettlementJournalBrick.Prepare(wallet, request, EndUtc));
        var empty = Request(1); empty.PlayRecord = null;
        Assert.Throws<InvalidOperationException>(() => SettlementJournalBrick.Prepare(wallet, empty, EndUtc));
    }

    [Test] public void SnapshotKeepsFrozenPayloadIndependentOfNextRound()
    {
        var wallet = new WalletSessionBrick(); wallet.Load(0, 0);
        var round = wallet.EnsurePlayRecord("player", "world", "run", "session", StartUtc);
        round.Theft("a", 1, 80); wallet.BeginEscape(50); var record = round.Freeze(50, EndUtc);
        wallet.BindRequest(new SettlementRequest { PlayerId = "player", SessionId = "session", RunId = "run",
            SettlementId = wallet.SettlementId, Balance = wallet.Balance, PlayRecord = record, RoundId = record.RoundId, PlayRecordHash = record.Fingerprint() });
        var restored = WalletSessionBrick.Restore(wallet.Export());
        Assert.IsTrue(restored.Request.SamePayload(wallet.Request)); Assert.IsTrue(restored.PlayRecord.State.Frozen);
        Assert.AreEqual(80, restored.Request.PlayRecord.StolenMoney);
    }

    [Test] public void QueryRejectsDuplicateLateClosedAndDifferentAccountResponses()
    {
        var query = new PlayHistoryQueryBrick(); query.Open("a"); Assert.IsTrue(query.TryBegin(out int old));
        Assert.IsFalse(query.TryBegin(out _)); Assert.IsFalse(query.Accepts(old, "b"));
        query.Close(); Assert.IsFalse(query.Accepts(old, "a")); query.Open("b"); query.TryBegin(out int current);
        query.Complete(old, "a"); Assert.IsTrue(query.Busy); query.Complete(current, "b"); Assert.IsFalse(query.Busy);
    }
}
