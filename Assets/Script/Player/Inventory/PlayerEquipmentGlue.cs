using System;
using System.Collections.Generic;
using EchoZone.Combat.Glue;
using EchoZone.Online.Migration;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Equipment
{
    /// <summary>소유자의 장착 요청을 검증하고 인벤토리·총기·방어구 상태를 연결합니다.</summary>
    [RequireComponent(typeof(PlayerInventory), typeof(NetworkWeaponFireGlue))]
    public sealed class PlayerEquipmentGlue : NetworkBehaviour
    {
        /// <summary>아이템별 장착 위치와 전투 설정을 조회합니다.</summary>
        [SerializeField] private EquipmentCatalog catalog;
        private PlayerInventory inventory;
        private NetworkWeaponFireGlue weapon;
        private PlayerStats stats;
        private readonly NetworkVariable<NetworkInventorySlot> equippedWeapon = new();
        private readonly NetworkVariable<NetworkInventorySlot> equippedArmor = new();
        private readonly NetworkVariable<bool> menuOpen = new();
        private bool localMenuOpen;
        private bool startingInventoryGranted;

        /// <summary>서버가 승인한 현재 총기 장비입니다.</summary>
        public NetworkInventorySlot WeaponSlot => equippedWeapon.Value;
        /// <summary>서버가 승인한 현재 방어구 장비입니다.</summary>
        public NetworkInventorySlot ArmorSlot => equippedArmor.Value;
        /// <summary>장비 목록과 가격을 UI에 제공합니다.</summary>
        public EquipmentCatalog Catalog => catalog;
        /// <summary>현재 총기가 있어야만 발사할 수 있습니다.</summary>
        public bool HasWeapon => !equippedWeapon.Value.ItemId.IsEmpty;
        /// <summary>소유자의 UI 입력을 즉시 차단하고 서버에도 같은 상태를 전달합니다.</summary>
        public bool IsMenuOpen => IsOwner ? localMenuOpen : menuOpen.Value;
        /// <summary>서버에서 검증할 메뉴 사격 차단 상태입니다.</summary>
        public bool ServerMenuOpen => menuOpen.Value;
        /// <summary>장착한 방어구 하나의 피해 감소율입니다.</summary>
        public float DamageReduction => catalog != null && catalog.TryGet(equippedArmor.Value.ItemId.ToString(), out var d)
            ? d.damageReduction : 0;
        /// <summary>UI가 장착 결과에 반응하도록 알립니다.</summary>
        public event Action Changed;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            weapon = GetComponent<NetworkWeaponFireGlue>();
            stats = GetComponent<PlayerStats>();
        }

        /// <summary>장착 복제 이벤트를 등록하고 기존 시작 총기를 개별 장비로 등록합니다.</summary>
        public override void OnNetworkSpawn()
        {
            equippedWeapon.OnValueChanged += OnEquipmentChanged;
            equippedArmor.OnValueChanged += OnEquipmentChanged;
            localMenuOpen = false;
            if (IsServer && catalog != null && equippedWeapon.Value.ItemId.IsEmpty)
            {
                foreach (var d in catalog.Definitions)
                    if (d != null && d.kind == EquipmentKind.Weapon && d.weapon == weapon.CurrentConfig &&
                        d.item != null && catalog.TryGet(d.item.ItemId, out _))
                    {
                        equippedWeapon.Value = new NetworkInventorySlot(d.item.ItemId, 1,
                            Guid.NewGuid().ToString("N"), weapon.ServerMagazineRounds);
                        break;
                    }
            }
            RefreshView();
        }

        /// <summary>디스폰 때 UI 차단과 복제 이벤트 구독을 정리합니다.</summary>
        public override void OnNetworkDespawn()
        {
            EquipmentInventoryView.Instance?.Unbind(this);
            equippedWeapon.OnValueChanged -= OnEquipmentChanged;
            equippedArmor.OnValueChanged -= OnEquipmentChanged;
            localMenuOpen = false;
        }

        /// <summary>저장 상태가 없는 신규 플레이어에게 기본 총의 예비 탄약을 한 번만 지급합니다.</summary>
        public void GrantStartingInventory()
        {
            if (!IsServer || startingInventoryGranted || catalog == null || inventory == null)
            {
                return;
            }

            startingInventoryGranted = true;
            if (catalog.Ammunition != null && catalog.StartingAmmunition > 0)
            {
                inventory.AddUpToCapacity(catalog.Ammunition, catalog.StartingAmmunition);
            }
        }

        /// <summary>기존 중앙 갱신에서 탄창 UI와 메뉴 열림 키를 갱신합니다.</summary>
        public void ManualUpdate()
        {
            if (!IsSpawned) return;
            if (IsServer && HasWeapon && equippedWeapon.Value.MagazineRounds != weapon.ServerMagazineRounds)
            {
                var value = equippedWeapon.Value;
                value.MagazineRounds = weapon.ServerMagazineRounds;
                equippedWeapon.Value = value;
            }
            if (IsOwner && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.iKey.wasPressedThisFrame)
                SetMenuOpen(!localMenuOpen);
            if (IsOwner && stats != null && stats.IsDead && localMenuOpen) SetMenuOpen(false);
            if (IsOwner) EquipmentInventoryView.Instance?.Render(this);
        }

        /// <summary>화면 열림 상태를 소유자만 변경할 수 있습니다.</summary>
        public void SetMenuOpen(bool open)
        {
            if (!IsOwner || !IsSpawned) return;
            localMenuOpen = open;
            SetMenuOpenRpc(open);
            Changed?.Invoke();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SetMenuOpenRpc(bool open) => menuOpen.Value = open;

        /// <summary>화면에 표시됐던 개별 ID를 보내 슬롯 인덱스 변경 경합을 방지합니다.</summary>
        public void RequestEquip(string instanceId)
        {
            if (IsOwner && IsSpawned) EquipRpc(instanceId);
        }

        /// <summary>빈 인벤토리 칸이 있을 때 장비를 해제합니다.</summary>
        public void RequestUnequip(EquipmentKind kind)
        {
            if (IsOwner && IsSpawned) UnequipRpc(kind);
        }

        /// <summary>가격은 보내지 않고 서버 카탈로그의 아이템 ID로 구매를 요청합니다.</summary>
        public void RequestBuy(int shopId, string itemId)
        {
            if (IsOwner && IsSpawned) BuyRpc(shopId, itemId);
        }

        /// <summary>장착 해제된 개별 장비 또는 표시 당시의 예비 탄약 전체를 판매 요청합니다.</summary>
        public void RequestSell(int shopId, string itemId, string instanceId, int quantity)
        {
            if (IsOwner && IsSpawned) SellRpc(shopId, itemId, instanceId, quantity);
        }

        /// <summary>UI와 서버가 같은 카탈로그 기준으로 판매 예상액을 계산합니다. 기본 지급 총기는 판매하지 않습니다.</summary>
        public long SaleQuote(InventorySlot slot)
        {
            if (catalog == null || slot?.Item == null || slot.Quantity <= 0) return 0;
            if (catalog.TryGet(slot.Item.ItemId, out var definition))
                return EchoZone.Heist.ExtractionReturnBrick.SaleCredit(1, 1,
                    definition.kind == EquipmentKind.Weapon ? slot.PurchaseValue : definition.price, catalog.ResalePercent);
            return slot.Item == catalog.Ammunition ? AmmunitionSaleQuote(slot.Quantity) : 0;
        }

        /// <summary>여러 슬롯에 나뉜 예비 탄약 전체의 판매가를 슬롯 최대 수량으로 잘리지 않게 계산합니다.</summary>
        public long AmmunitionSaleQuote(int quantity) => catalog == null ? 0 :
            EchoZone.Heist.ExtractionReturnBrick.SaleCredit(quantity, catalog.AmmunitionBundle,
                catalog.AmmunitionPrice, catalog.ResalePercent);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SellRpc(int shopId, string itemId, string instanceId, int quantity)
        {
            if (!CanChange || quantity <= 0 || !TryGetComponent<EchoZone.Heist.PlayerWalletGlue>(out var wallet)) return;
            var shop = EquipmentShopGlue.Find(shopId);
            if (shop == null || (shop.transform.position - transform.position).sqrMagnitude >
                catalog.InteractionDistance * catalog.InteractionDistance) return;
            InventorySlot candidate = null;
            if (!string.IsNullOrEmpty(instanceId))
            {
                foreach (var slot in inventory.Slots)
                    if (slot?.InstanceId == instanceId && slot.Item != null && slot.Item.ItemId == itemId)
                    { candidate = slot; break; }
                if (candidate == null || quantity != 1 || !catalog.TryGet(itemId, out _)) return;
            }
            else
            {
                if (catalog.Ammunition == null || itemId != catalog.Ammunition.ItemId ||
                    inventory.GetTotalQuantity(catalog.Ammunition) != quantity) return;
            }
            long credit = string.IsNullOrEmpty(instanceId) ? AmmunitionSaleQuote(quantity) : SaleQuote(candidate);
            if (!wallet.CanCreditServer(credit)) return;
            if (!string.IsNullOrEmpty(instanceId))
            {
                if (!inventory.TryExchangeInstance(instanceId, null, out _)) return;
            }
            else if (inventory.RemoveUpToQuantity(catalog.Ammunition, quantity) != quantity) return;
            wallet.CreditServer(credit);
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void BuyRpc(int shopId, string itemId)
        {
            if (!CanChange || !TryGetComponent<EchoZone.Heist.PlayerWalletGlue>(out var wallet) || !wallet.IsWalletLoaded) return;
            var shop = EquipmentShopGlue.Find(shopId);
            if (shop == null || (shop.transform.position - transform.position).sqrMagnitude >
                catalog.InteractionDistance * catalog.InteractionDistance) return;
            if (catalog.TryGet(itemId, out var definition))
            {
                if (!inventory.CanAdd(definition.item, 1)) return;
                if (definition.kind == EquipmentKind.Weapon) RecoveryCatalog.Load().WeaponId(definition.weapon);
                if (!wallet.TrySpendServer(definition.price)) return;
                var item = new InventorySlot(definition.item, 1, Guid.NewGuid().ToString("N"),
                    definition.kind == EquipmentKind.Weapon ? definition.weapon.MagazineCapacity : -1,
                    definition.kind == EquipmentKind.Weapon ? definition.price : 0);
                if (!inventory.TryAddInstance(item)) wallet.CreditServer(definition.price);
            }
            else if (catalog.Ammunition != null && catalog.Ammunition.ItemId == itemId &&
                inventory.CanAdd(catalog.Ammunition, catalog.AmmunitionBundle) && wallet.TrySpendServer(catalog.AmmunitionPrice))
                inventory.AddUpToCapacity(catalog.Ammunition, catalog.AmmunitionBundle);
        }

        private bool CanChange => IsServer && IsSpawned && catalog != null && stats != null && !stats.IsDead &&
            !SessionWorldMigrationGlue.IsRestoring &&
            !(TryGetComponent<EchoZone.Heist.PlayerWalletGlue>(out var wallet) && wallet.IsEscaping);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void EquipRpc(string instanceId)
        {
            if (!CanChange || string.IsNullOrEmpty(instanceId)) return;
            InventorySlot incoming = null;
            foreach (var slot in inventory.Slots)
                if (slot != null && slot.InstanceId == instanceId) { incoming = slot; break; }
            if (incoming?.Item == null || !catalog.TryGet(incoming.Item.ItemId, out var definition)) return;
            if (definition.kind == EquipmentKind.Weapon) RecoveryCatalog.Load().WeaponId(definition.weapon);
            var oldValue = definition.kind == EquipmentKind.Weapon ? equippedWeapon.Value : equippedArmor.Value;
            var oldSlot = ToInventory(oldValue);
            if (!inventory.TryExchangeInstance(instanceId, oldSlot, out var removed)) return;
            var value = new NetworkInventorySlot(removed.Item.ItemId, 1, removed.InstanceId,
                removed.MagazineRounds, removed.PurchaseValue);
            if (definition.kind == EquipmentKind.Weapon)
            {
                weapon.EquipMagazineServer(definition.weapon, removed.MagazineRounds);
                value.MagazineRounds = weapon.ServerMagazineRounds;
                equippedWeapon.Value = value;
            }
            else equippedArmor.Value = value;
            Publish();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void UnequipRpc(EquipmentKind kind)
        {
            if (!CanChange || (kind != EquipmentKind.Weapon && kind != EquipmentKind.Armor)) return;
            var value = kind == EquipmentKind.Weapon ? equippedWeapon.Value : equippedArmor.Value;
            var slot = ToInventory(value);
            if (slot == null || !inventory.TryAddInstance(slot)) return;
            if (kind == EquipmentKind.Weapon) equippedWeapon.Value = default;
            else equippedArmor.Value = default;
            Publish();
        }

        private InventorySlot ToInventory(NetworkInventorySlot value)
        {
            if (value.ItemId.IsEmpty) return null;
            if (catalog == null || !catalog.TryGet(value.ItemId.ToString(), out var definition))
                throw new InvalidOperationException("Unknown equipped item: " + value.ItemId);
            int rounds = definition.kind == EquipmentKind.Weapon ? weapon.ServerMagazineRounds : value.MagazineRounds;
            return new InventorySlot(definition.item, 1, value.InstanceId.ToString(), rounds, value.PurchaseValue);
        }

        private void OnEquipmentChanged(NetworkInventorySlot previous, NetworkInventorySlot current)
        {
            RefreshView();
            Changed?.Invoke();
        }

        private void RefreshView() => weapon?.SetEquipmentVisible(HasWeapon);

        private void Publish() => GetComponent<EchoZone.Online.Reconnect.PlayerSessionCacheReporter>()?.PublishEquipmentSnapshot();

        /// <summary>탈출 시 반환할 코인·예비 탄약·총기를 서버 상태에서 고정하고 환불 총액을 계산합니다.</summary>
        public bool TryBuildExtractionReturns(out EchoZone.Heist.SettlementReturnItem[] returns, out long credit)
        {
            var result = new List<EchoZone.Heist.SettlementReturnItem>();
            credit = 0;
            if (!IsServer || inventory == null || catalog == null)
            {
                returns = Array.Empty<EchoZone.Heist.SettlementReturnItem>();
                return false;
            }

            try
            {
                foreach (var slot in inventory.Slots)
                {
                    if (slot?.Item == null || slot.Quantity <= 0) continue;
                    if (slot.Item is CurrencyItemData currency)
                    {
                        long value = EchoZone.Heist.ExtractionReturnBrick.CoinCredit(slot.Quantity, currency.CreditValue);
                        AddStackReturn(result, slot.Item.ItemId, slot.Quantity, value);
                        credit = checked(credit + value);
                    }
                    else if (slot.Item == catalog.Ammunition)
                    {
                        AddStackReturn(result, slot.Item.ItemId, slot.Quantity, 0);
                    }
                    else if (!string.IsNullOrEmpty(slot.InstanceId) && catalog.TryGet(slot.Item.ItemId, out var definition) &&
                        definition.kind == EquipmentKind.Weapon)
                    {
                        result.Add(new EchoZone.Heist.SettlementReturnItem { ItemId = slot.Item.ItemId, Quantity = 1,
                            InstanceId = slot.InstanceId, Credit = EchoZone.Heist.ExtractionReturnBrick.WeaponCredit(slot.PurchaseValue) });
                        credit = checked(credit + EchoZone.Heist.ExtractionReturnBrick.WeaponCredit(slot.PurchaseValue));
                    }
                }

                for (int i = 0; i < result.Count; i++)
                    if (catalog.Ammunition != null && result[i].ItemId == catalog.Ammunition.ItemId &&
                        string.IsNullOrEmpty(result[i].InstanceId))
                    {
                        result[i].Credit = EchoZone.Heist.ExtractionReturnBrick.AmmunitionCredit(
                            result[i].Quantity, catalog.AmmunitionBundle, catalog.AmmunitionPrice);
                        credit = checked(credit + result[i].Credit);
                    }

                var equipped = equippedWeapon.Value;
                if (!equipped.ItemId.IsEmpty)
                {
                    result.Add(new EchoZone.Heist.SettlementReturnItem { ItemId = equipped.ItemId.ToString(), Quantity = 1,
                        InstanceId = equipped.InstanceId.ToString(), Equipped = true,
                        Credit = EchoZone.Heist.ExtractionReturnBrick.WeaponCredit(equipped.PurchaseValue) });
                    credit = checked(credit + EchoZone.Heist.ExtractionReturnBrick.WeaponCredit(equipped.PurchaseValue));
                }
            }
            catch (OverflowException)
            {
                returns = Array.Empty<EchoZone.Heist.SettlementReturnItem>();
                credit = 0;
                return false;
            }

            returns = result.ToArray();
            return true;
        }

        /// <summary>같은 종류의 중첩 반환 항목을 하나로 합쳐 정산 본문을 작게 유지합니다.</summary>
        private static void AddStackReturn(List<EchoZone.Heist.SettlementReturnItem> result, string itemId, int quantity, long credit)
        {
            int index = result.FindIndex(r => r.ItemId == itemId && string.IsNullOrEmpty(r.InstanceId));
            if (index < 0) result.Add(new EchoZone.Heist.SettlementReturnItem { ItemId = itemId, Quantity = quantity, Credit = credit });
            else { result[index].Quantity = checked(result[index].Quantity + quantity); result[index].Credit = checked(result[index].Credit + credit); }
        }

        /// <summary>Cloud가 확정한 반환 목록이 현재 서버 상태와 같을 때만 코인·탄약·총기를 제거합니다.</summary>
        public bool TryApplyExtractionReturns(IReadOnlyList<EchoZone.Heist.SettlementReturnItem> returns)
        {
            if (!IsServer || inventory == null || catalog == null || returns == null) return false;
            foreach (var entry in returns)
            {
                if (entry == null || entry.Quantity <= 0) return false;
                if (!string.IsNullOrEmpty(entry.InstanceId))
                {
                    NetworkInventorySlot value = equippedWeapon.Value;
                    if (entry.Equipped)
                    {
                        if (value.ItemId.ToString() != entry.ItemId || value.InstanceId.ToString() != entry.InstanceId ||
                            value.PurchaseValue != entry.Credit) return false;
                    }
                    else
                    {
                        InventorySlot found = null;
                        foreach (var slot in inventory.Slots) if (slot?.InstanceId == entry.InstanceId) { found = slot; break; }
                        if (found?.Item == null || found.Item.ItemId != entry.ItemId || found.PurchaseValue != entry.Credit ||
                            !catalog.TryGet(entry.ItemId, out var definition) || definition.kind != EquipmentKind.Weapon) return false;
                    }
                    continue;
                }

                ItemData item = null;
                foreach (var slot in inventory.Slots)
                    if (slot?.Item != null && slot.Item.ItemId == entry.ItemId) { item = slot.Item; break; }
                if (item == null || inventory.GetTotalQuantity(item) < entry.Quantity) return false;
                long expected = item is CurrencyItemData currency
                    ? EchoZone.Heist.ExtractionReturnBrick.CoinCredit(entry.Quantity, currency.CreditValue)
                    : item == catalog.Ammunition
                        ? EchoZone.Heist.ExtractionReturnBrick.AmmunitionCredit(
                            entry.Quantity, catalog.AmmunitionBundle, catalog.AmmunitionPrice)
                        : -1;
                if (expected != entry.Credit) return false;
            }

            foreach (var entry in returns)
            {
                if (entry.Equipped) equippedWeapon.Value = default;
                else if (!string.IsNullOrEmpty(entry.InstanceId))
                {
                    if (!inventory.TryExchangeInstance(entry.InstanceId, null, out _)) return false;
                }
                else
                {
                    ItemData item = null;
                    foreach (var slot in inventory.Slots)
                        if (slot?.Item != null && slot.Item.ItemId == entry.ItemId) { item = slot.Item; break; }
                    if (item == null || inventory.RemoveUpToQuantity(item, entry.Quantity) != entry.Quantity) return false;
                }
            }
            RefreshView();
            Publish();
            return true;
        }

        /// <summary>사망 시 인벤토리와 장착품을 한 상자에 옮기고 성공한 경우에만 소유 상태를 비웁니다.</summary>
        public bool DropOnDeathServer()
        {
            if (!IsServer || !IsSpawned || SessionWorldMigrationGlue.IsRestoring) return false;
            var slots = new System.Collections.Generic.List<InventorySlot>();
            foreach (var slot in inventory.Slots) slots.Add(slot.Copy());
            var gun=ToInventory(equippedWeapon.Value); var armor=ToInventory(equippedArmor.Value);
            if(gun!=null) slots.Add(gun); if(armor!=null) slots.Add(armor);
            if(slots.Count>0 && (LootWorldGlue.Instance==null || !LootWorldGlue.Instance.Spawn(transform.position,slots)))
            {
                Debug.LogError("Death loot spawn failed; items preserved to prevent silent loss.",this);
                return false;
            }
            equippedWeapon.Value=default; equippedArmor.Value=default; menuOpen.Value=false;
            inventory.ReplaceSlots(null); Publish(); return true;
        }

        /// <summary>장착 슬롯을 재접속·마이그레이션용으로 직렬화합니다.</summary>
        public string Capture() => JsonUtility.ToJson(new SavedEquipment {
            weaponItem = equippedWeapon.Value.ItemId.ToString(), weaponInstance = equippedWeapon.Value.InstanceId.ToString(),
            rounds = weapon.ServerMagazineRounds, armorItem = equippedArmor.Value.ItemId.ToString(),
            armorInstance = equippedArmor.Value.InstanceId.ToString(), weaponPurchaseValue = equippedWeapon.Value.PurchaseValue,
            armorPurchaseValue = equippedArmor.Value.PurchaseValue });

        /// <summary>구형 저장은 기본 장비를 유지하고 새 저장의 빈 슬롯은 실제 해제 상태로 복원합니다.</summary>
        public void Restore(string json)
        {
            if (!IsServer || string.IsNullOrEmpty(json)) return;
            var saved = JsonUtility.FromJson<SavedEquipment>(json);
            if (saved == null) throw new InvalidOperationException("Equipment snapshot missing.");
            if (!string.IsNullOrEmpty(saved.weaponItem))
            {
                if (!catalog.TryGet(saved.weaponItem, out var definition) || definition.kind != EquipmentKind.Weapon)
                    throw new InvalidOperationException("Unknown weapon item.");
                // 재접속의 WeaponJson이 복원한 재장전·발사 대기를 지우지 않습니다.
                weapon.ConfigureWeapon(definition.weapon);
            }
            if (!string.IsNullOrEmpty(saved.armorItem) &&
                (!catalog.TryGet(saved.armorItem, out var armor) || armor.kind != EquipmentKind.Armor))
                throw new InvalidOperationException("Unknown armor item.");
            equippedWeapon.Value = string.IsNullOrEmpty(saved.weaponItem) ? default :
                new NetworkInventorySlot(saved.weaponItem, 1, saved.weaponInstance, saved.rounds, saved.weaponPurchaseValue);
            equippedArmor.Value = string.IsNullOrEmpty(saved.armorItem) ? default :
                new NetworkInventorySlot(saved.armorItem, 1, saved.armorInstance, -1, saved.armorPurchaseValue);
            menuOpen.Value = false;
            RefreshView();
        }

        [Serializable] private sealed class SavedEquipment
        {
            public string weaponItem, weaponInstance, armorItem, armorInstance;
            public int rounds, weaponPurchaseValue, armorPurchaseValue;
        }
    }
}
