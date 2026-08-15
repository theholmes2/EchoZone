using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 네트워크와 저장 데이터의 ItemId를 프로젝트의 ItemData로 변환하는 중앙 데이터 허브입니다.
/// </summary>
[CreateAssetMenu(
    fileName = "ItemCatalog",
    menuName = "EchoZone/Item/Catalog")]
public sealed class ItemCatalog : ScriptableObject
{
    /// <summary>게임에서 사용할 모든 아이템 정의 목록입니다.</summary>
    [SerializeField] private List<ItemData> items = new();

    /// <summary>아이템 ID에 해당하는 ItemData 검색을 시도합니다.</summary>
    /// <param name="itemId">검색할 아이템 고유 ID입니다.</param>
    /// <param name="item">검색된 아이템 정의입니다.</param>
    /// <returns>일치하는 아이템 정의를 찾았으면 <see langword="true"/>입니다.</returns>
    public bool TryGetItem(string itemId, out ItemData item)
    {
        for (int i = 0; i < items.Count; i++)
        {
            ItemData candidate = items[i];
            if (candidate == null)
            {
                continue;
            }

            if (string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal))
            {
                item = candidate;
                return true;
            }
        }

        item = null;
        return false;
    }
}
