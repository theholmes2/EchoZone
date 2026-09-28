using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>경찰을 분산하는 수색 후보 위치의 순수 기하 계산입니다.</summary>
    public static class CrimeSearchBrick
    {
        /// <summary>건물 주변 원 위의 후보를 계산합니다. 이동 가능 여부는 Glue가 검사합니다.</summary>
        public static Vector3 Point(Vector3 center, float radius, int index, int count)
        {
            float angle = 2f * Mathf.PI * (index % Mathf.Max(3, count)) / Mathf.Max(3, count);
            return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Max(0f, radius);
        }
    }
}
