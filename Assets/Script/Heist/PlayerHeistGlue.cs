using EchoZone.Pet;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>로컬 HUD 선택을 서버 절도·장물 인계·신고 요청으로 연결합니다.</summary>
    public sealed class PlayerHeistGlue : NetworkBehaviour
    {
        /// <summary>보상 수입을 계정 기준의 세션 지갑에 전달합니다.</summary>
        private PlayerWalletGlue wallet;
        /// <summary>요청한 플레이어에게만 보여줄 최근 처리 결과입니다.</summary>
        private readonly NetworkVariable<FixedString128Bytes> feedback = new(default, NetworkVariableReadPermission.Owner);
        /// <summary>반복 RPC 제한 시각입니다.</summary>
        private double nextRequest;
        /// <summary>폴링 없이 사망을 알려주는 기존 체력 컴포넌트입니다.</summary>
        private PlayerStats stats;
        /// <summary>현재 치명타의 공격자 참조입니다.</summary>
        private EchoZone.Combat.Glue.DamageReceiverGlue receiver;
        /// <summary>서버만 체력 이벤트에 현상금 처리를 연결합니다.</summary>
        public override void OnNetworkSpawn()
        {
            stats = GetComponent<PlayerStats>(); receiver = GetComponent<EchoZone.Combat.Glue.DamageReceiverGlue>();
            wallet = GetComponent<PlayerWalletGlue>();
            if (IsServer) stats.AddHealthChangedListener(HealthChanged);
        }
        /// <summary>재접속·종료 때 이벤트 중복 등록을 방지합니다.</summary>
        public override void OnNetworkDespawn() { if (stats != null) stats.RemoveHealthChangedListener(HealthChanged); }
        /// <summary>체력 0 이벤트에서만 서버 체포·현상금 판정을 요청합니다.</summary>
        private void HealthChanged(int health)
        { if (IsServer && health <= 0) HeistWorldGlue.Instance?.CaptureWanted(NetworkObject, receiver != null ? receiver.CurrentDamageSource : null); }
        /// <summary>절도 현장에서 펫을 잃은 주인에게 결과를 표시합니다.</summary>
        public void NotifyCaughtServer()
        { if (IsServer && IsSpawned) feedback.Value = new FixedString128Bytes("펫 압수. 절도 현장 적발로 수배되었습니다."); }
        /// <summary>서버의 단일 체포 판정에서 현상금을 지급합니다.</summary>
        public void AddBountyServer(int amount)
        { if (IsServer && IsSpawned && amount > 0) { wallet?.CreditServer(amount); feedback.Value = new FixedString128Bytes($"체포 현상금 +{amount:N0}"); NotifySoundServer(EchoZone.Audio.GameplaySoundId.RewardReceived); } }
        /// <summary>이전 확정 잔액과 현재 수입·지출을 포함한 개인 지갑입니다.</summary>
        public long RewardMoney => wallet != null ? wallet.Balance : 0;
        /// <summary>최근 서버 결과 문자열입니다.</summary>
        public string Feedback => wallet != null && !string.IsNullOrEmpty(wallet.Status) ? wallet.Status : feedback.Value.ToString();
        /// <summary>로컬 버튼에서 건물 절도를 요청합니다.</summary>
        public void RequestSteal(int building) { if (IsOwner && IsSpawned) ActRpc(0, building, 0); }
        /// <summary>로컬 버튼에서 대기 펫을 가져가거나 신고합니다.</summary>
        public void RequestPet(ulong pet, bool report) { if (IsOwner && IsSpawned) ActRpc(report ? (byte)2 : (byte)1, -1, pet); }
        /// <summary>서버 회수 완료에서만 감사비를 적립합니다.</summary>
        public void AddRewardServer(int amount)
        { if (IsServer && IsSpawned && amount > 0) { wallet?.CreditServer(amount); feedback.Value = new FixedString128Bytes($"신고 감사비 +{amount:N0}"); NotifySoundServer(EchoZone.Audio.GameplaySoundId.RewardReceived); } }

        /// <summary>서버가 확정한 개인 결과만 소유자에게 전달합니다. 복원 중 과거 이벤트는 재생하지 않습니다.</summary>
        public void NotifySoundServer(EchoZone.Audio.GameplaySoundId id)
        {
            if (IsServer && IsSpawned && !EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring) SoundOwnerRpc(id);
        }

        /// <summary>요청자 한 명의 로컬 UI에서만 효과음을 재생합니다.</summary>
        [Rpc(SendTo.Owner)]
        private void SoundOwnerRpc(EchoZone.Audio.GameplaySoundId id)
        { if (IsOwner && IsClient) EchoZone.Audio.GameplaySoundGlue.PlayUI(id); }
        /// <summary>자기 플레이어의 요청만 받고 거리·체력·소유권을 다시 검사합니다.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void ActRpc(byte action, int building, ulong petId)
        {
            if (EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring) return;
            var world = HeistWorldGlue.Instance;
            if (!IsServer || world == null || (wallet != null && wallet.IsEscaping) || GetComponent<PlayerStats>().IsDead || NetworkManager.ServerTime.Time < nextRequest) return;
            nextRequest = NetworkManager.ServerTime.Time + world.Config.RequestInterval;
            bool success = false;
            if (action == 0)
            {
                var site = world.Site(building);
                if (site != null && world.CanReach(NetworkObject, site.Entrance, world.Config.InteractionDistance))
                    foreach (var pet in PetUpdateManager.Pets)
                        if (pet != null && pet.IsOwnedBy(NetworkObject) && pet.TryGetComponent<PetHeistGlue>(out var worker) && worker.TryStartServer(NetworkObject, site)) { success = true; break; }
            }
            else if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(petId, out var obj) && obj.TryGetComponent<PetStateGlue>(out var pet))
            {
                if (action == 1) success = pet.TryClaimServer(gameObject);
                else if (action == 2) success = world.Report(NetworkObject, obj.GetComponent<PetHeistGlue>());
            }
            feedback.Value = new FixedString128Bytes(success
                ? (action == 0 ? "펫이 건물로 이동합니다." : action == 1 ? "펫을 데려갑니다." : "신고 접수. 경찰 회수 후 보상 지급.")
                : "처리 불가: 거리·벽·펫·잔액·신고 상태 확인");
            NotifySoundServer(action == 0
                ? (success ? EchoZone.Audio.GameplaySoundId.TheftStarted : EchoZone.Audio.GameplaySoundId.TheftFailed)
                : (success ? EchoZone.Audio.GameplaySoundId.RequestSucceeded : EchoZone.Audio.GameplaySoundId.RequestFailed));
        }
    }
}
