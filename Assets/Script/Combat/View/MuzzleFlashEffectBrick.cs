using System.Collections;
using UnityEngine;

namespace Combat.View
{
    /// <summary>승인된 발사 신호를 파티클과 짧은 총구 조명으로 표현하는 View Brick입니다.</summary>
    public class MuzzleFlashEffectBrick : MonoBehaviour
    {
        [Header("Components")]
        /// <summary>발사 순간 재생할 선택적 총구 불꽃 파티클입니다.</summary>
        [SerializeField] private ParticleSystem muzzleFlashParticle;
        /// <summary>발사 순간 짧게 활성화해 주변을 밝힐 로컬 Point Light입니다.</summary>
        [SerializeField] private Light muzzleLight;

        [Header("Settings")]
        /// <summary>한 번 발사할 때 총구 조명을 켜 둘 시간입니다.</summary>
        [SerializeField] private float lightDuration = 0.05f;

        /// <summary>현재 실행 중인 조명 점멸 코루틴입니다.</summary>
        private Coroutine _flashCoroutine;

        /// <summary>
        /// 외부(Glue 스크립트 등)에서 총기 사격 이벤트가 발생했을 때 호출하는 함수입니다.
        /// </summary>
        public void PlayEffect()
        {
            if (muzzleFlashParticle != null)
            {
                muzzleFlashParticle.Play();
            }

            if (muzzleLight != null)
            {
                if (_flashCoroutine != null)
                {
                    StopCoroutine(_flashCoroutine);
                }

                _flashCoroutine = StartCoroutine(FlashLightCoroutine());
            }
        }

        /// <summary>조명을 설정 시간 동안 켠 뒤 다시 끕니다.</summary>
        private IEnumerator FlashLightCoroutine()
        {
            muzzleLight.enabled = true;
            yield return new WaitForSeconds(lightDuration);
            muzzleLight.enabled = false;
            _flashCoroutine = null;
        }

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
