using System;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Enemy
{
    /// <summary>서버 권한으로 기초 경찰 생성 시점과 스폰 지점을 연결하는 작은 Manager입니다.</summary>
    public sealed partial class EnemySpawnManager : MonoBehaviour
    {
        /// <summary>동일 런타임 오브젝트에 연결된 경찰 네트워크 풀입니다.</summary>
        private EnemyNetworkPoolGlue pool;

        /// <summary>풀을 캐시합니다.</summary>
        private void Awake() => pool = GetComponent<EnemyNetworkPoolGlue>();

        /// <summary>세션 종료·호스트 교체 후 새 서버 스폰 수를 초기화합니다.</summary>
        public void ResetSession()
        {
            initialized = false;
            aliveEnemyCount = remainingInitialSpawnCount = 0;
            nextSpawnTime = 0f;
            if (policeStations == null) return;
            foreach (var station in policeStations)
                if (station != null) { station.AliveCount = 0; station.NextSpawnPointIndex = 0; }
        }
        /// <summary>경찰서 한 곳에서 사용할 생성 위치, 순찰 경로, 유지 인원을 묶습니다.</summary>
        [Serializable]
        private sealed class PoliceStationSpawnGroup
        {
            /// <summary>Inspector에서 경찰서를 구분할 이름입니다.</summary>
            [SerializeField] private string stationName;
            /// <summary>이 경찰서 소속 경찰이 태어날 위치들입니다.</summary>
            [SerializeField] private Transform[] spawnPoints;
            /// <summary>문 앞과 건물 옆·뒤를 순서대로 잇는 순찰 경로입니다.</summary>
            [SerializeField] private Transform[] patrolPoints;
            /// <summary>이 경찰서가 유지할 경찰 수입니다.</summary>
            [SerializeField, Min(0)] private int desiredAliveCount = 1;

            /// <summary>이 경찰서 소속으로 현재 살아 있는 경찰 수입니다.</summary>
            [NonSerialized] public int AliveCount;
            /// <summary>다음 생성에 사용할 위치 인덱스입니다.</summary>
            [NonSerialized] public int NextSpawnPointIndex;

            /// <summary>이 경찰서 소속 경찰이 태어날 위치들을 제공합니다.</summary>
            public Transform[] SpawnPoints => spawnPoints;
            /// <summary>문 앞과 건물 옆·뒤를 잇는 순찰 경로를 제공합니다.</summary>
            public Transform[] PatrolPoints => patrolPoints;
            /// <summary>이 경찰서가 유지할 경찰 수를 제공합니다.</summary>
            public int DesiredAliveCount => desiredAliveCount;
        }

        /// <summary>생성 간격과 최대 생존 수를 제공하는 스폰 데이터입니다.</summary>
        [SerializeField] private EnemySpawnConfig spawnConfig;
        /// <summary>생성할 경찰 프리팹과 행동 데이터를 제공합니다.</summary>
        [SerializeField] private PoliceEnemyConfig policeConfig;
        /// <summary>경찰서별 생성 위치, 순찰 경로, 유지 인원을 제공합니다.</summary>
        [SerializeField] private PoliceStationSpawnGroup[] policeStations;

        /// <summary>도주 계획에 사용할 경찰서 순찰 경로와 아직 생성되지 않은 경찰의 기준 시야거리입니다.</summary>
        public System.Collections.Generic.IEnumerable<Transform[]> PatrolRoutes
        {
            get
            {
                if (policeStations == null) yield break;
                foreach (var station in policeStations)
                    if (station != null && station.DesiredAliveCount > 0) yield return station.PatrolPoints;
            }
        }
        /// <summary>도주 목적지가 피해야 할 경찰서 생성 위치입니다.</summary>
        public System.Collections.Generic.IEnumerable<Transform> PoliceSpawnPoints
        {
            get
            {
                if (policeStations == null) yield break;
                foreach (var station in policeStations)
                    if (station != null && station.DesiredAliveCount > 0 && station.SpawnPoints != null)
                        foreach (var point in station.SpawnPoints) if (point != null) yield return point;
            }
        }
        /// <summary>경찰서에서 생성하는 경찰의 최대 탐지 거리입니다.</summary>
        public float PoliceSightDistance => policeConfig != null ? policeConfig.SightDistance : 0f;

        /// <summary>현재 세션에 살아 있는 경찰 수입니다.</summary>
        private int aliveEnemyCount;
        /// <summary>다음 경찰 생성을 허용할 서버 시각입니다.</summary>
        private float nextSpawnTime;
        /// <summary>세션 시작 직후 우선 생성해야 할 경찰의 남은 수입니다.</summary>
        private int remainingInitialSpawnCount;
        /// <summary>최초 스폰 수량과 시각을 준비했는지 나타냅니다.</summary>
        private bool initialized;

        /// <summary>중앙 게임 업데이트가 서버에서 호출할 기초 스폰 진입점입니다.</summary>
        public void ManualUpdate(float serverTime)
        {
            if (!IsServerActive() || spawnConfig == null || policeConfig == null ||
                policeConfig.PolicePrefab == null || spawnConfig.MaximumAliveCount <= 0)
            {
                return;
            }

            if (!initialized)
            {
                initialized = true;
                remainingInitialSpawnCount = Mathf.Min(
                    CountDesiredEnemies(),
                    spawnConfig.MaximumAliveCount);
                nextSpawnTime = remainingInitialSpawnCount > 0
                    ? serverTime
                    : serverTime + spawnConfig.SpawnIntervalSeconds;
            }

            if (aliveEnemyCount >= spawnConfig.MaximumAliveCount || serverTime < nextSpawnTime)
            {
                return;
            }

            bool spawned = TrySpawn();
            nextSpawnTime = serverTime + spawnConfig.SpawnIntervalSeconds;

            if (spawned && remainingInitialSpawnCount > 0)
            {
                remainingInitialSpawnCount--;

                if (remainingInitialSpawnCount > 0 &&
                    aliveEnemyCount < spawnConfig.MaximumAliveCount)
                {
                    nextSpawnTime = serverTime;
                }
            }
        }

        /// <summary>플레이어와 충분히 떨어진 유효 스폰 지점을 골라 경찰을 생성합니다.</summary>
        private bool TrySpawn()
        {
            if (policeStations == null || policeStations.Length == 0 ||
                policeConfig == null || policeConfig.PolicePrefab == null)
            {
                return false;
            }

            float minimumDistanceSquared =
                spawnConfig.MinimumPlayerDistance * spawnConfig.MinimumPlayerDistance;

            int firstStationIndex = UnityEngine.Random.Range(0, policeStations.Length);
            for (int stationOffset = 0; stationOffset < policeStations.Length; stationOffset++)
            {
                int stationIndex = (firstStationIndex + stationOffset) % policeStations.Length;
                PoliceStationSpawnGroup station = policeStations[stationIndex];
                if (station == null || station.AliveCount >= station.DesiredAliveCount ||
                    station.SpawnPoints == null || station.SpawnPoints.Length == 0)
                {
                    continue;
                }

                for (int pointOffset = 0; pointOffset < station.SpawnPoints.Length; pointOffset++)
                {
                    int pointIndex = (station.NextSpawnPointIndex + pointOffset) % station.SpawnPoints.Length;
                    Transform spawnPoint = station.SpawnPoints[pointIndex];
                    if (spawnPoint == null ||
                        IsTooCloseToPlayer(spawnPoint.position, minimumDistanceSquared) || IsOccupiedByPolice(spawnPoint.position))
                    {
                        continue;
                    }

                    if (pool == null) pool = GetComponent<EnemyNetworkPoolGlue>();
                    NetworkObject networkObject = pool != null ? pool.Rent(spawnPoint.position, spawnPoint.rotation) : null;
                    if (networkObject == null) return false;
                    GameObject enemyObject = networkObject.gameObject;

                    if (enemyObject.TryGetComponent(out PoliceEnemyBrainGlue enemyBrain))
                    {
                        enemyBrain.ConfigurePatrolRoute(station.PatrolPoints, stationIndex);
                    }

                    networkObject.Spawn(true);

                    if (enemyBrain != null)
                    {
                        PoliceWeaponType weaponType =
                            (station.AliveCount + stationIndex) % 2 == 0
                                ? PoliceWeaponType.Rifle
                                : PoliceWeaponType.Pistol;
                        enemyBrain.ConfigureWeaponType(weaponType);
                    }

                    aliveEnemyCount++;
                    station.AliveCount++;
                    station.NextSpawnPointIndex = (pointIndex + 1) % station.SpawnPoints.Length;

                    if (enemyBrain != null)
                    {
                        FindFirstObjectByType<EnemyUpdateManager>()?.Register(enemyBrain);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>후보 지점이 연결된 플레이어 중 한 명에게라도 최소 거리보다 가까운지 확인합니다.</summary>
        private bool IsTooCloseToPlayer(Vector3 spawnPosition, float minimumDistanceSquared)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                return true;
            }

            foreach (NetworkClient client in networkManager.ConnectedClientsList)
            {
                NetworkObject playerObject = client.PlayerObject;
                if (playerObject != null &&
                    (playerObject.transform.position - spawnPosition).sqrMagnitude < minimumDistanceSquared)
                {
                    return true;
                }
            }

            return false;
        }
        /// <summary>동일 스폰 지점에 기존 경찰이 남아 있으면 다음 후보나 다음 생성 주기를 기다립니다.</summary>
        private bool IsOccupiedByPolice(Vector3 position)
        {
            float spacing = Mathf.Max(policeConfig.DestinationSpacing, policeConfig.CollisionRadius * 2);
            foreach (var police in FindObjectsByType<PoliceEnemyBrainGlue>(FindObjectsSortMode.None))
                if (police.IsSpawned && (police.transform.position - position).sqrMagnitude < spacing * spacing) return true;
            return false;
        }

        /// <summary>경찰이 디스폰됐을 때 현재 생존 수를 갱신합니다.</summary>
        public void NotifyDespawned(int stationIndex)
        {
            aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);

            if (policeStations != null && stationIndex >= 0 && stationIndex < policeStations.Length &&
                policeStations[stationIndex] != null)
            {
                policeStations[stationIndex].AliveCount =
                    Mathf.Max(0, policeStations[stationIndex].AliveCount - 1);
            }
        }

        /// <summary>모든 경찰서가 유지하려는 경찰 수의 합을 계산합니다.</summary>
        private int CountDesiredEnemies()
        {
            if (policeStations == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < policeStations.Length; i++)
            {
                if (policeStations[i] != null)
                {
                    count += policeStations[i].DesiredAliveCount;
                }
            }

            return count;
        }

        /// <summary>현재 실행 환경이 권위 있는 서버인지 확인합니다.</summary>
        private static bool IsServerActive()
        {
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }
    }
}
