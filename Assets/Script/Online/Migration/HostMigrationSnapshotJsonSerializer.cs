using System;
using System.Collections.Generic;
using EchoZone.Online.Reconnect;
using UnityEngine;

namespace EchoZone.Online.Migration
{
    /// <summary>Host Migration 도메인 Snapshot과 JSON 전송 형식을 상호 변환하는 Brick입니다.</summary>
    public sealed class HostMigrationSnapshotJsonSerializer
    {
        /// <summary>현재 코드가 읽고 쓸 수 있는 JSON 구조 버전입니다.</summary>
        private const int CurrentSchemaVersion = 4;

        /// <summary>게임 로직용 Snapshot을 JSON 문자열로 변환합니다.</summary>
        /// <param name="snapshot">직렬화할 Host Migration 복사본입니다.</param>
        /// <returns>전송·저장 가능한 JSON이며 Snapshot이 없으면 빈 문자열입니다.</returns>
        public string Serialize(HostMigrationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            HostMigrationSnapshotDto dto = ToDto(snapshot);
            return JsonUtility.ToJson(dto);
        }

        /// <summary>JSON 문자열을 검증하고 게임 로직용 Snapshot으로 복원합니다.</summary>
        /// <param name="json">Cloud 또는 후보 Client에서 받은 JSON입니다.</param>
        /// <param name="snapshot">복원된 Host Migration 복사본입니다.</param>
        /// <returns>지원하는 형식의 유효한 Snapshot이면 <see langword="true"/>입니다.</returns>
        public bool TryDeserialize(
            string json,
            out HostMigrationSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                HostMigrationSnapshotDto dto =
                    JsonUtility.FromJson<HostMigrationSnapshotDto>(json);
                if (dto == null ||
                    (dto.schemaVersion < 1 || dto.schemaVersion > CurrentSchemaVersion) ||
                    string.IsNullOrWhiteSpace(dto.runId) ||
                    dto.snapshotVersion <= 0)
                {
                    return false;
                }

                snapshot = FromDto(dto);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>도메인 Snapshot의 중첩 상태를 JSON용 원시 필드 구조로 변환합니다.</summary>
        private static HostMigrationSnapshotDto ToDto(
            HostMigrationSnapshot snapshot)
        {
            HostMigrationSnapshotDto dto = new()
            {
                schemaVersion = CurrentSchemaVersion,
                runId = snapshot.RunId,
                snapshotVersion = snapshot.SnapshotVersion,
                world = snapshot.World
            };

            for (int i = 0; i < snapshot.Players.Count; i++)
            {
                HostMigrationPlayerSnapshot player = snapshot.Players[i];
                if (player?.State == null)
                {
                    continue;
                }

                PlayerMigrationDto playerDto = new()
                {
                    playerId = player.PlayerId,
                    health = player.State.Health,
                    stamina = player.State.Stamina,
                    mana = player.State.Mana,
                    equipmentJson = player.State.EquipmentJson
                };

                for (int slotIndex = 0;
                     slotIndex < player.State.InventorySlots.Count;
                     slotIndex++)
                {
                    CachedInventorySlot slot =
                        player.State.InventorySlots[slotIndex];
                    playerDto.inventorySlots.Add(new InventorySlotDto
                    {
                        itemId = slot.ItemId,
                        quantity = slot.Quantity,
                        instanceId = slot.InstanceId,
                        magazineRounds = slot.MagazineRounds,
                        purchaseValue = slot.PurchaseValue
                    });
                }

                dto.players.Add(playerDto);
            }

            for (int i = 0; i < snapshot.WorldItems.Count; i++)
            {
                WorldItemMigrationSnapshot worldItem = snapshot.WorldItems[i];
                if (worldItem == null)
                {
                    continue;
                }

                dto.worldItems.Add(new WorldItemMigrationDto
                {
                    worldItemId = worldItem.WorldItemId,
                    itemId = worldItem.ItemId,
                    quantity = worldItem.Quantity,
                    isDepleted = worldItem.IsDepleted,
                    position = worldItem.Position, rotation = worldItem.Rotation, hasTransform = worldItem.HasTransform
                });
            }

            return dto;
        }

        /// <summary>검증된 JSON 원시 필드 구조를 게임 로직용 Snapshot으로 변환합니다.</summary>
        private static HostMigrationSnapshot FromDto(
            HostMigrationSnapshotDto dto)
        {
            List<HostMigrationPlayerSnapshot> players = new();
            if (dto.players != null)
            {
                for (int i = 0; i < dto.players.Count; i++)
                {
                    PlayerMigrationDto playerDto = dto.players[i];
                    if (playerDto == null ||
                        string.IsNullOrWhiteSpace(playerDto.playerId))
                    {
                        continue;
                    }

                    List<CachedInventorySlot> inventorySlots = new();
                    if (playerDto.inventorySlots != null)
                    {
                        for (int slotIndex = 0;
                             slotIndex < playerDto.inventorySlots.Count;
                             slotIndex++)
                        {
                            InventorySlotDto slotDto =
                                playerDto.inventorySlots[slotIndex];
                            if (slotDto == null ||
                                string.IsNullOrWhiteSpace(slotDto.itemId) ||
                                slotDto.quantity <= 0)
                            {
                                continue;
                            }

                            inventorySlots.Add(new CachedInventorySlot(
                                slotDto.itemId,
                                slotDto.quantity, slotDto.instanceId,
                                dto.schemaVersion >= 3 ? slotDto.magazineRounds : -1,
                                dto.schemaVersion >= 4 ? slotDto.purchaseValue : 0));
                        }
                    }

                    players.Add(new HostMigrationPlayerSnapshot(
                        playerDto.playerId,
                        new PlayerSessionSnapshot(
                            inventorySlots,
                            playerDto.health,
                            playerDto.stamina,
                            playerDto.mana) { EquipmentJson = playerDto.equipmentJson }));
                }
            }

            List<WorldItemMigrationSnapshot> worldItems = new();
            if (dto.worldItems != null)
            {
                for (int i = 0; i < dto.worldItems.Count; i++)
                {
                    WorldItemMigrationDto itemDto = dto.worldItems[i];
                    if (itemDto == null ||
                        string.IsNullOrWhiteSpace(itemDto.worldItemId) ||
                        string.IsNullOrWhiteSpace(itemDto.itemId))
                    {
                        continue;
                    }

                    worldItems.Add(new WorldItemMigrationSnapshot(
                        itemDto.worldItemId,
                        itemDto.itemId,
                        itemDto.quantity,
                        itemDto.isDepleted, itemDto.position, itemDto.rotation, itemDto.hasTransform));
                }
            }

            return new HostMigrationSnapshot(
                dto.runId,
                dto.snapshotVersion,
                players,
                worldItems,
                dto.schemaVersion >= 2 && !string.IsNullOrWhiteSpace(dto.world?.worldId) ? dto.world : null);
        }

        /// <summary>전체 Host Migration Snapshot의 JSON 필드 구조입니다.</summary>
        [Serializable]
        private sealed class HostMigrationSnapshotDto
        {
            /// <summary>버전 2의 확장 월드이며 버전 1은 null입니다.</summary>
            public SessionWorldSnapshot world;
            /// <summary>schemaVersion 값을 저장합니다.</summary>
            public int schemaVersion;
            /// <summary>runId 값을 저장합니다.</summary>
            public string runId = string.Empty;
            /// <summary>snapshotVersion 값을 저장합니다.</summary>
            public long snapshotVersion;
            /// <summary>players 값을 저장합니다.</summary>
            public List<PlayerMigrationDto> players = new();
            /// <summary>worldItems 값을 저장합니다.</summary>
            public List<WorldItemMigrationDto> worldItems = new();
        }

        /// <summary>한 플레이어 상태의 JSON 필드 구조입니다.</summary>
        [Serializable]
        private sealed class PlayerMigrationDto
        {
            /// <summary>빈 장착 슬롯까지 보존하는 장비 복사본입니다.</summary>
            public string equipmentJson;
            /// <summary>playerId 값을 저장합니다.</summary>
            public string playerId = string.Empty;
            /// <summary>health 값을 저장합니다.</summary>
            public int health;
            /// <summary>stamina 값을 저장합니다.</summary>
            public int stamina;
            /// <summary>mana 값을 저장합니다.</summary>
            public int mana;
            /// <summary>inventorySlots 값을 저장합니다.</summary>
            public List<InventorySlotDto> inventorySlots = new();
        }

        /// <summary>인벤토리 한 슬롯의 JSON 필드 구조입니다.</summary>
        [Serializable]
        private sealed class InventorySlotDto
        {
            /// <summary>중복 장비의 개별 식별자입니다.</summary>
            public string instanceId;
            /// <summary>보관 장비의 탄창 잔량입니다.</summary>
            public int magazineRounds;
            /// <summary>버전 4부터 보존하는 실제 장비 구매 금액입니다.</summary>
            public int purchaseValue;
            /// <summary>itemId 값을 저장합니다.</summary>
            public string itemId = string.Empty;
            /// <summary>quantity 값을 저장합니다.</summary>
            public int quantity;
        }

        /// <summary>월드 아이템 한 개의 JSON 필드 구조입니다.</summary>
        [Serializable]
        private sealed class WorldItemMigrationDto
        {
            /// <summary>버전 2에서 보존하는 아이템 Transform입니다.</summary>
            public Vector3 position;
            public Quaternion rotation;
            public bool hasTransform;
            /// <summary>worldItemId 값을 저장합니다.</summary>
            public string worldItemId = string.Empty;
            /// <summary>itemId 값을 저장합니다.</summary>
            public string itemId = string.Empty;
            /// <summary>quantity 값을 저장합니다.</summary>
            public int quantity;
            /// <summary>isDepleted 값을 저장합니다.</summary>
            public bool isDepleted;
        }
    }
}
