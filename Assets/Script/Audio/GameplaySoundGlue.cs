using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Audio
{
    /// <summary>확정 이벤트를 씬의 독립 음성 풀에 연결합니다. 네트워크 판정은 호출자가 담당합니다.</summary>
    public sealed class GameplaySoundGlue : MonoBehaviour
    {
        /// <summary>현재 로컬 씬의 재생 연결점입니다.</summary>
        public static GameplaySoundGlue Instance { get; private set; }
        /// <summary>모든 클립과 재생 기본값입니다.</summary>
        [SerializeField] private GameplaySoundConfig config;
        /// <summary>UI 전용 2D 음성 풀입니다.</summary>
        [SerializeField] private AudioSource[] uiSources;
        /// <summary>플레이어 무기 전용 3D 음성 풀입니다.</summary>
        [SerializeField] private AudioSource[] weaponSources;
        /// <summary>경찰과 월드 전용 3D 음성 풀입니다.</summary>
        [SerializeField] private AudioSource[] worldSources;
        /// <summary>클립 배열 인덱스 조회표입니다.</summary>
        private Dictionary<GameplaySoundId, int> index;

        /// <summary>도메인 재로드 비활성화 시 이전 씬 참조를 제거합니다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => Instance = null;

        /// <summary>중복 시스템을 무시하고 ID 조회표만 준비합니다.</summary>
        private void Awake()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            if (config == null) return;
            config.Normalize();
            var ids = new List<GameplaySoundId>();
            foreach (var entry in config.Entries) ids.Add(entry.Id);
            index = GameplaySoundBrick.BuildIndex(ids);
        }

        /// <summary>자기 인스턴스일 때만 전역 참조를 해제합니다.</summary>
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>로컬 UI 효과음을 재생합니다.</summary>
        public static void PlayUI(GameplaySoundId id)
        { if (GameplaySoundBrick.Channel(id) == GameplaySoundChannel.UI) Instance?.Play(id, Vector3.zero); }

        /// <summary>서버가 전달한 실제 발생 위치에 3D 효과음을 재생합니다.</summary>
        public static void PlayWorld(GameplaySoundId id, Vector3 position)
        { if (GameplaySoundBrick.Channel(id) != GameplaySoundChannel.UI) Instance?.Play(id, position); }

        /// <summary>전용 서버·빈 클립은 무시하고 빈 음성 하나로 OneShot을 재생합니다. 풀 포화 시 새 소리를 생략합니다.</summary>
        private void Play(GameplaySoundId id, Vector3 position)
        {
#if UNITY_SERVER
            return;
#else
            var manager = NetworkManager.Singleton;
            if ((manager != null && manager.IsServer && !manager.IsClient) || index == null || config == null || !index.TryGetValue(id, out int entryIndex)) return;
            var entry = config.Entries[entryIndex];
            if (entry.Clip == null) return;
            var channel = GameplaySoundBrick.Channel(id);
            var sources = channel == GameplaySoundChannel.UI ? uiSources : channel == GameplaySoundChannel.Weapon ? weaponSources : worldSources;
            if (sources == null) return;
            foreach (var source in sources)
            {
                if (source == null || !source.isActiveAndEnabled || source.isPlaying) continue;
                source.transform.position = position;
                source.loop = false; source.playOnAwake = false; source.volume = 1;
                source.pitch = GameplaySoundBrick.Clamp(entry.Pitch, 0.1f, 3, 1);
                source.spatialBlend = channel == GameplaySoundChannel.UI ? 0 : 1;
                source.dopplerLevel = 0; source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = config.MinDistance; source.maxDistance = config.MaxDistance;
                source.PlayOneShot(entry.Clip, GameplaySoundBrick.Clamp(entry.Volume, 0, 1, 1));
                return;
            }
#endif
        }
    }
}
