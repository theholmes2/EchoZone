using TMPro;
using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>입력을 가로채지 않는 로컬 건물 툴팁을 화면 안쪽에 표시합니다.</summary>
    public sealed class BuildingHoverView : MonoBehaviour
    {
        /// <summary>표시·숨김 및 위치를 적용할 정보창입니다.</summary>
        [SerializeField] private RectTransform panel;
        /// <summary>기존 한글 폰트를 사용하는 정보 텍스트입니다.</summary>
        [SerializeField] private TMP_Text label;
        /// <summary>화면 좌표를 Canvas 좌표로 변환할 부모입니다.</summary>
        [SerializeField] private Canvas canvas;

        /// <summary>접속 전이나 건물에서 벗어난 경우 정보창을 숨깁니다.</summary>
        public void Hide() { if (panel != null) panel.gameObject.SetActive(false); }

        /// <summary>포인터를 따라가되 정보창 전체가 Canvas 경계 안에 남도록 제한합니다.</summary>
        public void Show(string text, Vector2 screenPoint, BuildingHoverConfig config)
        {
            if (panel == null || label == null || canvas == null) return;
            panel.gameObject.SetActive(true); label.text = text;
            var parent = (RectTransform)panel.parent;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, camera, out var local);
            Vector2 size = Vector2.Min(config.Size, parent.rect.size - Vector2.one * config.EdgePadding * 2);
            size = Vector2.Max(size, Vector2.one);
            panel.sizeDelta = size;
            local += config.Offset;
            local.x = Mathf.Clamp(local.x, parent.rect.xMin + config.EdgePadding, parent.rect.xMax - size.x - config.EdgePadding);
            local.y = Mathf.Clamp(local.y, parent.rect.yMin + size.y + config.EdgePadding, parent.rect.yMax - config.EdgePadding);
            panel.localPosition = new Vector3(local.x, local.y, 0);
        }
    }
}
