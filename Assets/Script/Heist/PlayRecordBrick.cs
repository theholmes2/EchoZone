#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace EchoZone.Heist
{
    /// <summary>완료 전적에 보관할 요약이며 사건 원장과 지갑 잔액은 포함하지 않습니다.</summary>
    public sealed class PlayRecordSummary
    {
        /// <summary>사망·재접속에도 유지하는 개인 플레이 회차입니다.</summary>
        public string RoundId { get; set; } = "";
        /// <summary>기록을 소유하는 인증 계정입니다.</summary>
        public string PlayerId { get; set; } = "";
        /// <summary>회차가 진행된 월드입니다.</summary>
        public string WorldId { get; set; } = "";
        /// <summary>마이그레이션에도 유지하는 방 실행입니다.</summary>
        public string RunId { get; set; } = "";
        /// <summary>최초 플레이를 시작한 세션입니다.</summary>
        public string SessionId { get; set; } = "";
        /// <summary>호스트가 회차를 시작한 UTC입니다.</summary>
        public string StartedAtUtc { get; set; } = "";
        /// <summary>호스트가 탈출을 승인하여 기록을 동결한 UTC입니다.</summary>
        public string EndedAtUtc { get; set; } = "";
        /// <summary>연결된 실제 플레이 시간이며 복구·단절 시간은 제외합니다.</summary>
        public double PlayedSeconds { get; set; }
        /// <summary>회수·압수와 무관하게 성공한 절도 총액입니다.</summary>
        public long StolenMoney { get; set; }
        /// <summary>중복을 제외한 절도 성공 건물 수입니다.</summary>
        public int RobbedBuildings { get; set; }
        /// <summary>이 플레이어의 유효 피해로 처치한 경찰 수입니다.</summary>
        public int PoliceKills { get; set; }
        /// <summary>자기 자신을 제외한 플레이어 처치 수입니다.</summary>
        public int PlayerKills { get; set; }
        /// <summary>회차 내 생존에서 사망으로 전환한 횟수입니다.</summary>
        public int Deaths { get; set; }
        /// <summary>지갑에 실제 적립한 체포 현상금입니다.</summary>
        public long BountyMoney { get; set; }
        /// <summary>서버가 정상 접수한 신고 횟수입니다.</summary>
        public int Reports { get; set; }
        /// <summary>경찰 회수 후 실제 적립한 신고 감사비입니다.</summary>
        public long ReportRewardMoney { get; set; }
        /// <summary>해당 탈출에서 확보한 펫 장물만의 금액입니다.</summary>
        public long ExtractedCargo { get; set; }

        /// <summary>현재 값을 별도 객체로 복사하여 이전 회차 재시도가 진행 중 기록을 바꾸지 않게 합니다.</summary>
        public PlayRecordSummary Copy() => (PlayRecordSummary)MemberwiseClone();

        /// <summary>완료 본문의 식별자·시간·음수 수치를 검증합니다.</summary>
        public void Validate()
        {
            if (!Guid.TryParseExact(RoundId, "N", out _) ||
                new[] { PlayerId, WorldId, RunId, SessionId }.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 256) ||
                !DateTimeOffset.TryParse(StartedAtUtc, out var start) || !DateTimeOffset.TryParse(EndedAtUtc, out var end) || end < start ||
                double.IsNaN(PlayedSeconds) || double.IsInfinity(PlayedSeconds) || PlayedSeconds < 0 ||
                StolenMoney < 0 || RobbedBuildings < 0 || PoliceKills < 0 || PlayerKills < 0 || Deaths < 0 ||
                BountyMoney < 0 || Reports < 0 || ReportRewardMoney < 0 || ExtractedCargo < 0)
                throw new InvalidOperationException("Invalid play record.");
        }

        /// <summary>직렬화 라이브러리·로케일과 무관한 고정 필드 순서로 본문 지문을 계산합니다.</summary>
        public string Fingerprint()
        {
            Validate();
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(RoundId); writer.Write(PlayerId); writer.Write(WorldId); writer.Write(RunId); writer.Write(SessionId);
                writer.Write(StartedAtUtc); writer.Write(EndedAtUtc); writer.Write(PlayedSeconds); writer.Write(StolenMoney);
                writer.Write(RobbedBuildings); writer.Write(PoliceKills); writer.Write(PlayerKills); writer.Write(Deaths);
                writer.Write(BountyMoney); writer.Write(Reports); writer.Write(ReportRewardMoney); writer.Write(ExtractedCargo);
            }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "");
        }
    }

    /// <summary>기존 지갑 Snapshot 내부에 보관하는 진행 중 회차와 중복 제외 상태입니다.</summary>
    public sealed class PlayRecordState
    {
        /// <summary>현재 회차 누적 요약입니다.</summary>
        public PlayRecordSummary Summary { get; set; } = new PlayRecordSummary();
        /// <summary>동일 건물의 반복 절도를 한 건물로 집계하기 위한 고정 ID입니다.</summary>
        public HashSet<int> Buildings { get; set; } = new HashSet<int>();
        /// <summary>서버 사건 재적용을 막는 식별자이며 완료 전적에는 복사하지 않습니다.</summary>
        public HashSet<string> Events { get; set; } = new HashSet<string>();
        /// <summary>같은 사망 콜백과 사망 상태 재접속을 중복 집계하지 않는 생명 상태입니다.</summary>
        public bool Dead { get; set; }
        /// <summary>탈출 승인 후 변경할 수 없는 회차인지 나타냅니다.</summary>
        public bool Frozen { get; set; }
    }

    /// <summary>확정된 서버 행동만 누적하며 Unity와 저장 API를 참조하지 않습니다.</summary>
    public sealed class PlayRecordBrick
    {
        /// <summary>기존 Snapshot에 그대로 포함할 복원 가능한 상태입니다.</summary>
        public PlayRecordState State { get; }
        /// <summary>저장 상태는 덧셈 없이 그대로 이어받습니다.</summary>
        public PlayRecordBrick(PlayRecordState state) => State = state ?? throw new ArgumentNullException(nameof(state));
        /// <summary>새 개인 회차를 발급합니다. 기존 회차에는 호출하지 않습니다.</summary>
        public static PlayRecordBrick Start(string player, string world, string run, string session, string utc) =>
            new PlayRecordBrick(new PlayRecordState { Summary = new PlayRecordSummary {
                RoundId = Guid.NewGuid().ToString("N"), PlayerId = player, WorldId = world, RunId = run, SessionId = session, StartedAtUtc = utc } });
        /// <summary>연결·복구 완료 상태에서만 실제 플레이 시간을 누적합니다.</summary>
        public void Tick(double delta, bool active)
        { if (active && !State.Frozen && delta > 0 && !double.IsNaN(delta) && !double.IsInfinity(delta)) State.Summary.PlayedSeconds += delta; }
        private bool Accept(string id) => !State.Frozen && !string.IsNullOrEmpty(id) && State.Events.Add(id);
        /// <summary>같은 원장 사건은 한 번만 누적하고 건물 개수는 중복을 제거합니다.</summary>
        public void Theft(string eventId, int building, int amount)
        { if (amount <= 0 || !Accept("theft:" + eventId)) return; State.Summary.StolenMoney = checked(State.Summary.StolenMoney + amount); State.Buildings.Add(building); State.Summary.RobbedBuildings = State.Buildings.Count; }
        /// <summary>확정된 치명타 사건을 플레이어 또는 경찰 처치로 한 번 집계합니다.</summary>
        public void Kill(string eventId, bool player)
        { if (!Accept("kill:" + eventId)) return; if (player) State.Summary.PlayerKills++; else State.Summary.PoliceKills++; }
        /// <summary>실제 체력 전환만 전달받아 사망 횟수와 다음 부활을 연결합니다.</summary>
        public void Health(bool dead)
        { if (State.Frozen) return; if (dead && !State.Dead) State.Summary.Deaths++; State.Dead = dead; }
        /// <summary>서버가 정상 접수한 신고를 기록합니다.</summary>
        public void Report(string eventId) { if (Accept("report:" + eventId)) State.Summary.Reports++; }
        /// <summary>실제 지급된 보상을 종류별로 분리합니다.</summary>
        public void Reward(string eventId, long amount, bool bounty)
        { if (amount <= 0 || !Accept("reward:" + eventId)) return; if (bounty) State.Summary.BountyMoney = checked(State.Summary.BountyMoney + amount); else State.Summary.ReportRewardMoney = checked(State.Summary.ReportRewardMoney + amount); }
        /// <summary>탈출 승인 시 장물만 기록하고 재시도에 사용할 본문을 동결합니다.</summary>
        public PlayRecordSummary Freeze(long cargo, string utc)
        {
            if (cargo < 0) throw new ArgumentOutOfRangeException(nameof(cargo));
            if (!State.Frozen) { State.Summary.ExtractedCargo = cargo; State.Summary.EndedAtUtc = utc; State.Summary.Validate(); State.Frozen = true; }
            return State.Summary.Copy();
        }
    }

    /// <summary>Cloud에 보관하는 한 회차와 서버 정산 순서입니다.</summary>
    public sealed class CompletedPlayRecord
    {
        /// <summary>표시할 회차 요약입니다.</summary>
        public PlayRecordSummary Summary { get; set; }
        /// <summary>계정 지갑 정산 Revision으로 결정되는 단조 증가 완료 순서입니다.</summary>
        public long Sequence { get; set; }
        /// <summary>Cloud에서 돈 지급을 확정한 UTC입니다.</summary>
        public string CompletedAtUtc { get; set; } = "";
        /// <summary>같은 회차 본문 변조를 거절할 지문입니다.</summary>
        public string Hash { get; set; } = "";
    }

    /// <summary>계정별 표시 기록만 보관하며 지갑·영수증을 소유하지 않습니다.</summary>
    public sealed class PlayHistoryData
    {
        /// <summary>최대 보관 개수로 정리된 완료 기록입니다.</summary>
        public List<CompletedPlayRecord> Records { get; set; } = new List<CompletedPlayRecord>();
    }

    /// <summary>서버의 보관 상한·조회 상한·중복·정렬 규칙입니다.</summary>
    public static class PlayHistoryBrick
    {
        /// <summary>한 계정에 보관할 완료 전적의 상한입니다.</summary>
        public const int RetentionCount = 100;
        /// <summary>타이틀 한 번의 조회가 반환할 최신 기록 수입니다.</summary>
        public const int QueryCount = 20;
        /// <summary>동일 회차는 재정렬하지 않으며 새 회차 저장 후 오래된 표시 기록만 제거합니다.</summary>
        public static void Append(PlayHistoryData history, CompletedPlayRecord record)
        {
            record.Summary.Validate();
            if (record.Sequence <= 0 || record.Hash != record.Summary.Fingerprint()) throw new InvalidOperationException("Invalid completed record.");
            var existing = history.Records.Find(r => r.Summary.RoundId == record.Summary.RoundId);
            if (existing != null)
            { if (existing.Hash != record.Hash || existing.Sequence != record.Sequence) throw new InvalidOperationException("Round payload conflict."); return; }
            if (history.Records.Any(r => r.Sequence == record.Sequence)) throw new InvalidOperationException("History sequence conflict.");
            history.Records.Add(record);
            history.Records = history.Records.OrderByDescending(r => r.Sequence).ThenBy(r => r.Summary.RoundId, StringComparer.Ordinal).Take(RetentionCount).ToList();
        }
        /// <summary>보관 개수와 독립적으로 최신 조회 수만 반환합니다.</summary>
        public static CompletedPlayRecord[] Recent(PlayHistoryData history) => history.Records.OrderByDescending(r => r.Sequence)
            .ThenBy(r => r.Summary.RoundId, StringComparer.Ordinal).Take(QueryCount).ToArray();

        /// <summary>확정 영수증만 표시 목록으로 옮기고 본문을 압축합니다. 호출자는 두 복사본을 같은 CAS 배치로 저장해야 합니다.</summary>
        public static bool StageCommitted(SettlementWalletRecord wallet, PlayHistoryData history, string playerId)
        {
            bool changed = false;
            foreach (var receipt in wallet.Receipts.OrderBy(r => r.Request.ExpectedRevision))
            {
                var request = receipt.Request;
                if (receipt.State != "Committed" || receipt.PlayRecordSaved || string.IsNullOrEmpty(request.RoundId)) continue;
                request.Validate();
                if (request.PlayerId != playerId || request.PlayRecord == null)
                    throw new InvalidOperationException("Accepted history payload missing; manual reconciliation required.");
                Append(history, new CompletedPlayRecord { Summary = request.PlayRecord.Copy(),
                    Sequence = checked(request.ExpectedRevision + 1), CompletedAtUtc = receipt.LastAttemptUtc, Hash = request.PlayRecordHash });
                receipt.PlayRecordSaved = true;
                request.PlayRecord = null;
                changed = true;
            }
            return changed;
        }
    }
}
