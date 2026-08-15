using UnityEngine;

/// <summary>
/// 월드 아이템을 플레이어의 공통 상호작용 시스템에 연결합니다.
/// </summary>
[RequireComponent(typeof(InteractionLockBehaviour))]
public class ItemPickupInteractable : InteractableBehaviour
{
    /// <summary>상호작용할 월드 아이템의 상태와 수량을 관리하는 컴포넌트입니다.</summary>
    ItemPickup itemPickup;

    /// <summary>같은 게임 오브젝트에 있는 월드 아이템 컴포넌트를 찾습니다.</summary>
    private void Awake()
    {
        itemPickup = GetComponent<ItemPickup>();
    }

    /// <summary>월드 아이템을 현재 획득할 수 있는지 판단합니다.</summary>
    /// <param name="interactor">아이템 획득을 시도하는 플레이어 게임 오브젝트입니다.</param>
    /// <returns>아이템과 플레이어가 존재하고 남은 수량이 있으면 <see langword="true"/>입니다.</returns>
    public override bool CanInteract(GameObject interactor)
    {
        if (itemPickup == null)
            return false;

        if (itemPickup.ItemDefinition == null)
            return false;

        int quantity = itemPickup.Quantity;

        if (quantity <= 0)
            return false;

        if (interactor == null)
            return false;


        return true;
    }

    /// <summary>아이템을 플레이어의 인벤토리에 넣고 월드 수량 차감을 시도합니다.</summary>
    /// <param name="interactor">아이템 획득을 시도하는 플레이어 게임 오브젝트입니다.</param>
    /// <returns>아이템이 한 개 이상 실제로 이동했으면 <see langword="true"/>입니다.</returns>
    public override bool TryInteract(GameObject interactor)
    {
        if (CanInteract(interactor) == false)
            return false;

        PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();

        if (inventory == null)
            return false;

        int addedQuantity = inventory.AddUpToCapacity(
            itemPickup.ItemDefinition,
            itemPickup.Quantity);

        if (addedQuantity <= 0)
            return false;

        itemPickup.RemoveQuantity(addedQuantity);

        return true;
    }
}
