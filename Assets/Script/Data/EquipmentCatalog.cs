using System;
using EchoZone.Combat;
using UnityEngine;

namespace EchoZone.Equipment
{
    /// <summary>인벤토리에서 분리해 장착할 대상 부위를 구분합니다.</summary>
    public enum EquipmentKind { Weapon, Armor }

    /// <summary>아이템과 실제 전투 설정 및 상점 가격을 연결하는 장비 정의입니다.</summary>
    [Serializable]
    public sealed class EquipmentDefinition
    {
        /// <summary>장비 한 개가 한 슬롯을 차지하도록 최대 중첩 수가 1인 아이템입니다.</summary>
        public ItemData item;
        /// <summary>총기 슬롯 또는 공통 방어구 슬롯 중 장착 위치입니다.</summary>
        public EquipmentKind kind;
        /// <summary>플레이어용 총기의 발사 규칙과 외형입니다.</summary>
        public WeaponFireConfig weapon;
        /// <summary>방어구 단계로 표시할 숫자입니다.</summary>
        [Range(1, 3)] public int armorLevel = 1;
        /// <summary>탄환 피해 중 방어구가 차단할 비율입니다.</summary>
        [Range(0, 1)] public float damageReduction = 0.35f;
        /// <summary>상점에서 한 개를 구매할 때 차감할 게임 잔액입니다.</summary>
        [Min(0)] public int price = 100;
    }

    /// <summary>장비·방어구·탄약 판매 수치를 Inspector에서 관리합니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Equipment/Catalog")]
    public sealed class EquipmentCatalog : ScriptableObject
    {
        /// <summary>아이템 ID별 장착 규칙과 가격 목록입니다.</summary>
        [SerializeField] private EquipmentDefinition[] definitions = Array.Empty<EquipmentDefinition>();
        /// <summary>상점에서 판매할 기존 중첩형 탄약 아이템입니다.</summary>
        [SerializeField] private ItemData ammunition;
        /// <summary>탄약 1회 구매 묶음 수량입니다.</summary>
        [SerializeField, Min(1)] private int ammunitionBundle = 60;
        /// <summary>저장 상태가 없는 신규 플레이어가 처음 보유할 예비 탄약 수량입니다.</summary>
        [SerializeField, Min(0)] private int startingAmmunition = 30;
        /// <summary>탄약 한 묶음의 가격입니다.</summary>
        [SerializeField, Min(0)] private int ammunitionPrice = 30;
        /// <summary>상점 요청을 승인할 최대 거리입니다.</summary>
        [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
        /// <summary>구매 기준 금액 중 상점 판매로 돌려받는 백분율입니다.</summary>
        [SerializeField, Range(0, 100)] private int resalePercent = 50;

        /// <summary>상점 판매 수입에 적용할 비율입니다.</summary>
        public int ResalePercent => resalePercent;

        /// <summary>판매 목록을 읽기 전용으로 제공합니다.</summary>
        public System.Collections.Generic.IReadOnlyList<EquipmentDefinition> Definitions => definitions;
        /// <summary>판매하는 탄약 정의입니다.</summary>
        public ItemData Ammunition => ammunition;
        /// <summary>한 묶음의 탄약 수량입니다.</summary>
        public int AmmunitionBundle => ammunitionBundle;
        /// <summary>신규 플레이어가 기본 총과 함께 받는 예비 탄약 수량입니다.</summary>
        public int StartingAmmunition => startingAmmunition;
        /// <summary>한 묶음의 탄약 가격입니다.</summary>
        public int AmmunitionPrice => ammunitionPrice;
        /// <summary>서버가 상점 접근을 검증할 거리입니다.</summary>
        public float InteractionDistance => interactionDistance;

        /// <summary>중복·잘못된 정의를 허용하지 않고 아이템 ID에 해당하는 장비를 찾습니다.</summary>
        public bool TryGet(string itemId, out EquipmentDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(itemId)) return false;
            foreach (var candidate in definitions)
            {
                if (candidate?.item == null || candidate.item.ItemId != itemId) continue;
                if (definition != null || candidate.item.MaxStackSize != 1 || candidate.price < 0) return false;
                if (candidate.kind == EquipmentKind.Weapon &&
                    (candidate.weapon == null || candidate.weapon.InfiniteReserveAmmunition)) return false;
                if (candidate.kind == EquipmentKind.Armor &&
                    (float.IsNaN(candidate.damageReduction) || candidate.damageReduction < 0 || candidate.damageReduction > 1)) return false;
                definition = candidate;
            }
            return definition != null;
        }
    }
}
