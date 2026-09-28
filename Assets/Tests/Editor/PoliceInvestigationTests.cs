using System.Collections.Generic;
using EchoZone.Heist;
using NUnit.Framework;

/// <summary>현장 조사 지연과 즉시 수배·체포·범인 식별을 검증합니다.</summary>
public sealed class PoliceInvestigationTests
{
    /// <summary>조사 기한 전에는 무고한 상태이며 기한부터 수배됩니다.</summary>
    [Test]
    public void Investigation_WaitsUntilDeadline()
    {
        var rules = new PoliceInvestigationBrick();
        rules.Schedule("A", 100, 15);
        rules.Tick(114.99); Assert.That(rules.IsWanted("A"), Is.False);
        rules.Tick(115); Assert.That(rules.IsWanted("A"), Is.True);
        Assert.That(rules.IsWanted("B"), Is.False);
    }

    /// <summary>같은 범인의 추가 증거는 이미 진행 중인 조사 기한을 늦추지 않습니다.</summary>
    [Test]
    public void DuplicateEvidence_DoesNotPostpone()
    {
        var rules = new PoliceInvestigationBrick();
        rules.Schedule("A", 100, 15); rules.Schedule("A", 110, 15);
        rules.Tick(115); Assert.That(rules.IsWanted("A"), Is.True);
    }

    /// <summary>피격 목격은 지연을 건너뛰며 체포 후 옛 타이머로 재수배되지 않습니다.</summary>
    [Test]
    public void Witness_IsImmediate_AndCaptureCancelsPending()
    {
        var rules = new PoliceInvestigationBrick();
        rules.Schedule("A", 100, 15); rules.Witness("A");
        Assert.That(rules.IsWanted("A"), Is.True);
        Assert.That(rules.Capture("A"), Is.True);
        Assert.That(rules.Capture("A"), Is.False);
        rules.Tick(200); Assert.That(rules.IsWanted("A"), Is.False);
    }

    /// <summary>조사 중 사망은 수배 보상을 주지 않고 대기 건만 제거합니다.</summary>
    [Test]
    public void DeathBeforeIdentification_CancelsWithoutBounty()
    {
        var rules = new PoliceInvestigationBrick();
        rules.Schedule("A", 100, 15);
        Assert.That(rules.Capture("A"), Is.False);
        rules.Tick(115); Assert.That(rules.IsWanted("A"), Is.False);
    }

    /// <summary>도난 원래 계정을 식별하고 새 운반자는 범인으로 추가하지 않습니다.</summary>
    [Test]
    public void Inspection_IdentifiesOriginalAccountsOnlyOnce()
    {
        var ledger = new HeistLedgerBrick();
        ledger.Record(1, 10, 100, 1000, "A");
        ledger.Record(1, 11, 101, 1000, "B");
        ledger.Record(2, 12, 102, 1000, "C");
        ledger.Acquired(10, 999);
        var ids = new HashSet<string>();
        Assert.That(ledger.Inspect(1, ids), Is.True);
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, ids);
        ids.Clear();
        Assert.That(ledger.Inspect(1, ids), Is.False);
        Assert.That(ids, Is.Empty);
    }

    /// <summary>탈출 정산한 절도도 증거가 남지만 반환한 장물은 새 조사 대상이 아닙니다.</summary>
    [Test]
    public void ExtractedEvidence_Remains_ReturnedEvidenceDoesNot()
    {
        var ledger = new HeistLedgerBrick();
        ledger.Record(1, 10, 100, 1000, "A");
        ledger.Record(1, 11, 101, 1000, "B");
        ledger.Extract(10); ledger.Return(11, null, 0, out _);
        var ids = new HashSet<string>(); ledger.Inspect(1, ids);
        CollectionAssert.AreEquivalent(new[] { "A" }, ids);
    }

    /// <summary>지연 0 설정은 같은 서버 시각에 수배를 확정합니다.</summary>
    [Test]
    public void ZeroDelay_CompletesAtCurrentTime()
    {
        var rules = new PoliceInvestigationBrick();
        rules.Schedule("A", 100, 0); rules.Tick(100);
        Assert.That(rules.IsWanted("A"), Is.True);
    }
}
