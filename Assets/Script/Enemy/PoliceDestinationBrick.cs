using System.Collections.Generic;
using UnityEngine;

namespace EchoZone.Enemy
{
    /// <summary>동일 목적지의 중복 점유를 막는 순수 위치 예약 규칙입니다. 경로 판정은 호출자가 수행합니다.</summary>
    public sealed class PoliceDestinationBrick
    {
        /// <summary>서버 세션에서 공유하는 예약 원장입니다. 새 세션 때 초기화합니다.</summary>
        public static readonly PoliceDestinationBrick Shared = new();
        /// <summary>경찰 식별자별 승인 목적지입니다.</summary>
        private readonly Dictionary<ulong, Vector3> destinations = new();
        /// <summary>다른 경찰과 설정 간격을 확보한 후보만 예약합니다.</summary>
        public bool TryClaim(ulong owner, Vector3 point, float spacing)
        {
            foreach (var pair in destinations)
            {
                Vector3 delta = pair.Value - point; delta.y = 0;
                if (pair.Key != owner && delta.sqrMagnitude < spacing * spacing) return false;
            }
            destinations[owner] = point;
            return true;
        }
        /// <summary>사망·목표 변경·풀 반환 시 예약을 해제합니다.</summary>
        public void Release(ulong owner) => destinations.Remove(owner);
        /// <summary>새 세션의 이전 예약을 제거합니다.</summary>
        public void Clear() => destinations.Clear();
        /// <summary>고정 순서의 원형 대체 후보를 계산합니다. 매 프레임 난수를 사용하지 않습니다.</summary>
        public static Vector3 Candidate(Vector3 center, int index, int count, float radius, ulong seed)
        {
            if (index == 0) return center;
            float angle = ((index - 1 + (int)(seed % (ulong)Mathf.Max(1, count))) % Mathf.Max(1, count)) * Mathf.PI * 2 / Mathf.Max(1, count);
            return center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
        }
    }
}
