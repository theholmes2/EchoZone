using System.Collections.Generic;

namespace EchoZone.Online.Reconnect
{
    /// <summary>임시 캐시에 저장할 아이템 식별자와 수량입니다.</summary>
    public readonly struct CachedInventorySlot
    {
        /// <summary>캐시에 저장할 아이템 슬롯 값을 생성합니다.</summary>
        /// <param name="itemId">중앙 ItemCatalog에서 다시 찾을 아이템 식별자입니다.</param>
        /// <param name="quantity">해당 슬롯의 아이템 수량입니다.</param>
        public CachedInventorySlot(string itemId, int quantity, string instanceId = null, int magazineRounds = -1, int purchaseValue = 0)
        {
            ItemId = itemId;
            Quantity = quantity;
            InstanceId = instanceId ?? string.Empty;
            MagazineRounds = magazineRounds;
            PurchaseValue = System.Math.Max(0, purchaseValue);
        }

        /// <summary>중앙 ItemCatalog에서 다시 찾을 아이템 식별자입니다.</summary>
        public string ItemId { get; }

        /// <summary>해당 슬롯의 아이템 수량입니다.</summary>
        public int Quantity { get; }
        /// <summary>중복 구매한 장비를 구분하는 서버 식별자입니다.</summary>
        public string InstanceId { get; }
        /// <summary>재접속 후에도 보존할 보관 총기의 탄창 잔량입니다.</summary>
        public int MagazineRounds { get; }
        /// <summary>탈출 반환 정산에서 사용할 실제 장비 구매 금액입니다.</summary>
        public int PurchaseValue { get; }
    }

    /// <summary>재접속할 플레이어에게 복원할 서버 권한 인벤토리와 스탯 복사본입니다.</summary>
    public sealed class PlayerSessionSnapshot
    {
        /// <summary>플레이어의 복원 가능한 인벤토리·스탯 복사본을 생성합니다.</summary>
        /// <param name="inventorySlots">아이템 식별자와 수량 목록입니다.</param>
        /// <param name="health">저장할 현재 체력입니다.</param>
        /// <param name="stamina">저장할 현재 기력입니다.</param>
        /// <param name="mana">저장할 현재 마나입니다.</param>
        public PlayerSessionSnapshot(
            List<CachedInventorySlot> inventorySlots,
            int health,
            int stamina,
            int mana)
        {
            InventorySlots = inventorySlots != null
                ? new List<CachedInventorySlot>(inventorySlots)
                : new List<CachedInventorySlot>();
            Health = health;
            Stamina = stamina;
            Mana = mana;
        }

        /// <summary>저장된 인벤토리 슬롯 목록입니다.</summary>
        public IReadOnlyList<CachedInventorySlot> InventorySlots { get; }

        /// <summary>저장된 현재 체력입니다.</summary>
        public int Health { get; }

        /// <summary>저장된 현재 기력입니다.</summary>
        public int Stamina { get; }

        /// <summary>저장된 현재 마나입니다.</summary>
        public int Mana { get; }
        /// <summary>장착 종류와 분리된 탄약/잔여시간 복구 정보입니다.</summary>
        public string WeaponDefinitionId { get; set; }
        /// <summary>복구 시 동일 콘텐츠 설정인지 확인할 버전입니다.</summary>
        public int CatalogVersion { get; set; }
        /// <summary>정의가 아닌 현재 발사 규칙 상태입니다.</summary>
        public string WeaponJson { get; set; }
        /// <summary>인벤토리와 별개인 장착 총기·방어구의 개별 상태입니다.</summary>
        public string EquipmentJson { get; set; }
    }
}
