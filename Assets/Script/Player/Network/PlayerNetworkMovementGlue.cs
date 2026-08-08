using EchoZone.Player.Input;
using EchoZone.Player.Movement;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Player.Network
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerNetworkMovementGlue : NetworkBehaviour
    {
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private PlayerMovementMotor movementMotor;

        private Vector2 serverMoveInput;
        private Vector2 lastSubmittedInput;

        public override void OnNetworkSpawn()
        {
            RefreshInputAuthority();
        }

        public override void OnGainedOwnership()
        {
            RefreshInputAuthority();
        }

        public override void OnLostOwnership()
        {
            RefreshInputAuthority();
        }

        public override void OnNetworkDespawn()
        {
            inputReader?.SetInputEnabled(false);
            serverMoveInput = Vector2.zero;
        }

        private void FixedUpdate()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                SubmitInputWhenChanged();
            }

            if (IsServer)
            {
                movementMotor?.SimulateMovement(serverMoveInput, Time.fixedDeltaTime);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitMovementInputRpc(Vector2 input)
        {
            if (!IsFinite(input))
            {
                serverMoveInput = Vector2.zero;
                return;
            }

            serverMoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void Configure(
            PlayerInputReader targetInputReader,
            PlayerMovementMotor targetMovementMotor)
        {
            inputReader = targetInputReader;
            movementMotor = targetMovementMotor;
        }

        private void RefreshInputAuthority()
        {
            inputReader?.SetInputEnabled(IsOwner);

            if (!IsOwner)
            {
                lastSubmittedInput = Vector2.zero;
            }
        }

        private void SubmitInputWhenChanged()
        {
            Vector2 currentInput = inputReader != null
                ? Vector2.ClampMagnitude(inputReader.MoveInput, 1f)
                : Vector2.zero;

            if ((currentInput - lastSubmittedInput).sqrMagnitude <= 0.0001f)
            {
                return;
            }

            lastSubmittedInput = currentInput;
            SubmitMovementInputRpc(currentInput);
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsInfinity(value.y);
        }
    }
}
