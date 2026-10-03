using System;

namespace EchoZone.Heist
{
    /// <summary>Unity 시간이나 네트워크에 의존하지 않는 주기적 건물 현금 보충 계산입니다.</summary>
    public static class BuildingIncomeBrick
    {
        /// <summary>경과 주기를 한 번에 반영합니다. 상한 초과 반환금은 보존하고 만기에는 다음 주기로 이동합니다.</summary>
        public static int Advance(int money, double now, double nextAt, double interval, int amount, int cap, out double next)
        {
            interval = Math.Max(1, interval);
            next = nextAt;
            if (now < nextAt) return money;
            double cycles = Math.Floor((now - nextAt) / interval) + 1;
            next = nextAt + cycles * interval;
            if (amount <= 0 || money >= cap) return money;
            double income = Math.Min((double)cap - money, cycles * amount);
            return money + (int)income;
        }
    }
}
