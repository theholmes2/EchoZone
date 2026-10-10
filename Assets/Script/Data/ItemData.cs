using UnityEngine;


/// <summary>
/// 아이템의 이름, 아이콘, 최대 중첩 수 등 변하지 않는 정의 데이터를 보관합니다.
/// </summary>
[CreateAssetMenu(fileName = "NewItemData", menuName = "ScriptableObjects/ItemData", order = 1)]
public class ItemData : ScriptableObject
{

    [Header("Item Basic Info")]
    [SerializeField] private string itemId;           // 아이템 ID
    [SerializeField] private string itemName;       // 이름
    [SerializeField] private Sprite itemIcon;       // 아이콘
    [SerializeField] private string itemDescription;// 설명

    [Header("Item Settings")]
    [SerializeField, Min(1)] private int maxStackSize = 1;     // 최대 중첩 수
    [SerializeField] private GameObject worldPrefab;// 월드 Prefab
    [SerializeField] private GameObject worldVisualPrefab;

    /// <summary>아이템 종류를 구분하는 고유 식별자입니다.</summary>
    public string ItemId => itemId;

    /// <summary>화면에 표시할 아이템 이름입니다.</summary>
    public string ItemName => itemName;

    /// <summary>인벤토리 UI 등에 표시할 아이콘입니다.</summary>
    public Sprite ItemIcon => itemIcon;

    /// <summary>아이템 설명 문구입니다.</summary>
    public string ItemDescription => itemDescription;

    /// <summary>슬롯 하나에 담을 수 있는 최대 수량입니다.</summary>
    public int MaxStackSize => Mathf.Max(1, maxStackSize);

    /// <summary>아이템을 월드에 표시할 때 사용할 프리팹입니다.</summary>
    public GameObject WorldPrefab => worldPrefab;

    /// <summary>공용 네트워크 수집 오브젝트 안에서 이 아이템을 나타낼 시각 전용 프리팹입니다.</summary>
    public GameObject WorldVisualPrefab => worldVisualPrefab;

}
