using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어의 인벤토리 슬롯을 관리하고 가능한 수량만큼 아이템을 저장합니다.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    [SerializeField] private InventoryConfig config;

    /// <summary>현재 인벤토리가 보유한 슬롯 데이터입니다.</summary>
    [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

    /// <summary>인벤토리 슬롯 구성이 변경된 뒤 발생하는 이벤트입니다.</summary>
    [SerializeField] private UnityEvent inventoryChanged = new();

    /// <summary>외부에서 읽을 수 있는 현재 인벤토리 슬롯 목록입니다.</summary>
    public IReadOnlyList<InventorySlot> Slots => slots;
    /// <summary>구매 전에 전체 묶음을 보관할 공간이 있는지 부작용 없이 검사합니다.</summary>
    public bool CanAdd(ItemData item, int quantity)
    {
        if (config == null || item == null || quantity <= 0) return false;
        long capacity = (long)Mathf.Max(0, config.MaxSlots - slots.Count) * item.MaxStackSize;
        foreach (var slot in slots)
            if (slot != null && slot.Item == item && string.IsNullOrEmpty(slot.InstanceId)) capacity += slot.GetRemainingSpace();
        return capacity >= quantity;
    }

    /// <summary>다른 보관함이나 장착 슬롯에서 가져온 개별 장비를 복사해 보관합니다.</summary>
    public bool TryAddInstance(InventorySlot source)
    {
        if (config == null || source?.Item == null || source.Quantity != 1 ||
            source.Item.MaxStackSize != 1 || string.IsNullOrEmpty(source.InstanceId) ||
            slots.Count >= config.MaxSlots || ContainsInstance(source.InstanceId)) return false;
        slots.Add(source.Copy());
        inventoryChanged.Invoke();
        return true;
    }

    /// <summary>서버가 선택한 장비를 꺼내고 기존 장착품을 같은 칸에 넣는 원자적 교환입니다.</summary>
    public bool TryExchangeInstance(string expectedInstanceId, InventorySlot replacement, out InventorySlot removed)
    {
        removed = null;
        if (string.IsNullOrEmpty(expectedInstanceId)) return false;
        int index = slots.FindIndex(slot => slot != null && slot.InstanceId == expectedInstanceId);
        if (index < 0 || slots[index].Quantity != 1 || slots[index].Item == null ||
            slots[index].Item.MaxStackSize != 1) return false;
        if (replacement != null && (replacement.Item == null || replacement.Quantity != 1 ||
            replacement.Item.MaxStackSize != 1 || string.IsNullOrEmpty(replacement.InstanceId) ||
            ContainsInstance(replacement.InstanceId))) return false;
        removed = slots[index].Copy();
        if (replacement == null) slots.RemoveAt(index);
        else slots[index] = replacement.Copy();
        inventoryChanged.Invoke();
        return true;
    }

    /// <summary>동일 장비가 인벤토리 안에서 중복 삽입되는 것을 막습니다.</summary>
    private bool ContainsInstance(string id) => slots.Exists(slot => slot != null && slot.InstanceId == id);

    /// <summary>인벤토리 변경 이벤트를 받을 함수를 등록합니다.</summary>
    /// <param name="listener">인벤토리가 변경된 뒤 실행할 함수입니다.</param>
    public void AddInventoryChangedListener(UnityAction listener)
    {
        inventoryChanged.AddListener(listener);
    }

    /// <summary>인벤토리 변경 이벤트에 등록했던 함수를 제거합니다.</summary>
    /// <param name="listener">이벤트에서 제거할 함수입니다.</param>
    public void RemoveInventoryChangedListener(UnityAction listener)
    {
        inventoryChanged.RemoveListener(listener);
    }

    /// <summary>
    /// 기존 슬롯을 먼저 채운 뒤 빈 슬롯을 생성하여 가능한 수량만큼 아이템을 추가합니다.
    /// </summary>
    /// <param name="item">추가할 아이템 정의입니다.</param>
    /// <param name="requestedQuantity">추가를 요청한 수량입니다.</param>
    /// <returns>인벤토리에 실제로 추가된 수량입니다.</returns>
    public int AddUpToCapacity(ItemData item, int requestedQuantity)
    {
        if (item == null || requestedQuantity <= 0 || config == null)
        {
            return 0;
        }

        int remainingQuantity = requestedQuantity;

        // 1. 기존의 같은 아이템 슬롯부터 채운다.
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];

            if (slot == null || slot.Item != item)
            {
                continue;
            }

            remainingQuantity = slot.Add(remainingQuantity);

            if (remainingQuantity <= 0)
            {
                break;
            }
        }

        // 2. 남은 수량은 빈 슬롯이 있는 동안 새 슬롯에 넣는다.
        while (remainingQuantity > 0 && slots.Count < config.MaxSlots)
        {
            int amountForNewSlot = Mathf.Min(
                remainingQuantity,
                item.MaxStackSize);

            slots.Add(new InventorySlot(item, amountForNewSlot));
            remainingQuantity -= amountForNewSlot;
        }

        int addedQuantity = requestedQuantity - remainingQuantity;
        if (addedQuantity > 0)
        {
            inventoryChanged.Invoke();
        }

        return addedQuantity;
    }

    /// <summary>모든 슬롯에서 지정한 아이템의 총수량을 계산합니다.</summary>
    public int GetTotalQuantity(ItemData item)
    {
        if (item == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot != null && slot.Item == item)
            {
                total += slot.Quantity;
            }
        }

        return total;
    }

    /// <summary>뒤쪽 슬롯부터 지정한 아이템을 가능한 수량만큼 제거합니다.</summary>
    /// <returns>실제로 제거된 수량입니다.</returns>
    public int RemoveUpToQuantity(ItemData item, int requestedQuantity)
    {
        if (item == null || requestedQuantity <= 0)
        {
            return 0;
        }

        int remaining = requestedQuantity;
        for (int i = slots.Count - 1; i >= 0 && remaining > 0; i--)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.Item != item)
            {
                continue;
            }

            remaining -= slot.Remove(remaining);
            if (slot.Quantity <= 0)
            {
                slots.RemoveAt(i);
            }
        }

        int removed = requestedQuantity - remaining;
        if (removed > 0)
        {
            inventoryChanged.Invoke();
        }

        return removed;
    }

    /// <summary>
    /// 네트워크 등 외부 상태에서 받은 슬롯 목록으로 현재 인벤토리 복사본을 교체합니다.
    /// </summary>
    /// <param name="sourceSlots">적용할 슬롯 목록입니다.</param>
    public void ReplaceSlots(IReadOnlyList<InventorySlot> sourceSlots)
    {
        slots.Clear();

        if (sourceSlots != null && config != null)
        {
            for (int i = 0; i < sourceSlots.Count && slots.Count < config.MaxSlots; i++)
            {
                InventorySlot sourceSlot = sourceSlots[i];
                if (sourceSlot == null || sourceSlot.Item == null || sourceSlot.Quantity <= 0)
                {
                    continue;
                }

                slots.Add(sourceSlot.Copy());
            }
        }

        inventoryChanged.Invoke();
    }
}
