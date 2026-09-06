using UnityEngine;
using Unity.Netcode;
using EchoZone.Player.View; // CharacterAnimatorView가 있는 네임스페이스
using EchoZone.Environment.View;

namespace EchoZone.Player.Glue
{
    /// <summary>
    /// 멀티플레이어 환경에서 오직 로컬 오너 플레이어의 회전/위치 데이터를 
    /// 맵의 셰이더 연출 컨트롤러와 결합(Glue)해주는 스크립트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSeeThroughViewGlue : NetworkBehaviour
    {
        [Header("Bricks (View)")]
        [Tooltip("캐릭터의 회전을 담당하고 있는 애니메이터 뷰 컴포넌트입니다.")]
        [SerializeField] private CharacterAnimatorView characterAnimatorView;

        /// <summary>맵(Scene)에 존재하는 순수 로컬 셰이더 연출 컨트롤러입니다.</summary>
        private SeeThroughTunnelController sceneSeeThroughController;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // 🌟 매우 중요: 내가 조종하는 로컬 오너 캐릭터가 아니라면 이 컴포넌트를 즉시 끕니다.
            // 이 방어 코드 덕분에 다른 플레이어가 접속해도 내 화면의 셰이더가 오염되지 않습니다.
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            // 자동으로 씬에 배치된 셰이더 컨트롤러를 찾아옵니다. (싱글톤이 있다면 싱글톤 접근도 좋습니다)
            sceneSeeThroughController = Object.FindAnyObjectByType<SeeThroughTunnelController>();

            if (characterAnimatorView == null)
            {
                characterAnimatorView = GetComponentInChildren<CharacterAnimatorView>(true);
            }
        }

        /// <summary>상위 플레이어 업데이트 Glue가 호출하여 로컬 시스루 연출을 갱신합니다.</summary>
        public void ManualUpdate()
        {
            if (!IsSpawned || !IsOwner || characterAnimatorView == null) return;

            if (sceneSeeThroughController == null)
            {
                sceneSeeThroughController = Object.FindAnyObjectByType<SeeThroughTunnelController>();
            }

            if (sceneSeeThroughController == null) return;

            // 🧪 [접착] 캐릭터 회전 브릭(CharacterAnimatorView)의 transform(위치+회전값)을 
            // 맵에 배치된 셰이더 컨트롤러에 매 프레임 동기화합니다.
            sceneSeeThroughController.UpdateShaderVariables(characterAnimatorView.transform);
        }

        public override void OnNetworkDespawn()
        {
            // 플레이어가 게임에서 나가거나 파괴될 때 셰이더 변수가 남는 것을 방지하기 위한 안전장치
            if (IsOwner)
            {
                sceneSeeThroughController?.ClearShaderVariables();
            }

            sceneSeeThroughController = null;
            base.OnNetworkDespawn();
        }
    }
}
