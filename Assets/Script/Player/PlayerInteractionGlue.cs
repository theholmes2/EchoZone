using EchoZone.Player.Input;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 플레이어 입력과 감지된 상호작용 후보를 연결하여 실제 상호작용을 요청합니다.
/// </summary>
public class PlayerInteractionGlue : NetworkBehaviour
{
    /// <summary>현재 플레이어 주변의 상호작용 후보를 제공하는 센서입니다.</summary>
    private PlayerInteractionSensor playerInteractionSensor;

    /// <summary>현재 플레이어의 이동 및 상호작용 입력을 제공하는 입력 브릭입니다.</summary>
    private PlayerInputReader playerInputReader;
    /// <summary>사망 중 아이템 획득 요청을 차단하는 체력 데이터입니다.</summary>
    private PlayerStats stats;

    /// <summary>이 플레이어에 연결된 입력 리더와 상호작용 센서 참조를 준비합니다.</summary>
    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        playerInteractionSensor = GetComponentInChildren<PlayerInteractionSensor>();
        if (playerInteractionSensor == null)
        {
            Debug.LogError("PlayerInteractionSensor 컴포넌트를 찾을 수 없습니다.", this);
        }

        playerInputReader = GetComponentInChildren<PlayerInputReader>();
        if (playerInputReader == null)
        {
            Debug.LogError("PlayerInputReader 컴포넌트를 찾을 수 없습니다.", this);
        }

    }

    /// <summary>
    /// 상호작용 입력이 발생하면 센서의 첫 번째 후보에게 상호작용을 요청합니다.
    /// </summary>
    private void Update()
    {
        if (!IsOwner)
            return;
        if (playerInputReader == null)
            return;
        if (playerInteractionSensor == null)
            return;
        if (!playerInputReader.InteractPressedThisFrame)
            return;

        playerInteractionSensor.RemoveMissingCandidates();

        // Despawn(false)한 대상은 여전히 존재하므로 첫 후보만 검사하면
        // 이후 입력까지 막힌다. 네트워크 유효성 판단은 센서가 아닌 Glue에서 한다.
        foreach (InteractableBehaviour candidate in playerInteractionSensor.Candidates)
        {
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;
            if (candidate is EchoZone.Pet.PetClaimInteractable && !candidate.CanInteract(gameObject))
                continue;

            NetworkObject networkObject = candidate.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsSpawned)
                continue;

            RequestInteractRpc(networkObject.NetworkObjectId);
            break;
        }
    }

    /// <summary>
    /// 소유 클라이언트가 지정한 네트워크 대상과의 상호작용을 서버에 요청합니다.
    /// </summary>
    /// <param name="targetNetworkObjectId">상호작용을 요청한 대상의 네트워크 오브젝트 ID입니다.</param>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestInteractRpc(ulong targetNetworkObjectId)
    {
        if (!IsServer || !IsSpawned || (stats != null && stats.IsDead) ||
            (TryGetComponent<EchoZone.Heist.PlayerWalletGlue>(out var wallet) && wallet.IsEscaping)) return;
        bool found = NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
      targetNetworkObjectId,out NetworkObject targetNetworkObject);

        if (found == false)
            return;

        InteractableBehaviour interactableBehaviour= targetNetworkObject.GetComponent<InteractableBehaviour>();
        if(interactableBehaviour == null)   
            return;

        bool isInSensorRange = false;

        for (int i = 0; i < playerInteractionSensor.Candidates.Count; i++)
        {
            if (playerInteractionSensor.Candidates[i] == interactableBehaviour)
            {
                isInSensorRange = true;
                break;
            }
        }

        if (isInSensorRange == false)
            return;

        InteractionLockBehaviour interactionLock =
            targetNetworkObject.GetComponent<InteractionLockBehaviour>();

        if (interactionLock == null)
            return;

        bool interactionSucceeded = interactionLock.Execute(() =>
        {
            if (interactableBehaviour.CanInteract(gameObject) == false)
                return false;

            return interactableBehaviour.TryInteract(gameObject);
        });

        if (interactionSucceeded == false)
            return;

    }


}
