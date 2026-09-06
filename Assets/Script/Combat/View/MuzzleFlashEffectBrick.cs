using System.Collections;
using UnityEngine;

namespace Combat.View
{
    public class MuzzleFlashEffectBrick : MonoBehaviour
    {
        [Header("Components")]
        /// <summary>발사 순간 재생할 선택적 총구 불꽃 파티클입니다.</summary>
        [SerializeField] private ParticleSystem muzzleFlashParticle; // 총구 불꽃 파티클
        /// <summary>발사 순간 짧게 활성화해 주변을 밝힐 로컬 Point Light입니다.</summary>
        [SerializeField] private Light muzzleLight;                  // 실시간 그림자용 Point Light

        [Header("Settings")]
        /// <summary>한 번 발사할 때 총구 조명을 켜 둘 시간입니다.</summary>
        [SerializeField] private float lightDuration = 0.05f;       // 불빛과 그림자가 유지될 시간

        /// <summary>현재 실행 중인 조명 점멸 코루틴입니다.</summary>
        private Coroutine _flashCoroutine;

        /// <summary>
        /// 외부(Glue 스크립트 등)에서 총기 사격 이벤트가 발생했을 때 호출하는 함수입니다.
        /// </summary>
        public void PlayEffect()
        {
            // 1. 파티클 시스템 재생
            if (muzzleFlashParticle != null)
            {
                muzzleFlashParticle.Play();
            }

            // 2. 실시간 불빛 및 그림자 코루틴 제어
            if (muzzleLight != null)
            {
                // 연사 시 코루틴이 겹치지 않도록 이미 실행 중인 코루틴이 있다면 안전하게 중지
                if (_flashCoroutine != null)
                {
                    StopCoroutine(_flashCoroutine);
                }

                // 새로 불빛 켜기 코루틴 시작
                _flashCoroutine = StartCoroutine(FlashLightCoroutine());
            }
        }

        /// <summary>조명을 설정 시간 동안 켠 뒤 다시 끕니다.</summary>
        private IEnumerator FlashLightCoroutine()
        {
            // 빛과 실시간 그림자 활성화
            muzzleLight.enabled = true;

            // 설정한 시간(0.05초) 동안 대기
            yield return new WaitForSeconds(lightDuration);

            // 빛과 실시간 그림자 비활성화
            muzzleLight.enabled = false;
            _flashCoroutine = null;
        }

        // 게임 도중 총기를 바꾸거나 파괴될 때 불빛이 켜진 채로 멈추는 버그 방지
        /// <summary>View가 비활성화될 때 조명이 켜진 상태로 남지 않도록 정리합니다.</summary>
        private void OnDisable()
        {
            if (muzzleLight != null)
            {
                muzzleLight.enabled = false;
            }
            _flashCoroutine = null;
        }
    }
}
