using UnityEngine;

namespace EchoZone.Combat.Weapon
{
    /// <summary>Unity 오브젝트와 네트워크를 모르는 발사 가능 여부·탄약 규칙 Brick입니다.</summary>
    public sealed class WeaponFireBrick
    {
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

        public int Ammunition => ammunition;
        public bool IsReloading => isReloading;

        public void Configure(WeaponFireConfig weaponConfig)
        {
            config = weaponConfig;
            ammunition = config != null ? config.MagazineCapacity : 0;
            nextFireTime = 0f;
            lastFireTime = 0f;
            isReloading = false;
            hasFiredSinceReload = false;
        }

        public bool CanFire(float currentTime)
        {
            return config != null &&
                   !isReloading &&
                   ammunition > 0 &&
                   currentTime >= nextFireTime;
        }

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

        public bool ShouldAutoReload(float currentTime)
        {
            if (config == null || isReloading || !hasFiredSinceReload || ammunition >= config.MagazineCapacity)
            {
                return false;
            }

            return ammunition <= 0 || currentTime >= lastFireTime + config.AutoReloadDelaySeconds;
        }

        public bool IsReloadComplete(float currentTime)
        {
            return isReloading && currentTime >= reloadCompleteTime;
        }

        public int GetRequiredAmmunition()
        {
            return config == null ? 0 : Mathf.Max(0, config.MagazineCapacity - ammunition);
        }

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
