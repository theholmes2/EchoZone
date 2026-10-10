using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace EchoZone.Equipment
{
    /// <summary>이름·수량·탄창·가격으로 표시하는 로컬 장비 창입니다.</summary>
    public sealed class EquipmentInventoryView : MonoBehaviour
    {
        /// <summary>기존 중앙 갱신에서 접근할 현재 씬의 창입니다.</summary>
        public static EquipmentInventoryView Instance { get; private set; }
        /// <summary>I 키로 켜고 끄는 패널입니다.</summary>
        [SerializeField] private GameObject panel;
        /// <summary>잔액과 장착 상태를 표시합니다.</summary>
        [SerializeField] private TMP_Text heading;
        /// <summary>인벤토리 버튼을 배치하는 격자입니다.</summary>
        [SerializeField] private Transform inventoryRows;
        /// <summary>상점 판매 버튼을 배치하는 격자입니다.</summary>
        [SerializeField] private Transform shopRows;
        /// <summary>텍스트를 포함한 비활성 버튼 견본입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button rowTemplate;
        /// <summary>총기 장착 해제 버튼입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button unequipWeapon;
        /// <summary>방어구 장착 해제 버튼입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button unequipArmor;
        /// <summary>창 닫기 버튼입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button close;
        /// <summary>인벤토리 클릭 동작을 장착 또는 상점 판매로 전환합니다.</summary>
        [SerializeField] private UnityEngine.UI.Button sellToggle;
        private bool selling;
        private PlayerEquipmentGlue player;
        private string previous;
        private readonly List<UnityEngine.UI.Button> inventoryButtons = new();
        private readonly List<UnityEngine.UI.Button> shopButtons = new();

        private void Awake()
        {
            Instance = this;
            panel.SetActive(false);
            rowTemplate.gameObject.SetActive(false);
            unequipWeapon.onClick.AddListener(() => player?.RequestUnequip(EquipmentKind.Weapon));
            unequipArmor.onClick.AddListener(() => player?.RequestUnequip(EquipmentKind.Armor));
            close.onClick.AddListener(() => player?.SetMenuOpen(false));
            sellToggle?.onClick.AddListener(() => { selling = !selling; previous = null; });
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>디스폰한 소유자의 창과 입력 참조를 정리합니다.</summary>
        public void Unbind(PlayerEquipmentGlue owner)
        {
            if (player != owner) return;
            player = null; previous = null; selling = false; panel.SetActive(false);
        }

        /// <summary>로컬 오너의 변경된 상태만 텍스트와 클릭 콜백으로 반영합니다.</summary>
        public void Render(PlayerEquipmentGlue owner)
        {
            if (owner == null || !owner.IsOwner) return;
            player = owner;
            panel.SetActive(owner.IsMenuOpen);
            if (!owner.IsMenuOpen || owner.Catalog == null) return;
            var inventory = owner.GetComponent<PlayerInventory>();
            var wallet = owner.GetComponent<EchoZone.Heist.PlayerWalletGlue>();
            var shop = EquipmentShopGlue.Nearest(owner.transform.position, owner.Catalog.InteractionDistance);
            if (shop == null) selling = false;
            var key = new StringBuilder().Append(owner.NetworkObjectId).Append('|').Append(wallet?.Balance)
                .Append('|').Append(owner.WeaponSlot.ItemId).Append('|').Append(owner.WeaponSlot.MagazineRounds)
                .Append('|').Append(owner.ArmorSlot.ItemId).Append('|').Append(shop != null ? shop.ShopId : -1)
                .Append('|').Append(selling);
            foreach (var slot in inventory.Slots)
                key.Append('|').Append(slot.Item.ItemId).Append(':').Append(slot.Quantity).Append(':')
                    .Append(slot.InstanceId).Append(':').Append(slot.MagazineRounds).Append(':').Append(slot.PurchaseValue);
            string next = key.ToString();
            if (next == previous) return;
            previous = next;
            heading.text = $"인벤토리 [I] · 잔액 {wallet?.Balance ?? 0}\n총기: {Label(owner, owner.WeaponSlot)}\n방어구: {Label(owner, owner.ArmorSlot)}\n" +
                (shop == null ? "상점 근처에서 구매·판매 가능" : selling
                    ? "판매 모드 · 왼쪽 아이템 클릭 (장착품은 먼저 해제)" : "장착 모드 · 오른쪽 구매 / 판매 전환 버튼");
            if (sellToggle != null)
            {
                sellToggle.interactable = shop != null;
                sellToggle.GetComponentInChildren<TMP_Text>(true).text = selling ? "장착 모드로" : "판매 모드로";
            }
            unequipWeapon.interactable = owner.HasWeapon;
            unequipArmor.interactable = !owner.ArmorSlot.ItemId.IsEmpty;
            int index = 0;
            foreach (var slot in inventory.Slots)
            {
                var button = Row(inventoryButtons, inventoryRows, index++);
                string id = slot.InstanceId;
                string itemId = slot.Item.ItemId;
                int quantity = slot.Item == owner.Catalog.Ammunition
                    ? inventory.GetTotalQuantity(slot.Item) : slot.Quantity;
                long quote = slot.Item == owner.Catalog.Ammunition ? owner.AmmunitionSaleQuote(quantity) : owner.SaleQuote(slot);
                button.GetComponentInChildren<TMP_Text>(true).text = $"{slot.Item.ItemName} ×{slot.Quantity}" +
                    (selling ? (slot.Item == owner.Catalog.Ammunition ? $"\n예비탄 전체 {quantity}발 판매 {quote}" : $"\n판매 {quote}")
                        : slot.MagazineRounds >= 0 ? $"\n탄창 {slot.MagazineRounds}" : "");
                button.interactable = selling ? quote > 0 : !string.IsNullOrEmpty(id) && owner.Catalog.TryGet(slot.Item.ItemId, out _);
                button.onClick.RemoveAllListeners();
                if (selling && shop != null)
                {
                    int shopId = shop.ShopId;
                    button.onClick.AddListener(() => owner.RequestSell(shopId, itemId, id, quantity));
                }
                else button.onClick.AddListener(() => owner.RequestEquip(id));
            }
            HideRemainder(inventoryButtons, index);
            index = 0;
            if (shop != null)
            {
                foreach (var definition in owner.Catalog.Definitions)
                {
                    if (definition?.item == null) continue;
                    AddPurchaseRow(index++, $"{definition.item.ItemName}\n구매 {definition.price}", shop.ShopId, definition.item.ItemId);
                }
                if (owner.Catalog.Ammunition != null)
                    AddPurchaseRow(index++, $"탄약 ×{owner.Catalog.AmmunitionBundle}\n구매 {owner.Catalog.AmmunitionPrice}", shop.ShopId, owner.Catalog.Ammunition.ItemId);
            }
            HideRemainder(shopButtons, index);
        }
        private string Label(PlayerEquipmentGlue owner, NetworkInventorySlot slot)
        {
            if (slot.ItemId.IsEmpty) return "없음";
            if (!owner.Catalog.TryGet(slot.ItemId.ToString(), out var definition)) return "정의 누락";
            return definition.kind == EquipmentKind.Weapon ? $"{definition.item.ItemName} ({slot.MagazineRounds}발)" :
                $"{definition.item.ItemName} · 피해 감소 {definition.damageReduction:P0}";
        }
        private void AddPurchaseRow(int index, string label, int shopId, string itemId)
        {
            var button = Row(shopButtons, shopRows, index);
            button.interactable = true;
            button.GetComponentInChildren<TMP_Text>(true).text = label;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => player?.RequestBuy(shopId, itemId));
        }
        private UnityEngine.UI.Button Row(List<UnityEngine.UI.Button> buttons, Transform parent, int index)
        {
            while (buttons.Count <= index) buttons.Add(Instantiate(rowTemplate, parent));
            buttons[index].gameObject.SetActive(true);
            return buttons[index];
        }
        private static void HideRemainder(List<UnityEngine.UI.Button> buttons, int used)
        {
            for (int i = used; i < buttons.Count; i++) buttons[i].gameObject.SetActive(false);
        }
    }
}
