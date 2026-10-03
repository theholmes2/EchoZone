using System.Collections.Generic;
using EchoZone.Pet;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>서버 장물 원장과 건물 현금·수배·감사비 동기화를 연결합니다. 현재 세션 범위의 데이터입니다.</summary>
    public sealed partial class HeistWorldGlue : NetworkBehaviour
    {
        /// <summary>현재 실행 중인 세션의 범죄 규칙 연결점입니다.</summary>
        public static HeistWorldGlue Instance { get; private set; }
        /// <summary>공통 기획 설정입니다.</summary>
        [SerializeField] private HeistConfig config;
        /// <summary>모든 클라이언트가 읽는 건물 현금·검사 상태입니다.</summary>
        public NetworkList<HeistBuildingState> Buildings;
        /// <summary>검사로 확인한 수배 ClientId 목록입니다.</summary>
        public NetworkList<ulong> WantedPlayers;
        /// <summary>씬 출입구 목록입니다.</summary>
        private readonly Dictionary<int, HeistBuildingSite> sites = new();
        /// <summary>검사 중복 배정을 막는 건물별 경찰 참조입니다.</summary>
        private readonly Dictionary<int, PoliceHeistDutyGlue> inspections = new();
        /// <summary>펫별 최초 유효 신고자입니다. 회수 전에 소유되면 취소됩니다.</summary>
        private readonly Dictionary<string, string> reports = new();
        /// <summary>완료된 회수·탈출 펫의 재생성을 막는 고정 ID 집합입니다.</summary>
        private readonly HashSet<string> retiredPets = new();
        /// <summary>동일 세션의 시작 펫 중복 지급을 막는 계정 이력입니다.</summary>
        private readonly HashSet<string> starterPets = new();
        /// <summary>최초 펫을 아직 지급하지 않은 계정만 승인합니다.</summary>
        public bool TryGrantStarter(string playerId) => IsServer && starterPets.Add(playerId);
        /// <summary>같은 유실 펫을 여러 경찰이 동시에 회수하지 않도록 예약합니다.</summary>
        private readonly Dictionary<ulong, PoliceHeistDutyGlue> recoveries = new();
        /// <summary>회수 후보를 한 경찰에게만 배정합니다.</summary>
        public bool TryReserveRecovery(PetHeistGlue pet, PoliceHeistDutyGlue officer)
        {
            if (!IsServer || pet == null || !pet.CanBeRecovered || officer == null || recoveries.ContainsValue(officer)) return false;
            if (recoveries.TryGetValue(pet.NetworkObjectId, out var owner) && owner != null && owner != officer) return false;
            recoveries[pet.NetworkObjectId] = officer; return true;
        }
        /// <summary>작업 종료·사망·전투 전환 때 회수 예약을 해제합니다.</summary>
        public void ReleaseRecovery(PoliceHeistDutyGlue officer)
        {
            var keys = new List<ulong>();
            foreach (var pair in recoveries) if (pair.Value == officer) keys.Add(pair.Key);
            foreach (ulong key in keys) recoveries.Remove(key);
        }
        /// <summary>비네트워크 순수 돈 이동 원장입니다.</summary>
        private HeistLedgerBrick ledger;
        /// <summary>현재 세션의 계정별 지연 수배 및 즉시 수배 규칙입니다.</summary>
        private PoliceInvestigationBrick investigation;
        /// <summary>같은 세션 재접속 시 미정산 금액을 유지하는 계정별 서버 지갑입니다.</summary>
        private readonly Dictionary<string, WalletSessionBrick> wallets = new();
        /// <summary>ClientId가 바뀌어도 같은 인증 계정의 지갑을 반환합니다.</summary>
        public WalletSessionBrick Wallet(string playerId)
        {
            if (!IsServer) return null;
            // 새 PlayerObject가 요청했을 때만 완료된 지갑을 교체합니다. 미정산 재접속 지갑은 유지합니다.
            if (!wallets.TryGetValue(playerId, out var wallet) || (wallet.Settled && (wallet.Finalized || wallet.Request == null)))
                wallets[playerId] = wallet = new WalletSessionBrick();
            return wallet;
        }
        /// <summary>승인된 탈출 장물을 건물로 반환하지 않고 원장에서 정산 완료로 표시합니다.</summary>
        public int ExtractCargo(PetHeistGlue pet)
        {
            if (!IsServer || pet == null || !pet.IsSpawned) return 0;
            string petId = pet.GetComponent<PetStateGlue>().PetId;
            int amount = ledger.Extract(petId);
            reports.Remove(petId); retiredPets.Add(petId);
            pet.SetCargoServer(0);
            return amount;
        }
        /// <summary>현재 규칙 에셋입니다.</summary>
        public HeistConfig Config => config;
        /// <summary>Cloud 정산 요청에 포함할 현재 실행 식별자입니다.</summary>
        public string WorldId => worldId;
        /// <summary>고정 펫 집합의 미정산 원장 ID를 제공합니다.</summary>
        public string[] SettlementLedgerIds(HashSet<string> pets) => ledger.EntryIds(pets);
        /// <summary>Cloud 접수/확정 대기 펫은 획득·회수·공격으로 변경하지 않습니다.</summary>
        public bool IsSettlementPet(string petId)
        {
            foreach (var wallet in wallets.Values)
                if (wallet.Escaping && wallet.Request != null && System.Array.IndexOf(wallet.Request.PetIds, petId) >= 0) return true;
            return false;
        }
        /// <summary>로컬 HUD가 읽는 출입구 목록입니다.</summary>
        public IEnumerable<HeistBuildingSite> Sites => sites.Values;

        /// <summary>Native 네트워크 목록을 메인 스레드에서 준비합니다.</summary>
        private void Awake() { Buildings = new NetworkList<HeistBuildingState>(); WantedPlayers = new NetworkList<ulong>(); }
        /// <summary>새 세션의 건물 돈을 초기화합니다. 기존 클라우드 진행도와 혼합하지 않습니다.</summary>
        public override void OnNetworkSpawn()
        {
            Instance = this;
            sites.Clear(); inspections.Clear(); reports.Clear(); recoveries.Clear(); inspectionRetryAt.Clear();
            retirementGeneration++; pendingRetirements.Clear(); retirementJournal.Clear(); retirementSaving = false; nextRetirementSave = 0;
            foreach (var site in FindObjectsByType<HeistBuildingSite>(FindObjectsSortMode.None))
                if (!sites.TryAdd(site.Id, site)) Debug.LogError($"Duplicate heist building id: {site.Id}", site);
            if (!IsServer) return;
            worldId = null;
            walletsToVerify.Clear();
            ledger = new HeistLedgerBrick(); investigation = new PoliceInvestigationBrick();
            EchoZone.Enemy.PoliceDestinationBrick.Shared.Clear();
            wallets.Clear(); Buildings.Clear(); WantedPlayers.Clear(); starterPets.Clear(); retiredPets.Clear();
            var ids = new List<int>(sites.Keys); ids.Sort();
            foreach (int id in ids) Buildings.Add(new HeistBuildingState { Id = id, Money = sites[id].IsLootSite ? config.InitialBuildingMoney : 0,
                NextInspection = NetworkManager.ServerTime.Time + config.FirstInspectionDelay + id * config.InspectionStagger });
            ResetBuildingIncome(NetworkManager.ServerTime.Time);
        }
        /// <summary>시스템 정리 시 세션 참조를 해제합니다.</summary>
        public override void OnNetworkDespawn() { retirementGeneration++; if (Instance == this) Instance = null; inspections.Clear(); reports.Clear(); }
        /// <summary>식별자로 씬 출입구를 해석합니다.</summary>
        public HeistBuildingSite Site(int id) => sites.TryGetValue(id, out var site) ? site : null;
        /// <summary>동기화 목록에서 건물 상태를 읽습니다.</summary>
        public HeistBuildingState Status(int id) { foreach (var item in Buildings) if (item.Id == id) return item; return default; }
        /// <summary>한 건물의 변경만 전송합니다.</summary>
        private void Write(HeistBuildingState value) { for (int i = 0; i < Buildings.Count; i++) if (Buildings[i].Id == value.Id) { Buildings[i] = value; return; } }
        /// <summary>수배된 사람인지 판단합니다.</summary>
        public bool IsWanted(ulong clientId) => IsSpawned && WantedPlayers.Contains(clientId);
        /// <summary>수배 전에는 공격 대상이 아닙니다. 소유 펫도 주인의 수배 여부를 따릅니다.</summary>
        public bool CanPoliceAttack(NetworkObject target)
        {
            if (!IsServer || !IsSpawned || target == null || !target.IsSpawned) return false;
            if (target.IsPlayerObject) return IsWanted(target.OwnerClientId);
            var pet = target.GetComponent<PetStateGlue>();
            if (pet != null && IsSettlementPet(pet.PetId)) return false;
            return pet != null && pet.TryGetOwner(out var owner) && IsWanted(owner.OwnerClientId) &&
                   !(target.TryGetComponent<PetHeistGlue>(out var heist) && heist.IsInside);
        }
        /// <summary>같은 층 가까운 위치와 벽 차단을 서버에서 검사합니다.</summary>
        public bool CanReach(NetworkObject player, Vector3 destination, float distance, NetworkObject ignored = null)
        {
            if (player == null || !player.IsSpawned || !player.IsPlayerObject ||
                !player.TryGetComponent<PlayerStats>(out var hp) || hp.IsDead || Vector3.Distance(player.transform.position, destination) > distance) return false;
            Vector3 start = player.transform.position + Vector3.up * 0.8f;
            Vector3 end = destination + Vector3.up * 0.8f;
            foreach (var hit in Physics.RaycastAll(start, (end - start).normalized, (end - start).magnitude, config.BlockingLayers, QueryTriggerInteraction.Ignore))
            {
                var obj = hit.collider.GetComponentInParent<NetworkObject>();
                if (obj != player && (ignored == null || obj != ignored)) return false;
            }
            return true;
        }
        /// <summary>같은 프레임에도 잔액을 먼저 차감한 뒤 기록하여 중복 획득을 막습니다.</summary>
        public bool CompleteTheft(PetHeistGlue pet, int building, NetworkObject player)
        {
            if (!IsServer || pet == null || !pet.IsSpawned || Site(building) == null || !Site(building).IsLootSite || !pet.GetComponent<PetStateGlue>().IsOwnedBy(player)) return false;
            var state = Status(building);
            int amount = Mathf.Min(state.Money, config.MoneyPerTheft, config.PetCapacity - pet.Cargo);
            if (amount <= 0) return false;
            state.Money -= amount; Write(state);
            string petId = pet.GetComponent<PetStateGlue>().PetId;
            ledger.Record(building, petId, PlayerIdentity(player.OwnerClientId), amount);
            pet.SetCargoServer(ledger.Cargo(petId));
            return true;
        }
        /// <summary>경찰이 도착할 수 있는 검사 예정 건물을 중복 없이 예약합니다.</summary>
        public HeistBuildingSite ReserveInspection(PoliceHeistDutyGlue officer)
        {
            if (!IsServer || !CanDispatchDuty(officer) || inspections.ContainsValue(officer)) return null;
            return SelectInspection(officer);
        }
        /// <summary>예약한 경찰이 입장하면 검사 중 표시를 시작합니다.</summary>
        public void StartInspection(HeistBuildingSite site, PoliceHeistDutyGlue officer)
        {
            if (!IsServer || site == null || !inspections.TryGetValue(site.Id, out var current) || current != officer) return;
            if (Vector3.Distance(officer.transform.position, site.Entrance) > config.ArrivalDistance + 0.1f) return;
            var state = Status(site.Id); state.Inspecting = true; state.InspectionEnRoute = false;
            state.InspectionEndsAt = NetworkManager.ServerTime.Time + config.InspectionSeconds; Write(state);
            // 포획 디스폰은 펫 갱신 목록을 변경하므로 복사 목록에서 순회합니다.
            foreach (var pet in new List<PetStateGlue>(PetUpdateManager.Pets))
                if (pet != null && pet.TryGetComponent<PetHeistGlue>(out var job)) TryCatchStealing(job);
        }
        /// <summary>같은 건물에서 경찰과 절도 중 펫이 겹친 경우만 신원을 확인하고 펫을 압수합니다.</summary>
        public bool TryCatchStealing(PetHeistGlue pet)
        {
            if (!IsServer || !IsSpawned || pet == null || !pet.IsSpawned || !pet.IsInside ||
                !Status(pet.BuildingId).Inspecting || !pet.GetComponent<PetStateGlue>().TryGetOwner(out var owner)) return false;
            WitnessPoliceCrimeServer(owner);
            string petId = pet.GetComponent<PetStateGlue>().PetId;
            foreach (var pair in ledger.Return(petId, null, 0f, out _))
            { var state = Status(pair.Key); state.Money += pair.Value; Write(state); }
            reports.Remove(petId); retiredPets.Add(petId);
            owner.GetComponent<PlayerHeistGlue>()?.NotifyCaughtServer();
            pet.CancelServer();
            pendingRetirements.Add(petId);
            PublishWanted();
            return true;
        }
        /// <summary>사망 이벤트에서 수배를 해제하고 유효한 다른 플레이어에게만 현상금을 지급합니다.</summary>
        public void CaptureWanted(NetworkObject victim, NetworkObject attacker)
        {
            if (!IsServer || !IsSpawned || victim == null || !victim.IsPlayerObject ||
                !victim.GetComponent<PlayerStats>().IsDead) return;
            bool captured = investigation.Capture(PlayerIdentity(victim.OwnerClientId));
            PublishWanted();
            if (!captured) return;
            if (attacker != null && attacker.IsSpawned && attacker.IsPlayerObject && attacker != victim &&
                attacker.OwnerClientId != victim.OwnerClientId && !attacker.GetComponent<PlayerStats>().IsDead)
                attacker.GetComponent<PlayerHeistGlue>()?.AddBountyServer(config.CaptureBounty);
        }
        /// <summary>검사를 마친 건물의 도난 원장을 적발한 뒤 다음 검사 시각을 예약합니다.</summary>
        public void FinishInspection(HeistBuildingSite site, PoliceHeistDutyGlue officer, bool completed)
        {
            if (!IsServer || site == null || !inspections.TryGetValue(site.Id, out var current) || current != officer) return;
            inspections.Remove(site.Id);
            EchoZone.Enemy.PoliceDestinationBrick.Shared.Release(officer.NetworkObjectId);
            var state = Status(site.Id); state.Inspecting = false; state.InspectionEnRoute = false; state.InspectionEndsAt = 0;
            var thieves = new HashSet<string>();
            if (completed && ledger.Inspect(site.Id, thieves))
            {
                state.SearchUntil = NetworkManager.ServerTime.Time + config.CrimeSearchSeconds;
                foreach (string thief in thieves)
                    investigation.Schedule(thief, NetworkManager.ServerTime.Time, config.InvestigationDelaySeconds);
            }
            if (completed) { state.NextInspection = NetworkManager.ServerTime.Time + config.InspectionInterval; inspectionRetryAt.Remove(site.Id); }
            else inspectionRetryAt[site.Id] = NetworkManager.ServerTime.Time + config.InspectionRetrySeconds;
            Write(state); PublishWanted();
        }
        /// <summary>기존 서버 루프에서 조사 기한과 재접속한 계정의 수배 표시를 갱신합니다.</summary>
        public void ManualUpdateServer(double serverTime)
        {
            if (!IsServer || !IsSpawned || investigation == null) return;
            TickBuildingIncome(serverTime);
            investigation.Tick(serverTime);
            PublishWanted();
            FlushRetirements();
        }
        /// <summary>서버에서 확인한 경찰 공격·현장 적발 범인을 즉시 수배합니다.</summary>
        public void WitnessPoliceCrimeServer(NetworkObject offender)
        {
            if (!IsServer || !IsSpawned || offender == null || !offender.IsSpawned || !offender.IsPlayerObject) return;
            investigation.Witness(PlayerIdentity(offender.OwnerClientId));
            PublishWanted();
        }
        /// <summary>인증 계정을 사용합니다. 인증 없는 로컬 테스트만 세션 ClientId로 구분합니다.</summary>
        public static string PlayerIdentity(ulong clientId)
        {
            var cache = EchoZone.Online.Reconnect.NetworkPlayerSessionCacheGlue.Instance;
            return cache != null && cache.TryGetPlayerId(clientId, out string id) && !string.IsNullOrEmpty(id)
                ? id : "local-client:" + clientId;
        }
        /// <summary>계정별 수배를 현재 접속 ClientId로 변환하여 기존 HUD에 전달합니다.</summary>
        private void PublishWanted()
        {
            for (int i = WantedPlayers.Count - 1; i >= 0; i--)
                if (!NetworkManager.ConnectedClients.ContainsKey(WantedPlayers[i]) ||
                    !investigation.IsWanted(PlayerIdentity(WantedPlayers[i]))) WantedPlayers.RemoveAt(i);
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
                if (investigation.IsWanted(PlayerIdentity(id)) && !WantedPlayers.Contains(id)) WantedPlayers.Add(id);
        }
        /// <summary>가져가기와 신고를 동시에 처리하지 않으며 장물 보상 중복을 막습니다.</summary>
        public void NotifyAcquired(PetStateGlue pet, NetworkObject player)
        {
            if (!IsServer || !IsSpawned) return;
            if (recoveries.TryGetValue(pet.NetworkObjectId, out var officer) && officer != null)
            { officer.CancelServer(); ReleaseRecovery(officer); }
            reports.Remove(pet.PetId);
            pet.GetComponent<PetHeistGlue>()?.SetReportedServer(false);
            ledger.Acquired(pet.PetId, PlayerIdentity(player.OwnerClientId));
        }
        /// <summary>대기 중인 장물 펫을 신고합니다. 지급은 실제 경찰 회수 때만 실행합니다.</summary>
        public bool Report(NetworkObject reporter, PetHeistGlue pet)
        {
            if (!IsServer || pet == null || !pet.IsSpawned || !pet.IsAbandonedCargo ||
                !CanReach(reporter, pet.transform.position, config.InteractionDistance, pet.NetworkObject) || reports.ContainsKey(pet.GetComponent<PetStateGlue>().PetId)) return false;
            reports.Add(pet.GetComponent<PetStateGlue>().PetId, PlayerIdentity(reporter.OwnerClientId)); pet.SetReportedServer(true); return true;
        }
        /// <summary>신고 접수 여부를 경찰 작업 선택에 제공합니다.</summary>
        public bool HasReport(ulong pet) => NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(pet, out var obj) &&
            obj.TryGetComponent<PetStateGlue>(out var state) && reports.ContainsKey(state.PetId);
        /// <summary>장물을 직접 인수한 경찰만 건물별 전액 반환과 감사비를 처리합니다.</summary>
        public bool Confiscate(PoliceHeistDutyGlue police, PetHeistGlue pet)
        {
            if (!IsServer || police == null || !police.IsSpawned || police.GetComponent<PlayerStats>().IsDead ||
                pet == null || !pet.CanBeRecovered || !recoveries.TryGetValue(pet.NetworkObjectId, out var assigned) || assigned != police ||
                Vector3.Distance(police.transform.position, pet.transform.position) > config.ConfiscationDistance + 0.2f) return false;
            string petId = pet.GetComponent<PetStateGlue>().PetId;
            if (retiredPets.Contains(petId)) return false;
            string reporter = reports.TryGetValue(petId, out var id) ? id : null;
            // 탈출 전송 금액은 고정되어 있으므로 정산 중 감사비가 버려지지 않게 회수를 재시도합니다.
            if (reporter != null && wallets.TryGetValue(reporter, out var reporterWallet) &&
                reporterWallet.Escaping && !reporterWallet.Settled) return false;
            string[] ledgerIds = ledger.EntryIds(new HashSet<string> { petId });
            var restored = ledger.Return(petId, reporter, config.ReportRewardRate, out int reward);
            if (restored.Count == 0 && !pet.GetComponent<PetStateGlue>().IsCollectionWaiting) return false;
            foreach (var pair in restored) { var state = Status(pair.Key); state.Money += pair.Value; Write(state); }
            reports.Remove(petId); retiredPets.Add(petId); pet.SetReportedServer(false); pet.SetCargoServer(0); PublishWanted();
            if (reporter != null && reward > 0) Wallet(reporter)?.Credit(reward);
            var rewardWallet = reporter == null ? null : Wallet(reporter);
            retirementJournal.Add(new RetirementRequestRecord { SettlementId = System.Guid.NewGuid().ToString("N"),
                SessionId = FindFirstObjectByType<EchoZone.Online.Relay.RelaySessionGlue>()?.SessionId,
                RunId = FindFirstObjectByType<EchoZone.Online.Migration.HostMigrationSnapshotCollector>()?.RunId,
                PlayerId = reporter ?? "", CustodianPlayerId = reporter ?? PlayerIdentity(NetworkManager.LocalClientId),
                PetId = petId, LedgerIds = ledgerIds, Reward = reward, ReturnedAmounts = restored,
                ExpectedRevision = rewardWallet?.Revision ?? 0, Balance = rewardWallet?.Balance ?? 0,
                WalletLoaded = rewardWallet?.Loaded ?? false, CreatedAtUtc = System.DateTime.UtcNow.ToString("O"), State = "AwaitingCheckpoint" });
            ReleaseRecovery(police);
            pet.CancelServer(); pendingRetirements.Add(petId);
            return true;
        }
    }
}
