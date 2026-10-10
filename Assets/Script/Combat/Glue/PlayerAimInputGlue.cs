using EchoZone.Combat.Aiming;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EchoZone.Combat.Glue
{
    /// <summary>로컬 마우스 입력·카메라 Raycast와 순수 조준 Brick을 연결하는 Glue입니다.</summary>
    public sealed class PlayerAimInputGlue : MonoBehaviour
    {
        /// <summary>마우스 화면 좌표를 월드 Ray로 변환할 로컬 플레이 카메라입니다.</summary>
        [SerializeField] private Camera gameplayCamera;
        /// <summary>조준 방향을 계산할 때 시작점으로 사용할 플레이어 Transform입니다.</summary>
        [SerializeField] private Transform aimOrigin;
        /// <summary>마우스 Ray가 조준점으로 인정할 지면 레이어입니다.</summary>
        [SerializeField] private LayerMask aimSurfaceMask = ~0;
        /// <summary>카메라에서 조준 지면을 탐색할 최대 Ray 거리입니다.</summary>
        [SerializeField] private float maximumRayDistance = 500f;
        /// <summary>좌클릭 발사 요청을 전달할 네트워크 발사 Glue입니다.</summary>
        [SerializeField] private NetworkWeaponFireGlue networkWeaponFireGlue;
        /// <summary>발사와 재장전 바인딩을 제공하는 원본 Input Action 에셋입니다.</summary>
        [SerializeField] private InputActionAsset inputActions;
        /// <summary>총기 입력이 들어 있는 액션 맵 이름입니다.</summary>
        [SerializeField] private string actionMapName = "Player";
        /// <summary>자동사격에 사용할 버튼 액션 이름입니다.</summary>
        [SerializeField] private string fireActionName = "Attack";
        /// <summary>수동 재장전에 사용할 버튼 액션 이름입니다.</summary>
        [SerializeField] private string reloadActionName = "Reload";

        /// <summary>Unity 입력을 모르는 순수 조준 방향 계산 Brick입니다.</summary>
        private readonly AimDirectionBrick aimDirectionBrick = new();
        /// <summary>현재 포인터의 화면 좌표를 읽는 로컬 Input Action입니다.</summary>
        private InputAction pointAction;
        /// <summary>좌클릭 발사 입력을 읽는 로컬 Input Action입니다.</summary>
        private InputAction fireAction;
        /// <summary>수동 재장전 입력을 읽는 로컬 Input Action입니다.</summary>
        private InputAction reloadAction;
        /// <summary>이 플레이어가 독립적으로 사용하도록 복제한 입력 에셋입니다.</summary>
        private InputActionAsset runtimeInputActions;
        /// <summary>마지막으로 Raycast에 성공한 월드 조준 지점입니다.</summary>
        private Vector3 currentAimPoint;
        /// <summary>현재 프레임에 사용할 수 있는 유효한 조준점이 있는지 나타냅니다.</summary>
        private bool hasAimPoint;

        /// <summary>마지막으로 계산된 유효한 수평 조준 방향입니다.</summary>
        public Vector3 CurrentAimDirection => aimDirectionBrick.CurrentAimDirection;
        /// <summary>현재 마우스 Ray가 유효한 조준 표면을 가리키는지 나타냅니다.</summary>
        public bool HasAimPoint => hasAimPoint;

        /// <summary>OnEnable 작업을 수행합니다.</summary>
        private void OnEnable()
        {
            pointAction = new InputAction("AimPoint", InputActionType.Value, "<Mouse>/position");
            if (inputActions != null)
            {
                runtimeInputActions = Instantiate(inputActions);
                InputActionMap actionMap = runtimeInputActions.FindActionMap(actionMapName, true);
                fireAction = actionMap.FindAction(fireActionName, true);
                reloadAction = actionMap.FindAction(reloadActionName, true);
            }

            pointAction.Enable();
            fireAction?.Enable();
            reloadAction?.Enable();

            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }
        }

        /// <summary>OnDisable 작업을 수행합니다.</summary>
        private void OnDisable()
        {
            pointAction?.Disable();
            pointAction?.Dispose();
            pointAction = null;

            fireAction?.Disable();
            fireAction = null;

            reloadAction?.Disable();
            reloadAction = null;

            if (runtimeInputActions != null)
            {
                Destroy(runtimeInputActions);
                runtimeInputActions = null;
            }
        }

        /// <summary>중앙 플레이어 업데이트 Glue가 입력 단계에서 호출할 조준·발사 진입점입니다.</summary>
        public void ManualUpdate(float deltaTime)
        {
            if (TryGetComponent<EchoZone.Equipment.PlayerEquipmentGlue>(out var equipment) && equipment.IsMenuOpen)
            {
                hasAimPoint = false;
                return;
            }
            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }

            if (gameplayCamera == null || aimOrigin == null || pointAction == null || fireAction == null)
            {
                return;
            }

            Vector2 mouseScreenPosition = pointAction.ReadValue<Vector2>();
            Ray ray = gameplayCamera.ScreenPointToRay(mouseScreenPosition);
            Plane aimPlane = new(Vector3.up, aimOrigin.position);
            hasAimPoint = aimPlane.Raycast(ray, out float aimDistance) &&
                          aimDistance <= maximumRayDistance;

            if (hasAimPoint)
            {
                currentAimPoint = ray.GetPoint(aimDistance);
                aimDirectionBrick.TryCalculateDirection(aimOrigin.position, currentAimPoint, out _);
            }

            bool overUi = UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (hasAimPoint && fireAction.IsPressed() && !overUi)
            {
                networkWeaponFireGlue?.RequestFire(aimDirectionBrick.CurrentAimDirection);
            }

            if (reloadAction?.WasPressedThisFrame() == true)
            {
                networkWeaponFireGlue?.RequestReload();
            }
        }
    }
}
