using System.Collections.Generic;
using EchoZone.Combat.Weapon;
using EchoZone.Online.Migration;
using EchoZone.Online.Reconnect;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>장비 인스턴스 보존과 방어구 감소율 및 구형 저장 호환을 검증합니다.</summary>
public sealed class EquipmentInventoryTests
{
    /// <summary>탈출 후 새 회차만 기본 펫 지급을 다시 허용하고 일반 재접속과 반복 조회는 중복 지급하지 않습니다.</summary>
    [Test] public void StarterPetResetsOnlyWhenCompletedWalletStartsNewVisit()
    {
        var go = new GameObject("StarterPetVisitTest");
        try
        {
            var world = go.AddComponent<EchoZone.Heist.HeistWorldGlue>();
            typeof(Unity.Netcode.NetworkBehaviour).GetProperty("IsServer").SetValue(world, true);
            Assert.IsTrue(world.TryGrantStarter("player"));
            var wallet = world.Wallet("player");
            Assert.IsFalse(world.TryGrantStarter("player"));
            Assert.AreSame(wallet, world.Wallet("player"));
            wallet.Load(100, 0);
            wallet.BeginEscape(0);
            Assert.AreSame(wallet, world.Wallet("player"));
            Assert.IsFalse(world.TryGrantStarter("player"));
            wallet.Confirm(100, 1, wallet.SettlementId);
            wallet.MarkFinalized();
            var next = world.Wallet("player");
            Assert.AreNotSame(wallet, next);
            Assert.IsTrue(world.TryGrantStarter("player"));
            Assert.AreSame(next, world.Wallet("player"));
            Assert.IsFalse(world.TryGrantStarter("player"));
        }
        finally { Object.DestroyImmediate(go); }
    }
    /// <summary>탈출 캐시만 빈 티켓으로 새 입장을 허용하며 정상 재접속과 연결 중 중복 입장 정책을 보존합니다.</summary>
    [TestCase(true, false, "", true, false)]
    [TestCase(true, true, "", false, false)]
    [TestCase(true, false, "old-ticket", false, false)]
    [TestCase(false, false, "", false, false)]
    [TestCase(false, false, "old-ticket", true, true)]
    public void AdmissionSeparatesEscapedAndDisconnectedPlayers(bool escaped, bool connected, string ticket, bool accepted, bool restore)
    {
        var go = new GameObject("AdmissionCacheTest");
        try
        {
            var cache = go.AddComponent<NetworkPlayerSessionCacheGlue>();
            cache.SaveLiveSnapshot("player", new PlayerSessionSnapshot(null, 25, 10, 0) { EscapeFinalized = escaped });
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var entries = (System.Collections.IDictionary)typeof(NetworkPlayerSessionCacheGlue).GetField("cachedStates", fields).GetValue(cache);
            var entry = entries["player"];
            entry.GetType().GetField("IsConnected").SetValue(entry, connected);
            entry.GetType().GetField("Ticket").SetValue(entry, "old-ticket");
            var result = typeof(NetworkPlayerSessionCacheGlue).GetMethod("ValidateCredential", fields)
                .Invoke(cache, new object[] { 1UL, new ReconnectCredential("player", ticket) });
            Assert.AreEqual(accepted, result);
            var authorized = (HashSet<ulong>)typeof(NetworkPlayerSessionCacheGlue).GetField("restoreAuthorizedClientIds", fields).GetValue(cache);
            Assert.AreEqual(restore, authorized.Contains(1UL));
            Assert.AreEqual(!(escaped && accepted), entries.Contains("player"));
        }
        finally { Object.DestroyImmediate(go); }
    }
    /// <summary>실패 로딩이 타이틀을 가린 후 복귀해도 메뉴를 다시 한 번 엽니다.</summary>
    [Test] public void FailedAdmissionReopensTitleAfterOverlay()
    {
        var brick = new TitleReturnBrick();
        Assert.IsTrue(brick.ShouldEnterLocalTitle(true));
        Assert.IsFalse(brick.ShouldEnterLocalTitle(false)); // 로딩이 메뉴를 가립니다.
        Assert.IsFalse(brick.ShouldEnterLocalTitle(false)); // 타이틀 정리 중에도 아직 가려집니다.
        Assert.IsTrue(brick.ShouldEnterLocalTitle(true));
        Assert.IsFalse(brick.ShouldEnterLocalTitle(true));
    }

    /// <summary>탈출 완료 표시는 마이그레이션 왕복 후에도 신규 입장으로 처리되며 미완료 복원과 구분됩니다.</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void EscapeFinalizationSurvivesMigrationAndControlsAdmission(bool finalized)
    {
        var snapshot = new HostMigrationSnapshot("run", 1, new[] {
            new HostMigrationPlayerSnapshot("player", new PlayerSessionSnapshot(null, 25, 10, 0) { EscapeFinalized = finalized }) }, null);
        var serializer = new HostMigrationSnapshotJsonSerializer();
        Assert.IsTrue(serializer.TryDeserialize(serializer.Serialize(snapshot), out var restored));
        Assert.AreEqual(finalized, restored.Players[0].State.EscapeFinalized);
        var go = new GameObject("EscapeCacheTest");
        try
        {
            var cache = go.AddComponent<NetworkPlayerSessionCacheGlue>();
            cache.PrepareHostMigration(restored.Players);
            var validate = typeof(NetworkPlayerSessionCacheGlue).GetMethod("ValidateCredential",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsTrue((bool)validate.Invoke(cache, new object[] { 1UL, new ReconnectCredential("player", "") }));
            var authorized = (HashSet<ulong>)typeof(NetworkPlayerSessionCacheGlue).GetField("restoreAuthorizedClientIds",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(cache);
            Assert.AreEqual(!finalized, authorized.Contains(1UL));
        }
        finally { Object.DestroyImmediate(go); }
    }
    /// <summary>판매는 구매 기준가의 절반이며 묶음 탄약 소수점은 최종 금액에서 버립니다.</summary>
    [TestCase(1, 1, 101, 50, 50L)]
    [TestCase(60, 60, 30, 50, 15L)]
    [TestCase(600, 60, 30, 50, 150L)]
    [TestCase(3, 60, 30, 50, 0L)]
    [TestCase(1, 1, 0, 50, 0L)]
    public void SaleUsesConfiguredPercent(int quantity, int bundle, int price, int percent, long expected)
        => Assert.AreEqual(expected, EchoZone.Heist.ExtractionReturnBrick.SaleCredit(quantity, bundle, price, percent));

    /// <summary>복구·종료 중에는 메뉴를 열지 않고 퇴장 완료 때 한 번만 복구합니다.</summary>
    [Test] public void TitleReturnsOnlyAfterConnectionWorkEnds()
    {
        var brick = new TitleReturnBrick();
        Assert.IsFalse(brick.ShouldReturn(false, false, false, false));
        Assert.IsFalse(brick.ShouldReturn(true, false, false, false));
        Assert.IsFalse(brick.ShouldReturn(false, true, false, false));
        Assert.IsFalse(brick.ShouldReturn(false, false, true, false));
        Assert.IsFalse(brick.ShouldReturn(false, false, false, true));
        Assert.IsTrue(brick.ShouldReturn(false, false, false, false));
        Assert.IsFalse(brick.ShouldReturn(false, false, false, false));
    }
    /// <summary>탈출 복귀 후 참가 메뉴를 매 프레임 덮어쓰지 않으며 다음 탈출에서는 다시 복귀합니다.</summary>
    [Test] public void LocalTitleOpensOncePerReturn()
    {
        var brick = new TitleReturnBrick();
        Assert.IsFalse(brick.ShouldEnterLocalTitle(false));
        Assert.IsFalse(brick.ShouldReturn(true, false, false, false));
        Assert.IsTrue(brick.ShouldEnterLocalTitle(true));
        Assert.IsFalse(brick.ShouldEnterLocalTitle(true));
        Assert.IsFalse(brick.ShouldReturn(false, false, false, false));
        Assert.IsFalse(brick.ShouldEnterLocalTitle(false));
        Assert.IsTrue(brick.ShouldEnterLocalTitle(true));
        Assert.IsFalse(brick.ShouldEnterLocalTitle(true));
    }
    /// <summary>각 방어구는 설정된 단일 감소율만 적용합니다.</summary>
    [TestCase(.35f, 65f)]
    [TestCase(.50f, 50f)]
    [TestCase(.65f, 35f)]
    public void ArmorAppliesSingleReduction(float reduction, float expected)
        => Assert.AreEqual(expected, ArmorDamageBrick.Reduce(100, reduction), .001f);

    /// <summary>잘못된 숫자가 체력으로 전파되지 않습니다.</summary>
    [Test] public void ArmorRejectsNonFiniteDamage()
    {
        Assert.AreEqual(0, ArmorDamageBrick.Reduce(float.NaN, .35f));
        Assert.AreEqual(100, ArmorDamageBrick.Reduce(100, float.NaN));
    }

    /// <summary>같은 종류 총기 두 개의 탄창 상태가 Snapshot 왕복 후에도 분리됩니다.</summary>
    [Test] public void DuplicateGunsKeepTheirMagazinesAcrossMigration()
    {
        var player = new PlayerSessionSnapshot(new List<CachedInventorySlot> {
            new("gun", 1, "first", 3, 100), new("gun", 1, "second", 17, 300) }, 100, 100, 0) { EquipmentJson = "{\"weaponItem\":\"gun\"}" };
        var source = new HostMigrationSnapshot("run", 1,
            new[] { new HostMigrationPlayerSnapshot("player", player) }, null);
        var serializer = new HostMigrationSnapshotJsonSerializer();
        Assert.IsTrue(serializer.TryDeserialize(serializer.Serialize(source), out var restored));
        var slots = restored.Players[0].State.InventorySlots;
        Assert.AreEqual("first", slots[0].InstanceId);
        Assert.AreEqual(3, slots[0].MagazineRounds);
        Assert.AreEqual("second", slots[1].InstanceId);
        Assert.AreEqual(17, slots[1].MagazineRounds);
        Assert.AreEqual(100, slots[0].PurchaseValue);
        Assert.AreEqual(300, slots[1].PurchaseValue);
        Assert.AreEqual(player.EquipmentJson, restored.Players[0].State.EquipmentJson);
    }

    /// <summary>구형 슬롯에 탄창 필드가 없으면 빈 탄창으로 잘못 단정하지 않습니다.</summary>
    [Test] public void LegacySlotsUseUninitializedMagazine()
    {
        var serializer = new HostMigrationSnapshotJsonSerializer();
        Assert.IsTrue(serializer.TryDeserialize("{\"schemaVersion\":2,\"runId\":\"run\",\"snapshotVersion\":1,\"players\":[{\"playerId\":\"p\",\"inventorySlots\":[{\"itemId\":\"gun\",\"quantity\":1}]}]}", out var restored));
        Assert.AreEqual(-1, restored.Players[0].State.InventorySlots[0].MagazineRounds);
        Assert.AreEqual(0, restored.Players[0].State.InventorySlots[0].PurchaseValue);
        Assert.IsFalse(restored.Players[0].State.EscapeFinalized);
    }

    /// <summary>가득 찬 인벤토리에서도 장착 교환은 성공하고 잘못된 재요청은 실패합니다.</summary>
    [Test] public void ExchangeUsesOccupiedSlotAndRejectsStaleRequest()
    {
        var go = new GameObject("InventoryTest");
        var item = ScriptableObject.CreateInstance<ItemData>();
        var config = ScriptableObject.CreateInstance<InventoryConfig>();
        try
        {
            var inventory = go.AddComponent<PlayerInventory>();
            var configData = new SerializedObject(config);
            configData.FindProperty("maxSlots").intValue = 1;
            configData.ApplyModifiedPropertiesWithoutUndo();
            var serialized = new SerializedObject(inventory);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(inventory.TryAddInstance(new InventorySlot(item, 1, "new", 4)));
            Assert.IsFalse(inventory.TryAddInstance(new InventorySlot(item, 1, "new", 4)));
            Assert.IsFalse(inventory.TryAddInstance(new InventorySlot(item, 1, "extra", 4)));
            Assert.IsTrue(inventory.TryExchangeInstance("new", new InventorySlot(item, 1, "old", 9), out var removed));
            Assert.AreEqual(4, removed.MagazineRounds);
            Assert.AreEqual(9, inventory.Slots[0].MagazineRounds);
            Assert.IsFalse(inventory.TryExchangeInstance("new", null, out _));
            Assert.AreEqual(1, inventory.Slots.Count);
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(item); Object.DestroyImmediate(config); }
    }

    /// <summary>탄창이 다르면 네트워크 슬롯 변경으로 인식합니다.</summary>
    [Test] public void NetworkEqualityIncludesMagazineAndIdentity()
    {
        var source=new NetworkInventorySlot("gun",1,"first",3,100);
        Assert.IsTrue(source.Equals(new NetworkInventorySlot("gun",1,"first",3,100)));
        Assert.IsFalse(source.Equals(new NetworkInventorySlot("gun",1,"first",2)));
        Assert.IsFalse(source.Equals(new NetworkInventorySlot("gun",1,"second",3)));
        Assert.IsFalse(source.Equals(new NetworkInventorySlot("gun",1,"first",3,0)));
    }

    /// <summary>장비 교환은 잔량을 보존하고 교환 직후 발사를 허용하지 않습니다.</summary>
    [Test] public void EquipPreservesRoundsAndAddsFireDelay()
    {
        var config=ScriptableObject.CreateInstance<EchoZone.Combat.WeaponFireConfig>();
        try
        {
            var brick=new WeaponFireBrick(); brick.Configure(config); brick.RestoreMagazine(3); brick.DelayAfterEquip(10);
            Assert.AreEqual(3,brick.Ammunition); Assert.IsFalse(brick.CanFire(10));
            Assert.IsTrue(brick.CanFire(10+config.FireIntervalSeconds));
        }
        finally { Object.DestroyImmediate(config); }
    }
}
