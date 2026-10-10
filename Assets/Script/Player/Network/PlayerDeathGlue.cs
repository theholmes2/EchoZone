using EchoZone.Player.View;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Player.Network
{
    /// <summary>체력 이벤트를 입력 차단과 각 피어의 사망 연출에 연결합니다.</summary>
    [RequireComponent(typeof(PlayerStats))]
    [DisallowMultipleComponent]
    public sealed class PlayerDeathGlue : NetworkBehaviour
    {
        /// <summary>자동 부활 시간과 목적지입니다.</summary>
        [SerializeField] private PlayerRespawnConfig respawnConfig;
        /// <summary>서버가 사망 시 비울 인벤토리입니다.</summary>
        private PlayerInventory inventory;
        /// <summary>부활 시 탄창과 재장전 상태를 초기화할 총기 Glue입니다.</summary>
        private EchoZone.Combat.Glue.NetworkWeaponFireGlue weapon;
        /// <summary>위치 동기화의 서버 텔레포트를 담당합니다.</summary>
        private Unity.Netcode.Components.NetworkTransform networkTransform;
        /// <summary>사망 이전의 물리 속도를 제거할 본체입니다.</summary>
        private Rigidbody body;
        /// <summary>예약된 서버 부활 시각입니다. 음수이면 예약이 없습니다.</summary>
        private double respawnAt = -1d;
        /// <summary>예약이 없으면 -1, 있으면 부활까지 남은 시간을 저장합니다.</summary>
        public float CaptureRespawn(double now) => respawnAt < 0 ? -1f : (float)System.Math.Max(0, respawnAt - now);
        /// <summary>복구된 서버 시각을 기준으로 부활 예약을 이어갑니다.</summary>
        public void RestoreRespawn(float remaining, double now) { if (IsServer) respawnAt = remaining < 0 ? -1 : now + remaining; }
        /// <summary>같은 사망의 중복 처리와 생존 전환을 구분합니다.</summary>
        private bool wasDead;
        /// <summary>사망 판정의 원본입니다.</summary>
        private PlayerStats stats;
        /// <summary>서버 이동과 로컬 입력을 차단할 Glue입니다.</summary>
        private PlayerNetworkMovementGlue movement;
        /// <summary>사망 자세를 재생할 View입니다.</summary>
        private CharacterAnimatorView view;

        /// <summary>동일 프리팹의 연결 대상을 찾습니다.</summary>
        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            movement = GetComponent<PlayerNetworkMovementGlue>();
            view = GetComponentInChildren<CharacterAnimatorView>(true);
            inventory = GetComponent<PlayerInventory>();
            weapon = GetComponent<EchoZone.Combat.Glue.NetworkWeaponFireGlue>();
            networkTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            body = GetComponent<Rigidbody>();
        }

        /// <summary>초기 네트워크 체력 적용 후 이벤트와 현재 상태를 연결합니다.</summary>
        protected override void OnNetworkPostSpawn()
        {
            wasDead = false;
            respawnAt = -1d;
            stats.AddHealthChangedListener(HandleHealthChanged);
            HandleHealthChanged(0);
        }

        /// <summary>디스폰 시 구독을 해제합니다.</summary>
        public override void OnNetworkDespawn()
        {
            stats.RemoveHealthChangedListener(HandleHealthChanged);
            respawnAt = -1d;
        }

        /// <summary>기존 플레이어 갱신에서 서버만 부활 예약을 실행합니다.</summary>
        public void ManualUpdate(double serverTime)
        {
            if (!IsServer || !IsSpawned || EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring || respawnAt < 0d || serverTime < respawnAt || respawnConfig == null) return;
            respawnAt = -1d;
            if (!stats.IsDead) return;
            if (body != null)
            {
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.position = respawnConfig.Position;
                body.rotation = respawnConfig.Rotation;
            }
            if (networkTransform != null)
                networkTransform.Teleport(respawnConfig.Position, respawnConfig.Rotation, transform.localScale);
            else transform.SetPositionAndRotation(respawnConfig.Position, respawnConfig.Rotation);
            weapon?.ResetServerForRespawn();
            stats.ResetToMaximum();
        }

        /// <summary>사망 판정을 행동과 연출에 전달합니다.</summary>
        private void HandleHealthChanged(int health)
        {
            bool dead = stats.IsDead;
            if (IsServer && dead && !wasDead)
            {
                if (!EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring)
                {
                    if (TryGetComponent<EchoZone.Equipment.PlayerEquipmentGlue>(out var equipment)) equipment.DropOnDeathServer();
                    else inventory?.ReplaceSlots(null);
                }
                if (respawnConfig != null) respawnAt = NetworkManager.ServerTime.Time + respawnConfig.DelaySeconds;
                else Debug.LogError("PlayerRespawnConfig is missing; automatic respawn is unavailable.", this);
            }
            if (!dead)
            {
                respawnAt = -1d;
                if (wasDead) view?.ResetForSpawn();
            }
            wasDead = dead;
            movement?.SetDeathBlocked(stats.IsDead);
            view?.SetDead(stats.IsDead);
        }
    }
}
