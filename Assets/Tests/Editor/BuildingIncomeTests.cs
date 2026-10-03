using EchoZone.Heist;
using EchoZone.Online.Migration;
using NUnit.Framework;
using UnityEngine;

/// <summary>건물 주기 수입과 저장 호환성의 결정적 검사입니다.</summary>
public sealed class BuildingIncomeTests
{
    [Test] public void BeforeDueDoesNotPay()
    { Assert.AreEqual(100, BuildingIncomeBrick.Advance(100,29,30,30,500,20000,out var next)); Assert.AreEqual(30,next); }
    [Test] public void DuePaysOnce()
    { Assert.AreEqual(600, BuildingIncomeBrick.Advance(100,30,30,30,500,20000,out var next)); Assert.AreEqual(60,next); }
    [Test] public void DelayedUpdateCatchesUpWithoutLoop()
    { Assert.AreEqual(1600, BuildingIncomeBrick.Advance(100,95,30,30,500,20000,out var next)); Assert.AreEqual(120,next); }
    [Test] public void CapDoesNotBankUnpaidIncome()
    { Assert.AreEqual(20000, BuildingIncomeBrick.Advance(19900,95,30,30,500,20000,out var next)); Assert.AreEqual(120,next); }
    [Test] public void ReturnedMoneyAboveCapIsNotDestroyed()
    { Assert.AreEqual(23000, BuildingIncomeBrick.Advance(23000,30,30,30,500,20000,out _)); }
    [Test] public void ZeroAmountDisablesIncome()
    { Assert.AreEqual(100, BuildingIncomeBrick.Advance(100,30,30,30,0,20000,out _)); }
    [Test] public void LargeElapsedTimeDoesNotOverflowMoney()
    { Assert.AreEqual(int.MaxValue, BuildingIncomeBrick.Advance(100,1e15,30,30,int.MaxValue,int.MaxValue,out _)); }
    [Test] public void RemainingTimerRoundTripsAndOldRecordDefaults()
    {
        var record=JsonUtility.FromJson<BuildingRecord>(JsonUtility.ToJson(new BuildingRecord{hasIncomeTimer=true,incomeRemaining=12}));
        Assert.IsTrue(record.hasIncomeTimer); Assert.AreEqual(12,record.incomeRemaining);
        Assert.IsFalse(JsonUtility.FromJson<BuildingRecord>("{\"id\":1,\"money\":100}").hasIncomeTimer);
    }
}
