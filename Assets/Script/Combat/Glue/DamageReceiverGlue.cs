using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Combat.Glue
{
    /// <summary>서버의 적중 피해를 기존 체력 Brick으로 전달하는 대상 측 Glue입니다.</summary>
    [RequireComponent(typeof(PlayerStats))]
    public sealed class DamageReceiverGlue : NetworkBehaviour
    {
        /// <summary>무적·사망·체력 제한 규칙을 적용할 대상 상태입니다.</summary>
        private PlayerStats stats;
        /// <summary>동기 체력 이벤트 처리 중에만 유효한 공격자입니다. 이전 탄환의 공격자가 남지 않습니다.</summary>
        public NetworkObject CurrentDamageSource { get; private set; }
        /// <summary>서버에서 실제 체력이 감소한 뒤 공격자와 감소량을 알립니다. 무적·빗나감은 알리지 않습니다.</summary>
        public event System.Action<NetworkObject, int> ServerDamageApplied;

        /// <summary>동일 오브젝트의 체력 Brick을 연결합니다.</summary>
        private void Awake() => stats = GetComponent<PlayerStats>();

        /// <summary>서버에서만 피해를 적용하고 실제 감소한 체력을 반환합니다. 소수 피해는 내림합니다.</summary>
        public int ApplyServerDamage(float damage, NetworkObject source = null)
        {
            if (!IsServer || !IsSpawned || !isActiveAndEnabled || stats == null || EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring ||
                (TryGetComponent<EchoZone.Heist.PlayerWalletGlue>(out var wallet) && wallet.IsEscaping) ||
                !float.IsFinite(damage) || damage <= 0f)
                return 0;

            int healthDamage = (int)System.Math.Min(System.Math.Floor(damage), int.MaxValue);
            CurrentDamageSource = source;
            try
            {
                int applied = stats.ApplyDamage(healthDamage);
                if (applied > 0) ServerDamageApplied?.Invoke(source, applied);
                return applied;
            }
            finally { CurrentDamageSource = null; }
        }
    }
}
