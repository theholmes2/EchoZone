using System;

namespace EchoZone.Heist
{
    /// <summary>현금 적재량과 등급별 힘으로 속도 배율만 계산합니다.</summary>
    public static class PetLoadBrick
    {
        /// <summary>빈 짐은 원래 속도, 가득 찬 짐도 최소 5% 속도를 유지합니다.</summary>
        public static float SpeedMultiplier(int money, int capacity, float strength, float slowdown)
        {
            float load = Math.Clamp((float)Math.Max(0, money) / Math.Max(1, capacity), 0f, 1f);
            return 1f - Math.Clamp(slowdown, 0f, 0.95f) * load / Math.Max(1f, strength);
        }
    }
}
