using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어의 현재 체력, 기력, 마나를 관리하는 네트워크 비의존 시뮬레이션 Brick입니다.
/// </summary>
public sealed class PlayerStats : MonoBehaviour
{
    [Header("Stat Settings")]
    [SerializeField] private PlayerStatConfig config;

    [Header("Runtime State")]
    [SerializeField] private int currentHealth;
    [SerializeField] private int currentStamina;
    [SerializeField] private int currentMana;

    [Header("State Events")]
    [SerializeField] private UnityEvent<int> healthChanged = new();
    [SerializeField] private UnityEvent<int> staminaChanged = new();
    [SerializeField] private UnityEvent<int> manaChanged = new();

    /// <summary>현재 체력입니다.</summary>
    public int CurrentHealth => currentHealth;

    /// <summary>현재 기력입니다.</summary>
    public int CurrentStamina => currentStamina;

    /// <summary>현재 마나입니다.</summary>
    public int CurrentMana => currentMana;

    /// <summary>설정 데이터에서 읽은 최대 체력입니다.</summary>
    public int MaxHealth => config != null ? config.MaxHealth : 0;

    /// <summary>설정 데이터에서 읽은 최대 기력입니다.</summary>
    public int MaxStamina => config != null ? config.MaxStamina : 0;

    /// <summary>설정 데이터에서 읽은 최대 마나입니다.</summary>
    public int MaxMana => config != null ? config.MaxMana : 0;

    /// <summary>현재 체력이 모두 소진되었는지 나타냅니다.</summary>
    public bool IsDead => currentHealth <= 0;

    /// <summary>생성된 플레이어의 현재 상태를 설정된 최대치로 초기화합니다.</summary>
    private void Awake()
    {
        ResetToMaximum();
    }

    /// <summary>현재 체력, 기력, 마나를 설정 데이터의 최대치로 초기화합니다.</summary>
    public void ResetToMaximum()
    {
        SetCurrentValues(MaxHealth, MaxStamina, MaxMana);
    }

    /// <summary>현재 상태를 외부에서 전달받은 값으로 교체하고 실제로 바뀐 상태의 이벤트를 발생시킵니다.</summary>
    /// <param name="health">적용할 현재 체력입니다.</param>
    /// <param name="stamina">적용할 현재 기력입니다.</param>
    /// <param name="mana">적용할 현재 마나입니다.</param>
    public void SetCurrentValues(int health, int stamina, int mana)
    {
        SetHealth(health);
        SetStamina(stamina);
        SetMana(mana);
    }

    /// <summary>현재 체력에서 가능한 만큼 피해량을 차감합니다.</summary>
    /// <param name="requestedDamage">적용을 요청한 피해량입니다.</param>
    /// <returns>실제로 차감된 체력입니다.</returns>
    public int ApplyDamage(int requestedDamage)
    {
        if (requestedDamage <= 0 || currentHealth <= 0)
        {
            return 0;
        }

        int previousHealth = currentHealth;
        SetHealth(currentHealth - requestedDamage);
        return previousHealth - currentHealth;
    }

    /// <summary>현재 체력을 최대 체력 이내에서 가능한 만큼 회복합니다.</summary>
    /// <param name="requestedAmount">회복을 요청한 체력입니다.</param>
    /// <returns>실제로 회복된 체력입니다.</returns>
    public int RestoreHealth(int requestedAmount)
    {
        if (requestedAmount <= 0 || currentHealth >= MaxHealth)
        {
            return 0;
        }

        int previousHealth = currentHealth;
        SetHealth(currentHealth + requestedAmount);
        return currentHealth - previousHealth;
    }

    /// <summary>현재 기력에서 요청한 수량을 사용할 수 있으면 차감합니다.</summary>
    /// <param name="amount">사용할 기력입니다.</param>
    /// <returns>기력을 모두 지불했으면 <see langword="true"/>입니다.</returns>
    public bool TryConsumeStamina(int amount)
    {
        if (amount <= 0 || currentStamina < amount)
        {
            return false;
        }

        SetStamina(currentStamina - amount);
        return true;
    }

    /// <summary>현재 기력을 최대 기력 이내에서 가능한 만큼 회복합니다.</summary>
    /// <param name="requestedAmount">회복을 요청한 기력입니다.</param>
    /// <returns>실제로 회복된 기력입니다.</returns>
    public int RestoreStamina(int requestedAmount)
    {
        if (requestedAmount <= 0 || currentStamina >= MaxStamina)
        {
            return 0;
        }

        int previousStamina = currentStamina;
        SetStamina(currentStamina + requestedAmount);
        return currentStamina - previousStamina;
    }

    /// <summary>현재 마나에서 요청한 수량을 사용할 수 있으면 차감합니다.</summary>
    /// <param name="amount">사용할 마나입니다.</param>
    /// <returns>마나를 모두 지불했으면 <see langword="true"/>입니다.</returns>
    public bool TryConsumeMana(int amount)
    {
        if (amount <= 0 || currentMana < amount)
        {
            return false;
        }

        SetMana(currentMana - amount);
        return true;
    }

    /// <summary>현재 마나를 최대 마나 이내에서 가능한 만큼 회복합니다.</summary>
    /// <param name="requestedAmount">회복을 요청한 마나입니다.</param>
    /// <returns>실제로 회복된 마나입니다.</returns>
    public int RestoreMana(int requestedAmount)
    {
        if (requestedAmount <= 0 || currentMana >= MaxMana)
        {
            return 0;
        }

        int previousMana = currentMana;
        SetMana(currentMana + requestedAmount);
        return currentMana - previousMana;
    }

    /// <summary>체력 변경 이벤트를 받을 함수를 등록합니다.</summary>
    /// <param name="listener">현재 체력을 받을 함수입니다.</param>
    public void AddHealthChangedListener(UnityAction<int> listener)
    {
        healthChanged.AddListener(listener);
    }

    /// <summary>체력 변경 이벤트에 등록했던 함수를 제거합니다.</summary>
    /// <param name="listener">제거할 함수입니다.</param>
    public void RemoveHealthChangedListener(UnityAction<int> listener)
    {
        healthChanged.RemoveListener(listener);
    }

    /// <summary>기력 변경 이벤트를 받을 함수를 등록합니다.</summary>
    /// <param name="listener">현재 기력을 받을 함수입니다.</param>
    public void AddStaminaChangedListener(UnityAction<int> listener)
    {
        staminaChanged.AddListener(listener);
    }

    /// <summary>기력 변경 이벤트에 등록했던 함수를 제거합니다.</summary>
    /// <param name="listener">제거할 함수입니다.</param>
    public void RemoveStaminaChangedListener(UnityAction<int> listener)
    {
        staminaChanged.RemoveListener(listener);
    }

    /// <summary>마나 변경 이벤트를 받을 함수를 등록합니다.</summary>
    /// <param name="listener">현재 마나를 받을 함수입니다.</param>
    public void AddManaChangedListener(UnityAction<int> listener)
    {
        manaChanged.AddListener(listener);
    }

    /// <summary>마나 변경 이벤트에 등록했던 함수를 제거합니다.</summary>
    /// <param name="listener">제거할 함수입니다.</param>
    public void RemoveManaChangedListener(UnityAction<int> listener)
    {
        manaChanged.RemoveListener(listener);
    }

    /// <summary>현재 체력을 유효 범위로 제한하고 값이 달라졌을 때 이벤트를 발생시킵니다.</summary>
    /// <param name="value">적용할 체력입니다.</param>
    private void SetHealth(int value)
    {
        int clampedValue = Mathf.Clamp(value, 0, MaxHealth);
        if (currentHealth == clampedValue)
        {
            return;
        }

        currentHealth = clampedValue;
        healthChanged.Invoke(currentHealth);
    }

    /// <summary>현재 기력을 유효 범위로 제한하고 값이 달라졌을 때 이벤트를 발생시킵니다.</summary>
    /// <param name="value">적용할 기력입니다.</param>
    private void SetStamina(int value)
    {
        int clampedValue = Mathf.Clamp(value, 0, MaxStamina);
        if (currentStamina == clampedValue)
        {
            return;
        }

        currentStamina = clampedValue;
        staminaChanged.Invoke(currentStamina);
    }

    /// <summary>현재 마나를 유효 범위로 제한하고 값이 달라졌을 때 이벤트를 발생시킵니다.</summary>
    /// <param name="value">적용할 마나입니다.</param>
    private void SetMana(int value)
    {
        int clampedValue = Mathf.Clamp(value, 0, MaxMana);
        if (currentMana == clampedValue)
        {
            return;
        }

        currentMana = clampedValue;
        manaChanged.Invoke(currentMana);
    }
}
