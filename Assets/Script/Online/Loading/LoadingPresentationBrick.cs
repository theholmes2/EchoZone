using System;

namespace EchoZone.Online.Loading
{
    /// <summary>실제 완료 상태와 독립적으로 승인된 목표까지만 표시 진행률을 부드럽게 이동합니다.</summary>
    public sealed class LoadingPresentationBrick
    {
        private double start;
        private double target;
        private double elapsed;
        /// <summary>숫자와 게이지에 공통 적용할 표시 진행률입니다.</summary>
        public double Progress { get; private set; }

        /// <summary>새 작업에서 이전 보간과 표시값을 초기화합니다.</summary>
        public void Reset() => start = target = elapsed = Progress = 0;

        /// <summary>실패하면 정지하고, 성공 전에는 90%까지만 승인된 목표를 따라갑니다.</summary>
        public void Tick(double delta, double approved, bool succeeded, bool failed, double stepSeconds, double finishSeconds)
        {
            if (failed) return;
            double next = succeeded ? 1 : Math.Min(.9, FiniteNonnegative(approved));
            if (next > target)
            {
                start = Progress;
                target = next;
                elapsed = 0;
            }
            elapsed += FiniteNonnegative(delta);
            double duration = FiniteNonnegative(succeeded ? finishSeconds : stepSeconds);
            double t = duration > 0 ? Math.Min(1, elapsed / duration) : 1;
            double eased = t * t * (3 - 2 * t);
            Progress = Math.Max(Progress, Math.Min(target, start + (target - start) * eased));
        }

        private static double FiniteNonnegative(double value) =>
            double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Max(0, value);
    }
}
