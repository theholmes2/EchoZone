using System;

namespace EchoZone.Heist
{
    /// <summary>Unity와 통신을 모르는 개인 세션 지갑입니다. 장물은 탈출 승인 전 잔액에 포함하지 않습니다.</summary>
    public sealed class WalletSessionBrick
    {
        /// <summary>클라우드 기준 잔액을 읽었는지 나타냅니다.</summary>
        public bool Loaded { get; private set; }
        /// <summary>현재 게임의 수입과 지출을 반영한 개인 잔액입니다.</summary>
        public long Balance { get; private set; }
        /// <summary>읽어온 계정 버전이며 정산 조건부 쓰기에 사용합니다.</summary>
        public long Revision { get; private set; }
        /// <summary>탈출 승인 이후 금액이 고정되어 변경할 수 없는 상태입니다.</summary>
        public bool Escaping { get; private set; }
        /// <summary>클라우드가 정산을 확인한 상태입니다.</summary>
        public bool Settled { get; private set; }
        /// <summary>매 탈출마다 생성하고 재시도에는 유지하는 정산 식별자입니다.</summary>
        public string SettlementId { get; private set; } = string.Empty;
        /// <summary>지갑 로드 전에 발생한 서버 보상은 잃지 않고 누적합니다.</summary>
        private long pendingCredit;

        /// <summary>읽기 성공 때만 초기화합니다. 재연결로 같은 지갑을 재초기화하지 않습니다.</summary>
        public void Load(long balance, long revision)
        {
            if (Loaded) return;
            if (balance < 0 || revision < 0) throw new ArgumentOutOfRangeException();
            Balance = checked(balance + pendingCredit);
            Revision = revision; Loaded = true; pendingCredit = 0;
            Escaping = Settled = false;
        }

        /// <summary>서버가 검증한 감사비·현상금을 반영합니다.</summary>
        public void Credit(long amount)
        {
            if (amount <= 0 || Escaping) return;
            if (Loaded) Balance = checked(Balance + amount);
            else pendingCredit = checked(pendingCredit + amount);
        }

        /// <summary>향후 상점 등 서버 구매 처리에서 사용할 잔액 검증 및 차감입니다.</summary>
        public bool TrySpend(long amount)
        {
            if (!Loaded || Escaping || amount <= 0 || Balance < amount) return false;
            Balance -= amount; return true;
        }

        /// <summary>서버가 승인한 펫 장물을 한 번만 더하고 재전송할 최종 금액을 고정합니다.</summary>
        public bool BeginEscape(long cargo)
        {
            if (!Loaded || Escaping || cargo < 0) return false;
            Balance = checked(Balance + cargo);
            SettlementId = Guid.NewGuid().ToString("N");
            Escaping = true; return true;
        }

        /// <summary>전송한 잔액과 서버 응답이 일치할 때만 정산 완료합니다.</summary>
        public void Confirm(long balance, long revision, string settlementId)
        {
            if (!Escaping || Settled || settlementId != SettlementId || balance != Balance || revision != Revision + 1)
                throw new InvalidOperationException("Unexpected wallet settlement response.");
            Revision = revision; Settled = true;
        }
        /// <summary>Cloud의 최신 Revision과 정산 영수증을 대조합니다. 모르는 최신 쓰기는 덮어쓰지 않습니다.</summary>
        public void VerifyCloud(long balance, long revision, string settlementId)
        {
            if (!Loaded) return;
            if (revision == Revision) return;
            if (Escaping && !Settled && settlementId == SettlementId && balance == Balance && revision == Revision + 1)
            { Confirm(balance, revision, settlementId); return; }
            throw new InvalidOperationException("Migration wallet revision differs from Cloud; manual reconciliation required.");
        }
        /// <summary>로드 전 보상까지 포함한 지갑 복사본입니다.</summary>
        public string Export() => Newtonsoft.Json.JsonConvert.SerializeObject(new State { loaded = Loaded, balance = Balance,
            revision = Revision, escaping = Escaping, settled = Settled, settlementId = SettlementId, pendingCredit = pendingCredit });
        /// <summary>같은 세션 지갑을 새 호스트에서 복원합니다.</summary>
        public static WalletSessionBrick Restore(string json)
        {
            var s = Newtonsoft.Json.JsonConvert.DeserializeObject<State>(json);
            if (s == null || s.balance < 0 || s.revision < 0 || s.pendingCredit < 0) throw new InvalidOperationException("Invalid migration wallet");
            return new WalletSessionBrick { Loaded = s.loaded, Balance = s.balance, Revision = s.revision, Escaping = s.escaping,
                Settled = s.settled, SettlementId = s.settlementId, pendingCredit = s.pendingCredit };
        }
        /// <summary>지갑의 명시적 직렬화 필드입니다.</summary>
        private sealed class State
        {
            public bool loaded, escaping, settled;
            public long balance, revision, pendingCredit;
            public string settlementId;
        }
    }
}
