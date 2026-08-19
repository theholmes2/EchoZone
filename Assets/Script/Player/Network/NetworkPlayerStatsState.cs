using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Player.Network
{
    /// <summary>
    /// 서버 PlayerStats의 현재 상태를 각 클라이언트의 로컬 복사본에 연결하는 네트워크 Glue입니다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(PlayerStats))]
    public sealed class NetworkPlayerStatsState : NetworkBehaviour
    {
        /// <summary>서버만 변경하고 모든 클라이언트가 읽는 현재 체력입니다.</summary>
        private NetworkVariable<int> networkHealth = new(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>서버만 변경하고 해당 플레이어의 Owner만 읽는 현재 기력입니다.</summary>
        private NetworkVariable<int> networkStamina = new(
            0,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        /// <summary>서버만 변경하고 해당 플레이어의 Owner만 읽는 현재 마나입니다.</summary>
        private NetworkVariable<int> networkMana = new(
            0,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        /// <summary>서버 원본 또는 클라이언트 복사본의 상태를 보관하는 Brick입니다.</summary>
        private PlayerStats playerStats;

        /// <summary>같은 플레이어 오브젝트에 있는 상태 Brick을 찾습니다.</summary>
        private void Awake()
        {
            playerStats = GetComponent<PlayerStats>();
        }

        /// <summary>로컬 상태 Brick의 변경 이벤트를 구독합니다.</summary>
        private void OnEnable()
        {
            playerStats?.AddHealthChangedListener(HandleLocalHealthChanged);
            playerStats?.AddStaminaChangedListener(HandleLocalStaminaChanged);
            playerStats?.AddManaChangedListener(HandleLocalManaChanged);
        }

        /// <summary>로컬 상태 Brick의 변경 이벤트 구독을 해제합니다.</summary>
        private void OnDisable()
        {
            playerStats?.RemoveHealthChangedListener(HandleLocalHealthChanged);
            playerStats?.RemoveStaminaChangedListener(HandleLocalStaminaChanged);
            playerStats?.RemoveManaChangedListener(HandleLocalManaChanged);
        }

        /// <summary>네트워크 Spawn 시 값 변경 이벤트를 연결하고 현재 상태를 초기 동기화합니다.</summary>
        public override void OnNetworkSpawn()
        {
            networkHealth.OnValueChanged += HandleNetworkHealthChanged;
            networkStamina.OnValueChanged += HandleNetworkStaminaChanged;
            networkMana.OnValueChanged += HandleNetworkManaChanged;

            if (playerStats == null)
            {
                return;
            }

            if (IsServer)
            {
                CopyStatsToNetworkVariables();
                return;
            }

            ApplyVisibleNetworkValuesToStats();
        }

        /// <summary>네트워크 Despawn 시 값 변경 이벤트 연결을 해제합니다.</summary>
        public override void OnNetworkDespawn()
        {
            networkHealth.OnValueChanged -= HandleNetworkHealthChanged;
            networkStamina.OnValueChanged -= HandleNetworkStaminaChanged;
            networkMana.OnValueChanged -= HandleNetworkManaChanged;
        }

        /// <summary>서버의 현재 상태 전체를 각 NetworkVariable에 기록합니다.</summary>
        private void CopyStatsToNetworkVariables()
        {
            networkHealth.Value = playerStats.CurrentHealth;
            networkStamina.Value = playerStats.CurrentStamina;
            networkMana.Value = playerStats.CurrentMana;
        }

        /// <summary>현재 클라이언트가 읽을 수 있는 네트워크 값을 로컬 상태 복사본에 적용합니다.</summary>
        private void ApplyVisibleNetworkValuesToStats()
        {
            int stamina = IsOwner
                ? networkStamina.Value
                : playerStats.CurrentStamina;
            int mana = IsOwner
                ? networkMana.Value
                : playerStats.CurrentMana;

            playerStats.SetCurrentValues(
                networkHealth.Value,
                stamina,
                mana);
        }

        /// <summary>서버에서 체력이 변경되면 전체 공개 네트워크 값에 기록합니다.</summary>
        /// <param name="currentHealth">변경된 현재 체력입니다.</param>
        private void HandleLocalHealthChanged(int currentHealth)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            networkHealth.Value = currentHealth;
        }

        /// <summary>서버에서 기력이 변경되면 Owner 전용 네트워크 값에 기록합니다.</summary>
        /// <param name="currentStamina">변경된 현재 기력입니다.</param>
        private void HandleLocalStaminaChanged(int currentStamina)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            networkStamina.Value = currentStamina;
        }

        /// <summary>서버에서 마나가 변경되면 Owner 전용 네트워크 값에 기록합니다.</summary>
        /// <param name="currentMana">변경된 현재 마나입니다.</param>
        private void HandleLocalManaChanged(int currentMana)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            networkMana.Value = currentMana;
        }

        /// <summary>서버 체력이 변경되면 클라이언트의 로컬 상태 복사본에 적용합니다.</summary>
        /// <param name="previousHealth">변경 전 체력입니다.</param>
        /// <param name="currentHealth">변경 후 체력입니다.</param>
        private void HandleNetworkHealthChanged(int previousHealth, int currentHealth)
        {
            if (IsServer || playerStats == null)
            {
                return;
            }

            playerStats.SetCurrentValues(
                currentHealth,
                playerStats.CurrentStamina,
                playerStats.CurrentMana);
        }

        /// <summary>서버 기력이 변경되면 Owner Client의 로컬 상태 복사본에 적용합니다.</summary>
        /// <param name="previousStamina">변경 전 기력입니다.</param>
        /// <param name="currentStamina">변경 후 기력입니다.</param>
        private void HandleNetworkStaminaChanged(int previousStamina, int currentStamina)
        {
            if (IsServer || !IsOwner || playerStats == null)
            {
                return;
            }

            playerStats.SetCurrentValues(
                playerStats.CurrentHealth,
                currentStamina,
                playerStats.CurrentMana);
        }

        /// <summary>서버 마나가 변경되면 Owner Client의 로컬 상태 복사본에 적용합니다.</summary>
        /// <param name="previousMana">변경 전 마나입니다.</param>
        /// <param name="currentMana">변경 후 마나입니다.</param>
        private void HandleNetworkManaChanged(int previousMana, int currentMana)
        {
            if (IsServer || !IsOwner || playerStats == null)
            {
                return;
            }

            playerStats.SetCurrentValues(
                playerStats.CurrentHealth,
                playerStats.CurrentStamina,
                currentMana);
        }
    }
}
