using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Pet
{
    /// <summary>기존 체력 이벤트를 피격 표시와 체력 소진 도주에 연결합니다. 디스폰하지 않습니다.</summary>
    [RequireComponent(typeof(PlayerStats), typeof(PetStateGlue))]
    public sealed class PetDeathGlue : NetworkBehaviour
    {
        /// <summary>공통 체력 컴포넌트입니다.</summary>
        private PlayerStats stats;
        /// <summary>체력 소진 이후 도주를 실행할 상태 Glue입니다.</summary>
        private PetStateGlue state;
        /// <summary>각 피어의 동물 표시입니다.</summary>
        private PetAnimatorView view;
        /// <summary>초기 동기화와 실제 피격을 구분할 이전 체력입니다.</summary>
        private int previousHealth;

        /// <summary>같은 프리팹의 재사용 컴포넌트를 연결합니다.</summary>
        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            state = GetComponent<PetStateGlue>();
            view = GetComponent<PetAnimatorView>();
        }

        /// <summary>초기 체력 동기화 이후 이벤트를 구독합니다.</summary>
        protected override void OnNetworkPostSpawn()
        {
            previousHealth = stats.CurrentHealth;
            view?.ResetView();
            stats.AddHealthChangedListener(HandleHealthChanged);
            HandleHealthChanged(previousHealth);
        }

        /// <summary>체력 감소는 피격 표시로, 0은 사망 처리로 연결합니다.</summary>
        private void HandleHealthChanged(int health)
        {
            if (health < previousHealth) view?.PlayHit();
            previousHealth = health;
            if (IsServer && health <= 0) state.BeginEscapeServer();
        }

        /// <summary>이벤트와 이전 예약을 정리합니다.</summary>
        public override void OnNetworkDespawn()
        {
            stats.RemoveHealthChangedListener(HandleHealthChanged);
        }
    }
}
