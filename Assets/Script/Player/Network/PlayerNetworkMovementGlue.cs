using EchoZone.CameraSystem;
using EchoZone.Combat.Aiming;
using EchoZone.Combat.Glue;
using EchoZone.Combat.View;
using EchoZone.Player.Input;
using EchoZone.Player.Glue;
using EchoZone.Player.Movement;
using EchoZone.Player.View;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Player.Network
{
    /// <summary>
    /// 소유 클라이언트의 이동 입력을 서버에 전달하고 서버 권한으로 이동 시뮬레이션을 실행하는 Glue입니다.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerNetworkMovementGlue : NetworkBehaviour
    {
        /// <summary>로컬 소유 플레이어의 이동 입력을 제공하는 Brick입니다.</summary>
        [SerializeField] private PlayerInputReader inputReader;

        /// <summary>서버에서 실제 Rigidbody 이동을 수행하는 Brick입니다.</summary>
        [SerializeField] private PlayerMovementMotor movementMotor;

        /// <summary>로컬 입력을 화면 기준 월드 방향으로 바꿀 때 사용할 플레이 카메라입니다.</summary>
        [SerializeField] private Transform movementCamera;

        /// <summary>네트워크 위치 변화를 관찰해 걷기 연출만 갱신하는 캐릭터 View입니다.</summary>
        [SerializeField] private CharacterAnimatorView characterView;

        /// <summary>소유 플레이어의 마우스 조준과 발사 입력을 처리하는 Glue입니다.</summary>
        [SerializeField] private PlayerAimInputGlue aimInputGlue;

        /// <summary>모든 피어의 캐릭터 외형에 조준 방향을 적용하는 Glue입니다.</summary>
        [SerializeField] private CharacterAimPresentationGlue aimPresentationGlue;

        /// <summary>로컬 오너 위치를 씬의 건물 시스루 View에 전달하는 Glue입니다.</summary>
        [SerializeField] private PlayerSeeThroughViewGlue seeThroughViewGlue;

        /// <summary>서버 권한 총기 상태의 시간 진행을 담당하는 Glue입니다.</summary>
        [SerializeField] private NetworkWeaponFireGlue weaponFireGlue;

        /// <summary>Unity Camera와 네트워크를 모르는 방향 변환 Brick입니다.</summary>
        private readonly CameraRelativeMovementBrick cameraRelativeMovement = new();

        /// <summary>서버가 마지막으로 승인하여 시뮬레이션에 사용하는 이동 입력입니다.</summary>
        private Vector2 serverMoveInput;

        /// <summary>소유 클라이언트가 서버에 마지막으로 제출한 이동 입력입니다.</summary>
        private Vector2 lastSubmittedInput;

        /// <summary>로컬 플레이어만 연결되는 플레이 전용 카메라 Glue입니다.</summary>
        private GameplayCameraGlue gameplayCameraGlue;

        /// <summary>모든 피어에서 View 이동량을 계산하기 위한 직전 네트워크 위치입니다.</summary>
        private Vector3 previousPresentationPosition;

        /// <summary>서버가 승인해 모든 피어에 공유하는 플레이어 조준 방향입니다.</summary>
        private readonly NetworkVariable<Vector3> networkAimDirection = new(
            Vector3.forward,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        /// <summary>소유 클라이언트가 마지막으로 서버에 제출한 조준 방향입니다.</summary>
        private Vector3 lastSubmittedAimDirection = Vector3.forward;

        /// <summary>네트워크에 생성되면 현재 소유권에 맞춰 로컬 입력 활성화를 갱신합니다.</summary>
        public override void OnNetworkSpawn()
        {
            previousPresentationPosition = transform.position;
            RefreshInputAuthority();
        }

        /// <summary>이 클라이언트가 소유권을 얻으면 로컬 입력을 활성화합니다.</summary>
        public override void OnGainedOwnership()
        {
            RefreshInputAuthority();
        }

        /// <summary>이 클라이언트가 소유권을 잃으면 로컬 입력을 비활성화합니다.</summary>
        public override void OnLostOwnership()
        {
            RefreshInputAuthority();
        }

        /// <summary>네트워크에서 제거되면 입력을 비활성화하고 서버 입력 상태를 초기화합니다.</summary>
        public override void OnNetworkDespawn()
        {
            inputReader?.SetInputEnabled(false);
            if (aimInputGlue != null)
            {
                aimInputGlue.enabled = false;
            }

            gameplayCameraGlue?.UnbindFollowTarget(transform);
            serverMoveInput = Vector2.zero;
        }

        /// <summary>
        /// 소유 클라이언트에서는 입력 변경을 제출하고, 서버에서는 승인된 입력으로 이동을 실행합니다.
        /// </summary>
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

            UpdateCharacterPresentation();
        }

        /// <summary>입력, 네트워크 제출, 총기 시간, 조준 표시를 정해진 순서로 갱신합니다.</summary>
        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner && aimInputGlue != null)
            {
                aimInputGlue.ManualUpdate(Time.deltaTime);
                SubmitAimWhenChanged();
            }

            if (IsServer && weaponFireGlue != null)
            {
                weaponFireGlue.ManualUpdate((float)NetworkManager.ServerTime.TimeAsFloat);
            }

            Vector3 presentationDirection = IsOwner && aimInputGlue != null && aimInputGlue.HasAimPoint
                ? aimInputGlue.CurrentAimDirection
                : networkAimDirection.Value;
            aimPresentationGlue?.SetAimDirection(presentationDirection);
            aimPresentationGlue?.ManualUpdate(Time.deltaTime);
            seeThroughViewGlue?.ManualUpdate();
        }

        /// <summary>소유 클라이언트의 이동 입력을 서버로 보내 서버 측 입력 상태를 갱신합니다.</summary>
        /// <param name="input">소유 클라이언트가 제출한 2차원 이동 입력입니다.</param>
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

        /// <summary>소유 클라이언트의 유효한 조준 방향을 서버 권한 상태로 반영합니다.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitAimDirectionRpc(Vector3 direction)
        {
            if (!IsFinite(direction))
            {
                return;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                networkAimDirection.Value = direction.normalized;
            }
        }

        /// <summary>입력 Brick과 이동 Brick을 외부에서 연결합니다.</summary>
        /// <param name="targetInputReader">이동 입력을 제공할 입력 리더입니다.</param>
        /// <param name="targetMovementMotor">서버 이동을 수행할 모터입니다.</param>
        public void Configure(
            PlayerInputReader targetInputReader,
            PlayerMovementMotor targetMovementMotor)
        {
            inputReader = targetInputReader;
            movementMotor = targetMovementMotor;
        }

        /// <summary>현재 네트워크 소유권에 따라 입력 리더의 활성 상태를 갱신합니다.</summary>
        private void RefreshInputAuthority()
        {
            inputReader?.SetInputEnabled(IsOwner);
            if (aimInputGlue != null)
            {
                aimInputGlue.enabled = IsOwner;
            }

            if (IsOwner)
            {
                gameplayCameraGlue ??= FindFirstObjectByType<GameplayCameraGlue>();
                if (gameplayCameraGlue != null)
                {
                    gameplayCameraGlue.BindFollowTarget(transform);
                    movementCamera = gameplayCameraGlue.CameraTransform;
                }
                else if (movementCamera == null && Camera.main != null)
                {
                    movementCamera = Camera.main.transform;
                }
            }

            if (!IsOwner)
            {
                gameplayCameraGlue?.UnbindFollowTarget(transform);
                movementCamera = null;
                lastSubmittedInput = Vector2.zero;
            }
        }

        /// <summary>이동 입력이 이전 제출값과 달라졌을 때만 서버 RPC를 호출합니다.</summary>
        private void SubmitInputWhenChanged()
        {
            Vector2 currentInput = inputReader != null
                ? Vector2.ClampMagnitude(inputReader.MoveInput, 1f)
                : Vector2.zero;

            if (movementCamera != null)
            {
                currentInput = cameraRelativeMovement.ConvertToWorldInput(
                    currentInput,
                    movementCamera.forward,
                    movementCamera.right);
            }

            if ((currentInput - lastSubmittedInput).sqrMagnitude <= 0.0001f)
            {
                return;
            }

            lastSubmittedInput = currentInput;
            SubmitMovementInputRpc(currentInput);
        }

        /// <summary>벡터의 두 축이 전송 가능한 유한한 숫자인지 검사합니다.</summary>
        /// <param name="value">검사할 2차원 값입니다.</param>
        /// <returns>NaN 또는 무한대가 포함되지 않았으면 <see langword="true"/>입니다.</returns>
        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsInfinity(value.y);
        }

        /// <summary>벡터의 세 축이 전송 가능한 유한한 숫자인지 검사합니다.</summary>
        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        /// <summary>조준 방향이 변경됐을 때만 서버에 새 방향을 제출합니다.</summary>
        private void SubmitAimWhenChanged()
        {
            if (!aimInputGlue.HasAimPoint)
            {
                return;
            }

            Vector3 direction = aimInputGlue.CurrentAimDirection;
            if ((direction - lastSubmittedAimDirection).sqrMagnitude <= 0.0001f)
            {
                return;
            }

            lastSubmittedAimDirection = direction;
            SubmitAimDirectionRpc(direction);
        }

        /// <summary>동기화된 위치 변화만 읽어 캐릭터 걷기와 바라보는 방향을 갱신합니다.</summary>
        private void UpdateCharacterPresentation()
        {
            if (characterView == null)
            {
                return;
            }

            Vector3 delta = transform.position - previousPresentationPosition;
            previousPresentationPosition = transform.position;
            characterView.UpdateMovementAnimation(new Vector2(delta.x, delta.z));
        }
    }
}
