using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoZone.Player.Input
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";

        private InputActionAsset runtimeInputActions;
        private InputAction moveAction;

        public Vector2 MoveInput { get; private set; }

        private void Awake()
        {
            SetInputEnabled(false);
        }

        private void Update()
        {
            MoveInput = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        }

        private void OnDestroy()
        {
            if (runtimeInputActions != null)
            {
                Destroy(runtimeInputActions);
            }
        }

        public void SetInputEnabled(bool inputEnabled)
        {
            if (inputEnabled)
            {
                EnsureRuntimeActions();

                if (moveAction == null)
                {
                    enabled = false;
                    return;
                }

                moveAction.Enable();
                enabled = true;
                return;
            }

            if (moveAction != null)
            {
                moveAction.Disable();
            }

            MoveInput = Vector2.zero;
            enabled = false;
        }

        public void Configure(
            InputActionAsset sourceInputActions,
            string sourceActionMapName,
            string sourceMoveActionName)
        {
            inputActions = sourceInputActions;
            actionMapName = sourceActionMapName;
            moveActionName = sourceMoveActionName;
        }

        private void EnsureRuntimeActions()
        {
            if (moveAction != null)
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
        }
    }
}
