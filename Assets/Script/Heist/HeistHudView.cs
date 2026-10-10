using TMPro;
using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>수배·주변 건물·펫 선택을 표시하는 로컬 uGUI View입니다.</summary>
    public sealed class HeistHudView : MonoBehaviour
    {
        /// <summary>접속 중에만 표시할 HUD 컨테이너입니다.</summary>
        [SerializeField] private GameObject content;
        /// <summary>우측 상단 수배 명단입니다.</summary>
        [SerializeField] private TMP_Text wantedText;
        /// <summary>주변 건물 현금·검사 상태입니다.</summary>
        [SerializeField] private TMP_Text buildingText;
        /// <summary>주변 대기 펫과 자신의 장물·보상을 표시합니다.</summary>
        [SerializeField] private TMP_Text petText;
        /// <summary>최근 서버 결과입니다.</summary>
        [SerializeField] private TMP_Text feedbackText;
        /// <summary>건물 절도 지시 버튼입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button stealButton;
        /// <summary>대기 펫 인계 버튼입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button takeButton;
        /// <summary>장물 신고 버튼입니다.</summary>
        [SerializeField] private UnityEngine.UI.Button reportButton;
        /// <summary>가까운 아이템이 있을 때만 켜는 상호작용 안내창입니다.</summary>
        [SerializeField] private GameObject itemPrompt;
        /// <summary>상호작용 키와 아이템 이름·수량·설명을 표시합니다.</summary>
        [SerializeField] private TMP_Text itemPromptText;
        /// <summary>View는 서버 규칙을 모르며 클릭만 Glue에 전달합니다.</summary>
        public void Bind(UnityEngine.Events.UnityAction steal, UnityEngine.Events.UnityAction take, UnityEngine.Events.UnityAction report)
        { stealButton.onClick.AddListener(steal); takeButton.onClick.AddListener(take); reportButton.onClick.AddListener(report); }
        /// <summary>세션에 맞춰 표시를 켜고 끕니다.</summary>
        public void SetVisible(bool value) { if (content.activeSelf != value) content.SetActive(value); }
        /// <summary>문자열과 버튼 사용 가능 여부만 반영합니다.</summary>
        public void Show(string wanted, string building, string pets, string feedback, bool steal, bool take, bool report)
        { wantedText.text = wanted; buildingText.text = building; petText.text = pets; feedbackText.text = feedback;
            stealButton.interactable = steal; takeButton.interactable = take; reportButton.interactable = report; }
        /// <summary>가까운 아이템 안내 문구를 표시하거나 후보가 없으면 숨깁니다.</summary>
        public void ShowItemPrompt(string prompt)
        {
            bool visible = !string.IsNullOrEmpty(prompt);
            if (itemPrompt != null && itemPrompt.activeSelf != visible) itemPrompt.SetActive(visible);
            if (visible && itemPromptText != null) itemPromptText.text = prompt;
        }
    }
}
