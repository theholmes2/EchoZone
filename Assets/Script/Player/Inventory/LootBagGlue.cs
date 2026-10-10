using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Equipment
{
    /// <summary>아이템 개별 상태와 남은 수량을 서버에서만 보관하는 전리품 상자입니다.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class LootBagGlue : NetworkBehaviour
    {
        private readonly List<InventorySlot> contents = new();
        private readonly NetworkVariable<FixedString128Bytes> presentationItemId = new();
        private readonly NetworkVariable<bool> loosePresentation = new();
        private string stableId;
        private bool randomAmmo;
        private GameObject presentationInstance;
        private Renderer containerRenderer;
        /// <summary>복구할 상자에 안정적인 ID와 독립된 슬롯 복사본을 연결합니다.</summary>
        public void Initialize(string id, bool random, IEnumerable<InventorySlot> slots)
        {
            stableId = id; randomAmmo = random; contents.Clear();
            foreach (var slot in slots) if (slot?.Item != null && slot.Quantity > 0) contents.Add(slot.Copy());
            loosePresentation.Value = random && contents.Count > 0;
            presentationItemId.Value = loosePresentation.Value
                ? new FixedString128Bytes(contents[0].Item.ItemId)
                : default;
        }
        /// <summary>랜덤 생성 상한 집계에서 사망 전리품을 제외합니다.</summary>
        public bool IsRandomAmmo => randomAmmo;
        /// <summary>복제된 단일 전리품 ID를 카탈로그 정의로 변환하여 로컬 안내에 제공합니다.</summary>
        public bool TryGetPresentationItem(out ItemData item)
        {
            item = null;
            return loosePresentation.Value && !presentationItemId.Value.IsEmpty &&
                LootWorldGlue.Instance?.Config?.items != null &&
                LootWorldGlue.Instance.Config.items.TryGetItem(presentationItemId.Value.ToString(), out item);
        }
        /// <summary>복제된 아이템 ID를 사용해 모든 클라이언트에서 동일한 바닥 전리품 외형을 표시합니다.</summary>
        public override void OnNetworkSpawn()
        {
            containerRenderer = GetComponent<Renderer>();
            presentationItemId.OnValueChanged += PresentationChanged;
            loosePresentation.OnValueChanged += LoosePresentationChanged;
            RefreshPresentation();
        }
        /// <summary>재사용이나 Despawn 시 표시 이벤트와 생성한 로컬 외형을 정리합니다.</summary>
        public override void OnNetworkDespawn()
        {
            presentationItemId.OnValueChanged -= PresentationChanged;
            loosePresentation.OnValueChanged -= LoosePresentationChanged;
            DisposePresentation();
        }
        private void PresentationChanged(FixedString128Bytes previous, FixedString128Bytes current) => RefreshPresentation();
        private void LoosePresentationChanged(bool previous, bool current) => RefreshPresentation();
        /// <summary>전리품 상자 또는 단일 아이템 외형 중 현재 복제 상태에 맞는 표시를 적용합니다.</summary>
        private void RefreshPresentation()
        {
            DisposePresentation();
            bool showItem = loosePresentation.Value && !presentationItemId.Value.IsEmpty;
            if (containerRenderer != null) containerRenderer.enabled = !showItem;
            if (!showItem || LootWorldGlue.Instance?.Config?.items == null ||
                !LootWorldGlue.Instance.Config.items.TryGetItem(presentationItemId.Value.ToString(), out var item) ||
                item.WorldVisualPrefab == null) return;
            presentationInstance = Instantiate(item.WorldVisualPrefab, transform);
            presentationInstance.name = $"{item.ItemName} Visual";
            presentationInstance.transform.SetLocalPositionAndRotation(
                LootWorldGlue.Instance.Config.looseVisualOffset,
                Quaternion.identity);
        }
        /// <summary>파괴 예약 프레임에도 빛과 파티클이 남지 않도록 외형을 먼저 비활성화합니다.</summary>
        private void DisposePresentation()
        {
            if (presentationInstance == null) return;
            presentationInstance.SetActive(false);
            Destroy(presentationInstance);
            presentationInstance = null;
        }
        /// <summary>기존 E 상호작용이 거리·생존을 재검증한 뒤 가능한 수량만 이동합니다.</summary>
        public bool Take(GameObject interactor)
        {
            var world = LootWorldGlue.Instance;
            if (!IsServer || !IsSpawned || world == null || EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring ||
                interactor == null || !interactor.TryGetComponent<PlayerInventory>(out var inventory) ||
                !interactor.TryGetComponent<PlayerStats>(out var stats) || stats.IsDead ||
                (interactor.TryGetComponent<EchoZone.Heist.PlayerWalletGlue>(out var wallet) && wallet.IsEscaping) ||
                (interactor.transform.position-transform.position).sqrMagnitude > world.Config.interactionDistance * world.Config.interactionDistance) return false;
            bool changed=false;
            for(int i=contents.Count-1;i>=0;i--)
            {
                var slot=contents[i];
                if(!string.IsNullOrEmpty(slot.InstanceId))
                {
                    if(!inventory.TryAddInstance(slot)) continue;
                    contents.RemoveAt(i); changed=true;
                }
                else
                {
                    int added=inventory.AddUpToCapacity(slot.Item,slot.Quantity);
                    if(added<=0) continue;
                    slot.Remove(added); changed=true;
                    if(slot.Quantity==0) contents.RemoveAt(i);
                }
            }
            if(contents.Count==0) NetworkObject.Despawn(true);
            return changed;
        }
        /// <summary>호스트가 바뀌어도 상자 위치와 총별 잔량을 보존합니다.</summary>
        public LootBagRecord Capture()
        {
            var record=new LootBagRecord { id=stableId, position=transform.position, randomAmmo=randomAmmo };
            foreach(var slot in contents) record.slots.Add(new LootSlotRecord { itemId=slot.Item.ItemId, quantity=slot.Quantity, instanceId=slot.InstanceId, rounds=slot.MagazineRounds, purchaseValue=slot.PurchaseValue });
            return record;
        }
    }
    /// <summary>상자 안의 아이템을 Unity 참조 없이 저장합니다.</summary>
    [Serializable] public sealed class LootSlotRecord
    {
        /// <summary>아이템 정의와 개별 장비 식별자입니다.</summary>
        public string itemId, instanceId;
        /// <summary>중첩 수량과 총의 탄창 잔량입니다.</summary>
        public int quantity, rounds;
        /// <summary>구매 총기의 반환액을 사망 전리품에서도 유지합니다.</summary>
        public int purchaseValue;
    }
    /// <summary>마이그레이션에서 생성할 전리품 상자 기록입니다.</summary>
    [Serializable] public sealed class LootBagRecord
    {
        /// <summary>상자의 안정적 식별자입니다.</summary>
        public string id;
        /// <summary>복원할 상자 위치입니다.</summary>
        public Vector3 position;
        /// <summary>랜덤 탄약 상한에 포함되는 상자인지 나타냅니다.</summary>
        public bool randomAmmo;
        /// <summary>아직 획득하지 않은 아이템 목록입니다.</summary>
        public List<LootSlotRecord> slots=new();
    }
}
