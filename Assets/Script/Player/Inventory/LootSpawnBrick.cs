using System.Collections.Generic;

namespace EchoZone.Equipment
{
    /// <summary>Unity 오브젝트 없이 랜덤 전리품 가중치를 선택하는 계산 Brick입니다.</summary>
    public sealed class LootSpawnBrick
    {
        /// <summary>0~1 표본을 양수 가중치 구간에 대응시켜 선택할 항목 인덱스를 반환합니다.</summary>
        public int SelectWeightedIndex(IReadOnlyList<float> weights, float sample)
        {
            if (weights == null || weights.Count == 0) return -1;
            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0f) total += weights[i];
            if (total <= 0f) return -1;

            float cursor = System.Math.Clamp(sample, 0f, 0.999999f) * total;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                cursor -= weights[i];
                if (cursor < 0f) return i;
            }
            return weights.Count - 1;
        }
    }
}
