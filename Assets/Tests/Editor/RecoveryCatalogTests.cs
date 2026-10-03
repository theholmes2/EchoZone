using System;
using EchoZone.Combat.Weapon;
using NUnit.Framework;
using UnityEngine;

/// <summary>실제 연결된 복구 카탈로그의 종류 해석·오류 거부·종류별 상태 적용을 검증합니다.</summary>
public sealed class RecoveryCatalogTests
{
    [Test] public void PetDefinitionUsesRegisteredPrefabAndConfigs()
    {
        var catalog = RecoveryCatalog.Load(); var pet = catalog.Pet("pet.dog.basic");
        Assert.AreEqual(pet.Id, pet.Prefab.DefinitionId);
        Assert.AreEqual(pet.Behaviour, pet.Prefab.BehaviourConfig);
        Assert.AreEqual(pet.Follow, pet.Prefab.GetComponent<EchoZone.Pet.PetFollowGlue>().FollowConfig);
    }

    [TestCase("weapon.police.pistol")]
    [TestCase("weapon.police.ak74")]
    [TestCase("weapon.player.default")]
    public void DefinitionIsSelectedBeforeAmmoState(string id)
    {
        var catalog = RecoveryCatalog.Load(); var definition = catalog.Weapon(id);
        var brick = new WeaponFireBrick(); brick.Configure(definition.Config);
        brick.Restore("{\"ammo\":3,\"reloading\":true,\"reload\":2,\"fire\":1,\"sinceFire\":0,\"fired\":true}", 100);
        Assert.AreEqual(3, brick.Ammunition); Assert.IsTrue(brick.IsReloading);
        Assert.AreEqual(id, catalog.WeaponId(definition.Config));
        Assert.IsNotNull(definition.Config.WeaponPrefab);
    }

    [Test] public void UnknownAndEmptyIdsFailClosed()
    {
        var catalog = RecoveryCatalog.Load();
        Assert.Throws<InvalidOperationException>(() => catalog.Pet(""));
        Assert.Throws<InvalidOperationException>(() => catalog.Pet("missing"));
        Assert.Throws<InvalidOperationException>(() => catalog.Weapon(""));
        Assert.Throws<InvalidOperationException>(() => catalog.Weapon("missing"));
    }

    [Test] public void LegacyOrChangedVersionIsRejected()
    {
        var catalog = RecoveryCatalog.Load();
        Assert.Throws<InvalidOperationException>(() => catalog.RequireVersion(0));
        Assert.Throws<InvalidOperationException>(() => catalog.RequireVersion(catalog.Version + 1));
    }

    [Test] public void DuplicateDefinitionIsRejected()
    {
        var copy = UnityEngine.Object.Instantiate(RecoveryCatalog.Load());
        try { copy.Weapons = new[] { copy.Weapons[0], copy.Weapons[0] }; Assert.Throws<InvalidOperationException>(() => copy.Validate()); }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }

    [Test] public void MissingConfigOrPrefabIsRejected()
    {
        var copy = UnityEngine.Object.Instantiate(RecoveryCatalog.Load());
        try
        {
            copy.Weapons = new[] { new WeaponDefinition { Id = "missing.config" } };
            Assert.Throws<InvalidOperationException>(() => copy.Validate());
            copy.Weapons = Array.Empty<WeaponDefinition>();
            copy.Pets = new[] { new PetDefinition { Id = "missing.prefab" } };
            Assert.Throws<InvalidOperationException>(() => copy.Validate());
        }
        finally { UnityEngine.Object.DestroyImmediate(copy); }
    }

    [Test] public void SnapshotKeepsInstanceAndDefinitionSeparate()
    {
        var world = new EchoZone.Online.Migration.SessionWorldSnapshot { catalogVersion = RecoveryCatalog.Load().Version };
        world.pets.Add(new EchoZone.Online.Migration.ActorRecord { id = "instance-A", definitionId = "pet.dog.basic", health = 47 });
        var restored = JsonUtility.FromJson<EchoZone.Online.Migration.SessionWorldSnapshot>(JsonUtility.ToJson(world));
        RecoveryCatalog.Load().RequireVersion(restored.catalogVersion);
        Assert.AreEqual("instance-A", restored.pets[0].id);
        Assert.AreEqual("pet.dog.basic", RecoveryCatalog.Load().Pet(restored.pets[0].definitionId).Id);
        Assert.AreEqual(47, restored.pets[0].health);
    }
}
