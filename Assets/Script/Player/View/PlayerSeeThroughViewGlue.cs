using UnityEngine;
using Unity.Netcode;
using EchoZone.Player.View;
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

        /// <summary>OnNetworkSpawn 작업을 수행합니다.</summary>
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                enabled = false;
                return;
            }

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

            sceneSeeThroughController.UpdateShaderVariables(characterAnimatorView.transform);
        }

        /// <summary>OnNetworkDespawn 작업을 수행합니다.</summary>
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                sceneSeeThroughController?.ClearShaderVariables();
            }

            sceneSeeThroughController = null;
            base.OnNetworkDespawn();
        }
    }
}
