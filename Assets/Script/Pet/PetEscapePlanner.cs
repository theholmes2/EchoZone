using System.Collections.Generic;
using EchoZone.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace EchoZone.Pet
{
    /// <summary>현재 살아 있는 경찰의 위험 반경과 순수 원 후보 계산을 연결하는 서버 도주 계획기입니다.</summary>
    public sealed class PetEscapePlanner : MonoBehaviour
    {
        /// <summary>도주 탐색 설정입니다.</summary>
        [SerializeField] private PetBehaviourConfig config;
        /// <summary>이번 검사에서 공유할 위험 구간 목록입니다.</summary>
        private readonly List<PetDangerZone> zones = new();
        /// <summary>후보 검사에 재사용하는 경로입니다.</summary>
        private NavMeshPath path;
        /// <summary>마지막 위험 목록 갱신 시각입니다.</summary>
        private float refreshedAt = float.NegativeInfinity;

        /// <summary>Unity 네이티브 경로 객체는 메인 스레드에서 준비합니다.</summary>
        private void Awake() { path = new NavMeshPath(); }

        /// <summary>동일 검사 주기에서는 위험 목록을 재사용합니다.</summary>
        public void RefreshThreats(NavMeshAgent agent)
        {
            if (Time.time - refreshedAt < Mathf.Max(0.1f, config.RecheckSeconds)) return;
            refreshedAt = Time.time;
            zones.Clear();
            foreach (var police in FindObjectsByType<PoliceEnemyBrainGlue>(FindObjectsSortMode.None))
            {
                if (!police.IsSpawned || (police.TryGetComponent<PlayerStats>(out var stats) && stats.IsDead)) continue;
                float radius = police.SightDistance + config.PoliceMargin;
                zones.Add(new PetDangerZone(police.transform.position, police.transform.position, radius));
            }
        }

        /// <summary>시야각의 현재 방향과 무관하게 경찰 시야반경 바깥인지 검사합니다.</summary>
        public bool IsSafe(Vector3 position)
        {
            foreach (var zone in zones)
                if (PetEscapeBrick.DistanceToSegment(position, zone.Start, zone.End) <= zone.Radius) return false;
            return true;
        }

        /// <summary>원 위 안전 후보 중 완전한 경로가 연결되는 가장 짧은 경로를 고릅니다.</summary>
        public bool TryPlan(NavMeshAgent agent, Vector3 center, out Vector3 destination)
        {
            destination = default;
            if (config == null || !agent.isOnNavMesh) return false;
            RefreshThreats(agent);
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            float bestLength = float.PositiveInfinity;
            for (int ring = 0; ring < Mathf.Clamp(config.RingCount, 1, 5); ring++)
            for (int i = 0; i < Mathf.Clamp(config.CandidateCount, 8, 64); i++)
            {
                float radius = Mathf.Max(config.EscapeRadius, config.MinimumPlayerDistance) + ring * config.RingSpacing;
                Vector3 candidate = PetEscapeBrick.RingPoint(center, radius, i, config.CandidateCount);
                if (!NavMesh.SamplePosition(candidate, out var hit, config.SampleRadius, filter) ||
                    PetEscapeBrick.Distance(hit.position, center) < config.MinimumPlayerDistance || !IsSafe(hit.position)) continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                var corners = path.corners;
                if (!IsEscapePathSafe(corners)) continue;
                float length = 0f;
                for (int j = 1; j < corners.Length; j++) length += Vector3.Distance(corners[j - 1], corners[j]);
                if (length >= bestLength) continue;
                bestLength = length;
                destination = hit.position;
            }
            return float.IsFinite(bestLength);
        }

        /// <summary>출발부터 위험 영역 안인 경우 탈출은 허용하되 더 접근하거나 안전해진 뒤 재진입하지 못하게 합니다.</summary>
        public bool IsEscapePathSafe(Vector3[] corners)
        {
            if (corners == null || corners.Length == 0) return false;
            float spacing = Mathf.Max(0.1f, config.PathSampleSpacing);
            foreach (var zone in zones)
            {
                float radius = zone.Radius + spacing * 0.5f;
                float previous = PetEscapeBrick.DistanceToSegment(corners[0], zone.Start, zone.End);
                bool escaped = previous > radius;
                int samples = 0;
                for (int i = 1; i < corners.Length; i++)
                {
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(corners[i - 1], corners[i]) / spacing));
                    if ((samples += steps) > config.MaximumPathSamples) return false;
                    for (int j = 1; j <= steps; j++)
                    {
                        Vector3 point = Vector3.Lerp(corners[i - 1], corners[i], (float)j / steps);
                        float distance = PetEscapeBrick.DistanceToSegment(point, zone.Start, zone.End);
                        if (escaped && distance <= radius) return false;
                        if (!escaped && distance + 0.01f < previous) return false;
                        if (distance > radius) escaped = true;
                        previous = distance;
                    }
                }
            }
            return true;
        }
    }
}
