using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EchoZone.Combat.Glue;
using EchoZone.Enemy;
using EchoZone.Heist;
using EchoZone.Pet;
using EchoZone.Player.Network;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace EchoZone.Online.Migration
{
    /// <summary>기존 마이그레이션 호출에서만 동작하는 월드 복구 장벽과 고정 ID 연결 계층입니다.</summary>
    public static class SessionWorldMigrationGlue
    {
        /// <summary>관계 연결이 끝나기 전 서버 게임 규칙을 중단합니다.</summary>
        public static bool IsRestoring { get; private set; }
        /// <summary>중복 적용을 막는 현재 네트워크 서버 인스턴스입니다.</summary>
        private static NetworkManager appliedManager;
        /// <summary>마지막 완료 복원 식별자입니다.</summary>
        private static string appliedKey;
        /// <summary>현재 Run에서 승인한 콘텐츠 버전입니다. 실행 중 카탈로그 교체를 허용하지 않습니다.</summary>
        private static int runCatalogVersion;
        /// <summary>늦게 생성될 플레이어의 계정별 상태입니다.</summary>
        private static readonly Dictionary<string, ActorRecord> pendingPlayers = new();
        /// <summary>복구 뒤 아직 재접속하지 않은 계정의 스탯·인벤토리를 다음 체크포인트에도 보존합니다.</summary>
        private static readonly Dictionary<string, HostMigrationPlayerSnapshot> pendingPlayerStats = new();

        /// <summary>도메인 리로드를 꺼도 이전 실행 상태를 남기지 않습니다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        { IsRestoring = false; appliedManager = null; appliedKey = null; runCatalogVersion = 0; pendingPlayers.Clear(); pendingPlayerStats.Clear(); }

        /// <summary>호스트 선출 이벤트에서 새 서버 AI가 실행되기 전에 장벽을 닫습니다.</summary>
        public static void Begin() { IsRestoring = true; appliedKey = null; }

        /// <summary>서버 복구 또는 클라이언트 재연결이 끝난 뒤 기존 루프를 재개합니다.</summary>
        public static void Complete() => IsRestoring = false;
        /// <summary>같은 서버에서 이미 적용한 스냅샷은 인벤토리까지 포함해 재적용하지 않습니다.</summary>
        public static bool AlreadyApplied(HostMigrationSnapshot snapshot) => snapshot != null && appliedManager == NetworkManager.Singleton && appliedKey == snapshot.RunId + ":" + snapshot.SnapshotVersion;
        /// <summary>늦게 재접속할 플레이어의 기본 상태를 체크포인트에 추가합니다.</summary>
        public static void IncludePendingPlayers(List<HostMigrationPlayerSnapshot> players)
        {
            foreach (var pair in pendingPlayerStats)
                if (!players.Exists(p => p.PlayerId == pair.Key)) players.Add(pair.Value);
        }
        /// <summary>이미 접속한 계정은 Cloud Revision과 동일 정산 영수증을 실제 조회해 복구 전에 대조합니다.</summary>
        public static async Task VerifyConnectedWallets(HostMigrationSnapshot snapshot, string sessionId)
        {
            if (snapshot?.World == null) return;
            var config = ExtractionPoint.Instance?.Config;
            if (config == null && snapshot.World.wallets.Count > 0) throw new InvalidOperationException("Wallet verification config missing.");
            var service = new WalletCloudService();
            foreach (var record in snapshot.World.wallets)
            {
                var wallet = WalletSessionBrick.Restore(record.json);
                if (!wallet.Loaded) continue;
                var cloud = await service.LoadForRun(config, sessionId, record.playerId, snapshot.RunId);
                if (cloud.Recoveries.Exists(r => r.RunId == snapshot.RunId && r.State != "CheckpointConfirmed"))
                    throw new InvalidOperationException("Recovery journal is newer than checkpoint; manual reconciliation required.");
                if (cloud.Pending?.State == "Conflict") throw new InvalidOperationException("Pending wallet conflict; manual reconciliation required.");
                ValidateSettlementReceipt(wallet, cloud);
                wallet.VerifyCloud(cloud.Balance, cloud.Revision, cloud.LastSettlementId);
                record.json = wallet.Export();
            }
        }

        /// <summary>같은 정산 ID라도 대상 펫·원장이 바뀐 복사본은 적용하지 않습니다.</summary>
        public static void ValidateSettlementReceipt(WalletSessionBrick wallet, WalletCloudRecord cloud)
        {
            if (wallet.Request == null) return;
            var receipt = cloud.Receipts.Find(r => r.Request.SettlementId == wallet.SettlementId);
            if (receipt != null && !receipt.Request.SamePayload(wallet.Request))
                throw new InvalidOperationException("Settlement target mismatch; manual reconciliation required.");
            if (cloud.LastSettlementId == wallet.SettlementId && receipt == null)
                throw new InvalidOperationException("Settlement receipt missing; manual reconciliation required.");
        }

        /// <summary>현재 월드의 각 기존 컴포넌트에서 확장 상태를 모읍니다.</summary>
        public static SessionWorldSnapshot Capture(string runId)
        {
            var world = HeistWorldGlue.Instance;
            if (world == null || !world.IsServer || IsRestoring) return null;
            float now = (float)NetworkManager.Singleton.ServerTime.Time;
            var snapshot = world.CaptureMigration(runId, now);
            var catalog = RecoveryCatalog.Load();
            if (runCatalogVersion == 0) runCatalogVersion = catalog.Version;
            catalog.RequireVersion(runCatalogVersion);
            snapshot.catalogVersion = runCatalogVersion;
            foreach (var pet in PetUpdateManager.Pets) if (pet != null && pet.IsSpawned) snapshot.pets.Add(pet.CaptureMigration());
            foreach (var police in Object.FindObjectsByType<PoliceEnemyBrainGlue>(FindObjectsSortMode.None))
                if (police.IsSpawned) snapshot.police.Add(police.CaptureMigration(now));
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                var obj = client.PlayerObject; if (obj == null) continue;
                snapshot.players.Add(new ActorRecord { id = HeistWorldGlue.PlayerIdentity(client.ClientId),
                    position = obj.transform.position, rotation = obj.transform.rotation,
                    weaponDefinitionId = obj.GetComponent<NetworkWeaponFireGlue>()?.DefinitionId,
                    weaponJson = obj.GetComponent<NetworkWeaponFireGlue>()?.CaptureMigrationWeapon(now),
                    respawnRemaining = obj.GetComponent<PlayerDeathGlue>()?.CaptureRespawn(now) ?? -1 });
            }
            foreach (var pair in pendingPlayers)
                if (!snapshot.players.Exists(p => p.id == pair.Key)) snapshot.players.Add(pair.Value);
            Object.FindFirstObjectByType<EnemySpawnManager>()?.CaptureMigration(snapshot, now);
            return snapshot;
        }

        /// <summary>같은 스냅샷 재적용은 건너뛰고 객체→원장→소유 관계 순으로 복구합니다.</summary>
        public static void Restore(HostMigrationSnapshot snapshot)
        {
            if (snapshot.World == null) return;
            var manager = NetworkManager.Singleton;
            string key = snapshot.RunId + ":" + snapshot.SnapshotVersion;
            if (appliedManager == manager && appliedKey == key) return;
            IsRestoring = true;
            var world = HeistWorldGlue.Instance;
            if (world == null || !world.IsSpawned) throw new InvalidOperationException("Heist world is not ready for migration.");
            var data = JsonUtility.FromJson<SessionWorldSnapshot>(JsonUtility.ToJson(snapshot.World));
            SessionWorldSnapshotValidator.Validate(data);
            var catalog = RecoveryCatalog.Load(); catalog.RequireVersion(data.catalogVersion);
            runCatalogVersion = data.catalogVersion;
            foreach (var record in data.pets)
                if (!manager.NetworkConfig.Prefabs.Contains(catalog.Pet(record.definitionId).Prefab.gameObject))
                    throw new InvalidOperationException("Recovery pet prefab is not registered with NGO.");
            foreach (var record in data.players) catalog.Weapon(record.weaponDefinitionId);
            foreach (var record in data.police) catalog.Weapon(record.weaponDefinitionId);
            SeparateRestorePositions(data, world.Config);
            foreach (var pet in new List<PetStateGlue>(PetUpdateManager.Pets))
                if (pet != null && pet.IsSpawned) pet.NetworkObject.Despawn(true);
            float now = (float)manager.ServerTime.Time;
            var spawner = Object.FindFirstObjectByType<EnemySpawnManager>();
            if (data.police.Count > 0 && spawner == null) throw new InvalidOperationException("Migration police spawner is missing.");
            spawner?.RestoreMigration(data, now);
            world.RestoreMigration(data, now);
            PoliceDestinationBrick.Shared.Clear();
            foreach (var record in data.pets)
            {
                if (world.IsRetiredPet(record.id)) continue;
                var follow = Object.Instantiate(catalog.Pet(record.definitionId).Prefab, record.position, record.rotation);
                follow.NetworkObject.Spawn();
                follow.GetComponent<PetStateGlue>().RestoreMigration(record);
                follow.GetComponent<PetHeistGlue>()?.SetReportedServer(world.IsReportedPet(record.id));
            }
            pendingPlayers.Clear();
            pendingPlayerStats.Clear();
            foreach (var record in snapshot.Players) pendingPlayerStats[record.PlayerId] = record;
            foreach (var record in data.players) pendingPlayers.Add(record.id, record);
            foreach (var client in manager.ConnectedClientsList)
                client.PlayerObject?.GetComponent<PlayerWalletGlue>()?.RebindAfterMigration();
            ApplyPendingPlayers();
            appliedManager = manager; appliedKey = key;
        }

        /// <summary>재접속 PlayerObject가 생기면 위치·총기·부활 예약을 한 번만 복원합니다.</summary>
        public static void ApplyPendingPlayers()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer) return;
            foreach (var client in manager.ConnectedClientsList)
            {
                var obj = client.PlayerObject;
                if (obj == null || !obj.IsSpawned) continue;
                string id = HeistWorldGlue.PlayerIdentity(client.ClientId);
                if (!pendingPlayers.TryGetValue(id, out var record)) continue;
                Place(obj, record.position, record.rotation);
                float now = (float)manager.ServerTime.Time;
                obj.GetComponent<NetworkWeaponFireGlue>()?.RestoreDefinition(record.weaponDefinitionId, record.weaponJson, RecoveryCatalog.Load().Version, now);
                obj.GetComponent<PlayerDeathGlue>()?.RestoreRespawn(record.respawnRemaining, now);
                pendingPlayers.Remove(id);
                pendingPlayerStats.Remove(id);
            }
            foreach (var police in Object.FindObjectsByType<PoliceEnemyBrainGlue>(FindObjectsSortMode.None))
                if (police.IsSpawned) police.ResolveMigrationTarget();
        }

        /// <summary>대상 관계는 세션별 NetworkObjectId 대신 계정 또는 고정 펫 ID로 저장합니다.</summary>
        public static string TargetId(Transform target)
        {
            if (target == null) return null;
            var obj = target.GetComponentInParent<NetworkObject>();
            if (obj == null) return null;
            if (obj.IsPlayerObject) return "player:" + HeistWorldGlue.PlayerIdentity(obj.OwnerClientId);
            var pet = obj.GetComponent<PetStateGlue>(); return pet != null ? "pet:" + pet.PetId : null;
        }

        /// <summary>복원한 객체 목록에서 영속 관계를 현재 Transform으로 연결합니다.</summary>
        public static Transform ResolveTarget(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith("player:", StringComparison.Ordinal))
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                    if ("player:" + HeistWorldGlue.PlayerIdentity(client.ClientId) == id) return client.PlayerObject?.transform;
            foreach (var pet in PetUpdateManager.Pets)
                if (pet != null && "pet:" + pet.PetId == id) return pet.transform;
            return null;
        }

        /// <summary>NavMesh와 물리 위치를 함께 복구하고 네트워크 텔레포트를 전송합니다.</summary>
        public static void Place(NetworkObject obj, Vector3 position, Quaternion rotation)
        {
            var agent = obj.GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                if (!NavMesh.SamplePosition(position, out var hit, Mathf.Max(agent.radius, 0.1f), filter))
                    throw new InvalidOperationException("Migration position is outside NavMesh: " + obj.name);
                position = hit.position; agent.Warp(position);
                if (agent.isOnNavMesh) agent.ResetPath();
            }
            if (obj.TryGetComponent<Rigidbody>(out var body))
            {
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.position = position; body.rotation = rotation;
            }
            obj.transform.SetPositionAndRotation(position, rotation);
            var sync = obj.GetComponent<NetworkTransform>();
            if (sync != null && sync.IsSpawned) sync.Teleport(position, rotation, obj.transform.localScale);
        }

        /// <summary>같은 문으로 취소 복귀한 개체만 유효한 NavMesh 후보로 분산합니다.</summary>
        private static void SeparateRestorePositions(SessionWorldSnapshot data, HeistConfig config)
        {
            var occupied = new List<Vector3>();
            var actors = new List<ActorRecord>(data.police); actors.AddRange(data.pets);
            foreach (var actor in actors)
            {
                bool placed = false;
                for (int i = 0; i < config.RestorePositionCandidates; i++)
                {
                    float radius = config.RestoreSpacing * (1 + i / 8);
                    var candidate = PoliceDestinationBrick.Candidate(actor.position, i, 8, radius, 0);
                    if (!NavMesh.SamplePosition(candidate, out var hit, config.RestoreSampleRadius, NavMesh.AllAreas)) continue;
                    bool overlaps = occupied.Exists(p => (p - hit.position).sqrMagnitude < config.RestoreSpacing * config.RestoreSpacing);
                    if (overlaps) continue;
                    actor.position = hit.position; occupied.Add(hit.position); placed = true; break;
                }
                if (!placed) throw new InvalidOperationException("No separated restore position for actor " + actor.id);
            }
        }
    }
}
