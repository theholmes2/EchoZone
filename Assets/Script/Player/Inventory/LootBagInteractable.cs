using UnityEngine;

namespace EchoZone.Equipment
{
    /// <summary>전리품 상자를 기존 E 상호작용 센서와 실행 잠금에 연결합니다.</summary>
    [RequireComponent(typeof(LootBagGlue), typeof(InteractionLockBehaviour))]
    public sealed class LootBagInteractable : InteractableBehaviour
    {
        private LootBagGlue bag;
        private void Awake() => bag=GetComponent<LootBagGlue>();
        /// <summary>스폰된 상자에만 상호작용을 허용합니다.</summary>
        public override bool CanInteract(GameObject interactor) => bag != null && bag.IsSpawned && interactor != null;
        /// <summary>단일 바닥 아이템이면 정의 정보를, 사망 전리품이면 가방 획득 안내를 표시합니다.</summary>
        public override bool TryGetInteractionPrompt(GameObject interactor, string binding, out string prompt)
        {
            prompt = string.Empty;
            if (!CanInteract(interactor)) return false;
            if (bag.TryGetPresentationItem(out var item))
            {
                string description = string.IsNullOrWhiteSpace(item.ItemDescription)
                    ? "인벤토리에 보관할 수 있는 아이템입니다."
                    : item.ItemDescription;
                prompt = $"[{binding}] 획득\n{item.ItemName}\n{description}";
                return true;
            }
            prompt = $"[{binding}] 획득\n전리품 가방\n보관된 아이템을 인벤토리 공간만큼 획득합니다.";
            return true;
        }
        /// <summary>서버 상자에 인벤토리 이동을 요청합니다.</summary>
        public override bool TryInteract(GameObject interactor) => CanInteract(interactor) && bag.Take(interactor);
    }
}
