using UnityEngine;

/// <summary>월드에서 주웠을 때 인벤토리 대신 개인 지갑에 적립할 화폐 아이템을 정의합니다.</summary>
[CreateAssetMenu(fileName = "CurrencyItem", menuName = "EchoZone/Item/Currency")]
public sealed class CurrencyItemData : ItemData
{
    [SerializeField, Min(1)] private int creditValue = 1;

    /// <summary>화폐 한 개를 획득했을 때 서버 지갑에 적립할 금액입니다.</summary>
    public int CreditValue => Mathf.Max(1, creditValue);
}
