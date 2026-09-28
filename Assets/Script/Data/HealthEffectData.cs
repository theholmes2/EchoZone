using UnityEngine;

/// <summary>
/// 체력에 적용할 수 있는 데이터 기반 효과 종류입니다.
/// </summary>
public enum HealthEffectType
{
    /// <summary>현재 체력을 최대 체력까지 회복합니다.</summary>
    FullRestore = 0,

    /// <summary>설정된 고정 피해량만큼 현재 체력을 감소시킵니다.</summary>
    FixedDamage = 1
}

/// <summary>
/// 월드 소비 아이템이 플레이어 체력에 적용할 효과 종류와 수치를 보관하는 불변 설계 데이터입니다.
/// </summary>
[CreateAssetMenu(
    fileName = "HealthEffectData",
    menuName = "EchoZone/Player/Health Effect")]
/// <summary>HealthEffectData 관련 기능과 데이터를 제공하는 형식입니다.</summary>
public sealed class HealthEffectData : ScriptableObject
{
    [SerializeField] private HealthEffectType effectType;
    [SerializeField, Min(0)] private int amount;

    /// <summary>체력에 적용할 효과 종류입니다.</summary>
    public HealthEffectType EffectType => effectType;

    /// <summary>고정 수치 효과에서 사용할 값입니다.</summary>
    public int Amount => Mathf.Max(0, amount);
}
