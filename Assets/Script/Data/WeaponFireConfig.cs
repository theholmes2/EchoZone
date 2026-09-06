using UnityEngine;

namespace EchoZone.Combat
{
    /// <summary>총기 발사, 조준, 투사체 계산에 필요한 기획 수치를 보관하는 데이터입니다.</summary>
    [CreateAssetMenu(fileName = "WeaponFireConfig", menuName = "EchoZone/Combat/Weapon Fire Config")]
    public sealed class WeaponFireConfig : ScriptableObject
    {
        [Header("Fire")]
        /// <summary>연속된 두 발사 사이에 반드시 지나야 하는 최소 시간입니다.</summary>
        [SerializeField, Min(0.01f)] private float fireIntervalSeconds = 0.2f;
        /// <summary>한 탄창에 들어가는 최대 탄약 수입니다.</summary>
        [SerializeField, Min(0)] private int magazineCapacity = 12;
        /// <summary>재장전 시작부터 탄창이 채워질 때까지 걸리는 시간입니다.</summary>
        [SerializeField, Min(0f)] private float reloadSeconds = 1.5f;
        /// <summary>마지막 발사 후 자동 재장전을 시작하기까지 기다리는 시간입니다.</summary>
        [SerializeField, Min(0f)] private float autoReloadDelaySeconds = 2f;

        [Header("Projectile")]
        /// <summary>투사체가 초당 이동하는 월드 거리입니다.</summary>
        [SerializeField, Min(0f)] private float projectileSpeed = 24f;
        /// <summary>충돌하지 않은 투사체가 자동으로 사라질 때까지의 시간입니다.</summary>
        [SerializeField, Min(0f)] private float projectileLifetimeSeconds = 3f;
        /// <summary>투사체가 유효한 대상에 적중했을 때 전달할 기본 피해량입니다.</summary>
        [SerializeField, Min(0f)] private float damage = 10f;

        [Header("Accuracy")]
        /// <summary>플레이어가 정지한 상태에서 적용할 최대 탄퍼짐 각도입니다.</summary>
        [SerializeField, Range(0f, 45f)] private float stationarySpreadDegrees = 1f;
        /// <summary>플레이어가 이동 중일 때 적용할 최대 탄퍼짐 각도입니다.</summary>
        [SerializeField, Range(0f, 45f)] private float movingSpreadDegrees = 8f;
        /// <summary>정지 사격과 이동 사격을 구분하는 이동량 기준입니다.</summary>
        [SerializeField, Min(0f)] private float movingThreshold = 0.01f;

        [Header("Aim Presentation")]
        /// <summary>캐릭터 외형이 조준 방향으로 회전하는 초당 각도입니다.</summary>
        [SerializeField, Min(0f)] private float aimTurnDegreesPerSecond = 1080f;

        [Header("Scene References")]
        /// <summary>서버가 발사 승인 후 생성할 네트워크 투사체 프리팹입니다.</summary>
        [SerializeField] private GameObject projectilePrefab;
        /// <summary>재장전할 때 플레이어 인벤토리에서 소비할 탄약 아이템입니다.</summary>
        [SerializeField] private ItemData ammunitionItem;

        /// <summary>발사 간격의 읽기 전용 값입니다.</summary>
        public float FireIntervalSeconds => fireIntervalSeconds;
        /// <summary>탄창 용량의 읽기 전용 값입니다.</summary>
        public int MagazineCapacity => magazineCapacity;
        /// <summary>재장전 시간의 읽기 전용 값입니다.</summary>
        public float ReloadSeconds => reloadSeconds;
        /// <summary>자동 재장전 대기시간의 읽기 전용 값입니다.</summary>
        public float AutoReloadDelaySeconds => autoReloadDelaySeconds;
        /// <summary>투사체 속도의 읽기 전용 값입니다.</summary>
        public float ProjectileSpeed => projectileSpeed;
        /// <summary>투사체 수명의 읽기 전용 값입니다.</summary>
        public float ProjectileLifetimeSeconds => projectileLifetimeSeconds;
        /// <summary>기본 피해량의 읽기 전용 값입니다.</summary>
        public float Damage => damage;
        /// <summary>정지 탄퍼짐의 읽기 전용 값입니다.</summary>
        public float StationarySpreadDegrees => stationarySpreadDegrees;
        /// <summary>이동 탄퍼짐의 읽기 전용 값입니다.</summary>
        public float MovingSpreadDegrees => movingSpreadDegrees;
        /// <summary>이동 판정 기준의 읽기 전용 값입니다.</summary>
        public float MovingThreshold => movingThreshold;
        /// <summary>조준 회전 속도의 읽기 전용 값입니다.</summary>
        public float AimTurnDegreesPerSecond => aimTurnDegreesPerSecond;
        /// <summary>네트워크 투사체 프리팹의 읽기 전용 참조입니다.</summary>
        public GameObject ProjectilePrefab => projectilePrefab;
        /// <summary>재장전에 소비할 탄약 아이템의 읽기 전용 참조입니다.</summary>
        public ItemData AmmunitionItem => ammunitionItem;
    }
}
