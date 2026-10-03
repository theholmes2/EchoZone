using System.Collections.Generic;

namespace EchoZone.Heist
{
    public sealed partial class HeistWorldGlue
    {
        /// <summary>서버만 보관하는 다음 보충 시각입니다. 마이그레이션에는 남은 시간만 저장합니다.</summary>
        private readonly Dictionary<int, double> buildingIncomeAt = new();

        /// <summary>새 세션은 온전한 보충 주기로 시작하며 일반 건물은 등록하지 않습니다.</summary>
        private void ResetBuildingIncome(double now)
        {
            buildingIncomeAt.Clear();
            foreach (var site in sites.Values)
                if (site.IsLootSite) buildingIncomeAt[site.Id] = now + System.Math.Max(1, config.BuildingIncomeInterval);
        }

        /// <summary>기존 서버 갱신에서 만기된 건물만 계산하고 금액이 변했을 때만 복제 목록을 갱신합니다.</summary>
        private void TickBuildingIncome(double now)
        {
            foreach (var site in sites.Values)
            {
                if (!site.IsLootSite || !buildingIncomeAt.TryGetValue(site.Id, out double nextAt) || now < nextAt) continue;
                var state = Status(site.Id);
                int money = BuildingIncomeBrick.Advance(state.Money, now, nextAt, config.BuildingIncomeInterval,
                    config.BuildingIncomeAmount, config.BuildingIncomeCap, out double next);
                buildingIncomeAt[site.Id] = next;
                if (state.Money == money) continue;
                state.Money = money;
                Write(state);
            }
        }
    }
}
