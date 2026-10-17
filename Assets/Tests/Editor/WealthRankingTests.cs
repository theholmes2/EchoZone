using System.Collections.Generic;
using System.Linq;
using EchoZone.Heist;
using NUnit.Framework;

/// <summary>공개 보유금 순위와 개인정보 없는 표시 규칙을 검증합니다.</summary>
public sealed class WealthRankingTests
{
    /// <summary>내림차순, 동점 공동 순위와 본인 행을 구분합니다.</summary>
    [Test] public void OrdersMoneyAndSharesTiedRanks()
    {
        var rows = WealthRankingBrick.Build(new Dictionary<string, long> { { "a", 5 }, { "b", 10 }, { "c", 10 } }, "a");
        Assert.That(rows.Select(r => r.Balance), Is.EqualTo(new long[] { 10, 10, 5 }));
        Assert.That(rows.Select(r => r.Rank), Is.EqualTo(new[] { 1, 1, 3 }));
        Assert.That(rows[2].IsSelf, Is.True);
    }
    /// <summary>점수 하락도 최신 잔액으로 정렬하며 20명까지만 표시합니다.</summary>
    [Test] public void BoundsResultsAndAcceptsLowerBalances()
    {
        var data = Enumerable.Range(0, 30).ToDictionary(i => "p" + i, i => (long)i);
        data["p29"] = 0;
        var rows = WealthRankingBrick.Build(data, "p29");
        Assert.That(rows.Length, Is.EqualTo(20));
        Assert.That(rows[0].Balance, Is.EqualTo(28));
        Assert.That(rows.Any(r => r.IsSelf), Is.False);
    }
    /// <summary>공개 이름은 안정적이고 원본 계정 식별자를 포함하지 않습니다.</summary>
    [Test] public void AliasIsStableAndDoesNotExposeAccount()
    {
        Assert.That(WealthRankingBrick.Alias("secret-login"), Is.EqualTo(WealthRankingBrick.Alias("secret-login")));
        Assert.That(WealthRankingBrick.Alias("secret-login"), Does.Not.Contain("secret-login"));
        Assert.That(WealthRankingBrick.Alias("other"), Is.Not.EqualTo(WealthRankingBrick.Alias("secret-login")));
        Assert.That(WealthRankingBrick.Build(new Dictionary<string,long>(), "self"), Is.Empty);
    }
}
