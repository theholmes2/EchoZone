using EchoZone.Online.Migration;
using UnityEngine;

namespace EchoZone.Enemy
{
    public sealed partial class EnemySpawnManager
    {
        /// <summary>초기 지급 수량과 다음 경찰 생성까지 남은 시간을 저장합니다.</summary>
        public void CaptureMigration(SessionWorldSnapshot snapshot, float now)
        {
            snapshot.nextSpawnRemaining = Mathf.Max(0, nextSpawnTime - now);
            snapshot.remainingInitial = remainingInitialSpawnCount;
            if (policeStations != null)
                foreach (var station in policeStations) snapshot.stationSpawnIndices.Add(station != null ? station.NextSpawnPointIndex : 0);
        }

        /// <summary>풀에서 경찰을 복원하고 실제 생성 목록으로 생존 수를 재구성합니다.</summary>
        public void RestoreMigration(SessionWorldSnapshot snapshot, float now)
        {
            if (!IsServerActive()) return;
            if (pool == null) pool = GetComponent<EnemyNetworkPoolGlue>();
            foreach (var old in FindObjectsByType<PoliceEnemyBrainGlue>(FindObjectsSortMode.None))
                if (old.IsSpawned) pool.DespawnServer(old.NetworkObject);
            ResetSession(); initialized = true;
            nextSpawnTime = now + snapshot.nextSpawnRemaining; remainingInitialSpawnCount = snapshot.remainingInitial;
            for (int i = 0; policeStations != null && i < policeStations.Length; i++)
                if (policeStations[i] != null && i < snapshot.stationSpawnIndices.Count)
                    policeStations[i].NextSpawnPointIndex = snapshot.stationSpawnIndices[i];
            foreach (var record in snapshot.police)
            {
                var obj = pool.Rent(record.position, record.rotation);
                if (obj == null) throw new System.InvalidOperationException("Police migration pool is unavailable.");
                var brain = obj.GetComponent<PoliceEnemyBrainGlue>();
                var station = policeStations != null && record.station >= 0 && record.station < policeStations.Length ? policeStations[record.station] : null;
                brain.ConfigurePatrolRoute(station != null ? station.PatrolPoints : null, record.station);
                obj.Spawn(true); brain.RestoreMigration(record, now);
                aliveEnemyCount++; if (station != null) station.AliveCount++;
            }
        }
    }
}
