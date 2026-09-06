using Combat.View; // MuzzleFlashEffect가 위치한 네임스페이스
using EchoZone.Combat.Glue;
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
        // 💡 나중에 사운드, 카메라 흔들림 브릭이 추가되면 여기에 변수를 늘려주면 됩니다.

        /// <summary>발사 알림 이벤트와 로컬 총구 연출을 연결합니다.</summary>
        private void Awake()
        {
            // 두 브릭이 인스펙터에 정상적으로 조립되어 있는지 검증
            if (networkWeaponFireGlue != null && muzzleFlashEffectBrick != null)
            {
                // ⚡ [접착] 사격 성공 알림 신호가 오면 -> 총구 불빛을 재생하라!
                networkWeaponFireGlue.OnFireClientNotified += muzzleFlashEffectBrick.PlayEffect;
            }
        }

        /// <summary>오브젝트가 파괴될 때 발사 이벤트 구독을 해제합니다.</summary>
        private void OnDestroy()
        {
            // 메모리 누수(누수 방지)를 위해 오브젝트 파괴 시 연결을 끊어줍니다.
            if (networkWeaponFireGlue != null && muzzleFlashEffectBrick != null)
            {
                networkWeaponFireGlue.OnFireClientNotified -= muzzleFlashEffectBrick.PlayEffect;
            }
        }
    }
}
