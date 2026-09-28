using UnityEngine;

/// <summary>로컬 건물 정보창의 탐색·배치·갱신 설정입니다. 조준 레이와 독립적입니다.</summary>
[CreateAssetMenu(menuName = "EchoZone/Heist/Building Hover Config")]
public sealed class BuildingHoverConfig : ScriptableObject
{
    /// <summary>건물 Collider 탐색에만 사용할 레이어입니다.</summary>
    public LayerMask BuildingLayers = ~0;
    /// <summary>카메라에서 건물까지 검사할 최대 거리입니다.</summary>
    [Min(1)] public float RayDistance = 500;
    /// <summary>동기화 상태의 문자열 갱신 간격입니다.</summary>
    [Min(0.02f)] public float RefreshSeconds = 0.1f;
    /// <summary>Canvas 기준 정보창 크기입니다.</summary>
    public Vector2 Size = new(330, 170);
    /// <summary>마우스 기준 정보창 위치 보정입니다.</summary>
    public Vector2 Offset = new(22, -22);
    /// <summary>화면 가장자리와의 최소 여백입니다.</summary>
    [Min(0)] public float EdgePadding = 12;
}
