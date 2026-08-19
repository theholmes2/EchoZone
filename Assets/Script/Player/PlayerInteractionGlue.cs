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

    /// <summary>이 플레이어에 연결된 입력 리더와 상호작용 센서 참조를 준비합니다.</summary>
    private void Awake()
    {
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

        if (playerInteractionSensor.Candidates.Count <= 0)
            return;
        if (playerInteractionSensor.Candidates[0] == null)
            return;

        InteractableBehaviour interactableBehaviour = playerInteractionSensor.Candidates[0];
        NetworkObject networkObject = interactableBehaviour.GetComponent<NetworkObject>();
        if (networkObject == null)
            return;
        if (networkObject.IsSpawned == false)
            return;
        RequestInteractRpc(networkObject.NetworkObjectId);
    }

    /// <summary>
    /// 소유 클라이언트가 지정한 네트워크 대상과의 상호작용을 서버에 요청합니다.
    /// </summary>
    /// <param name="targetNetworkObjectId">상호작용을 요청한 대상의 네트워크 오브젝트 ID입니다.</param>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestInteractRpc(ulong targetNetworkObjectId)
    {
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
