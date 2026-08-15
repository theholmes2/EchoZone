using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoZone.Player.Input
{
    /// <summary>
    /// Unity Input System에서 로컬 플레이어의 입력값을 읽어 다른 시스템에 제공합니다.
    /// 입력값을 소비하는 이동 및 상호작용 로직은 직접 실행하지 않습니다.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        /// <summary>입력 액션과 액션 맵이 정의된 원본 Input Action 에셋입니다.</summary>
        [SerializeField] private InputActionAsset inputActions;

        /// <summary>플레이어 입력 액션이 들어 있는 액션 맵 이름입니다.</summary>
        [SerializeField] private string actionMapName = "Player";

        /// <summary>이동 입력에 사용할 액션 이름입니다.</summary>
        [SerializeField] private string moveActionName = "Move";

        /// <summary>상호작용 입력에 사용할 액션 이름입니다.</summary>
        [SerializeField] private string interactActionName = "Interact";

        /// <summary>이 플레이어가 독립적으로 사용하도록 원본에서 복제한 입력 에셋입니다.</summary>
        private InputActionAsset runtimeInputActions;

        /// <summary>복제된 입력 에셋에서 찾은 이동 액션입니다.</summary>
        private InputAction moveAction;

        /// <summary>복제된 입력 에셋에서 찾은 상호작용 액션입니다.</summary>
        private InputAction interactAction;

        /// <summary>현재 프레임에 읽은 2차원 이동 입력값입니다.</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>현재 프레임에 상호작용 버튼을 누르기 시작했는지 나타냅니다.</summary>
        public bool InteractPressedThisFrame { get; private set; }

        /// <summary>소유권이 확인되기 전에는 입력을 읽지 않도록 비활성화합니다.</summary>
        private void Awake()
        {
            SetInputEnabled(false);
        }

        /// <summary>활성화된 이동 및 상호작용 액션의 현재 값을 매 프레임 읽습니다.</summary>
        private void Update()
        {
            MoveInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            InteractPressedThisFrame = interactAction?.WasPressedThisFrame() ?? false;
        }

        /// <summary>이 플레이어용으로 복제했던 입력 에셋을 제거합니다.</summary>
        private void OnDestroy()
        {
            if (runtimeInputActions != null)
            {
                Destroy(runtimeInputActions);
            }
        }

        /// <summary>이 컴포넌트가 로컬 입력을 읽을 수 있는지 설정합니다.</summary>
        /// <param name="inputEnabled">입력을 활성화하려면 <see langword="true"/>입니다.</param>
        public void SetInputEnabled(bool inputEnabled)
        {
            if (inputEnabled)
            {
                EnsureRuntimeActions();

                if (moveAction == null || interactAction == null)
                {
                    enabled = false;
                    return;
                }

                moveAction.Enable();
                interactAction.Enable();
                enabled = true;
                return;
            }

            if (moveAction != null)
            {
                moveAction.Disable();
            }

            if (interactAction != null)
            {
                interactAction.Disable();
            }

            MoveInput = Vector2.zero;
            InteractPressedThisFrame = false;
            enabled = false;
        }

        /// <summary>사용할 Input Action 에셋과 액션 이름을 외부에서 설정합니다.</summary>
        /// <param name="sourceInputActions">복제하여 사용할 원본 Input Action 에셋입니다.</param>
        /// <param name="sourceActionMapName">플레이어 입력 액션 맵 이름입니다.</param>
        /// <param name="sourceMoveActionName">이동 액션 이름입니다.</param>
        public void Configure(
            InputActionAsset sourceInputActions,
            string sourceActionMapName,
            string sourceMoveActionName)
        {
            inputActions = sourceInputActions;
            actionMapName = sourceActionMapName;
            moveActionName = sourceMoveActionName;
        }

        /// <summary>아직 준비되지 않았다면 입력 에셋을 복제하고 이동 및 상호작용 액션을 찾습니다.</summary>
        private void EnsureRuntimeActions()
        {
            if (moveAction != null && interactAction != null)
            {
                return;
            }

            if (inputActions == null)
            {
                Debug.LogError("PlayerInputReader requires an InputActionAsset.", this);
                return;
            }

            runtimeInputActions = Instantiate(inputActions);
            InputActionMap actionMap = runtimeInputActions.FindActionMap(actionMapName, true);
            moveAction = actionMap.FindAction(moveActionName, true);
            interactAction = actionMap.FindAction(interactActionName, true);
        }
    }
}
