using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버의 월드 아이템 수량을 모든 클라이언트의 ItemPickup에 동기화하는 Glue입니다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(ItemPickup))]
public sealed class NetworkItemPickupState : NetworkBehaviour
{
    /// <summary>서버만 변경하고 모든 클라이언트가 읽을 수 있는 동기화 수량입니다.</summary>
    private NetworkVariable<int> networkQuantity = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>로컬 월드 아이템 수량을 제공하고 적용받는 Brick입니다.</summary>
    private ItemPickup itemPickup;

    /// <summary>같은 게임 오브젝트에 있는 월드 아이템 Brick을 찾습니다.</summary>
    private void Awake()
    {
        itemPickup = GetComponent<ItemPickup>();
    }

    /// <summary>로컬 아이템 수량 변경 이벤트를 구독합니다.</summary>
    private void OnEnable()
    {
        itemPickup?.AddQuantityChangedListener(HandleLocalQuantityChanged);
    }

    /// <summary>로컬 아이템 수량 변경 이벤트 구독을 해제합니다.</summary>
    private void OnDisable()
    {
        itemPickup?.RemoveQuantityChangedListener(HandleLocalQuantityChanged);
    }

    /// <summary>네트워크 Spawn 시 서버 수량을 초기화하고 수량 변경 알림을 연결합니다.</summary>
    public override void OnNetworkSpawn()
    {
        networkQuantity.OnValueChanged += HandleNetworkQuantityChanged;

        if (IsServer)
        {
            networkQuantity.Value = itemPickup != null ? itemPickup.Quantity : 0;
            return;
        }

        itemPickup?.SetQuantity(networkQuantity.Value);
    }

    /// <summary>네트워크 Despawn 시 수량 변경 알림 연결을 해제합니다.</summary>
    public override void OnNetworkDespawn()
    {
        networkQuantity.OnValueChanged -= HandleNetworkQuantityChanged;
    }

    /// <summary>서버에서 변경된 ItemPickup 수량을 NetworkVariable에 기록합니다.</summary>
    /// <param name="currentQuantity">서버의 현재 월드 아이템 수량입니다.</param>
    private void HandleLocalQuantityChanged(int currentQuantity)
    {
        if (!IsServer || !IsSpawned)
        {
            return;
        }

        networkQuantity.Value = currentQuantity;
    }

    /// <summary>NetworkVariable의 변경된 수량을 각 클라이언트의 ItemPickup에 적용합니다.</summary>
    /// <param name="previousQuantity">변경 전 네트워크 수량입니다.</param>
    /// <param name="currentQuantity">변경 후 네트워크 수량입니다.</param>
    private void HandleNetworkQuantityChanged(int previousQuantity, int currentQuantity)
    {
        itemPickup?.SetQuantity(currentQuantity);
    }
}
