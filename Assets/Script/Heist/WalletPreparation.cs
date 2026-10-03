#nullable disable
using System;
using System.Threading.Tasks;

namespace EchoZone.Heist
{
    /// <summary>단일 방 정책하의 최초 준비 순서입니다. 원자적 create-if-absent를 보장하는 잠금은 아닙니다.</summary>
    public static class WalletPreparation
    {
        /// <summary>기존 값에는 쓰지 않고 누락 시에만 빈 레코드를 만든 뒤 저장 결과를 반드시 재조회합니다.</summary>
        public static async Task<SettlementWalletRecord> EnsureAsync(
            Func<Task<SettlementWalletRecord>> read, Func<SettlementWalletRecord, Task> create)
        {
            var existing = await read();
            if (existing != null) return existing;
            await create(new SettlementWalletRecord());
            return await read() ?? throw new InvalidOperationException("Wallet creation not confirmed; admission blocked.");
        }
    }

    /// <summary>한 클라이언트 흐름의 준비 요청 중복과 실패 후 빠른 수동 재시도를 차단합니다.</summary>
    public sealed class WalletAdmissionGate
    {
        /// <summary>요청 결과가 아직 확정되지 않았는지 나타냅니다.</summary>
        public bool Busy { get; private set; }
        /// <summary>실패 후 다음 수동 시도를 허용할 시각입니다.</summary>
        public double RetryAt { get; private set; }
        /// <summary>진행 중 또는 대기시간 이내에는 새 요청을 시작하지 않습니다.</summary>
        public bool TryBegin(double now)
        { if (Busy || now < RetryAt) return false; Busy = true; return true; }
        /// <summary>요청 완료 뒤에만 잠금을 풀며 실패 시 최소 재시도 간격을 둡니다.</summary>
        public void Complete(bool success, double now, double delay)
        { Busy = false; RetryAt = success ? 0 : now + Math.Max(1, delay); }
    }
}
