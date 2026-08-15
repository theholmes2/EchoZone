using UnityEngine;

/// <summary>
/// 플레이어 인벤토리의 슬롯 제한처럼 함께 조정되는 불변 설계 데이터를 보관합니다.
/// </summary>
[CreateAssetMenu(
    fileName = "InventoryConfig",
    menuName = "EchoZone/Inventory/Config")]
public sealed class InventoryConfig : ScriptableObject
{
    /// <summary>인벤토리가 보유할 수 있는 최대 슬롯 수입니다.</summary>
    [SerializeField, Min(1)] private int maxSlots = 1;

    /// <summary>인벤토리가 보유할 수 있는 최대 슬롯 수를 제공합니다.</summary>
    public int MaxSlots => Mathf.Max(1, maxSlots);
}
