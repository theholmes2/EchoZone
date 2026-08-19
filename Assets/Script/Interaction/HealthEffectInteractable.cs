using UnityEngine;

/// <summary>
/// 데이터로 정의된 체력 효과를 월드 아이템 상호작용과 PlayerStats에 연결하는 Glue입니다.
/// </summary>
[RequireComponent(typeof(ItemPickup))]
[RequireComponent(typeof(InteractionLockBehaviour))]
public sealed class HealthEffectInteractable : InteractableBehaviour
{
    [SerializeField] private HealthEffectData effectData;

    /// <summary>월드에 남아 있는 소비 아이템 수량을 관리하는 Brick입니다.</summary>
    private ItemPickup itemPickup;

    /// <summary>같은 게임 오브젝트에 있는 월드 아이템 Brick을 찾습니다.</summary>
    private void Awake()
    {
        itemPickup = GetComponent<ItemPickup>();
    }

    /// <summary>요청 플레이어에게 현재 체력 효과를 적용할 수 있는지 판단합니다.</summary>
    /// <param name="interactor">효과 적용을 요청한 플레이어 게임 오브젝트입니다.</param>
    /// <returns>아이템 수량과 효과 조건을 모두 만족하면 <see langword="true"/>입니다.</returns>
    public override bool CanInteract(GameObject interactor)
    {
        if (itemPickup == null ||
            itemPickup.Quantity <= 0 ||
            effectData == null ||
            interactor == null)
        {
            return false;
        }

        PlayerStats playerStats = interactor.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            return false;
        }

        switch (effectData.EffectType)
        {
            case HealthEffectType.FullRestore:
                return playerStats.CurrentHealth < playerStats.MaxHealth;

            case HealthEffectType.FixedDamage:
                return effectData.Amount > 0 && !playerStats.IsDead;

            default:
                return false;
        }
    }

    /// <summary>요청 플레이어의 체력을 변경하고 사용된 월드 아이템 수량을 한 개 차감합니다.</summary>
    /// <param name="interactor">효과 적용을 요청한 플레이어 게임 오브젝트입니다.</param>
    /// <returns>체력이 실제로 변경되고 아이템을 소비했으면 <see langword="true"/>입니다.</returns>
    public override bool TryInteract(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return false;
        }

        PlayerStats playerStats = interactor.GetComponent<PlayerStats>();
        int changedHealth;

        switch (effectData.EffectType)
        {
            case HealthEffectType.FullRestore:
                changedHealth = playerStats.RestoreHealth(playerStats.MaxHealth);
                break;

            case HealthEffectType.FixedDamage:
                changedHealth = playerStats.ApplyDamage(effectData.Amount);
                break;

            default:
                return false;
        }

        if (changedHealth <= 0)
        {
            return false;
        }

        int removedQuantity = itemPickup.RemoveQuantity(1);
        return removedQuantity == 1;
    }
}
