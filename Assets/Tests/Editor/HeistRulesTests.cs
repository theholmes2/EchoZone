using EchoZone.Heist;
using NUnit.Framework;

/// <summary>도난 발견·현장 적발·중복 체포 및 펫 적재 규칙을 검증합니다.</summary>
public sealed class HeistRulesTests
{
    /// <summary>도난만 발견하면 신원을 알 수 없으며 새 절도만 추가 수색을 발생시킵니다.</summary>
    [Test]
    public void MissingMoney_DoesNotIdentifyThief()
    {
        var ledger = new HeistLedgerBrick();
        ledger.Record(1, 10, 100, 1000);
        Assert.That(ledger.Inspect(1), Is.True);
        Assert.That(ledger.Wanted, Is.Empty);
        Assert.That(ledger.Inspect(1), Is.False);
        ledger.Record(1, 10, 100, 1000);
        Assert.That(ledger.Inspect(1), Is.True);
    }
    /// <summary>수배는 돈 반환으로 풀리지 않으며 체포는 한 번만 성공합니다.</summary>
    [Test]
    public void WitnessedCrime_SurvivesReturn_AndCaptureIsOnce()
    {
        var ledger = new HeistLedgerBrick();
        ledger.Record(1, 10, 100, 1000); ledger.Witness(100);
        ledger.Return(10, null, 0, out _);
        Assert.That(ledger.Wanted.Contains(100), Is.True);
        Assert.That(ledger.Capture(100), Is.True);
        Assert.That(ledger.Capture(100), Is.False);
    }
    /// <summary>절도 완료 전 현장 적발도 수배 원인이 됩니다.</summary>
    [Test]
    public void CaughtBeforeCompletion_CanBeWanted()
    {
        var ledger = new HeistLedgerBrick(); ledger.Witness(100);
        Assert.That(ledger.Wanted.Contains(100), Is.True);
        Assert.That(ledger.Cargo(10), Is.Zero);
    }
    /// <summary>도둑·운반자에게 감사비를 지급하지 않으며 중복 반환을 막습니다.</summary>
    [TestCase(100ul, 0)]
    [TestCase(200ul, 0)]
    [TestCase(300ul, 50)]
    public void ReportReward_RejectsParticipantsAndDuplicates(ulong reporter, int expected)
    {
        var ledger = new HeistLedgerBrick();
        ledger.Record(1, 10, 100, 1000); ledger.Acquired(10, 200);
        var returned = ledger.Return(10, reporter, 0.05f, out int reward);
        Assert.That(returned[1], Is.EqualTo(1000));
        Assert.That(reward, Is.EqualTo(expected));
        Assert.That(ledger.Return(10, reporter, 0.05f, out reward), Is.Empty);
        Assert.That(reward, Is.Zero);
    }
    /// <summary>적재량은 감속시키고 높은 등급의 힘은 감속을 줄입니다.</summary>
    [Test]
    public void LoadSpeed_RespectsWeightAndStrength()
    {
        Assert.That(PetLoadBrick.SpeedMultiplier(0, 5000, 1, 0.65f), Is.EqualTo(1));
        float full = PetLoadBrick.SpeedMultiplier(5000, 5000, 1, 0.65f);
        Assert.That(full, Is.EqualTo(0.35f).Within(0.0001));
        Assert.That(full, Is.LessThan(PetLoadBrick.SpeedMultiplier(2500, 5000, 1, 0.65f)));
        Assert.That(full, Is.LessThan(PetLoadBrick.SpeedMultiplier(5000, 5000, 2, 0.65f)));
    }
}
