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
        /// <summary>탈출이 승인된 플레이어는 새 사격·재장전을 실행할 수 없습니다.</summary>
        private bool ExtractionBlocked => EchoZone.Online.Migration.SessionWorldMigrationGlue.IsRestoring ||
            (TryGetComponent<EchoZone.Equipment.PlayerEquipmentGlue>(out var equipment) &&
                (!equipment.HasWeapon || (IsServer ? equipment.ServerMenuOpen : equipment.IsMenuOpen))) ||
            (TryGetComponent<EchoZone.Heist.PlayerWalletGlue>(out var wallet) && wallet.IsEscaping);
        /// <summary>서버 발사 검증과 투사체 생성에 사용할 총기 데이터입니다.</summary>
        [SerializeField] private WeaponFireConfig config;
        /// <summary>Config에 지정한 무기 외형을 자식으로 배치할 손의 무기 소켓입니다.</summary>
        [SerializeField] private Transform weaponSocket;
        /// <summary>승인된 투사체가 생성될 오른손 앞 총구 위치입니다.</summary>
        [SerializeField] private Transform muzzle;
        /// <summary>이동 사격 여부를 서버에서 판정할 플레이어 물리 본체입니다.</summary>
        [SerializeField] private Rigidbody playerBody;
        /// <summary>서버가 예비 탄약 수량을 조회하고 실제 장전량만 차감할 인벤토리입니다.</summary>
        [SerializeField] private PlayerInventory playerInventory;

        /// <summary>서버가 승인한 발사를 받은 각 Client에서 발생하는 로컬 연출 이벤트입니다.</summary>
        public event Action OnFireClientNotified;

        /// <summary>현재 장착한 총기의 연속 발사 간격입니다.</summary>
        public float FireIntervalSeconds => config != null ? config.FireIntervalSeconds : 0f;
        /// <summary>에너미가 한 점사 묶음에서 발사할 탄 수입니다.</summary>
        public int EnemyBurstShotCount => config != null ? config.EnemyBurstShotCount : 1;
        /// <summary>에너미의 점사 묶음 사이 대기시간입니다.</summary>
        public float EnemyBurstPauseSeconds => config != null ? config.EnemyBurstPauseSeconds : 0f;
        /// <summary>투사체가 실제로 생성되는 현재 총구의 월드 위치입니다.</summary>
        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position;

        /// <summary>탄약·쿨타임·재장전 규칙을 담당하는 순수 Brick입니다.</summary>
        private readonly WeaponFireBrick weaponFireBrick = new();
        /// <summary>후발 접속자에게도 정확한 무기 외형을 전달하는 서버 권위 종류 ID입니다.</summary>
        private readonly NetworkVariable<Unity.Collections.FixedString128Bytes> equippedDefinition = new();
        /// <summary>Snapshot에 저장할 종류 ID입니다. 오브젝트 이름은 사용하지 않습니다.</summary>
        public string DefinitionId => RecoveryCatalog.Load().WeaponId(config);
        /// <summary>카탈로그 설정을 먼저 복원한 뒤 탄약/잔여시간을 적용합니다.</summary>
        public void RestoreDefinition(string id, string json, int version, float now)
        {
            if (!IsServer) return;
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Weapon state missing.");
            var catalog = RecoveryCatalog.Load(); catalog.RequireVersion(version);
            ConfigureWeapon(catalog.Weapon(id).Config);
            weaponFireBrick.Restore(json, now);
        }
        /// <summary>서버 총기 규칙 복사본을 반환합니다.</summary>
        public string CaptureMigrationWeapon(float now) => weaponFireBrick.Export(now);
        /// <summary>서버 장비 교환이 저장할 현재 탄창 잔량입니다.</summary>
        public int ServerMagazineRounds => weaponFireBrick.Ammunition;
        /// <summary>현재 무기 정의를 장비 카탈로그와 연결합니다.</summary>
        public WeaponFireConfig CurrentConfig => config;
        /// <summary>장비 교환은 기존 탄창을 복원하며 무료 재장전을 발생시키지 않습니다.</summary>
        public void EquipMagazineServer(WeaponFireConfig definition, int rounds)
        {
            if (!IsServer || !IsSpawned) return;
            ConfigureWeapon(definition);
            weaponFireBrick.RestoreMagazine(rounds < 0 ? definition.MagazineCapacity : rounds);
            weaponFireBrick.DelayAfterEquip((float)NetworkManager.ServerTime.Time);
        }
        /// <summary>서버가 탄퍼짐을 계산할 때 사용하는 순수 Brick입니다.</summary>
        private readonly AimDirectionBrick aimDirectionBrick = new();
        /// <summary>자동사격 중 불필요한 매 프레임 RPC를 막는 다음 로컬 요청 시각입니다.</summary>
        private float nextLocalFireRequestTime;
        /// <summary>현재 WeaponSocket 아래에 생성한 무기 외형입니다.</summary>
        private GameObject spawnedWeaponVisual;
        /// <summary>미장착 상태에서도 마지막 총기 정의는 복구용으로 보존하고 외형만 숨깁니다.</summary>
        private bool equipmentVisible = true;
        /// <summary>장착 슬롯 상태에 맞춰 총기 외형을 표시합니다.</summary>
        public void SetEquipmentVisible(bool visible)
        {
            equipmentVisible = visible;
            if (spawnedWeaponVisual != null) spawnedWeaponVisual.SetActive(visible);
        }
        /// <summary>발사·장전 승인 시 사망 여부를 확인할 체력 데이터입니다.</summary>
        private PlayerStats stats;

        /// <summary>Config의 무기 프리팹을 WeaponSocket 원점에 배치합니다.</summary>
        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            if (weaponSocket == null)
            {
                weaponSocket = FindChildByName(transform, "WeaponSocket");
            }

            ApplyWeaponPresentation();
        }

        /// <summary>현재 Config의 무기 외형으로 WeaponSocket 표시를 다시 구성합니다.</summary>
        public void ApplyWeaponPresentation()
        {
            if (weaponSocket == null || config == null || config.WeaponPrefab == null)
            {
                return;
            }

            for (int i = weaponSocket.childCount - 1; i >= 0; i--)
            {
                Transform child = weaponSocket.GetChild(i);
                if (child != null && child.name != "Muzzle")
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }

            spawnedWeaponVisual = Instantiate(config.WeaponPrefab, weaponSocket);
            spawnedWeaponVisual.name = config.WeaponPrefab.name;
            spawnedWeaponVisual.transform.localPosition = Vector3.zero;
            spawnedWeaponVisual.transform.localRotation = Quaternion.identity;
            spawnedWeaponVisual.SetActive(equipmentVisible);

            if (muzzle != null && muzzle.parent == weaponSocket)
            {
                muzzle.localPosition = config.MuzzleLocalPosition;
                muzzle.localRotation = Quaternion.identity;
            }
        }

        /// <summary>장착 총기 Config를 교체하고 발사 규칙과 외형을 함께 다시 구성합니다.</summary>
        public void ConfigureWeapon(WeaponFireConfig weaponConfig)
        {
            if (weaponConfig == null) throw new InvalidOperationException("Weapon config missing.");
            string definition = RecoveryCatalog.Load().WeaponId(weaponConfig);
            if (IsServer && IsSpawned) equippedDefinition.Value = new Unity.Collections.FixedString128Bytes(definition);
            if (weaponConfig == null || config == weaponConfig)
            {
                return;
            }

            config = weaponConfig;
            if (IsServer)
            {
                weaponFireBrick.Configure(config);
            }

            ApplyWeaponPresentation();
        }

        /// <summary>OnNetworkSpawn 작업을 수행합니다.</summary>
        public override void OnNetworkSpawn()
        {
            equippedDefinition.OnValueChanged += HandleDefinitionChanged;
            nextLocalFireRequestTime = 0f;
            if (IsServer)
            {
                equippedDefinition.Value = new Unity.Collections.FixedString128Bytes(DefinitionId);
                weaponFireBrick.Configure(config);
            }
            else if (!equippedDefinition.Value.IsEmpty) HandleDefinitionChanged(default, equippedDefinition.Value);
        }

        /// <summary>풀 재사용/디스폰 때 종류 변경 구독을 해제합니다.</summary>
        public override void OnNetworkDespawn() => equippedDefinition.OnValueChanged -= HandleDefinitionChanged;

        /// <summary>클라이언트는 서버 종류 ID로 외형과 로컬 요청 간격을 맞춥니다.</summary>
        private void HandleDefinitionChanged(Unity.Collections.FixedString128Bytes previous, Unity.Collections.FixedString128Bytes current)
        {
            if (!IsServer && !current.IsEmpty) ConfigureWeapon(RecoveryCatalog.Load().Weapon(current.ToString()).Config);
        }

        /// <summary>서버 부활 시 장착 총은 유지하고 탄창·쿨타임·재장전을 초기화합니다.</summary>
        public void ResetServerForRespawn()
        {
            if (!IsServer || !IsSpawned) return;
            weaponFireBrick.Configure(config);
            nextLocalFireRequestTime = 0f;
        }

        /// <summary>로컬 입력 Glue가 호출하는 발사 요청 진입점입니다.</summary>
        public void RequestFire(Vector3 requestedDirection)
        {
            if (ExtractionBlocked || (stats != null && stats.IsDead) || (!IsOwner && !IsServer) || config == null || requestedDirection.sqrMagnitude < 0.0001f ||
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
        /// <summary>RequestReloadRpc 작업을 수행합니다.</summary>
        private void RequestReloadRpc()
        {
            TryBeginServerReload((float)NetworkManager.ServerTime.TimeAsFloat);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        /// <summary>RequestFireRpc 작업을 수행합니다.</summary>
        private void RequestFireRpc(Vector3 requestedDirection)
        {
            if (ExtractionBlocked || (stats != null && stats.IsDead) || !IsServer || config == null || config.ProjectilePrefab == null ||
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

            var pool = ProjectileNetworkPoolGlue.Instance;
            GameObject projectileObject;
            if (pool != null && pool.Supports(config.ProjectilePrefab))
            {
                NetworkObject rented = pool.Rent(muzzle.position, Quaternion.LookRotation(finalDirection, Vector3.up));
                if (rented == null) return;
                projectileObject = rented.gameObject;
            }
            else
            {
                // 풀이 배치되지 않은 기존 테스트 씬과 다른 종류의 투사체는 종전 생성 경로를 유지합니다.
                projectileObject = Instantiate(config.ProjectilePrefab, muzzle.position, Quaternion.LookRotation(finalDirection, Vector3.up));
            }

            if (!projectileObject.TryGetComponent(out ProjectileNetworkGlue projectileGlue) ||
                !projectileObject.TryGetComponent(out NetworkObject networkObject))
            {
                Destroy(projectileObject);
                return;
            }

            networkObject.Spawn(true);
            projectileGlue.InitializeServer(muzzle.position, finalDirection, config.Damage, NetworkObject);
            weaponFireBrick.CommitFire(currentServerTime);
            NotifyFireClientRpc(muzzle.position, NetworkObject.IsPlayerObject);

            if (weaponFireBrick.Ammunition <= 0)
            {
                TryBeginServerReload(currentServerTime);
            }
        }

        /// <summary>서버의 재장전과 총기 시간 상태를 갱신합니다.</summary>
        public void ManualUpdate(float serverTime)
        {
            if (ExtractionBlocked || (stats != null && stats.IsDead) || !IsServer || config == null ||
                (!config.InfiniteReserveAmmunition && playerInventory == null))
            {
                return;
            }

            if (weaponFireBrick.IsReloadComplete(serverTime))
            {
                int requested = weaponFireBrick.GetRequiredAmmunition();
                int loaded = config.InfiniteReserveAmmunition
                    ? requested
                    : playerInventory.RemoveUpToQuantity(config.AmmunitionItem, requested);
                weaponFireBrick.CompleteReload(loaded);
                return;
            }

            if (weaponFireBrick.ShouldAutoReload(serverTime))
            {
                TryBeginServerReload(serverTime);
            }
        }

        /// <summary>TryBeginServerReload 작업을 수행합니다.</summary>
        private void TryBeginServerReload(float serverTime)
        {
            if (ExtractionBlocked || (stats != null && stats.IsDead) || !IsServer || config == null)
            {
                return;
            }

            if (config.InfiniteReserveAmmunition)
            {
                weaponFireBrick.TryBeginReload(serverTime, config.MagazineCapacity);
                return;
            }

            if (playerInventory == null || config.AmmunitionItem == null)
            {
                return;
            }

            int availableReserve = playerInventory.GetTotalQuantity(config.AmmunitionItem);
            weaponFireBrick.TryBeginReload(serverTime, availableReserve);
        }

        /// <summary>IsFinite 작업을 수행합니다.</summary>
        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        /// <summary>지정한 계층에서 이름이 일치하는 첫 Transform을 찾습니다.</summary>
        private static Transform FindChildByName(Transform root, string childName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform found = FindChildByName(child, childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }


        /// <summary>서버가 승인한 발사 사실을 모든 Client의 해당 플레이어 인스턴스에 전달합니다.</summary>
        [ClientRpc]
        private void NotifyFireClientRpc(Vector3 shotPosition, bool playerShot)
        {
            EchoZone.Audio.GameplaySoundGlue.PlayWorld(playerShot ? EchoZone.Audio.GameplaySoundId.PlayerShot : EchoZone.Audio.GameplaySoundId.PoliceShot, shotPosition);
            OnFireClientNotified?.Invoke();
        }

    }
}
