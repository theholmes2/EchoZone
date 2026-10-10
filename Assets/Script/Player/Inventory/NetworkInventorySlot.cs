using System;
using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// 인벤토리 슬롯을 네트워크로 전송하기 위한 ItemId와 수량만 보관하는 원시 데이터입니다.
/// </summary>
public struct NetworkInventorySlot : INetworkSerializable, IEquatable<NetworkInventorySlot>
{
    /// <summary>ItemCatalog에서 ItemData를 찾을 아이템 고유 ID입니다.</summary>
    public FixedString64Bytes ItemId;

    /// <summary>슬롯에 저장된 아이템 수량입니다.</summary>
    public int Quantity;
    /// <summary>동일 종류 장비의 개별 식별자를 소유 클라이언트에 전달합니다.</summary>
    public FixedString64Bytes InstanceId;
    /// <summary>보관 장비의 탄창 잔량입니다.</summary>
    public int MagazineRounds;
    /// <summary>서버가 승인한 장비의 실제 구매 금액입니다.</summary>
    public int PurchaseValue;

    /// <summary>아이템 ID와 수량으로 네트워크 슬롯 데이터를 생성합니다.</summary>
    /// <param name="itemId">전송할 아이템 고유 ID입니다.</param>
    /// <param name="quantity">전송할 슬롯 수량입니다.</param>
    public NetworkInventorySlot(string itemId, int quantity, string instanceId = null, int magazineRounds = -1, int purchaseValue = 0)
    {
        ItemId = itemId;
        Quantity = quantity;
        InstanceId = instanceId ?? string.Empty;
        MagazineRounds = magazineRounds;
        PurchaseValue = Math.Max(0, purchaseValue);
    }

    /// <summary>NGO 버퍼에 아이템 ID와 수량을 직렬화하거나 역직렬화합니다.</summary>
    /// <typeparam name="T">읽기 또는 쓰기 버퍼 타입입니다.</typeparam>
    /// <param name="serializer">NGO가 제공하는 버퍼 직렬화 도구입니다.</param>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ItemId);
        serializer.SerializeValue(ref Quantity);
        serializer.SerializeValue(ref InstanceId);
        serializer.SerializeValue(ref MagazineRounds);
        serializer.SerializeValue(ref PurchaseValue);
    }

    /// <summary>두 네트워크 슬롯의 아이템 ID와 수량이 같은지 비교합니다.</summary>
    /// <param name="other">비교할 다른 네트워크 슬롯입니다.</param>
    /// <returns>아이템 ID와 수량이 모두 같으면 <see langword="true"/>입니다.</returns>
    public bool Equals(NetworkInventorySlot other)
    {
        return ItemId.Equals(other.ItemId) && Quantity == other.Quantity &&
            InstanceId.Equals(other.InstanceId) && MagazineRounds == other.MagazineRounds &&
            PurchaseValue == other.PurchaseValue;
    }
}
