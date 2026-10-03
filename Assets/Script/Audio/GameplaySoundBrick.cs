using System;
using System.Collections.Generic;

namespace EchoZone.Audio
{
    /// <summary>Unity 오브젝트 없이 식별자 조회와 안전한 재생 수치를 결정합니다.</summary>
    public static class GameplaySoundBrick
    {
        /// <summary>첫 번째 유효 항목만 채택하므로 중복 ID가 여러 번 재생되지 않습니다.</summary>
        public static Dictionary<GameplaySoundId, int> BuildIndex(IReadOnlyList<GameplaySoundId> ids)
        {
            var result = new Dictionary<GameplaySoundId, int>();
            for (int i = 0; i < ids.Count; i++)
                if (Enum.IsDefined(typeof(GameplaySoundId), ids[i]) && !result.ContainsKey(ids[i])) result.Add(ids[i], i);
            return result;
        }

        /// <summary>효과 종류에 고정된 공간 채널을 선택합니다.</summary>
        public static GameplaySoundChannel Channel(GameplaySoundId id) => id == GameplaySoundId.PlayerShot
            ? GameplaySoundChannel.Weapon : id == GameplaySoundId.PoliceShot ? GameplaySoundChannel.World : GameplaySoundChannel.UI;

        /// <summary>NaN과 무한대까지 포함해 범위를 보정합니다.</summary>
        public static float Clamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));
    }
}
