using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Equipment
{
    /// <summary>기존 서버 중앙 갱신에서 상자 생성과 마이그레이션을 담당합니다.</summary>
    public sealed class LootWorldGlue : MonoBehaviour
    {
        /// <summary>현재 씬의 상자 생성 연결입니다.</summary>
        public static LootWorldGlue Instance { get; private set; }
        /// <summary>상자 프리팹·아이템·생성 정책입니다.</summary>
        [SerializeField] private LootWorldConfig config;
        /// <summary>미리 도로·공터에 배치한 생성 후보입니다.</summary>
        [SerializeField] private Transform[] spawnPoints=Array.Empty<Transform>();
        private readonly LootSpawnBrick spawnBrick = new();
        private readonly List<float> spawnWeights = new();
        private double nextSpawn = -1;
        private int remainingInitialSpawnCount = -1;
        /// <summary>상자 거리 검사에 사용하는 공통 설정입니다.</summary>
        public LootWorldConfig Config => config;
        private void Awake() => Instance=this;
        private void OnDestroy() { if(Instance==this) Instance=null; }
        /// <summary>소유자 유무와 무관하게 서버가 한 번씩 생성 회차를 갱신합니다.</summary>
        public void ManualUpdate(double now)
        {
            if(config==null || NetworkManager.Singleton==null || !NetworkManager.Singleton.IsServer || EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring) return;
            if(nextSpawn<0)
            {
                remainingInitialSpawnCount=Math.Min(config.initialSpawnCount,config.randomBoxLimit);
                nextSpawn=now;
            }
            if(now<nextSpawn) return;
            bool initialSpawn=remainingInitialSpawnCount>0;
            nextSpawn=now+(initialSpawn ? 0.2 : Math.Max(1,config.spawnInterval));
            int count=0; foreach(var bag in FindObjectsByType<LootBagGlue>(FindObjectsSortMode.None)) if(bag.IsSpawned && bag.IsRandomAmmo) count++;
            if(count>=config.randomBoxLimit) { remainingInitialSpawnCount=0; return; }
            if(spawnPoints.Length==0 || config.RandomDrops.Count==0 || (!initialSpawn && UnityEngine.Random.value>config.spawnChance)) return;
            spawnWeights.Clear();
            for(int i=0;i<config.RandomDrops.Count;i++) spawnWeights.Add(config.RandomDrops[i]?.weight ?? 0f);
            int selectedIndex=spawnBrick.SelectWeightedIndex(spawnWeights,UnityEngine.Random.value);
            if(selectedIndex<0) return;
            RandomLootEntry selected=config.RandomDrops[selectedIndex];
            if(selected?.item==null || selected.amount<=0) return;
            int start=UnityEngine.Random.Range(0,spawnPoints.Length);
            for(int i=0;i<spawnPoints.Length;i++)
            {
                var point=spawnPoints[(start+i)%spawnPoints.Length]; if(point==null) continue;
                if(Physics.CheckSphere(point.position,config.clearance,config.obstacleLayers,QueryTriggerInteraction.Ignore)) continue;
                Vector3 skyCheckOrigin = point.position + Vector3.up * config.clearance;
                if(config.openSkyCheckHeight > 0f && Physics.Raycast(
                    skyCheckOrigin,
                    Vector3.up,
                    config.openSkyCheckHeight,
                    config.obstacleLayers,
                    QueryTriggerInteraction.Ignore)) continue;
                var slots=new List<InventorySlot>(); int left=selected.amount;
                while(left>0) { int amount=Math.Min(left,selected.item.MaxStackSize); slots.Add(new InventorySlot(selected.item,amount)); left-=amount; }
                if(Spawn(point.position,slots,true) && initialSpawn) remainingInitialSpawnCount--;
                break;
            }
        }
        /// <summary>서버에서 아이템 복사본을 상자로 생성하며 원본 인벤토리는 호출자가 성공 후 비웁니다.</summary>
        public bool Spawn(Vector3 position, IEnumerable<InventorySlot> slots, bool random=false, string id=null)
        {
            var manager=NetworkManager.Singleton;
            if(manager==null || !manager.IsServer || config?.prefab==null || !manager.NetworkConfig.Prefabs.Contains(config.prefab.gameObject)) return false;
            var bag=Instantiate(config.prefab,position,Quaternion.identity);
            bag.Initialize(id ?? Guid.NewGuid().ToString("N"),random,slots);
            bag.NetworkObject.Spawn(true); return true;
        }
        /// <summary>상자 및 남은 생성 시간을 현재 세션 Snapshot에 저장합니다.</summary>
        public void Capture(EchoZone.Online.Migration.SessionWorldSnapshot snapshot,double now)
        {
            snapshot.lootSpawnRemaining=(float)Math.Max(0,nextSpawn<0 ? config.spawnInterval : nextSpawn-now);
            foreach(var bag in FindObjectsByType<LootBagGlue>(FindObjectsSortMode.None)) if(bag.IsSpawned) snapshot.lootBags.Add(bag.Capture());
        }
        /// <summary>모든 ID를 검증한 뒤 기존 상자를 교체하고 남은 생성 시간을 복원합니다.</summary>
        public void Restore(EchoZone.Online.Migration.SessionWorldSnapshot snapshot,double now)
        {
            var entries=snapshot.lootBags ?? new List<LootBagRecord>();
            var ids=new HashSet<string>(); var instances=new HashSet<string>();
            var restored=new List<List<InventorySlot>>();
            foreach(var record in entries)
            {
                if(record==null || string.IsNullOrEmpty(record.id) || !ids.Add(record.id) || record.slots==null ||
                    !float.IsFinite(record.position.x) || !float.IsFinite(record.position.y) || !float.IsFinite(record.position.z)) throw new InvalidOperationException("Invalid loot record.");
                var slots=new List<InventorySlot>();
                foreach(var slot in record.slots)
                {
                    if(slot==null || !config.items.TryGetItem(slot.itemId,out var item) || slot.quantity<=0 || slot.quantity>item.MaxStackSize ||
                        (!string.IsNullOrEmpty(slot.instanceId) && !instances.Add(slot.instanceId))) throw new InvalidOperationException("Invalid loot item.");
                    slots.Add(new InventorySlot(item,slot.quantity,slot.instanceId,slot.rounds,slot.purchaseValue));
                }
                restored.Add(slots);
            }
            if(entries.Count>0 && (config.prefab==null || !NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(config.prefab.gameObject))) throw new InvalidOperationException("Loot prefab not registered.");
            foreach(var bag in FindObjectsByType<LootBagGlue>(FindObjectsSortMode.None)) if(bag.IsSpawned) bag.NetworkObject.Despawn(true);
            for(int i=0;i<entries.Count;i++) Spawn(entries[i].position,restored[i],entries[i].randomAmmo,entries[i].id);
            remainingInitialSpawnCount=0;
            nextSpawn=now+Math.Max(0,snapshot.lootSpawnRemaining);
        }
    }
}
