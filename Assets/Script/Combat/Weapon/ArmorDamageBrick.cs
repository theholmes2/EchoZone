using System;

namespace EchoZone.Combat.Weapon
{
    /// <summary>방어구의 단일 피해 감소율을 적용하는 순수 계산입니다.</summary>
    public static class ArmorDamageBrick
    {
        /// <summary>방어구 등급을 중첩하지 않고 유효한 감소율 하나만 적용합니다.</summary>
        public static float Reduce(float damage, float reduction)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0) return 0;
            if (float.IsNaN(reduction) || float.IsInfinity(reduction)) reduction = 0;
            return damage * (1f - Math.Max(0f, Math.Min(1f, reduction)));
        }
    }
}
