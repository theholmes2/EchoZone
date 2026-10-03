using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Enemy
{
    public sealed partial class PoliceEnemyBrainGlue
    {
        /// <summary>경찰이 전투 중이라 플레이어와 물리적으로 충돌해야 하는지 모든 피어에 공유합니다.</summary>
        private readonly NetworkVariable<bool> blocksPlayers = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>경찰 본체의 물리 충돌체입니다.</summary>
        private Collider policeBodyCollider;
        /// <summary>플레이어 충돌체별로 마지막 적용한 IgnoreCollision 상태를 저장합니다.</summary>
        private readonly Dictionary<int, bool> ignoredPlayerColliders = new();
        /// <summary>늦게 생성된 플레이어도 처리할 다음 충돌 목록 갱신 시각입니다.</summary>
        private float nextPlayerCollisionRefresh;

        /// <summary>경찰 생성 시 전투 충돌 상태 구독과 본체 참조를 준비합니다.</summary>
        private void InitializePlayerCollisions()
        {
            policeBodyCollider = GetComponent<Collider>();
            ignoredPlayerColliders.Clear();
            nextPlayerCollisionRefresh = 0f;
            blocksPlayers.OnValueChanged += HandleBlocksPlayersChanged;
            if (IsServer) blocksPlayers.Value = false;
            ApplyPlayerCollisions();
        }

        /// <summary>경찰 제거 전에 이벤트를 해제하고 남은 플레이어 충돌을 원래대로 돌립니다.</summary>
        private void ShutdownPlayerCollisions()
        {
            blocksPlayers.OnValueChanged -= HandleBlocksPlayersChanged;
            SetAllPlayerCollisionPairsIgnored(false);
            ignoredPlayerColliders.Clear();
        }

        /// <summary>후발 접속 플레이어를 일정 간격으로 찾아 현재 전투 충돌 규칙을 적용합니다.</summary>
        private void ManualUpdatePlayerCollisions()
        {
            if (Time.unscaledTime < nextPlayerCollisionRefresh) return;
            nextPlayerCollisionRefresh = Time.unscaledTime + 0.25f;
            ApplyPlayerCollisions();
        }

        /// <summary>서버 행동 상태를 경찰과 플레이어의 물리 충돌 여부로 변환합니다.</summary>
        private void SetPlayerBlockingServer(bool blocking)
        {
            if (IsServer && blocksPlayers.Value != blocking) blocksPlayers.Value = blocking;
        }

        /// <summary>복제된 전투 충돌 상태가 바뀌면 현재 피어의 물리 쌍을 즉시 갱신합니다.</summary>
        private void HandleBlocksPlayersChanged(bool previous, bool current) => ApplyPlayerCollisions();

        /// <summary>접속한 모든 플레이어 본체와 이 경찰의 충돌 쌍을 현재 상태에 맞춥니다.</summary>
        private void ApplyPlayerCollisions()
        {
            if (policeBodyCollider == null || NetworkManager == null || !NetworkManager.IsListening) return;
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                NetworkObject playerObject = client?.PlayerObject;
                if (playerObject == null || !playerObject.IsSpawned) continue;
                Collider playerCollider = playerObject.GetComponent<Collider>();
                if (playerCollider == null) continue;

                bool overlapping = policeBodyCollider.bounds.Intersects(playerCollider.bounds);
                bool ignore = !blocksPlayers.Value || overlapping;
                int id = playerCollider.GetInstanceID();
                if (ignoredPlayerColliders.TryGetValue(id, out bool applied) && applied == ignore) continue;
                Physics.IgnoreCollision(policeBodyCollider, playerCollider, ignore);
                ignoredPlayerColliders[id] = ignore;
            }
        }

        /// <summary>현재 존재하는 플레이어와의 충돌 무시 상태를 일괄 변경합니다.</summary>
        private void SetAllPlayerCollisionPairsIgnored(bool ignore)
        {
            if (policeBodyCollider == null || NetworkManager == null) return;
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                Collider playerCollider = client?.PlayerObject != null
                    ? client.PlayerObject.GetComponent<Collider>()
                    : null;
                if (playerCollider != null) Physics.IgnoreCollision(policeBodyCollider, playerCollider, ignore);
            }
        }
    }
}
