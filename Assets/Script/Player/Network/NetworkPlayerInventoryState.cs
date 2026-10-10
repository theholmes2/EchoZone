using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Player.Network
{
    /// <summary>
    /// 서버 PlayerInventory의 슬롯 결과를 소유 클라이언트의 인벤토리 복사본에 동기화하는 Glue입니다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class NetworkPlayerInventoryState : NetworkBehaviour
    {
        /// <summary>ItemId를 로컬 ItemData로 복원하는 중앙 아이템 데이터 허브입니다.</summary>
        [SerializeField] private ItemCatalog itemCatalog;

        /// <summary>서버만 변경하고 해당 플레이어의 Owner만 읽는 네트워크 슬롯 목록입니다.</summary>
        private NetworkList<NetworkInventorySlot> networkSlots = new(
            null,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        /// <summary>서버의 원본 또는 Owner Client의 복사본 인벤토리 Brick입니다.</summary>
        private PlayerInventory inventory;

        /// <summary>같은 플레이어 오브젝트에 있는 인벤토리 Brick을 찾습니다.</summary>
        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
        }

        /// <summary>로컬 PlayerInventory의 변경 이벤트를 구독합니다.</summary>
        private void OnEnable()
        {
            inventory?.AddInventoryChangedListener(HandleLocalInventoryChanged);
        }

        /// <summary>로컬 PlayerInventory의 변경 이벤트 구독을 해제합니다.</summary>
        private void OnDisable()
        {
            inventory?.RemoveInventoryChangedListener(HandleLocalInventoryChanged);
        }

        /// <summary>네트워크 Spawn 시 목록 이벤트를 연결하고 서버 또는 Owner 상태를 초기화합니다.</summary>
        public override void OnNetworkSpawn()
        {
            networkSlots.OnListChanged += HandleNetworkSlotsChanged;

            if (IsServer)
            {
                CopyInventoryToNetworkList();
                return;
            }

            if (IsOwner)
            {
                ApplyNetworkListToInventory();
            }
        }

        /// <summary>네트워크 Despawn 시 목록 변경 이벤트 연결을 해제합니다.</summary>
        public override void OnNetworkDespawn()
        {
            networkSlots.OnListChanged -= HandleNetworkSlotsChanged;
        }

        /// <summary>서버 인벤토리가 변경되면 네트워크 슬롯 목록을 다시 작성합니다.</summary>
        private void HandleLocalInventoryChanged()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            CopyInventoryToNetworkList();
        }

        /// <summary>서버 인벤토리 슬롯을 ItemId와 수량으로 변환하여 NetworkList에 기록합니다.</summary>
        private void CopyInventoryToNetworkList()
        {
            networkSlots.Clear();

            if (inventory == null)
            {
                return;
            }

            IReadOnlyList<InventorySlot> sourceSlots = inventory.Slots;
            for (int i = 0; i < sourceSlots.Count; i++)
            {
                InventorySlot slot = sourceSlots[i];
                if (slot == null || slot.Item == null || slot.Quantity <= 0)
                {
                    continue;
                }

                networkSlots.Add(new NetworkInventorySlot(
                    slot.Item.ItemId,
                    slot.Quantity, slot.InstanceId, slot.MagazineRounds, slot.PurchaseValue));
            }
        }

        /// <summary>네트워크 슬롯 목록이 변경되면 Owner Client의 인벤토리 복사본을 갱신합니다.</summary>
        /// <param name="changeEvent">NGO가 전달한 목록 변경 정보입니다.</param>
        private void HandleNetworkSlotsChanged(
            NetworkListEvent<NetworkInventorySlot> changeEvent)
        {
            if (IsServer || !IsOwner)
            {
                return;
            }

            ApplyNetworkListToInventory();
        }

        /// <summary>NetworkList의 ItemId를 ItemCatalog에서 찾아 로컬 인벤토리 슬롯으로 복원합니다.</summary>
        private void ApplyNetworkListToInventory()
        {
            if (inventory == null || itemCatalog == null)
            {
                return;
            }

            List<InventorySlot> restoredSlots = new(networkSlots.Count);
            for (int i = 0; i < networkSlots.Count; i++)
            {
                NetworkInventorySlot networkSlot = networkSlots[i];
                if (!itemCatalog.TryGetItem(networkSlot.ItemId.ToString(), out ItemData item))
                {
                    continue;
                }

                restoredSlots.Add(new InventorySlot(item, networkSlot.Quantity,
                    networkSlot.InstanceId.ToString(), networkSlot.MagazineRounds, networkSlot.PurchaseValue));
            }

            inventory.ReplaceSlots(restoredSlots);
        }
    }
}
