using System;
using System.Collections.Generic;

namespace EchoZone.Heist
{
    /// <summary>계정별 현장 조사 기한과 확정 수배를 관리하는 순수 세션 규칙입니다.</summary>
    public sealed class PoliceInvestigationBrick
    {
        /// <summary>조사가 끝날 서버 시각입니다. 중복 증거는 최초 기한을 늦추지 않습니다.</summary>
        private readonly Dictionary<string, double> pending = new();
        /// <summary>신원이 확정된 계정입니다. 연결 ClientId와 독립적입니다.</summary>
        private readonly HashSet<string> wanted = new();
        /// <summary>만료 순회 중 사전 변경을 피하는 재사용 목록입니다.</summary>
        private readonly List<string> expired = new();

        /// <summary>새 증거의 조사 완료를 예약합니다.</summary>
        public void Schedule(string playerId, double now, double delay)
        {
            if (string.IsNullOrEmpty(playerId) || wanted.Contains(playerId)) return;
            double deadline = now + Math.Max(0d, delay);
            if (!pending.TryGetValue(playerId, out double previous) || deadline < previous)
                pending[playerId] = deadline;
        }

        /// <summary>경찰 공격·현장 적발은 조사 대기 없이 수배합니다.</summary>
        public void Witness(string playerId)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            pending.Remove(playerId);
            wanted.Add(playerId);
        }

        /// <summary>기한이 지난 조사만 수배로 전환합니다.</summary>
        public void Tick(double now)
        {
            expired.Clear();
            foreach (var pair in pending) if (now >= pair.Value) expired.Add(pair.Key);
            foreach (string id in expired) Witness(id);
        }

        /// <summary>계정의 수배 여부입니다.</summary>
        public bool IsWanted(string playerId) => playerId != null && wanted.Contains(playerId);

        /// <summary>사망 시 대기 조사도 취소하고 확정 수배의 최초 체포인지 반환합니다.</summary>
        public bool Capture(string playerId)
        {
            if (playerId == null) return false;
            pending.Remove(playerId);
            return wanted.Remove(playerId);
        }
        /// <summary>절대 시각 대신 남은 조사 시간과 수배 계정을 직렬화합니다.</summary>
        public string Export(double now)
        {
            var remaining = new Dictionary<string, double>();
            foreach (var pair in pending) remaining[pair.Key] = Math.Max(0, pair.Value - now);
            return Newtonsoft.Json.JsonConvert.SerializeObject(new InvestigationState { pending = remaining, wanted = new List<string>(wanted) });
        }
        /// <summary>새 서버 시각에 남은 조사 시간을 다시 연결합니다.</summary>
        public void Restore(string json, double now)
        {
            pending.Clear(); wanted.Clear();
            if (string.IsNullOrEmpty(json)) return;
            var state = Newtonsoft.Json.JsonConvert.DeserializeObject<InvestigationState>(json);
            foreach (var pair in state.pending) Schedule(pair.Key, now, pair.Value);
            foreach (var id in state.wanted) Witness(id);
        }
        /// <summary>조사 원장 전송 형식입니다.</summary>
        private sealed class InvestigationState
        {
            public Dictionary<string, double> pending = new();
            public List<string> wanted = new();
        }
    }
}
