using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 월드에 놓인 아이템의 종류와 현재 남아 있는 수량을 관리합니다.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private ItemData itemDefinition;
    [SerializeField, Min(1)] private int quantity = 1;

    /// <summary>월드 아이템의 수량이 처음 0이 되는 순간 발생하는 이벤트입니다.</summary>
    [SerializeField] private UnityEvent depleted = new();

    /// <summary>월드 아이템의 수량이 변경될 때 변경된 수량과 함께 발생하는 이벤트입니다.</summary>
    [SerializeField] private UnityEvent<int> quantityChanged = new();

    /// <summary>이 월드 아이템이 나타내는 아이템 정의입니다.</summary>
    public ItemData ItemDefinition => itemDefinition;

    /// <summary>월드에 남아 있는 아이템 수량입니다.</summary>
    public int Quantity => quantity;

    /// <summary>남은 수량이 없는지 나타냅니다.</summary>
    public bool IsEmpty => quantity <= 0;

    /// <summary>아이템 고갈 이벤트를 받을 함수를 등록합니다.</summary>
    /// <param name="listener">아이템이 고갈될 때 실행할 함수입니다.</param>
    public void AddDepletedListener(UnityAction listener)
    {
        depleted.AddListener(listener);
    }

    /// <summary>아이템 고갈 이벤트에 등록했던 함수를 제거합니다.</summary>
    /// <param name="listener">이벤트에서 제거할 함수입니다.</param>
    public void RemoveDepletedListener(UnityAction listener)
    {
        depleted.RemoveListener(listener);
    }

    /// <summary>아이템 수량 변경 이벤트를 받을 함수를 등록합니다.</summary>
    /// <param name="listener">변경된 수량을 받을 함수입니다.</param>
    public void AddQuantityChangedListener(UnityAction<int> listener)
    {
        quantityChanged.AddListener(listener);
    }

    /// <summary>아이템 수량 변경 이벤트에 등록했던 함수를 제거합니다.</summary>
    /// <param name="listener">이벤트에서 제거할 함수입니다.</param>
    public void RemoveQuantityChangedListener(UnityAction<int> listener)
    {
        quantityChanged.RemoveListener(listener);
    }

    /// <summary>월드 아이템의 종류와 최초 수량을 설정합니다.</summary>
    /// <param name="item">설정할 아이템 정의입니다.</param>
    /// <param name="amount">설정할 최초 수량이며 최소 1로 보정됩니다.</param>
    public void Initialize(ItemData item, int amount)
    {
        itemDefinition = item;
        SetQuantity(Mathf.Max(1, amount));
    }

    /// <summary>현재 월드 아이템 수량을 0 이상으로 설정합니다.</summary>
    /// <param name="amount">설정할 수량입니다.</param>
    public void SetQuantity(int amount)
    {
        int clampedAmount = Mathf.Max(0, amount);
        if (quantity == clampedAmount)
        {
            return;
        }

        bool wasEmpty = IsEmpty;
        quantity = clampedAmount;
        quantityChanged.Invoke(quantity);

        if (wasEmpty == false && IsEmpty)
        {
            depleted.Invoke();
        }
    }

    /// <summary>요청한 만큼 현재 수량에서 차감합니다.</summary>
    /// <param name="requestedAmount">차감하려는 수량입니다.</param>
    /// <returns>실제로 차감된 수량입니다.</returns>
    public int RemoveQuantity(int requestedAmount)
    {
        if (requestedAmount <= 0)
        {
            return 0;
        }

        int removedAmount = Mathf.Min(quantity, requestedAmount);
        SetQuantity(quantity - removedAmount);

        return removedAmount;
    }

   
}
