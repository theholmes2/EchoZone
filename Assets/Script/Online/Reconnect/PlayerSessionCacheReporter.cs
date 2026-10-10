using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Online.Reconnect
{
    /// <summary>
    /// 서버 PlayerObject의 인벤토리·스탯 Brick을 관찰하여 임시 세션 캐시에 보고하는 Glue입니다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerInventory))]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerSessionCacheReporter : NetworkBehaviour
    {
        /// <summary>inventory 값을 저장합니다.</summary>
        private PlayerInventory inventory;
        /// <summary>playerStats 값을 저장합니다.</summary>
        private PlayerStats playerStats;
        /// <summary>playerId 값을 저장합니다.</summary>
        private string playerId = string.Empty;

        /// <summary>Collector가 세션이 바뀐 뒤에도 플레이어를 식별할 인증 PlayerId입니다.</summary>
        public string PlayerId => playerId;

        /// <summary>Awake 작업을 수행합니다.</summary>
        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            playerStats = GetComponent<PlayerStats>();
        }

        /// <summary>OnEnable 작업을 수행합니다.</summary>
        private void OnEnable()
        {
            inventory?.AddInventoryChangedListener(PublishSnapshot);
            playerStats?.AddHealthChangedListener(HandleStatChanged);
            playerStats?.AddStaminaChangedListener(HandleStatChanged);
            playerStats?.AddManaChangedListener(HandleStatChanged);
        }

        /// <summary>OnDisable 작업을 수행합니다.</summary>
        private void OnDisable()
        {
            inventory?.RemoveInventoryChangedListener(PublishSnapshot);
            playerStats?.RemoveHealthChangedListener(HandleStatChanged);
            playerStats?.RemoveStaminaChangedListener(HandleStatChanged);
            playerStats?.RemoveManaChangedListener(HandleStatChanged);
        }

        /// <summary>OnNetworkDespawn 작업을 수행합니다.</summary>
        public override void OnNetworkDespawn()
        {
            PublishSnapshot();
        }

        /// <summary>PlayerId를 연결하고 저장된 상태가 있으면 새 서버 PlayerObject에 적용합니다.</summary>
        /// <param name="authenticatedPlayerId">인증된 플레이어 식별자입니다.</param>
        /// <param name="snapshot">30초 캐시에 남아 있던 상태이며 최초 접속이면 null입니다.</param>
        /// <param name="itemCatalog">ItemId를 ItemData로 변환할 중앙 데이터 허브입니다.</param>
        public void Initialize(
            string authenticatedPlayerId,
            PlayerSessionSnapshot snapshot,
            ItemCatalog itemCatalog)
        {
            if (!IsServer)
            {
                return;
            }

            playerId = authenticatedPlayerId;

            if (snapshot != null)
            {
                ApplySnapshot(snapshot, itemCatalog);
            }
            else
            {
                GetComponent<EchoZone.Equipment.PlayerEquipmentGlue>()?.GrantStartingInventory();
            }

            PublishSnapshot();
        }

        /// <summary>스탯 변경 이벤트의 현재 값은 다시 읽으므로 전달된 단일 값은 사용하지 않습니다.</summary>
        private void HandleStatChanged(int currentValue)
        {
            PublishSnapshot();
        }

        /// <summary>현재 서버 인벤토리와 스탯을 불변 복사본으로 만들어 캐시에 전달합니다.</summary>
        private void PublishSnapshot()
        {
            if (!IsServer ||
                string.IsNullOrEmpty(playerId) ||
                inventory == null ||
                playerStats == null ||
                NetworkPlayerSessionCacheGlue.Instance == null)
            {
                return;
            }

            PlayerSessionSnapshot snapshot = CreateSnapshot();
            if (snapshot == null)
            {
                return;
            }

            NetworkPlayerSessionCacheGlue.Instance.SaveLiveSnapshot(
                playerId,
                snapshot);
        }

        /// <summary>장착 교환이 모두 끝난 뒤 최종 인벤토리와 장착 상태를 함께 캐시에 기록합니다.</summary>
        public void PublishEquipmentSnapshot() => PublishSnapshot();

        /// <summary>현재 서버 PlayerObject의 인벤토리와 스탯을 독립된 데이터 복사본으로 만듭니다.</summary>
        /// <returns>서버 상태와 인증 ID가 준비되었으면 복사본이며 아니면 null입니다.</returns>
        public PlayerSessionSnapshot CreateSnapshot()
        {
            if (!IsServer ||
                string.IsNullOrEmpty(playerId) ||
                inventory == null ||
                playerStats == null)
            {
                return null;
            }

            List<CachedInventorySlot> cachedSlots = new();
            IReadOnlyList<InventorySlot> slots = inventory.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                InventorySlot slot = slots[i];
                if (slot?.Item == null || slot.Quantity <= 0)
                {
                    continue;
                }

                cachedSlots.Add(new CachedInventorySlot(
                    slot.Item.ItemId,
                    slot.Quantity, slot.InstanceId, slot.MagazineRounds, slot.PurchaseValue));
            }

            var weapon = GetComponent<EchoZone.Combat.Glue.NetworkWeaponFireGlue>();
            return new PlayerSessionSnapshot(
                cachedSlots,
                playerStats.CurrentHealth,
                playerStats.CurrentStamina,
                playerStats.CurrentMana) { CatalogVersion = RecoveryCatalog.Load().Version,
                    EquipmentJson = GetComponent<EchoZone.Equipment.PlayerEquipmentGlue>()?.Capture(),
                    WeaponDefinitionId = weapon != null ? weapon.DefinitionId : null,
                    WeaponJson = weapon != null ? weapon.CaptureMigrationWeapon((float)NetworkManager.ServerTime.Time) : null };
        }

        /// <summary>Host Migration Snapshot을 이미 Spawn된 서버 PlayerObject에 적용합니다.</summary>
        public void ApplyMigrationSnapshot(PlayerSessionSnapshot snapshot)
        {
            if (!IsServer || snapshot == null)
            {
                return;
            }

            ApplySnapshot(snapshot, NetworkPlayerSessionCacheGlue.Instance?.ItemCatalog);
            PublishSnapshot();
        }

        /// <summary>캐시의 원시 데이터를 현재 서버 PlayerObject의 Brick에 복원합니다.</summary>
        private void ApplySnapshot(PlayerSessionSnapshot snapshot, ItemCatalog itemCatalog)
        {
            // 마이그레이션의 기본 스탯 DTO는 별도 ActorRecord에서 무기를 복원합니다.
            if (snapshot.CatalogVersion != 0 || !string.IsNullOrEmpty(snapshot.WeaponDefinitionId))
                GetComponent<EchoZone.Combat.Glue.NetworkWeaponFireGlue>()?.RestoreDefinition(
                    snapshot.WeaponDefinitionId, snapshot.WeaponJson, snapshot.CatalogVersion, (float)NetworkManager.ServerTime.Time);
            if (inventory == null || playerStats == null || itemCatalog == null)
            {
                return;
            }

            List<InventorySlot> restoredSlots = new(snapshot.InventorySlots.Count);
            for (int i = 0; i < snapshot.InventorySlots.Count; i++)
            {
                CachedInventorySlot cachedSlot = snapshot.InventorySlots[i];
                if (!itemCatalog.TryGetItem(cachedSlot.ItemId, out ItemData item))
                {
                    continue;
                }

                restoredSlots.Add(new InventorySlot(item, cachedSlot.Quantity,
                    cachedSlot.InstanceId, cachedSlot.MagazineRounds, cachedSlot.PurchaseValue));
            }

            inventory.ReplaceSlots(restoredSlots);
            GetComponent<EchoZone.Equipment.PlayerEquipmentGlue>()?.Restore(snapshot.EquipmentJson);
            playerStats.SetCurrentValues(
                snapshot.Health,
                snapshot.Stamina,
                snapshot.Mana);
        }
    }
}
