using Combat.View;
using EchoZone.Combat.Glue;
using EchoZone.Player.View;
using UnityEngine;

namespace Combat.Glue
{
    /// <summary>
    /// 무기 발사 데이터(알림)와 시각 효과(행동)를 이벤트로 연결해주는 허브 스크립트입니다.
    /// </summary>
    public class WeaponFireEventBinderGlue : MonoBehaviour
    {
        [Header("Rules & Network Brick")]
        /// <summary>서버가 승인한 발사를 모든 Client에 알리는 네트워크 Glue입니다.</summary>
        [SerializeField] private NetworkWeaponFireGlue networkWeaponFireGlue;

        [Header("Visual Effect Bricks")]
        /// <summary>Client별 로컬 총구 불빛과 파티클을 재생할 연출 컴포넌트입니다.</summary>
        [SerializeField] private MuzzleFlashEffectBrick muzzleFlashEffectBrick;
        /// <summary>승인된 발사에만 오른팔 반동을 재생하는 캐릭터 View입니다.</summary>
        [SerializeField] private CharacterAnimatorView characterView;

        /// <summary>발사 알림 이벤트와 로컬 총구 연출을 연결합니다.</summary>
        private void OnEnable()
        {
            if (networkWeaponFireGlue != null)
            {
                networkWeaponFireGlue.OnFireClientNotified += HandleFire;
            }
        }

        /// <summary>오브젝트가 파괴될 때 발사 이벤트 구독을 해제합니다.</summary>
        private void OnDisable()
        {
            if (networkWeaponFireGlue != null)
            {
                networkWeaponFireGlue.OnFireClientNotified -= HandleFire;
            }
            characterView?.ResetFireAnimation();
        }

        /// <summary>기존 발사 승인 알림을 불빛과 애니메이션 View로 각각 전달합니다.</summary>
        private void HandleFire()
        {
            muzzleFlashEffectBrick?.PlayEffect();
            characterView?.PlayFireAnimation();
        }
    }
}
