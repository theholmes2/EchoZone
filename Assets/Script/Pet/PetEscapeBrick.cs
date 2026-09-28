using UnityEngine;

namespace EchoZone.Pet
{
    /// <summary>경찰 위치 또는 순찰 경로 구간을 감싼 수평 위험 영역입니다.</summary>
    public readonly struct PetDangerZone
    {
        /// <summary>위험 구간의 양 끝입니다. 같은 점이면 원형 영역입니다.</summary>
        public readonly Vector3 Start, End;
        /// <summary>경찰 시야거리와 여유를 포함한 반경입니다.</summary>
        public readonly float Radius;
        /// <summary>순수 데이터 구간을 구성합니다.</summary>
        public PetDangerZone(Vector3 start, Vector3 end, float radius) { Start = start; End = end; Radius = radius; }
    }

    /// <summary>Unity Physics나 NavMesh를 모르는 도주 후보·수평 거리 계산입니다.</summary>
    public static class PetEscapeBrick
    {
        /// <summary>원 위의 일정 간격 후보를 계산합니다.</summary>
        public static Vector3 RingPoint(Vector3 center, float radius, int index, int count)
        {
            float radians = index * Mathf.PI * 2f / Mathf.Max(1, count);
            return center + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radius;
        }

        /// <summary>높이를 제외한 두 점 사이 거리입니다.</summary>
        public static float Distance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>점에서 순찰 선분 또는 경찰 위치까지의 수평 거리입니다.</summary>
        public static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = a.y = b.y = 0f;
            Vector3 segment = b - a;
            float t = segment.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - a, segment) / segment.sqrMagnitude) : 0f;
            return Vector3.Distance(point, a + segment * t);
        }
    }
}
