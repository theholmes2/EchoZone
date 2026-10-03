using System;
namespace EchoZone.Heist
{
    /// <summary>대기 시간 버킷을 먼저 비교하고 같은 버킷 안에서 이동 경로 비용을 비교합니다.</summary>
    public static class PoliceInspectionPriorityBrick
    {
        /// <summary>멀리 있어도 더 오래 밀린 작업은 결국 가까운 신규 작업보다 먼저 선택됩니다.</summary>
        public static int Compare(double dueA, float pathA, int idA, double dueB, float pathB, int idB, double now, float agingSeconds)
        {
            double a = Math.Floor(Math.Max(0, now - dueA) / Math.Max(1, agingSeconds));
            double b = Math.Floor(Math.Max(0, now - dueB) / Math.Max(1, agingSeconds));
            int age = b.CompareTo(a);
            if (age != 0) return age;
            int path = pathA.CompareTo(pathB); return path != 0 ? path : idA.CompareTo(idB);
        }
        /// <summary>전투·수색 인원을 제외한 가용 순찰과 이미 배정된 작업 수로 신규 배정을 제한합니다.</summary>
        public static bool CanDispatch(int freePatrol, int assigned, int minimumPatrol, int maximumDuties) =>
            freePatrol > Math.Max(0, minimumPatrol) && assigned < Math.Max(1, maximumDuties);
    }
}
