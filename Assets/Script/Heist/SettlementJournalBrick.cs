#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;

namespace EchoZone.Heist
{
    /// <summary>탈출 성공 뒤 인벤토리에서 회수하고 지갑에 환불할 서버 승인 항목입니다.</summary>
    public sealed class SettlementReturnItem
    {
        /// <summary>서버 카탈로그에서 반환 규칙을 다시 검증할 아이템 ID입니다.</summary>
        public string ItemId { get; set; } = "";
        /// <summary>코인·탄약처럼 중첩 가능한 반환 수량입니다.</summary>
        public int Quantity { get; set; }
        /// <summary>총기 한 개를 정확히 식별하는 서버 발급 ID입니다.</summary>
        public string InstanceId { get; set; } = "";
        /// <summary>인벤토리가 아니라 현재 장착 슬롯에서 회수할 총기인지 나타냅니다.</summary>
        public bool Equipped { get; set; }
        /// <summary>탈출 승인 시 서버가 계산해 최종 잔액에 포함한 반환액입니다.</summary>
        public long Credit { get; set; }

        /// <summary>같은 정산 재시도에서 반환 대상과 금액이 바뀌지 않았는지 비교합니다.</summary>
        public string StableKey() => $"{ItemId}\n{Quantity}\n{InstanceId}\n{Equipped}\n{Credit}";
    }

    /// <summary>접수 후 변경하지 않는 서버 승인 탈출 요청입니다. Unity와 Cloud 양쪽이 같은 계약을 사용합니다.</summary>
    public sealed class SettlementRequest
    {
        /// <summary>돈을 받을 인증 계정입니다.</summary>
        public string PlayerId { get; set; } = "";
        /// <summary>최초 승인한 방이며 재시도 시 현재 방으로 바꾸지 않습니다.</summary>
        public string SessionId { get; set; } = "";
        /// <summary>장물과 펫이 속한 실행 식별자입니다.</summary>
        public string RunId { get; set; } = "";
        /// <summary>모든 재시도에 유지하는 멱등성 키입니다.</summary>
        public string SettlementId { get; set; } = "";
        /// <summary>승인 시 읽은 지갑 버전입니다.</summary>
        public long ExpectedRevision { get; set; }
        /// <summary>장물까지 합산한 고정된 최종 잔액입니다.</summary>
        public long Balance { get; set; }
        /// <summary>완료 후 종료할 고정 펫 식별자들입니다.</summary>
        public string[] PetIds { get; set; } = Array.Empty<string>();
        /// <summary>완료 후 다시 지급하지 않을 장물 원장 식별자들입니다.</summary>
        public string[] LedgerIds { get; set; } = Array.Empty<string>();
        /// <summary>Cloud 성공 뒤 서버가 인벤토리에서 제거할 코인·탄약·총기 목록입니다.</summary>
        public SettlementReturnItem[] Returns { get; set; } = Array.Empty<SettlementReturnItem>();

        /// <summary>유효한 크기의 불변 요청만 받습니다. 동일 집합의 입력 순서는 비교에 영향을 주지 않습니다.</summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PlayerId) || string.IsNullOrWhiteSpace(SessionId) ||
                string.IsNullOrWhiteSpace(RunId) || !Guid.TryParseExact(SettlementId, "N", out _) ||
                Balance < 0 || Balance > 1000000000000L || ExpectedRevision < 0 ||
                PetIds == null || LedgerIds == null || Returns == null || PetIds.Length > 128 ||
                LedgerIds.Length > 4096 || Returns.Length > 256)
                throw new InvalidOperationException("Invalid settlement request.");
            ValidateIds(PetIds); ValidateIds(LedgerIds);
            if (Returns.Any(r => r == null || string.IsNullOrWhiteSpace(r.ItemId) || r.ItemId.Length > 256 ||
                r.Quantity <= 0 || r.Credit < 0 || r.Credit > 1000000000000L ||
                (!string.IsNullOrEmpty(r.InstanceId) && r.Quantity != 1) ||
                (r.Equipped && string.IsNullOrEmpty(r.InstanceId))) ||
                Returns.Select(r => r.StableKey()).Distinct(StringComparer.Ordinal).Count() != Returns.Length)
                throw new InvalidOperationException("Invalid settlement returns.");
        }

        /// <summary>중복 키와 비어 있는 대상 ID를 거절합니다.</summary>
        private static void ValidateIds(string[] ids)
        {
            if (ids.Any(string.IsNullOrWhiteSpace) || ids.Any(s => s.Length > 256) || ids.Distinct().Count() != ids.Length)
                throw new InvalidOperationException("Invalid settlement targets.");
        }

        /// <summary>같은 정산 ID로 내용이 바뀌면 재시도로 인정하지 않습니다.</summary>
        public bool SamePayload(SettlementRequest other)
        {
            if (other == null) return false;
            var leftPets = PetIds ?? Array.Empty<string>(); var rightPets = other.PetIds ?? Array.Empty<string>();
            var leftLedgers = LedgerIds ?? Array.Empty<string>(); var rightLedgers = other.LedgerIds ?? Array.Empty<string>();
            var leftReturns = Returns ?? Array.Empty<SettlementReturnItem>();
            var rightReturns = other.Returns ?? Array.Empty<SettlementReturnItem>();
            return PlayerId == other.PlayerId && SessionId == other.SessionId && RunId == other.RunId &&
                SettlementId == other.SettlementId && ExpectedRevision == other.ExpectedRevision && Balance == other.Balance &&
                leftPets.OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(rightPets.OrderBy(s => s, StringComparer.Ordinal)) &&
                leftLedgers.OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(rightLedgers.OrderBy(s => s, StringComparer.Ordinal)) &&
                leftReturns.Select(r => r?.StableKey() ?? "<null>").OrderBy(s => s, StringComparer.Ordinal)
                    .SequenceEqual(rightReturns.Select(r => r?.StableKey() ?? "<null>").OrderBy(s => s, StringComparer.Ordinal));
        }
    }

    /// <summary>서버가 접수한 요청과 처리 상태입니다. 잔액과 같은 Cloud Save 항목에 저장합니다.</summary>
    public sealed class SettlementJournalEntry
    {
        /// <summary>접수 시 동결한 요청 본문입니다.</summary>
        public SettlementRequest Request { get; set; }
        /// <summary>Prepared, Committed, Conflict 중 영속 처리 단계입니다.</summary>
        public string State { get; set; } = "Prepared";
        /// <summary>Cloud 서버의 최초 접수 UTC입니다.</summary>
        public string CreatedAtUtc { get; set; } = "";
        /// <summary>Cloud가 확인한 처리 시도 횟수입니다.</summary>
        public int Attempts { get; set; }
        /// <summary>Cloud 서버의 마지막 처리 UTC입니다.</summary>
        public string LastAttemptUtc { get; set; } = "";
    }

    /// <summary>기존 extraction_wallet_v1 필드를 보존하며 영속 outbox와 영수증을 함께 보관하는 봉투입니다.</summary>
    public class SettlementWalletRecord
    {
        /// <summary>마지막 확정 잔액입니다.</summary>
        public long Balance { get; set; }
        /// <summary>조건부 쓰기 기준 버전입니다.</summary>
        public long Revision { get; set; }
        /// <summary>마지막 성공 요청의 출처 방입니다.</summary>
        public string LastSessionId { get; set; } = "";
        /// <summary>마지막 성공 요청의 멱등성 키입니다.</summary>
        public string LastSettlementId { get; set; } = "";
        /// <summary>마지막 잔액 반영 UTC입니다.</summary>
        public string SavedAtUtc { get; set; } = "";
        /// <summary>계정당 한 개의 미완료 요청만 허용하여 서로 다른 잔액 덮어쓰기를 막습니다.</summary>
        public SettlementJournalEntry Pending { get; set; }
        /// <summary>이전 요청 재전송과 오래된 Snapshot 복구를 검증하는 불변 영수증입니다.</summary>
        public List<SettlementJournalEntry> Receipts { get; set; } = new List<SettlementJournalEntry>();
        /// <summary>신고 감사비 수령자 또는 자동 회수를 승인한 호스트가 보관하는 월드 회수 요청 인덱스입니다.</summary>
        public List<RecoveryJournalEntry> Recoveries { get; set; } = new List<RecoveryJournalEntry>();
    }

    /// <summary>방이 삭제되어도 RunId와 회수 본문을 찾을 수 있는 Protected 계정 인덱스입니다. 잔액은 직접 변경하지 않습니다.</summary>
    public sealed class RecoveryJournalEntry
    {
        /// <summary>고정 회수 요청 키입니다.</summary>
        public string SettlementId { get; set; } = "";
        /// <summary>회수 원장이 속한 실행입니다.</summary>
        public string RunId { get; set; } = "";
        /// <summary>재전송 비교용 고정 요청 데이터입니다.</summary>
        public string PayloadJson { get; set; } = "";
        /// <summary>Prepared 또는 CheckpointConfirmed입니다.</summary>
        public string State { get; set; } = "Prepared";
    }

    /// <summary>저장소와 무관한 접수·중복 검증·정산 계산입니다. 호출자는 변경한 봉투를 writeLock으로 저장해야 합니다.</summary>
    public static class SettlementJournalBrick
    {
        /// <summary>미완료 요청을 등록합니다. 같은 ID의 변경된 본문, 다른 미완료 요청, 오래된 Revision은 거절합니다.</summary>
        public static void Prepare(SettlementWalletRecord wallet, SettlementRequest request, string utc)
        {
            request.Validate();
            var receipt = wallet.Receipts.Find(r => r.Request.SettlementId == request.SettlementId);
            if (receipt != null) { RequireSame(receipt.Request, request); return; }
            if (wallet.Pending != null) { RequireSame(wallet.Pending.Request, request); return; }
            if (wallet.LastSettlementId == request.SettlementId)
                throw new InvalidOperationException("Legacy receipt requires manual reconciliation.");
            if (wallet.Revision != request.ExpectedRevision) throw new InvalidOperationException("Revision conflict; manual reconciliation required.");
            // 영수증을 조용히 삭제하면 오래된 요청의 멱등성이 깨지므로 보관 한도에서는 안전 보류합니다.
            if (wallet.Receipts.Count >= 128) throw new InvalidOperationException("Receipt archive required; manual reconciliation required.");
            wallet.Pending = new SettlementJournalEntry { Request = request, CreatedAtUtc = utc };
        }

        /// <summary>같은 항목의 Pending→영수증→잔액 변경을 한 번의 조건부 쓰기로 반영합니다.</summary>
        public static void Commit(SettlementWalletRecord wallet, string utc)
        {
            var pending = wallet.Pending;
            if (pending == null) return;
            pending.Attempts++; pending.LastAttemptUtc = utc;
            if (wallet.Revision != pending.Request.ExpectedRevision)
            { pending.State = "Conflict"; return; }
            wallet.Balance = pending.Request.Balance; wallet.Revision = checked(wallet.Revision + 1);
            wallet.LastSessionId = pending.Request.SessionId; wallet.LastSettlementId = pending.Request.SettlementId;
            wallet.SavedAtUtc = utc; pending.State = "Committed";
            wallet.Receipts.Add(pending); wallet.Pending = null;
        }

        /// <summary>모든 고정 금액·버전·대상 필드가 동일한 요청만 재전송으로 승인합니다.</summary>
        private static void RequireSame(SettlementRequest stored, SettlementRequest request)
        {
            if (!stored.SamePayload(request)) throw new InvalidOperationException("Settlement ID or payload conflict; manual reconciliation required.");
        }

        /// <summary>프레임마다 요청하지 않도록 재시도 지연을 상한까지 증가시킵니다.</summary>
        public static double RetryDelay(int failures, double first, double maximum) =>
            Math.Min(Math.Max(first, maximum), Math.Max(1, first) * Math.Pow(2, Math.Min(16, Math.Max(0, failures - 1))));
    }
}
