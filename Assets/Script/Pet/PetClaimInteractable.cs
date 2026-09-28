using UnityEngine;

namespace EchoZone.Pet
{
    /// <summary>기존 상호작용 센서·잠금·서버 RPC를 펫 주인 변경에 연결합니다.</summary>
    [RequireComponent(typeof(PetStateGlue), typeof(InteractionLockBehaviour))]
    public sealed class PetClaimInteractable : InteractableBehaviour
    {
        /// <summary>상호작용할 펫 행동입니다.</summary>
        private PetStateGlue pet;
        /// <summary>동일 프리팹 연결을 준비합니다.</summary>
        private void Awake() => pet = GetComponent<PetStateGlue>();
        /// <summary>대기하며 최소 한 점의 체력을 회복한 펫만 후보가 됩니다.</summary>
        public override bool CanInteract(GameObject interactor) => pet != null && pet.CanBeClaimed;
        /// <summary>서버가 재검증한 경우만 소속을 변경합니다.</summary>
        public override bool TryInteract(GameObject interactor) => pet != null && pet.TryClaimServer(interactor);
    }
}
