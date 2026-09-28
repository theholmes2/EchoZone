using System;
using System.Collections.Generic;
using EchoZone.Enemy;
using EchoZone.Heist;
using EchoZone.Online.Migration;
using NUnit.Framework;
using UnityEngine;

/// <summary>확장 마이그레이션의 원장·타이머·직렬화·목적지 순수 규칙 회귀 검사입니다.</summary>
public sealed class ExtendedMigrationTests
{
    [Test] public void LedgerRoundTripReturnsOnlyOnce()
    {
        var first = new HeistLedgerBrick(); first.Record(1, "pet-fixed", "thief-account", 1000);
        var restored = new HeistLedgerBrick(); restored.Restore(first.Export());
        Assert.AreEqual(1000, restored.Cargo("pet-fixed"));
        Assert.AreEqual(1000, restored.Return("pet-fixed", "reporter", .05f, out var reward)[1]);
        Assert.AreEqual(50, reward);
        Assert.IsEmpty(restored.Return("pet-fixed", "reporter", .05f, out reward)); Assert.AreEqual(0, reward);
    }
    [Test] public void CarrierCannotReceiveReportRewardAfterMigration()
    {
        var ledger = new HeistLedgerBrick(); ledger.Record(1, "pet", "thief", 1000); ledger.Acquired("pet", "carrier");
        var restored = new HeistLedgerBrick(); restored.Restore(ledger.Export());
        restored.Return("pet", "carrier", .05f, out int reward); Assert.AreEqual(0, reward);
    }
    [Test] public void ExtractedLootDoesNotReappearAfterSerialization()
    {
        var ledger = new HeistLedgerBrick(); ledger.Record(1, "pet", "thief", 1000); ledger.Extract("pet");
        var restored = new HeistLedgerBrick(); restored.Restore(ledger.Export());
        Assert.AreEqual(0, restored.Cargo("pet")); Assert.AreEqual(0, restored.Extract("pet"));
        Assert.IsEmpty(restored.Return("pet", "reporter", .05f, out _));
    }
    [Test] public void InvestigationUsesRemainingTimeNotOldClock()
    {
        var first = new PoliceInvestigationBrick(); first.Schedule("thief", 100, 15);
        var next = new PoliceInvestigationBrick(); next.Restore(first.Export(105), 1000);
        next.Tick(1009); Assert.IsFalse(next.IsWanted("thief")); next.Tick(1010); Assert.IsTrue(next.IsWanted("thief"));
    }
    [Test] public void WalletPendingCreditSurvivesMigration()
    {
        var first = new WalletSessionBrick(); first.Credit(50);
        var next = WalletSessionBrick.Restore(first.Export()); next.Load(100, 4); Assert.AreEqual(150, next.Balance);
    }
    [Test] public void CloudReceiptConfirmsSameEscapeWithoutPayingAgain()
    {
        var first = new WalletSessionBrick(); first.Load(100, 4); first.BeginEscape(1000);
        var next = WalletSessionBrick.Restore(first.Export()); next.VerifyCloud(1100, 5, first.SettlementId);
        Assert.IsTrue(next.Settled); Assert.AreEqual(1100, next.Balance); Assert.IsFalse(next.BeginEscape(1000));
    }
    [Test] public void UnknownNewCloudRevisionFailsClosed()
    {
        var wallet = new WalletSessionBrick(); wallet.Load(100, 4);
        Assert.Throws<InvalidOperationException>(() => wallet.VerifyCloud(500, 5, "different"));
        Assert.AreEqual(100, wallet.Balance);
    }
    [Test] public void DestinationReleaseAllowsNextOfficer()
    {
        var reservations = new PoliceDestinationBrick();
        Assert.IsTrue(reservations.TryClaim(1, Vector3.zero, 1));
        Assert.IsFalse(reservations.TryClaim(2, Vector3.right * .5f, 1));
        reservations.Release(1); Assert.IsTrue(reservations.TryClaim(2, Vector3.zero, 1));
    }
    [Test] public void PatrolRemainingWaitSurvivesClockChange()
    {
        var patrol = new PolicePatrolBrick(); patrol.Restore(2, 4, 3, 100);
        Assert.AreEqual(2, patrol.CurrentPointIndex); Assert.IsFalse(patrol.IsWaitComplete(102)); Assert.IsTrue(patrol.IsWaitComplete(103));
    }
    [Test] public void SchemaTwoRoundTripsWorldAndItemTransform()
    {
        var world = new SessionWorldSnapshot { worldId = "world" };
        world.pets.Add(new ActorRecord { id = "pet", position = Vector3.one, rotation = Quaternion.identity });
        var source = new HostMigrationSnapshot("run", 1, null, new[] { new WorldItemMigrationSnapshot("item", "ammo", 2, false, Vector3.one, Quaternion.identity, true) }, world);
        var serializer = new HostMigrationSnapshotJsonSerializer();
        Assert.IsTrue(serializer.TryDeserialize(serializer.Serialize(source), out var next));
        Assert.AreEqual("world", next.World.worldId); Assert.AreEqual("pet", next.World.pets[0].id);
        Assert.IsTrue(next.WorldItems[0].HasTransform); Assert.AreEqual(Vector3.one, next.WorldItems[0].Position);
    }
    [Test] public void SchemaOneRemainsReadableWithoutInventingWorld()
    {
        var serializer = new HostMigrationSnapshotJsonSerializer();
        Assert.IsTrue(serializer.TryDeserialize("{\"schemaVersion\":1,\"runId\":\"old\",\"snapshotVersion\":1,\"players\":[],\"worldItems\":[]}", out var snapshot));
        Assert.IsNull(snapshot.World);
    }
    [Test] public void DuplicateActorIdsRejectedBeforeApplying()
    {
        var world = new SessionWorldSnapshot { worldId = "world" };
        world.pets.Add(new ActorRecord { id = "same" }); world.pets.Add(new ActorRecord { id = "same" });
        Assert.Throws<ArgumentException>(() => SessionWorldSnapshotValidator.Validate(world));
    }
}
