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
    /// <summary>같은 종류의 장비를 개별적으로 구분하는 서버 발급 식별자입니다.</summary>
    [SerializeField] private string instanceId;
    /// <summary>보관된 총의 탄창 잔량이며 -1은 아직 초기화하지 않은 새 장비입니다.</summary>
    [SerializeField] private int magazineRounds = -1;
    /// <summary>이 장비를 실제로 구매할 때 지불한 금액이며 무료·획득 장비는 0입니다.</summary>
    [SerializeField] private int purchaseValue;

    /// <summary>장비 교환·복원 시 유지할 개별 식별자입니다.</summary>
    public string InstanceId => instanceId ?? string.Empty;
    /// <summary>보관된 탄창 잔량으로 장착 교환을 통한 무료 재장전을 방지합니다.</summary>
    public int MagazineRounds => magazineRounds;
    /// <summary>탈출 시 중복 지급 없이 돌려줄 실제 구매 금액입니다.</summary>
    public int PurchaseValue => purchaseValue;

    /// <summary>슬롯에 들어 있는 아이템 정의입니다.</summary>
    public ItemData Item => item;

    /// <summary>슬롯에 들어 있는 현재 아이템 수량입니다.</summary>
    public int Quantity => quantity;

    /// <summary>지정한 아이템과 수량으로 슬롯을 생성합니다.</summary>
    /// <param name="item">슬롯에 저장할 아이템 정의입니다.</param>
    /// <param name="quantity">저장할 수량이며 최대 중첩 수에 맞게 보정됩니다.</param>
    public InventorySlot(ItemData item, int quantity, string instanceId = null, int magazineRounds = -1, int purchaseValue = 0)
    {
        this.item = item;
        this.instanceId = instanceId ?? string.Empty;
        this.magazineRounds = Math.Max(-1, magazineRounds);
        this.purchaseValue = Math.Max(0, purchaseValue);

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

    /// <summary>슬롯의 개별 장비 상태까지 보존하는 독립 복사본을 생성합니다.</summary>
    public InventorySlot Copy() => new InventorySlot(item, quantity, InstanceId, magazineRounds, purchaseValue);

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
