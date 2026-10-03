using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoZone.Audio
{
    /// <summary>사용자가 클립과 재생 기본값을 한곳에서 지정하는 효과음 설정입니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Audio/Gameplay Sound Config")]
    public sealed class GameplaySoundConfig : ScriptableObject
    {
        /// <summary>ID별 클립과 기본 수치입니다. null 클립은 정상적인 미설정 상태입니다.</summary>
        public Entry[] Entries = Array.Empty<Entry>();
        /// <summary>3D 소리가 최대 음량인 거리입니다.</summary>
        [Min(0.1f)] public float MinDistance = 3f;
        /// <summary>3D 선형 감쇠가 끝나는 거리입니다.</summary>
        [Min(1f)] public float MaxDistance = 60f;

        /// <summary>인스펙터에서 편집하는 개별 효과음입니다.</summary>
        [Serializable]
        public sealed class Entry
        {
            /// <summary>재생 요청에 사용하는 ID입니다.</summary>
            public GameplaySoundId Id;
            /// <summary>직접 드래그할 음원입니다.</summary>
            public AudioClip Clip;
            /// <summary>클립 기본 음량입니다.</summary>
            [Range(0f, 1f)] public float Volume = 1f;
            /// <summary>양수 재생 속도입니다.</summary>
            [Range(0.1f, 3f)] public float Pitch = 1f;
        }

        /// <summary>기존 클립을 보존하며 중복 제거·누락 추가·수치 보정을 수행합니다.</summary>
        public void Normalize()
        {
            var unique = new Dictionary<GameplaySoundId, Entry>();
            foreach (var entry in Entries ?? Array.Empty<Entry>())
                if (entry != null && Enum.IsDefined(typeof(GameplaySoundId), entry.Id) && !unique.ContainsKey(entry.Id)) unique.Add(entry.Id, entry);
            var entries = new List<Entry>();
            foreach (GameplaySoundId id in Enum.GetValues(typeof(GameplaySoundId)))
            {
                var entry = unique.TryGetValue(id, out var found) ? found : new Entry { Id = id };
                entry.Volume = GameplaySoundBrick.Clamp(entry.Volume, 0, 1, 1);
                entry.Pitch = GameplaySoundBrick.Clamp(entry.Pitch, 0.1f, 3, 1);
                entries.Add(entry);
            }
            Entries = entries.ToArray();
            MinDistance = GameplaySoundBrick.Clamp(MinDistance, 0.1f, 1000, 3);
            MaxDistance = GameplaySoundBrick.Clamp(MaxDistance, MinDistance, 10000, 60);
        }

        /// <summary>에디터 편집 때 항목을 안전한 형태로 유지합니다.</summary>
        private void OnValidate() => Normalize();
    }
}
