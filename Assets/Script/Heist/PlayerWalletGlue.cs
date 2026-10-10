using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EchoZone.Online.Reconnect;
using EchoZone.Online.Relay;
using EchoZone.Pet;
using EchoZone.Equipment;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>기존 플레이어 갱신에서 계정 지갑·탈출 판정·Cloud Code 정산을 연결합니다.</summary>
    public sealed class PlayerWalletGlue : NetworkBehaviour
    {
        /// <summary>개인 돈은 소유 클라이언트에만 복제합니다.</summary>
        private readonly NetworkVariable<long> balance = new(0, NetworkVariableReadPermission.Owner);
        /// <summary>임시 검증 HUD에서 Cloud 지갑 조회 완료 여부를 소유자에게 표시합니다.</summary>
        private readonly NetworkVariable<bool> walletLoaded = new(false, NetworkVariableReadPermission.Owner);
        /// <summary>임시 검증 HUD에서 Cloud 지갑의 현재 Revision을 소유자에게 표시합니다.</summary>
        private readonly NetworkVariable<long> walletRevision = new(0, NetworkVariableReadPermission.Owner);
        /// <summary>정산 중에는 양쪽에서 이동·공격·상호작용을 막습니다.</summary>
        private readonly NetworkVariable<bool> escaping = new();
        /// <summary>로드·탈출 대기·저장 결과를 소유자 HUD에 표시합니다.</summary>
        private readonly NetworkVariable<FixedString128Bytes> status = new(default, NetworkVariableReadPermission.Owner);
        /// <summary>씬의 계정별 지갑을 참조합니다.</summary>
        private WalletSessionBrick wallet;
        /// <summary>네트워크 API만 담당하는 서비스입니다.</summary>
        private readonly WalletCloudService service = new();
        /// <summary>계정 조회와 명시적 퇴장에 재사용할 기존 Relay Glue입니다.</summary>
        private RelaySessionGlue relay;
        /// <summary>연결 승인에서 찾은 계정 ID입니다.</summary>
        private string playerId;
        /// <summary>비동기 요청을 시작한 원래 세션 ID입니다.</summary>
        private string sessionId;
        /// <summary>요청 중복 방지 플래그입니다.</summary>
        private bool busy;
        /// <summary>실패 재시도 제한 시각입니다.</summary>
        private float retryAt;
        /// <summary>일시 오류의 지수 백오프 횟수입니다.</summary>
        private int failures;
        /// <summary>정합성 오류는 자동 덮어쓰기나 무한 재시도 대신 운영 확인을 기다립니다.</summary>
        private bool reconciliationRequired;
        /// <summary>Cloud 성공 후 월드 종료 체크포인트까지 확인했는지 나타냅니다.</summary>
        private bool finalizationConfirmed;
        /// <summary>서버의 성공 통지를 받은 소유자가 Relay 정리를 재시도할지 나타냅니다.</summary>
        private bool ownerLeaveRequested, ownerLeaveBusy;
        /// <summary>퇴장 실패의 다음 재시도 시각과 누적 횟수입니다.</summary>
        private float ownerLeaveRetryAt;
        private int ownerLeaveFailures;
        /// <summary>범위 안에 연속으로 머문 서버 시각입니다.</summary>
        private double enteredAt = -1;
        /// <summary>오래된 async 응답을 새 스폰에 적용하지 않을 세대입니다.</summary>
        private int generation;
        /// <summary>정산 후 퇴장 RPC의 중복 발송 방지입니다.</summary>
        private bool leaveSent;
        /// <summary>지갑 참조를 얻기 전 발생한 서버 보상입니다.</summary>
        private long earlyCredit;
        /// <summary>현재 소유자의 개인 잔액입니다.</summary>
        public long Balance => balance.Value;
        /// <summary>Cloud 지갑을 읽고 세션 지갑에 적용했는지 나타냅니다.</summary>
        public bool IsWalletLoaded => walletLoaded.Value;
        /// <summary>마지막으로 확인한 Cloud 지갑 Revision입니다.</summary>
        public long WalletRevision => walletRevision.Value;
        /// <summary>탈출 승인 후 게임 행동을 막을 상태입니다.</summary>
        public bool IsEscaping => escaping.Value;
        /// <summary>HUD에 표시할 정산 안내입니다.</summary>
        public string Status => ownerLeaveRequested ? "정산 완료 · 방 나가기 재시도 중" : status.Value.ToString();
        /// <summary>복구 전 비동기 응답을 무효화하고 복원 원장의 지갑을 다시 연결합니다.</summary>
        public void RebindAfterMigration()
        {
            generation++; wallet = null; busy = false; enteredAt = -1; retryAt = 0;
            leaveSent = false; failures = 0; reconciliationRequired = finalizationConfirmed = false;
            ownerLeaveRequested = ownerLeaveBusy = false; ownerLeaveRetryAt = 0; ownerLeaveFailures = 0;
        }

        /// <summary>스폰 세대를 초기화하고 기존 Relay 연결점을 찾습니다.</summary>
        public override void OnNetworkSpawn()
        {
            generation++; busy = false; wallet = null; earlyCredit = 0; enteredAt = -1; retryAt = 0; leaveSent = false;
            failures = 0; reconciliationRequired = finalizationConfirmed = false;
            ownerLeaveRequested = ownerLeaveBusy = false; ownerLeaveRetryAt = 0; ownerLeaveFailures = 0;
            relay = FindFirstObjectByType<RelaySessionGlue>();
        }
        /// <summary>비동기 응답이 사라진 플레이어를 변경하지 않게 무효화합니다.</summary>
        public override void OnNetworkDespawn() => generation++;
        /// <summary>서버 보상을 지갑에 적립합니다. 클라우드에는 여기서 쓰지 않습니다.</summary>
        public void CreditServer(long amount)
        {
            if (!IsServer || !IsSpawned || amount <= 0 || IsEscaping) return;
            if (wallet == null) earlyCredit = checked(earlyCredit + amount);
            else wallet.Credit(amount);
        }
        /// <summary>향후 서버 상점에서 사용할 지출 진입점입니다.</summary>
        public bool TrySpendServer(long amount) => IsServer && IsSpawned && wallet != null && wallet.TrySpend(amount);

        /// <summary>상점에서 아이템을 제거하기 전에 실제 서버 잔액의 적립 가능 여부를 확인합니다.</summary>
        public bool CanCreditServer(long amount) => IsServer && IsSpawned && wallet != null && wallet.Loaded &&
            !wallet.Escaping && amount > 0 && wallet.Balance <= long.MaxValue - amount;

        /// <summary>독립 Update 없이 기존 플레이어 Update에서 서버만 실행합니다.</summary>
        public void ManualUpdate()
        {
            if (IsOwner && ownerLeaveRequested && !ownerLeaveBusy && Time.unscaledTime >= ownerLeaveRetryAt)
                _ = TryLeaveOwner();
            if (!IsServer || !IsSpawned || relay == null || EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring) return;
            var point = ExtractionPoint.Instance;
            var world = HeistWorldGlue.Instance;
            if (point == null || point.Config == null || world == null || string.IsNullOrEmpty(relay.SessionId)) return;
            if (reconciliationRequired) return;
            if (wallet == null)
            {
                if (NetworkPlayerSessionCacheGlue.Instance == null ||
                    !NetworkPlayerSessionCacheGlue.Instance.TryGetPlayerId(OwnerClientId, out playerId)) return;
                sessionId = relay.SessionId;
                wallet = world.Wallet(playerId); wallet.Credit(earlyCredit); earlyCredit = 0;
            }
            if (!wallet.Loaded || world.RequiresWalletVerification(playerId))
            {
                if (world.RequiresWalletVerification(playerId)) escaping.Value = true;
                if (!busy && Time.unscaledTime >= retryAt) _ = ReadWallet(point.Config, generation);
                return;
            }
            balance.Value = wallet.Balance;
            walletLoaded.Value = wallet.Loaded;
            walletRevision.Value = wallet.Revision;
            escaping.Value = wallet.Escaping;
            if (wallet.Settled)
            {
                if (!finalizationConfirmed)
                { if (!busy && Time.unscaledTime >= retryAt) _ = SaveWallet(point.Config, generation); return; }
                if (!leaveSent) { leaveSent = true; LeaveAfterSettlementRpc(); }
                return;
            }
            if (wallet.Escaping)
            {
                if (!busy && Time.unscaledTime >= retryAt) _ = SaveWallet(point.Config, generation);
                return;
            }
            if (GetComponent<PlayerStats>().IsDead || !CanExit(point))
            {
                enteredAt = -1; status.Value = default; return;
            }
            if (enteredAt < 0) enteredAt = NetworkManager.ServerTime.Time;
            double left = point.Config.HoldSeconds - (NetworkManager.ServerTime.Time - enteredAt);
            status.Value = new FixedString128Bytes($"탈출 대기 {Math.Max(0, left):0.0}초 · 펫과 함께 정산");
            if (left > 0) return;
            var pets = new List<PetStateGlue>(); long cargo = 0;
            foreach (var pet in PetUpdateManager.Pets)
                if (pet != null && pet.IsOwnedBy(NetworkObject))
                { pets.Add(pet); cargo += pet.GetComponent<PetHeistGlue>()?.Cargo ?? 0; }
            var equipment = GetComponent<PlayerEquipmentGlue>();
            if (equipment == null || !equipment.TryBuildExtractionReturns(out var returns, out long returnCredit))
            {
                status.Value = new FixedString128Bytes("반환 품목 검증 실패 · 탈출 보류");
                return;
            }
            if (!wallet.BeginEscape(checked(cargo + returnCredit))) return;
            GetComponent<PlayerHeistGlue>()?.NotifySoundServer(EchoZone.Audio.GameplaySoundId.ExtractionStarted);
            var petIds = new HashSet<string>(); foreach (var pet in pets) petIds.Add(pet.PetId);
            wallet.BindRequest(new SettlementRequest { PlayerId = playerId, SessionId = sessionId,
                RunId = FindFirstObjectByType<EchoZone.Online.Migration.HostMigrationSnapshotCollector>().RunId,
                SettlementId = wallet.SettlementId, ExpectedRevision = wallet.Revision, Balance = wallet.Balance,
                PetIds = new List<string>(petIds).ToArray(), LedgerIds = world.SettlementLedgerIds(petIds), Returns = returns });
            escaping.Value = true; balance.Value = wallet.Balance;
            if (TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
            { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            foreach (var pet in pets)
            {
                var job = pet.GetComponent<PetHeistGlue>();
                if (job != null) job.CancelServer();
            }
            _ = SaveWallet(point.Config, generation);
        }

        /// <summary>플레이어·소유 펫이 모두 지점 근처에 있고 벽 너머가 아닌지 서버가 검사합니다.</summary>
        private bool CanExit(ExtractionPoint point)
        {
            var config = point.Config; Vector3 goal = point.transform.position;
            if (Vector3.Distance(transform.position, goal) > config.PlayerRadius) return false;
            foreach (var pet in PetUpdateManager.Pets)
                if (pet != null && pet.IsOwnedBy(NetworkObject) &&
                    (Vector3.Distance(pet.transform.position, goal) > config.PetRadius ||
                     (pet.TryGetComponent<PetHeistGlue>(out var job) && job.IsBusy))) return false;
            Vector3 start = transform.position + Vector3.up * config.CheckHeight;
            Vector3 delta = goal + Vector3.up * config.CheckHeight - start;
            foreach (var hit in Physics.RaycastAll(start, delta.normalized, delta.magnitude, config.BlockingLayers, QueryTriggerInteraction.Ignore))
            {
                var obj = hit.collider.GetComponentInParent<NetworkObject>();
                if (obj == NetworkObject || (obj != null && obj.TryGetComponent<PetStateGlue>(out var pet) && pet.IsOwnedBy(NetworkObject))) continue;
                return false;
            }
            return true;
        }

        /// <summary>요청 중 종료·재스폰·호스트 변경을 검사합니다.</summary>
        private bool Current(int version) => this != null && IsSpawned && IsServer && generation == version && relay != null && relay.SessionId == sessionId;
        /// <summary>성공한 조회만 기준 잔액에 적용합니다.</summary>
        private async Task ReadWallet(ExtractionConfig config, int version)
        {
            busy = true; status.Value = new FixedString128Bytes("계정 잔액 불러오는 중");
            try
            {
                var result = await service.Load(config, sessionId, playerId);
                if (!Current(version)) return;
                if (result.Pending?.State == "Conflict") throw new InvalidOperationException("Revision conflict; manual reconciliation required.");
                if (result.Recoveries.Exists(r => r.State != "CheckpointConfirmed"))
                    throw new InvalidOperationException("Unconfirmed retirement checkpoint; manual reconciliation required.");
                EchoZone.Online.Migration.SessionWorldMigrationGlue.ValidateSettlementReceipt(wallet, result);
                wallet.VerifyCloud(result.Balance, result.Revision, result.LastSettlementId);
                wallet.Load(result.Balance, result.Revision);
                balance.Value = wallet.Balance;
                walletLoaded.Value = true;
                walletRevision.Value = wallet.Revision;
                HeistWorldGlue.Instance?.ConfirmWalletVerification(playerId);
                status.Value = default; failures = 0;
            }
            catch (Exception e)
            {
                if (!Current(version)) return;
                RecordFailure(e);
                Debug.LogWarning($"Wallet load failed: {e.Message}", this);
            }
            finally { if (Current(version)) { busy = false; SetRetry(config); } }
        }
        /// <summary>고정된 탈출 금액만 재전송하며 응답 성공 전에는 퇴장하지 않습니다.</summary>
        private async Task SaveWallet(ExtractionConfig config, int version)
        {
            busy = true; status.Value = new FixedString128Bytes("Cloud 확인 중 · 탈출 보류");
            try
            {
                var checkpoint = FindFirstObjectByType<EchoZone.Online.Migration.HostMigrationCloudCheckpointGlue>();
                if (!wallet.Settled)
                {
                    if (wallet.Request == null) throw new InvalidOperationException("Legacy pending request; manual reconciliation required.");
                    // 조회는 이전에 Cloud 접수된 본문만 재개하므로 응답 유실에도 새로운 ID를 만들지 않습니다.
                    var result = await service.Load(config, sessionId, playerId);
                    if (!Current(version)) return;
                    EchoZone.Online.Migration.SessionWorldMigrationGlue.ValidateSettlementReceipt(wallet, result);
                    wallet.VerifyCloud(result.Balance, result.Revision, result.LastSettlementId);
                    if (!wallet.Settled)
                    {
                        status.Value = new FixedString128Bytes("정산 요청 저장 중 · 펫 보관 중");
                        await service.Prepare(config, sessionId, playerId, wallet.Request);
                        if (!Current(version)) return;
                        if (checkpoint == null || !await checkpoint.SaveCurrentCheckpointAsync())
                            throw new Exception("Prepared request checkpoint not confirmed.");
                        if (!Current(version)) return;
                        result = await service.Load(config, sessionId, playerId);
                        if (!Current(version)) return;
                        if (result.Pending != null) throw new InvalidOperationException("Revision conflict; manual reconciliation required.");
                        EchoZone.Online.Migration.SessionWorldMigrationGlue.ValidateSettlementReceipt(wallet, result);
                        wallet.Confirm(result.Balance, result.Revision, result.LastSettlementId);
                        balance.Value = wallet.Balance;
                        walletRevision.Value = wallet.Revision;
                    }
                }
                var world = HeistWorldGlue.Instance;
                if (!wallet.ReturnsApplied)
                {
                    var equipment = GetComponent<PlayerEquipmentGlue>();
                    if (equipment == null || wallet.Request == null ||
                        !equipment.TryApplyExtractionReturns(wallet.Request.Returns ?? Array.Empty<SettlementReturnItem>()))
                        throw new InvalidOperationException("Settlement returns differ from the frozen server inventory; manual reconciliation required.");
                    wallet.MarkReturnsApplied();
                }
                var finished = new List<PetStateGlue>();
                if (wallet.Request != null)
                    foreach (var pet in PetUpdateManager.Pets)
                        if (pet != null && pet.IsSpawned && Array.IndexOf(wallet.Request.PetIds, pet.PetId) >= 0)
                        { world.ExtractCargo(pet.GetComponent<PetHeistGlue>()); finished.Add(pet); }
                wallet.MarkFinalized();
                if (checkpoint == null || !await checkpoint.SaveCurrentCheckpointAsync())
                    throw new Exception("Completed settlement checkpoint not confirmed.");
                if (!Current(version) || EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring) return;
                foreach (var pet in finished) if (pet != null && pet.IsSpawned) pet.NetworkObject.Despawn(true);
                finalizationConfirmed = true; failures = 0;
                GetComponent<PlayerHeistGlue>()?.NotifySoundServer(EchoZone.Audio.GameplaySoundId.SettlementSucceeded);
                status.Value = new FixedString128Bytes("탈출 정산 완료");
            }
            catch (Exception e)
            {
                if (!Current(version)) return;
                RecordFailure(e);
                if (failures == 1) GetComponent<PlayerHeistGlue>()?.NotifySoundServer(EchoZone.Audio.GameplaySoundId.SettlementFailed);
                Debug.LogWarning($"Escape settlement failed: {e.Message}", this);
            }
            finally { if (Current(version)) { busy = false; SetRetry(config); } }
        }
        /// <summary>정합성 오류는 보류하고 네트워크 오류는 같은 요청으로 제한 속도 재시도합니다.</summary>
        private void RecordFailure(Exception error)
        {
            failures++;
            reconciliationRequired = error is InvalidOperationException || error.Message.IndexOf("manual reconciliation", StringComparison.OrdinalIgnoreCase) >= 0;
            status.Value = new FixedString128Bytes(reconciliationRequired ? "Revision 충돌 / 수동 확인 필요" : "저장 재시도 중 · 금액/펫 보관 중");
        }
        /// <summary>현재 중앙 갱신이 다음 요청을 허용할 시각을 정합니다.</summary>
        private void SetRetry(ExtractionConfig config) => retryAt = Time.unscaledTime +
            (float)SettlementJournalBrick.RetryDelay(failures, config.RetrySeconds, config.MaximumRetrySeconds);
        /// <summary>소유자만 기존 명시적 세션 정리를 실행합니다. 일반 재접속으로 처리하지 않습니다.</summary>
        [Rpc(SendTo.Owner)]
        private void LeaveAfterSettlementRpc() { if (IsOwner && relay != null) ownerLeaveRequested = true; }

        /// <summary>Cloud 정산을 다시 호출하지 않고 기존 Relay 정리만 제한 속도로 재시도합니다.</summary>
        private async Task TryLeaveOwner()
        {
            int version = generation;
            ownerLeaveBusy = true;
            try
            {
                bool left = relay != null && await relay.LeaveAfterEscapeAsync();
                if (this != null && generation == version) ownerLeaveRequested = !left;
            }
            catch (Exception e) { if (this != null) Debug.LogWarning($"Settlement confirmed, leave retry: {e.Message}", this); }
            finally
            {
                if (this != null && generation == version)
                {
                    var config = ExtractionPoint.Instance?.Config;
                    ownerLeaveBusy = false;
                    ownerLeaveRetryAt = Time.unscaledTime + (float)SettlementJournalBrick.RetryDelay(++ownerLeaveFailures,
                        config?.RetrySeconds ?? 5, config?.MaximumRetrySeconds ?? 60);
                }
            }
        }
    }
}
