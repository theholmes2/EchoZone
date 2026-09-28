using System;
using System.Collections.Generic;

namespace EchoZone.Heist
{
    /// <summary>Unity 객체를 모르는 장물 원장입니다. 건물·펫·플레이어는 숫자 식별자로만 다룹니다.</summary>
    public sealed class HeistLedgerBrick
    {
        /// <summary>아직 반환되지 않은 한 번의 절도 기록입니다.</summary>
        private sealed class Entry
        {
            /// <summary>절도 기록 자체의 고정 식별자입니다.</summary>
            public string Id = Guid.NewGuid().ToString("N");
            public int Building, Amount;
            public string Pet, Thief;
            /// <summary>절도 당시 서버가 확인한 계정입니다. 재접속 후에도 범인을 바꾸지 않습니다.</summary>
            public string ThiefPlayerId;
            public bool Detected, Extracted;
            public HashSet<string> Participants = new();
        }
        /// <summary>미반환 기록입니다. 반환 시 제거하여 중복 보상을 막습니다.</summary>
        private readonly List<Entry> entries = new();
        /// <summary>목격으로 신원이 확인된 범인입니다. 현금 반환과 독립적으로 유지합니다.</summary>
        private readonly HashSet<ulong> wanted = new();
        /// <summary>호출자가 내부 명단을 변경하지 못하도록 복사합니다.</summary>
        public HashSet<ulong> Wanted => new(wanted);
        /// <summary>절도 현장에서 확인한 신원만 수배합니다.</summary>
        public void Witness(ulong player) => wanted.Add(player);
        /// <summary>체포된 수배를 한 번만 해제합니다. 반환값으로 중복 현상금 지급을 막습니다.</summary>
        public bool Capture(ulong player) => wanted.Remove(player);
        /// <summary>성공한 절도 한 건을 기록합니다.</summary>
        public void Record(int building, ulong pet, ulong thief, int amount, string thiefPlayerId = null)
            => Record(building, pet.ToString(), thiefPlayerId ?? thief.ToString(), amount);
        /// <summary>고정 펫 ID와 인증 계정으로 절도 증거를 저장합니다.</summary>
        public void Record(int building, string pet, string thief, int amount)
        {
            if (amount <= 0) return;
            var e = new Entry { Building = building, Pet = pet, Thief = thief, Amount = amount, ThiefPlayerId = thief };
            e.Participants.Add(thief);
            entries.Add(e);
        }
        /// <summary>펫을 가져간 사람은 장물 신고 보상 대상에서 제외합니다.</summary>
        public void Acquired(ulong pet, ulong player)
            => Acquired(pet.ToString(), player.ToString());
        /// <summary>계정별 장물 운반 참여자를 기록합니다.</summary>
        public void Acquired(string pet, string player)
        {
            foreach (var e in entries) if (e.Pet == pet) e.Participants.Add(player);
        }
        /// <summary>새로 발견한 현금 부족만 반환합니다. 원장의 범인 정보로 자동 수배하지 않습니다.</summary>
        public bool Inspect(int building) => Inspect(building, null);
        /// <summary>새로 발견한 증거의 원래 범인만 조사 목록에 추가합니다. 운반자는 제외합니다.</summary>
        public bool Inspect(int building, ISet<string> discoveredThieves)
        {
            bool discovered = false;
            foreach (var e in entries)
            {
                if (e.Building != building || e.Detected) continue;
                e.Detected = true;
                if (!string.IsNullOrEmpty(e.ThiefPlayerId)) discoveredThieves?.Add(e.ThiefPlayerId);
                discovered = true;
            }
            return discovered;
        }
        /// <summary>펫 장물의 총액입니다.</summary>
        public int Cargo(ulong pet) => Cargo(pet.ToString());
        /// <summary>고정 펫 ID의 미정산 금액입니다.</summary>
        public int Cargo(string pet) { int total = 0; foreach (var e in entries) if (e.Pet == pet && !e.Extracted) total += e.Amount; return total; }
        /// <summary>탈출한 돈은 다시 반환·신고할 수 없지만, 건물 검사에서 도난을 발견할 기록은 남깁니다.</summary>
        public int Extract(ulong pet)
            => Extract(pet.ToString());
        /// <summary>고정 펫 ID의 장물을 정산 처리합니다.</summary>
        public int Extract(string pet)
        {
            int total = 0;
            foreach (var e in entries) if (e.Pet == pet && !e.Extracted) { total += e.Amount; e.Extracted = true; }
            return total;
        }
        /// <summary>운반 중인 장물이 검사에서 적발됐는지 확인합니다.</summary>
        public bool IsDetected(ulong pet) { foreach (var e in entries) if (e.Pet == pet.ToString() && e.Detected) return true; return false; }
        /// <summary>돈을 원래 건물별로 합산하여 반환하고 신고자에게 지급할 감사비를 계산합니다.</summary>
        public Dictionary<int, int> Return(ulong pet, ulong? reporter, float rate, out int reward)
            => Return(pet.ToString(), reporter?.ToString(), rate, out reward);
        /// <summary>고정 ID 원장에서 장물을 제거하고 반환과 보상을 한 번만 계산합니다.</summary>
        public Dictionary<int, int> Return(string pet, string reporter, float rate, out int reward)
        {
            var restored = new Dictionary<int, int>();
            long eligible = 0;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.Pet != pet || e.Extracted) continue;
                restored.TryGetValue(e.Building, out int amount);
                restored[e.Building] = amount + e.Amount;
                if (!string.IsNullOrEmpty(reporter) && !e.Participants.Contains(reporter)) eligible += e.Amount;
                entries.RemoveAt(i);
            }
            reward = (int)Math.Floor(eligible * Math.Clamp(rate, 0f, 1f));
            return restored;
        }
        /// <summary>ClientId와 NetworkObjectId를 포함하지 않는 원장 복사본입니다.</summary>
        public string Export() => Newtonsoft.Json.JsonConvert.SerializeObject(entries);
        /// <summary>검증된 세션 원장을 교체합니다.</summary>
        public void Restore(string json)
        {
            entries.Clear();
            if (!string.IsNullOrEmpty(json)) entries.AddRange(Newtonsoft.Json.JsonConvert.DeserializeObject<List<Entry>>(json) ?? new());
        }
    }
}
