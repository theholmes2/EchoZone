using EchoZone.Combat.Aiming;
using EchoZone.Combat.Weapon;
using System;
using Unity.Netcode;
using UnityEngine;


namespace EchoZone.Combat.Glue
{
    /// <summary>소유 Client의 발사 요청과 서버 권한 발사 규칙·투사체 생성을 연결하는 Glue입니다.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkWeaponFireGlue : NetworkBehaviour
    {
        /// <summary>서버 발사 검증과 투사체 생성에 사용할 총기 데이터입니다.</summary>
        [SerializeField] private WeaponFireConfig config;
        /// <summary>승인된 투사체가 생성될 오른손 앞 총구 위치입니다.</summary>
        [SerializeField] private Transform muzzle;
        /// <summary>이동 사격 여부를 서버에서 판정할 플레이어 물리 본체입니다.</summary>
        [SerializeField] private Rigidbody playerBody;
        /// <summary>서버가 예비 탄약 수량을 조회하고 실제 장전량만 차감할 인벤토리입니다.</summary>
        [SerializeField] private PlayerInventory playerInventory;

        /// <summary>서버가 승인한 발사를 받은 각 Client에서 발생하는 로컬 연출 이벤트입니다.</summary>
        public event Action OnFireClientNotified;

        /// <summary>탄약·쿨타임·재장전 규칙을 담당하는 순수 Brick입니다.</summary>
        private readonly WeaponFireBrick weaponFireBrick = new();
        /// <summary>서버가 탄퍼짐을 계산할 때 사용하는 순수 Brick입니다.</summary>
        private readonly AimDirectionBrick aimDirectionBrick = new();
        /// <summary>자동사격 중 불필요한 매 프레임 RPC를 막는 다음 로컬 요청 시각입니다.</summary>
        private float nextLocalFireRequestTime;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                weaponFireBrick.Configure(config);
            }
        }

        /// <summary>로컬 입력 Glue가 호출하는 발사 요청 진입점입니다.</summary>
        public void RequestFire(Vector3 requestedDirection)
        {
            if (!IsOwner || config == null || requestedDirection.sqrMagnitude < 0.0001f ||
                Time.unscaledTime < nextLocalFireRequestTime)
            {
                return;
            }

            nextLocalFireRequestTime = Time.unscaledTime + config.FireIntervalSeconds;
            RequestFireRpc(requestedDirection);
        }

        /// <summary>소유 Client가 수동 재장전을 서버에 요청합니다.</summary>
        public void RequestReload()
        {
            if (IsOwner)
            {
                RequestReloadRpc();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestReloadRpc()
        {
            TryBeginServerReload((float)NetworkManager.ServerTime.TimeAsFloat);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void RequestFireRpc(Vector3 requestedDirection)
        {
            if (!IsServer || config == null || config.ProjectilePrefab == null ||
                muzzle == null || !IsFinite(requestedDirection))
            {
                return;
            }

            float currentServerTime = (float)NetworkManager.ServerTime.TimeAsFloat;
            if (!weaponFireBrick.CanFire(currentServerTime))
            {
                return;
            }

            float currentSpeed = playerBody != null ? playerBody.linearVelocity.magnitude : 0f;
            float spreadDegrees = weaponFireBrick.SelectSpread(currentSpeed);
            float randomValue = UnityEngine.Random.Range(-1f, 1f);
            Vector3 finalDirection = aimDirectionBrick.ApplySpread(
                requestedDirection.normalized,
                spreadDegrees,
                randomValue);

            GameObject projectileObject = Instantiate(
                config.ProjectilePrefab,
                muzzle.position,
                Quaternion.LookRotation(finalDirection, Vector3.up));

            if (!projectileObject.TryGetComponent(out ProjectileNetworkGlue projectileGlue) ||
                !projectileObject.TryGetComponent(out NetworkObject networkObject))
            {
                Destroy(projectileObject);
                return;
            }

            networkObject.Spawn(true);
            projectileGlue.InitializeServer(muzzle.position, finalDirection);
            weaponFireBrick.CommitFire(currentServerTime);
            NotifyFireClientRpc();

            if (weaponFireBrick.Ammunition <= 0)
            {
                TryBeginServerReload(currentServerTime);
            }
        }

        /// <summary>서버의 재장전과 총기 시간 상태를 갱신합니다.</summary>
        public void ManualUpdate(float serverTime)
        {
            if (!IsServer || config == null || playerInventory == null)
            {
                return;
            }

            if (weaponFireBrick.IsReloadComplete(serverTime))
            {
                int requested = weaponFireBrick.GetRequiredAmmunition();
                int loaded = playerInventory.RemoveUpToQuantity(config.AmmunitionItem, requested);
                weaponFireBrick.CompleteReload(loaded);
                return;
            }

            if (weaponFireBrick.ShouldAutoReload(serverTime))
            {
                TryBeginServerReload(serverTime);
            }
        }

        private void TryBeginServerReload(float serverTime)
        {
            if (!IsServer || config == null || playerInventory == null || config.AmmunitionItem == null)
            {
                return;
            }

            int availableReserve = playerInventory.GetTotalQuantity(config.AmmunitionItem);
            weaponFireBrick.TryBeginReload(serverTime, availableReserve);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }


        /// <summary>서버가 승인한 발사 사실을 모든 Client의 해당 플레이어 인스턴스에 전달합니다.</summary>
        [ClientRpc]
        private void NotifyFireClientRpc()
        {
            OnFireClientNotified?.Invoke();
        }

    }
}
