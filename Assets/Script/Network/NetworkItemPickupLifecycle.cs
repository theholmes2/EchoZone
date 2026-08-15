using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 월드 아이템의 고갈 이벤트를 NGO의 네트워크 Despawn과 연결하는 Glue입니다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(ItemPickup))]
public sealed class NetworkItemPickupLifecycle : NetworkBehaviour
{
    /// <summary>고갈 상태를 제공하는 월드 아이템 Brick입니다.</summary>
    private ItemPickup itemPickup;

    /// <summary>서버가 모든 클라이언트에서 제거할 네트워크 오브젝트입니다.</summary>
    private NetworkObject targetNetworkObject;

    /// <summary>같은 게임 오브젝트에 있는 아이템과 네트워크 오브젝트를 찾습니다.</summary>
    private void Awake()
    {
        itemPickup = GetComponent<ItemPickup>();
        targetNetworkObject = GetComponent<NetworkObject>();
    }

    /// <summary>아이템이 활성화되면 고갈 이벤트를 구독합니다.</summary>
    private void OnEnable()
    {
        itemPickup?.AddDepletedListener(HandleDepleted);
    }

    /// <summary>아이템이 비활성화되면 고갈 이벤트 구독을 해제합니다.</summary>
    private void OnDisable()
    {
        itemPickup?.RemoveDepletedListener(HandleDepleted);
    }

    /// <summary>서버에서 고갈된 아이템을 네트워크 전체에 Despawn합니다.</summary>
    private void HandleDepleted()
    {
        if (!IsServer)
        {
            return;
        }

        if (targetNetworkObject == null || !targetNetworkObject.IsSpawned)
        {
            return;
        }

        targetNetworkObject.Despawn();
    }
}
