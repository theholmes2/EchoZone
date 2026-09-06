using EchoZone.Player.View;
using UnityEngine;

namespace EchoZone.Combat.View
{
    /// <summary>현재 조준 방향을 캐릭터 View에 지속적으로 전달하는 연출 Glue입니다.</summary>
    public sealed class CharacterAimPresentationGlue : MonoBehaviour
    {
        /// <summary>회전 결과를 실제 캐릭터 외형에 적용하는 관찰자 View입니다.</summary>
        [SerializeField] private CharacterAnimatorView characterView;
        /// <summary>외형 회전 속도를 제공하는 연출 데이터입니다.</summary>
        [SerializeField] private WeaponFireConfig config;

        /// <summary>가장 최근 마우스 입력에서 계산된 유효한 수평 조준 방향입니다.</summary>
        private Vector3 latestAimDirection;

        /// <summary>입력 또는 네트워크 Glue가 계산한 최신 수평 조준 방향을 저장합니다.</summary>
        public void SetAimDirection(Vector3 aimDirection)
        {
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                latestAimDirection = aimDirection.normalized;
            }
        }

        /// <summary>중앙 View 업데이트가 현재 조준 방향을 캐릭터 외형에 적용합니다.</summary>
        public void ManualUpdate(float deltaTime)
        {
            if (characterView == null || latestAimDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float turnSpeed = config != null ? config.AimTurnDegreesPerSecond : 0f;
            characterView.SetFacing(latestAimDirection, turnSpeed, deltaTime);
        }
    }
}
