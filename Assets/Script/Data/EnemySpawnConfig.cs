using UnityEngine;

namespace EchoZone.Enemy
{
    /// <summary>경찰 생성 시점과 수량을 제한하는 기초 스폰 데이터를 보관합니다.</summary>
    [CreateAssetMenu(fileName = "EnemySpawnConfig", menuName = "EchoZone/Enemy/Enemy Spawn Config")]
    public sealed class EnemySpawnConfig : ScriptableObject
    {
        /// <summary>경찰서 순서별 고정 목표입니다. 미지정 구역은 기존 씬 값을 사용합니다.</summary>
        public int[] DistrictTargets = { 10, 10, 10, 10 };
        /// <summary>매 프레임 밀도에 따라 변하지 않는 구역별 목표를 반환합니다.</summary>
        public int TargetFor(int district, int fallback) => DistrictTargets != null && district >= 0 && district < DistrictTargets.Length
            ? Mathf.Max(0, DistrictTargets[district]) : Mathf.Max(0, fallback);
        /// <summary>세션 시작 시 우선 생성할 경찰 수입니다.</summary>
        [SerializeField, Min(0)] private int initialSpawnCount = 1;
        /// <summary>동시에 살아 있을 수 있는 최대 경찰 수입니다.</summary>
        [SerializeField, Min(0)] private int maximumAliveCount = 4;
        /// <summary>연속된 두 스폰 사이에 필요한 시간입니다.</summary>
        [SerializeField, Min(0f)] private float spawnIntervalSeconds = 10f;
        /// <summary>스폰 지점과 가장 가까운 플레이어 사이에 필요한 거리입니다.</summary>
        [SerializeField, Min(0f)] private float minimumPlayerDistance = 8f;

        public int InitialSpawnCount => initialSpawnCount;
        /// <summary>동시에 살아 있을 수 있는 최대 경찰 수입니다.</summary>
        public int MaximumAliveCount => maximumAliveCount;
        /// <summary>연속된 두 스폰 사이에 필요한 시간입니다.</summary>
        public float SpawnIntervalSeconds => spawnIntervalSeconds;
        /// <summary>스폰 지점과 가장 가까운 플레이어 사이에 필요한 거리입니다.</summary>
        public float MinimumPlayerDistance => minimumPlayerDistance;
    }
}
