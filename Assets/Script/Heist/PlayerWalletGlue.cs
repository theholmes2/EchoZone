using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EchoZone.Online.Reconnect;
using EchoZone.Online.Relay;
using EchoZone.Pet;
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
        /// <summary>탈출 승인 후 게임 행동을 막을 상태입니다.</summary>
        public bool IsEscaping => escaping.Value;
        /// <summary>HUD에 표시할 정산 안내입니다.</summary>
        public string Status => status.Value.ToString();
        /// <summary>복구 전 비동기 응답을 무효화하고 복원 원장의 지갑을 다시 연결합니다.</summary>
        public void RebindAfterMigration()
        { generation++; wallet = null; busy = false; enteredAt = -1; retryAt = 0; leaveSent = false; }

        /// <summary>스폰 세대를 초기화하고 기존 Relay 연결점을 찾습니다.</summary>
        public override void OnNetworkSpawn()
        {
            generation++; busy = false; wallet = null; earlyCredit = 0; enteredAt = -1; retryAt = 0; leaveSent = false;
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

        /// <summary>독립 Update 없이 기존 플레이어 Update에서 서버만 실행합니다.</summary>
        public void ManualUpdate()
        {
            if (!IsServer || !IsSpawned || relay == null || EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring) return;
            var point = ExtractionPoint.Instance;
            var world = HeistWorldGlue.Instance;
            if (point == null || point.Config == null || world == null || string.IsNullOrEmpty(relay.SessionId)) return;
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
            escaping.Value = wallet.Escaping;
            if (wallet.Settled)
            {
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
            if (!wallet.BeginEscape(cargo)) return;
            escaping.Value = true; balance.Value = wallet.Balance;
            if (TryGetComponent<Rigidbody>(out var body) && !body.isKinematic)
            { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            foreach (var pet in pets)
            {
                var job = pet.GetComponent<PetHeistGlue>();
                if (job != null) { job.CancelServer(); world.ExtractCargo(job); }
                pet.NetworkObject.Despawn(true);
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
                wallet.VerifyCloud(result.Balance, result.Revision, result.LastSettlementId);
                wallet.Load(result.Balance, result.Revision);
                HeistWorldGlue.Instance?.ConfirmWalletVerification(playerId);
                status.Value = default;
            }
            catch (Exception e)
            {
                if (!Current(version)) return;
                status.Value = new FixedString128Bytes("잔액 조회 실패 · 탈출 보류 · 자동 재시도");
                Debug.LogWarning($"Wallet load failed: {e.Message}", this);
            }
            finally { if (Current(version)) { busy = false; retryAt = Time.unscaledTime + config.RetrySeconds; } }
        }
        /// <summary>고정된 탈출 금액만 재전송하며 응답 성공 전에는 퇴장하지 않습니다.</summary>
        private async Task SaveWallet(ExtractionConfig config, int version)
        {
            busy = true; status.Value = new FixedString128Bytes("탈출 승인 · 클라우드 저장 중");
            try
            {
                var checkpoint = FindFirstObjectByType<EchoZone.Online.Migration.HostMigrationCloudCheckpointGlue>();
                if (checkpoint == null || !await checkpoint.SaveCurrentCheckpointAsync())
                    throw new InvalidOperationException("Extraction receipt checkpoint is not confirmed yet.");
                if (!Current(version) || EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring) return;
                var result = await service.Settle(config, sessionId, playerId, wallet.Revision, wallet.Balance, wallet.SettlementId);
                if (!Current(version)) return;
                wallet.Confirm(result.Balance, result.Revision, result.LastSettlementId);
                status.Value = new FixedString128Bytes("탈출 정산 완료");
            }
            catch (Exception e)
            {
                if (!Current(version)) return;
                status.Value = new FixedString128Bytes("탈출 저장 미확인 · 금액 보관 중 · 자동 재시도");
                Debug.LogWarning($"Escape settlement failed: {e.Message}", this);
            }
            finally { if (Current(version)) { busy = false; retryAt = Time.unscaledTime + config.RetrySeconds; } }
        }
        /// <summary>소유자만 기존 명시적 세션 정리를 실행합니다. 일반 재접속으로 처리하지 않습니다.</summary>
        [Rpc(SendTo.Owner)]
        private void LeaveAfterSettlementRpc() { if (IsOwner && relay != null) _ = relay.LeaveAfterEscapeAsync(); }
    }
}
