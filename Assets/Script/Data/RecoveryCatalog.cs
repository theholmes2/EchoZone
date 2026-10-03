using System;
using System.Collections.Generic;
using EchoZone.Combat;
using EchoZone.Pet;
using UnityEngine;

/// <summary>인벤토리와 별개로 펫/장착 무기의 안정적인 복구 종류 ID를 해석합니다.</summary>
[CreateAssetMenu(menuName = "EchoZone/Recovery Catalog")]
public sealed class RecoveryCatalog : ScriptableObject
{
    /// <summary>Config/Prefab 변경 시 올리는 콘텐츠 버전입니다. 진행 중 Run과 다른 버전은 복원을 거절합니다.</summary>
    public int Version = 1;
    /// <summary>펫 종류의 기본 프리팹과 동작 설정입니다.</summary>
    public PetDefinition[] Pets = Array.Empty<PetDefinition>();
    /// <summary>장착 무기의 기본 외형/발사 규칙입니다. Config가 기본 WeaponPrefab을 제공합니다.</summary>
    public WeaponDefinition[] Weapons = Array.Empty<WeaponDefinition>();
    /// <summary>빌드에 포함된 단일 카탈로그를 사용합니다. 누락은 기본 종류로 대체하지 않습니다.</summary>
    public static RecoveryCatalog Load()
    {
        var catalog = Resources.Load<RecoveryCatalog>("RecoveryCatalog");
        if (catalog == null) throw new InvalidOperationException("RecoveryCatalog is missing.");
        catalog.Validate(); return catalog;
    }
    /// <summary>모든 등록의 ID·프리팹·설정을 실제 사용 전에 검증합니다.</summary>
    public void Validate()
    {
        if (Version < 1 || Pets == null || Weapons == null) throw new InvalidOperationException("Invalid recovery catalog.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in Pets)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id) || p.Prefab == null || p.Follow == null || p.Behaviour == null)
                throw new InvalidOperationException("Invalid/duplicate pet definition.");
            if (p.Prefab.DefinitionId != p.Id || p.Prefab.BehaviourConfig != p.Behaviour ||
                p.Prefab.GetComponent<PetFollowGlue>()?.FollowConfig != p.Follow)
                throw new InvalidOperationException("Pet definition prefab/config mismatch: " + p.Id);
        }
        foreach (var w in Weapons)
            if (w == null || string.IsNullOrWhiteSpace(w.Id) || System.Text.Encoding.UTF8.GetByteCount(w.Id) > 120 || !ids.Add(w.Id) || w.Config == null ||
                w.Config.WeaponPrefab == null || w.Config.ProjectilePrefab == null)
                throw new InvalidOperationException("Invalid/duplicate weapon definition.");
        var configs = new HashSet<WeaponFireConfig>();
        foreach (var w in Weapons) if (!configs.Add(w.Config)) throw new InvalidOperationException("Ambiguous weapon config.");
    }
    /// <summary>種類 정보가 없던 구형 Snapshot도 묵시적으로 현재 종류를 선택하지 않습니다.</summary>
    public void RequireVersion(int version)
    { Validate(); if (version != Version) throw new InvalidOperationException("Recovery catalog version mismatch; legacy snapshots require explicit conversion."); }
    /// <summary>고정 펫 ID로 정확한 기본 프리팹과 설정을 찾습니다.</summary>
    public PetDefinition Pet(string id)
    { Validate(); foreach (var p in Pets) if (!string.IsNullOrWhiteSpace(id) && p.Id == id) return p; throw new InvalidOperationException("Unknown pet DefinitionId: " + id); }
    /// <summary>고정 무기 ID로 발사 Config와 기본 외형을 찾습니다.</summary>
    public WeaponDefinition Weapon(string id)
    { Validate(); foreach (var w in Weapons) if (!string.IsNullOrWhiteSpace(id) && w.Id == id) return w; throw new InvalidOperationException("Unknown weapon DefinitionId: " + id); }
    /// <summary>현재 설정을 저장용 ID로 변환하며 등록되지 않은 설정은 거절합니다.</summary>
    public string WeaponId(WeaponFireConfig config)
    { Validate(); foreach (var w in Weapons) if (w.Config == config) return w.Id; throw new InvalidOperationException("Unregistered weapon config."); }
}

/// <summary>개체 PetId와 구분되는 펫 종류 정의입니다.</summary>
[Serializable] public sealed class PetDefinition
{
    /// <summary>이름 변경과 무관하게 유지하는 직접 지정 ID입니다.</summary>
    public string Id;
    /// <summary>NGO에 등록된 해당 종류의 기본 프리팹입니다.</summary>
    public PetStateGlue Prefab;
    /// <summary>프리팹에 연결되어 있어야 할 추종 설정입니다.</summary>
    public PetFollowConfig Follow;
    /// <summary>프리팹에 연결되어 있어야 할 행동 설정입니다.</summary>
    public PetBehaviourConfig Behaviour;
}
/// <summary>권총/소총 등 장착 종류와 발사 설정 연결입니다.</summary>
[Serializable] public sealed class WeaponDefinition
{
    /// <summary>직접 지정한 안정적인 종류 ID입니다.</summary>
    public string Id;
    /// <summary>기본 WeaponPrefab과 투사체를 포함하는 종류 설정입니다.</summary>
    public WeaponFireConfig Config;
}
