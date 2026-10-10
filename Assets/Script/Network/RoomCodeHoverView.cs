using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>좌측 상단 방 코드는 평소 화면을 가리지 않게 낮추고, 마우스를 올리면 또렷하게 표시합니다.</summary>
public sealed class RoomCodeHoverView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.55f;

    /// <summary>직렬화 참조가 비어 있어도 같은 오브젝트의 투명도 그룹을 사용합니다.</summary>
    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        SetHovered(false);
    }

    /// <summary>포인터가 방 코드 영역에 들어오면 내용을 선명하게 표시합니다.</summary>
    public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);

    /// <summary>포인터가 방 코드 영역에서 나가면 기본 투명도로 돌아갑니다.</summary>
    public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

    /// <summary>호버 상태에 맞춰 패널 전체 투명도를 전환합니다.</summary>
    private void SetHovered(bool hovered)
    {
        if (canvasGroup != null) canvasGroup.alpha = hovered ? 1f : idleAlpha;
    }
}
