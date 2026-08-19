using UnityEngine;

/// <summary>
/// 플레이어의 체력, 기력, 마나 최대치와 회복 수치를 함께 관리하는 불변 설계 데이터입니다.
/// </summary>
[CreateAssetMenu(
    fileName = "PlayerStatConfig",
    menuName = "EchoZone/Player/Stat Config")]
public sealed class PlayerStatConfig : ScriptableObject
{
    [Header("Maximum Values")]
    [SerializeField, Min(1)] private int maxHealth;
    [SerializeField, Min(0)] private int maxStamina;
    [SerializeField, Min(0)] private int maxMana;

    [Header("Recovery Per Second")]
    [SerializeField, Min(0f)] private float healthRecoveryPerSecond;
    [SerializeField, Min(0f)] private float staminaRecoveryPerSecond;
    [SerializeField, Min(0f)] private float manaRecoveryPerSecond;

    /// <summary>플레이어가 가질 수 있는 최대 체력입니다.</summary>
    public int MaxHealth => Mathf.Max(1, maxHealth);

    /// <summary>플레이어가 가질 수 있는 최대 기력입니다.</summary>
    public int MaxStamina => Mathf.Max(0, maxStamina);

    /// <summary>플레이어가 가질 수 있는 최대 마나입니다.</summary>
    public int MaxMana => Mathf.Max(0, maxMana);

    /// <summary>초당 회복되는 체력입니다.</summary>
    public float HealthRecoveryPerSecond => Mathf.Max(0f, healthRecoveryPerSecond);

    /// <summary>초당 회복되는 기력입니다.</summary>
    public float StaminaRecoveryPerSecond => Mathf.Max(0f, staminaRecoveryPerSecond);

    /// <summary>초당 회복되는 마나입니다.</summary>
    public float ManaRecoveryPerSecond => Mathf.Max(0f, manaRecoveryPerSecond);
}
