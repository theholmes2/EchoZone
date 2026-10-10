using EchoZone.Pet;
using EchoZone.Enemy;
using EchoZone.Heist;
using EchoZone.Player.View;
using EchoZone.Online.Migration;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

/// <summary>검사 배정·도주 회차·추격 단계·표시 합성의 결정적 회귀 검사입니다.</summary>
public sealed class PolicePetPolicyTests
{
    [Test] public void ThirtySecondsAloneDoesNotCollect()
    { var b = new PetEscapeEpisodeBrick(); b.TryBegin(5,30,20); b.Tick(31,false); Assert.IsFalse(b.CanCollect(5)); }
    [Test] public void FiveEscapesWaitForGraceAndPreventSixth()
    { var b = new PetEscapeEpisodeBrick(); for(int i=0;i<5;i++) Assert.IsTrue(b.TryBegin(5,30,20)); Assert.IsFalse(b.TryBegin(5,30,20)); Assert.IsFalse(b.CanCollect(5)); b.Tick(30,false); Assert.IsTrue(b.CanCollect(5)); }
    [Test] public void ReplanningTicksDoNotIncrementCount()
    { var b = new PetEscapeEpisodeBrick(); b.TryBegin(5,30,20); for(int i=0;i<100;i++) b.Tick(.01f,true); Assert.AreEqual(1,b.Count); }
    [Test] public void FailedPathStopsButPreservesGrace()
    { var b = new PetEscapeEpisodeBrick(); b.TryBegin(5,30,20); b.Tick(20,true); Assert.IsTrue(b.Failed); Assert.IsFalse(b.CanCollect(5)); Assert.IsFalse(b.TryBegin(5,30,20)); b.Tick(10,false); Assert.IsTrue(b.CanCollect(5)); }
    [Test] public void AcquisitionStartsNewEpisode()
    { var b = new PetEscapeEpisodeBrick(); b.TryBegin(5,30,20); b.Tick(10,true); b.Reset(); b.TryBegin(5,30,20); Assert.AreEqual(1,b.Count); Assert.AreEqual(30,b.GraceRemaining); }
    [Test] public void EpisodeRestoresRemainingNotWallClock()
    { var b = new PetEscapeEpisodeBrick(); b.Restore(4,12,6,false); Assert.AreEqual(4,b.Count); Assert.AreEqual(12,b.GraceRemaining); Assert.AreEqual(6,b.FailureRemaining); }
    [Test] public void TravelingDoesNotSpendSearchTime()
    { var b = new PolicePursuitBrick(); b.Begin(30); b.Tick(25,false,false,5); Assert.IsFalse(b.Tick(1,true,false,5)); Assert.AreEqual(5,b.Remaining); Assert.IsTrue(b.Arrived); Assert.IsFalse(b.Tick(4,false,false,5)); Assert.IsTrue(b.Tick(1,false,false,5)); }
    [Test] public void TravelHasIndependentTimeout()
    { var b = new PolicePursuitBrick(); b.Begin(30); Assert.IsTrue(b.Tick(30,false,false,5)); Assert.IsFalse(b.Arrived); }
    [Test] public void PathFailureDoesNotRestartTravel()
    { var b = new PolicePursuitBrick(); b.Begin(30); Assert.IsTrue(b.Tick(0,false,true,5)); b.Begin(99); Assert.AreEqual(30,b.Remaining); }
    [Test] public void PursuitRoundTripKeepsSearchPhase()
    { var b = new PolicePursuitBrick(); b.Restore(true,true,3); Assert.IsTrue(b.Arrived); Assert.IsFalse(b.Tick(2,false,false,5)); Assert.AreEqual(1,b.Remaining); }
    [Test] public void VisibilityReasonsDoNotOverrideEachOther()
    { var b = new HealthBarVisibilityBrick(); b.Set(HealthBarHiddenReason.Interior,true); b.Set(HealthBarHiddenReason.Sight,true); b.Set(HealthBarHiddenReason.Interior,false); Assert.IsFalse(b.Visible); b.Set(HealthBarHiddenReason.Sight,false); Assert.IsTrue(b.Visible); }
    [Test] public void PoolResetClearsVisibilityReasons()
    { var b = new HealthBarVisibilityBrick(); b.Set(HealthBarHiddenReason.Interior,true); b.Reset(); Assert.IsTrue(b.Visible); }
    [Test] public void OverdueDistantBuildingBeatsNewNearbyBuilding()
    { Assert.Less(PoliceInspectionPriorityBrick.Compare(0,100,1,90,1,2,100,30),0); }
    [Test] public void SameAgePrefersShorterRealPath()
    { Assert.Greater(PoliceInspectionPriorityBrick.Compare(90,100,1,90,5,2,100,30),0); }
    [Test] public void PatrolReserveAndDutyCapAreEnforced()
    { Assert.IsFalse(PoliceInspectionPriorityBrick.CanDispatch(1,0,1,2)); Assert.IsFalse(PoliceInspectionPriorityBrick.CanDispatch(3,2,1,2)); Assert.IsTrue(PoliceInspectionPriorityBrick.CanDispatch(2,0,1,2)); }
    [Test] public void HearingRequiresMovementInsideHalfSightDistance()
    {
        var perception = new PolicePerceptionBrick();
        Assert.IsTrue(perception.CanHearMovement(Vector3.zero, new Vector3(7f, 0f, 0f), new Vector3(0.2f, 0f, 0f), 14f, 0.5f, 0.1f));
        Assert.IsFalse(perception.CanHearMovement(Vector3.zero, new Vector3(7.1f, 0f, 0f), new Vector3(0.2f, 0f, 0f), 14f, 0.5f, 0.1f));
        Assert.IsFalse(perception.CanHearMovement(Vector3.zero, new Vector3(6f, 0f, 0f), Vector3.zero, 14f, 0.5f, 0.1f));
    }
    [Test] public void LootWeightsSelectExpectedBoundaries()
    {
        var brick = new EchoZone.Equipment.LootSpawnBrick();
        var weights = new List<float> { 55f, 25f, 15f, 5f };
        Assert.AreEqual(0, brick.SelectWeightedIndex(weights, 0f));
        Assert.AreEqual(1, brick.SelectWeightedIndex(weights, 0.55f));
        Assert.AreEqual(2, brick.SelectWeightedIndex(weights, 0.80f));
        Assert.AreEqual(3, brick.SelectWeightedIndex(weights, 0.95f));
    }
    [Test] public void UnreportedReturnNeverPaysReward()
    { var b = new HeistLedgerBrick(); b.Record(1,"pet","owner",1000); Assert.AreEqual(1000,b.Return("pet",null,.05f,out int reward)[1]); Assert.AreEqual(0,reward); Assert.IsEmpty(b.Return("pet",null,.05f,out reward)); }
    [Test] public void NewMigrationFieldsRoundTripAndOldFieldsDefault()
    {
        var record = new ActorRecord { hasEscapeEpisode=true,escapeCount=5,escapeGraceRemaining=7,escapeFailureRemaining=4,state=(int)PetBehaviourState.CollectionWaiting,
            hasPursuitPhase=true,pursuitActive=true,pursuitArrived=false,pursuitRemaining=13,pursuitStuckRemaining=2 };
        var copy = JsonUtility.FromJson<ActorRecord>(JsonUtility.ToJson(record)); Assert.AreEqual(5,copy.escapeCount); Assert.AreEqual(7,copy.escapeGraceRemaining); Assert.AreEqual(13,copy.pursuitRemaining);
        var old = JsonUtility.FromJson<ActorRecord>("{\"state\":1}"); Assert.IsFalse(old.hasEscapeEpisode); Assert.IsFalse(old.hasPursuitPhase);
    }
}
