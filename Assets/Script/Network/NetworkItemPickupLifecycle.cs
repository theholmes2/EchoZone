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

    /// <summary>renderers 값을 저장합니다.</summary>
    private Renderer[] renderers;
    /// <summary>colliders 값을 저장합니다.</summary>
    private Collider[] colliders;
    /// <summary>rigidbodies 값을 저장합니다.</summary>
    private Rigidbody[] rigidbodies;
    /// <summary>originalKinematicStates 값을 저장합니다.</summary>
    private bool[] originalKinematicStates;

    /// <summary>같은 게임 오브젝트에 있는 아이템과 네트워크 오브젝트를 찾습니다.</summary>
    private void Awake()
    {
        itemPickup = GetComponent<ItemPickup>();
        targetNetworkObject = GetComponent<NetworkObject>();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        rigidbodies = GetComponentsInChildren<Rigidbody>(true);
        originalKinematicStates = new bool[rigidbodies.Length];
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            originalKinematicStates[i] = rigidbodies[i].isKinematic;
        }
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

    /// <summary>OnNetworkSpawn 작업을 수행합니다.</summary>
    public override void OnNetworkSpawn()
    {
        SetWorldPresentation(itemPickup != null && !itemPickup.IsEmpty);
    }

    /// <summary>
    /// 수량 동기화보다 Despawn이 먼저 도착할 수 있으므로 로컬 수량과 무관하게 숨깁니다.
    /// 식별 정보는 유지하고, 재Spawn 시 OnNetworkSpawn에서 표시 상태를 복원합니다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        SetWorldPresentation(false);
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

        SetWorldPresentation(false);
        // 씬 배치 오브젝트를 파괴하지 않아 새 Host도 동일 WorldItemId의 고갈 상태를 수집할 수 있게 합니다.
        targetNetworkObject.Despawn(false);
    }

    /// <summary>고갈 아이템의 식별 컴포넌트는 유지하고 화면과 충돌만 숨깁니다.</summary>
    private void SetWorldPresentation(bool visible)
    {
        // 식별용 오브젝트를 보존하되, 충돌체가 꺼진 동안 중력으로 낙하하지 않게 한다.
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody body = rigidbodies[i];
            if (!visible && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = !visible || originalKinematicStates[i];
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = visible;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = visible;
        }
    }
}
