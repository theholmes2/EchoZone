using System;
using UnityEngine;

/// <summary>
/// 한 종류의 아이템과 현재 중첩 수량을 보관하는 인벤토리 슬롯입니다.
/// </summary>
[Serializable]
public sealed class InventorySlot
{
    [SerializeField] private ItemData item;
    [SerializeField] private int quantity;

    /// <summary>슬롯에 들어 있는 아이템 정의입니다.</summary>
    public ItemData Item => item;

    /// <summary>슬롯에 들어 있는 현재 아이템 수량입니다.</summary>
    public int Quantity => quantity;

    /// <summary>지정한 아이템과 수량으로 슬롯을 생성합니다.</summary>
    /// <param name="item">슬롯에 저장할 아이템 정의입니다.</param>
    /// <param name="quantity">저장할 수량이며 최대 중첩 수에 맞게 보정됩니다.</param>
    public InventorySlot(ItemData item, int quantity)
    {
        this.item = item;

        this.quantity = item != null
            ? Mathf.Clamp(quantity, 0, item.MaxStackSize)
            : 0;
    }

    /// <summary>이 슬롯에 아이템을 더 담을 수 있는 공간을 계산합니다.</summary>
    /// <returns>추가로 담을 수 있는 수량입니다.</returns>
    public int GetRemainingSpace()
    {
        if (item == null)
        {
            return 0;
        }

        return Mathf.Max(0, item.MaxStackSize - quantity);
    }

    /// <summary>최대 중첩 수를 넘지 않는 범위에서 수량을 추가합니다.</summary>
    /// <param name="amount">추가하려는 수량입니다.</param>
    /// <returns>슬롯에 담지 못하고 남은 수량입니다.</returns>
    public int Add(int amount)
    {
        if (item == null || amount <= 0)
        {
            return amount;
        }

        int addedAmount = Mathf.Min(amount, GetRemainingSpace());
        quantity += addedAmount;

        return amount - addedAmount;
    }

    /// <summary>현재 슬롯에서 요청한 수량만큼 제거합니다.</summary>
    /// <param name="amount">제거하려는 수량입니다.</param>
    /// <returns>실제로 제거된 수량입니다.</returns>
    public int Remove(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int removedAmount = Mathf.Min(amount, quantity);
        quantity -= removedAmount;
        return removedAmount;
    }
}
