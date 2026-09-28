using UnityEngine;

namespace EchoZone.Combat.Weapon
{
    /// <summary>Unity 오브젝트와 네트워크를 모르는 발사 가능 여부·탄약 규칙 Brick입니다.</summary>
    public sealed class WeaponFireBrick
    {
        /// <summary>탄창과 재장전·발사 대기를 남은 시간으로 내보냅니다.</summary>
        public string Export(float now) => JsonUtility.ToJson(new SavedState { ammo = ammunition, reloading = isReloading,
            reload = Mathf.Max(0, reloadCompleteTime - now), fire = Mathf.Max(0, nextFireTime - now),
            sinceFire = Mathf.Max(0, now - lastFireTime), fired = hasFiredSinceReload });
        /// <summary>새 호스트의 게임 시각에 탄약 규칙을 복원합니다.</summary>
        public void Restore(string json, float now)
        {
            if (string.IsNullOrEmpty(json) || config == null) return;
            var s = JsonUtility.FromJson<SavedState>(json);
            ammunition = Mathf.Clamp(s.ammo, 0, config.MagazineCapacity); isReloading = s.reloading;
            reloadCompleteTime = now + s.reload; nextFireTime = now + s.fire;
            lastFireTime = now - s.sinceFire; hasFiredSinceReload = s.fired;
        }
        /// <summary>총기 복구용 순수 직렬화 데이터입니다.</summary>
        [System.Serializable] private sealed class SavedState { public int ammo; public bool reloading, fired; public float reload, fire, sinceFire; }
        /// <summary>발사와 재장전 판정에 사용할 기획 데이터입니다.</summary>
        private WeaponFireConfig config;
        /// <summary>현재 탄창에 남아 있는 탄약 수입니다.</summary>
        private int ammunition;
        /// <summary>다음 발사를 허용할 게임 시각입니다.</summary>
        private float nextFireTime;
        /// <summary>가장 최근 서버 승인 발사가 발생한 게임 시각입니다.</summary>
        private float lastFireTime;
        /// <summary>현재 재장전 과정이 진행 중인지 나타냅니다.</summary>
        private bool isReloading;
        /// <summary>진행 중인 재장전이 완료될 게임 시각입니다.</summary>
        private float reloadCompleteTime;
        /// <summary>현재 탄창에서 한 발 이상 사용해 자동 재장전 대상이 되었는지 나타냅니다.</summary>
        private bool hasFiredSinceReload;

        /// <summary>현재 탄창에 남아 있는 탄약 수입니다.</summary>
        public int Ammunition => ammunition;
        /// <summary>현재 재장전 과정이 진행 중인지 나타냅니다.</summary>
        public bool IsReloading => isReloading;

        /// <summary>Configure 작업을 수행합니다.</summary>
        public void Configure(WeaponFireConfig weaponConfig)
        {
            config = weaponConfig;
            ammunition = config != null ? config.MagazineCapacity : 0;
            nextFireTime = 0f;
            lastFireTime = 0f;
            isReloading = false;
            hasFiredSinceReload = false;
        }

        /// <summary>CanFire 작업을 수행합니다.</summary>
        public bool CanFire(float currentTime)
        {
            return config != null &&
                   !isReloading &&
                   ammunition > 0 &&
                   currentTime >= nextFireTime;
        }

        /// <summary>CommitFire 작업을 수행합니다.</summary>
        public void CommitFire(float currentTime)
        {
            if (config == null)
            {
                return;
            }

            ammunition = Mathf.Max(0, ammunition - 1);
            nextFireTime = currentTime + config.FireIntervalSeconds;
            lastFireTime = currentTime;
            hasFiredSinceReload = true;
        }

        /// <summary>TryBeginReload 작업을 수행합니다.</summary>
        public bool TryBeginReload(float currentTime, int availableReserve)
        {
            if (config == null || isReloading || ammunition >= config.MagazineCapacity || availableReserve <= 0)
            {
                return false;
            }

            isReloading = true;
            reloadCompleteTime = currentTime + config.ReloadSeconds;
            return true;
        }

        /// <summary>ShouldAutoReload 작업을 수행합니다.</summary>
        public bool ShouldAutoReload(float currentTime)
        {
            if (config == null || isReloading || !hasFiredSinceReload || ammunition >= config.MagazineCapacity)
            {
                return false;
            }

            return ammunition <= 0 || currentTime >= lastFireTime + config.AutoReloadDelaySeconds;
        }

        /// <summary>IsReloadComplete 작업을 수행합니다.</summary>
        public bool IsReloadComplete(float currentTime)
        {
            return isReloading && currentTime >= reloadCompleteTime;
        }

        /// <summary>GetRequiredAmmunition 작업을 수행합니다.</summary>
        public int GetRequiredAmmunition()
        {
            return config == null ? 0 : Mathf.Max(0, config.MagazineCapacity - ammunition);
        }

        /// <summary>CompleteReload 작업을 수행합니다.</summary>
        public void CompleteReload(int loadedAmmunition)
        {
            if (!isReloading || config == null)
            {
                return;
            }

            ammunition = Mathf.Min(config.MagazineCapacity, ammunition + Mathf.Max(0, loadedAmmunition));
            isReloading = false;
            hasFiredSinceReload = false;
        }

        /// <summary>SelectSpread 작업을 수행합니다.</summary>
        public float SelectSpread(float movementMagnitude)
        {
            if (config == null)
            {
                return 0f;
            }

            return movementMagnitude > config.MovingThreshold
                ? config.MovingSpreadDegrees
                : config.StationarySpreadDegrees;
        }
    }
}
