using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Online.Reconnect
{
    /// <summary>서버가 생성한 재접속 티켓을 해당 PlayerObject의 Owner에게만 전달하는 Glue입니다.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerReconnectTicketBridge : NetworkBehaviour
    {
        /// <summary>networkTicket 값을 저장합니다.</summary>
        private readonly NetworkVariable<FixedString128Bytes> networkTicket = new(
            default,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server);

        /// <summary>Spawn 시 티켓 변경을 구독하고 이미 받은 초기값을 Client 메모리에 적용합니다.</summary>
        public override void OnNetworkSpawn()
        {
            networkTicket.OnValueChanged += HandleTicketChanged;

            if (IsOwner && !networkTicket.Value.IsEmpty)
            {
                ReconnectTicketMemoryStore.Replace(networkTicket.Value.ToString());
            }
        }

        /// <summary>Despawn 시 티켓 변경 구독을 해제하며 재접속에 필요하므로 메모리 티켓은 유지합니다.</summary>
        public override void OnNetworkDespawn()
        {
            networkTicket.OnValueChanged -= HandleTicketChanged;
        }

        /// <summary>서버에서 다음 재접속에 사용할 새 티켓을 설정합니다.</summary>
        /// <param name="ticket">암호학적 난수로 생성된 새 티켓입니다.</param>
        public void AssignTicket(string ticket)
        {
            if (!IsServer || string.IsNullOrEmpty(ticket))
            {
                return;
            }

            networkTicket.Value = ticket;
        }

        /// <summary>서버 티켓이 변경되면 Owner Client의 메모리 티켓을 교체합니다.</summary>
        private void HandleTicketChanged(
            FixedString128Bytes previousTicket,
            FixedString128Bytes currentTicket)
        {
            if (!IsOwner || currentTicket.IsEmpty)
            {
                return;
            }

            ReconnectTicketMemoryStore.Replace(currentTicket.ToString());
        }
    }
}
