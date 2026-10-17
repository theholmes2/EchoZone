using System;
using System.Collections.Generic;

namespace EchoZone.Online.Loading
{
    /// <summary>서로 다른 온라인 작업의 표시와 재시도 경계를 구분합니다.</summary>
    public enum LoadingOperationKind { Initialization, SignIn, SignUp, Create, Join, Reconnect, Migration }

    /// <summary>Unity와 독립적으로 단계 완료·세대·표시 진행률을 관리합니다.</summary>
    public sealed class LoadingProgressBrick
    {
        private double[] weights = Array.Empty<double>();
        private bool[] completed = Array.Empty<bool>();
        private double elapsed;
        private double finishElapsed;
        /// <summary>이전 비동기 결과를 거절할 현재 작업 세대입니다.</summary>
        public long Generation { get; private set; }
        /// <summary>현재 표시할 온라인 작업입니다.</summary>
        public LoadingOperationKind Kind { get; private set; }
        /// <summary>진행 중인 단계의 인덱스입니다.</summary>
        public int Stage { get; private set; }
        /// <summary>UI가 아니라 작업 소유자가 확정한 성공 여부입니다.</summary>
        public bool Succeeded { get; private set; }
        /// <summary>작업 소유자 또는 제한 시간에 의해 실패했는지 나타냅니다.</summary>
        public bool Failed { get; private set; }
        /// <summary>실패 단계와 함께 표시할 이유입니다.</summary>
        public string Error { get; private set; } = string.Empty;
        /// <summary>완료 보간까지 포함한 0~1 표시 진행률입니다.</summary>
        public double Progress { get; private set; }
        /// <summary>이전 요청 정리가 완료되어 재시도가 허용되었는지 나타냅니다.</summary>
        public bool CanRetry { get; private set; }
        /// <summary>안전한 로컬 정리 경로가 준비되었는지 나타냅니다.</summary>
        public bool CanReturn { get; private set; }
        /// <summary>현재 작업에 부여된 단계별 완료 상태입니다.</summary>
        public IReadOnlyList<bool> Completed => Array.AsReadOnly(completed);
        /// <summary>새 요청의 세대를 발급하며 이전 표시를 초기화합니다.</summary>
        public long Begin(LoadingOperationKind kind, double[] stageWeights)
        {
            Generation++; Kind = kind; weights = (double[])stageWeights.Clone();
            completed = new bool[weights.Length]; Stage = 0; elapsed = finishElapsed = Progress = 0;
            Succeeded = Failed = CanRetry = CanReturn = false; Error = string.Empty;
            return Generation;
        }
        /// <summary>현재 진행 중인 세대의 응답만 적용할 수 있습니다.</summary>
        public bool Accepts(long id) => id == Generation && !Failed && !Succeeded;
        /// <summary>실제로 끝난 단계를 멱등적으로 반영합니다.</summary>
        public bool Complete(long id, int index)
        {
            if (!Accepts(id) || index < 0 || index >= completed.Length) return false;
            completed[index] = true;
            double total = 0, done = 0;
            for (int i = 0; i < weights.Length; i++) { double w = Math.Max(0, weights[i]); total += w; if (completed[i]) done += w; }
            Progress = total > 0 ? Math.Min(.9, .9 * done / total) : 0;
            Stage = 0; while (Stage < completed.Length && completed[Stage]) Stage++;
            return true;
        }
        /// <summary>모든 실제 단계가 끝났을 때만 성공 보간을 허용합니다.</summary>
        public bool Succeed(long id)
        {
            if (!Accepts(id) || Stage != completed.Length) return false;
            Succeeded = true; Progress = .9; return true;
        }
        /// <summary>실패 즉시 세대를 바꿔 늦은 성공 응답을 무효화합니다.</summary>
        public bool Fail(long id, string error)
        {
            if (!Accepts(id)) return false;
            Generation++; Failed = true; Error = error; CanRetry = CanReturn = false; return true;
        }
        /// <summary>정리 상태가 달라질 때 실패 화면의 버튼 권한만 갱신합니다.</summary>
        public void SetActions(bool retry, bool title) { CanRetry = Failed && retry; CanReturn = Failed && title; }
        /// <summary>타이틀 복귀 또는 재시도 시작 전에 이전 세대를 무효화합니다.</summary>
        public void Invalidate() { Generation++; CanRetry = CanReturn = false; }
        /// <summary>단계 완료와 무관한 시간 경과는 성공을 만들지 않으며 성공 후에만 마지막 10%를 보간합니다.</summary>
        public void Tick(double delta, double finishSeconds)
        {
            delta = double.IsNaN(delta) || double.IsInfinity(delta) ? 0 : Math.Max(0, delta);
            elapsed += delta;
            if (Succeeded) { finishElapsed += delta; Progress = .9 + .1 * Math.Min(1, finishElapsed / Math.Max(.01, finishSeconds)); }
        }
        /// <summary>짧은 작업의 깜빡임을 피하고 실패는 즉시 표시합니다.</summary>
        public bool ShouldShow(double delay) => Failed || elapsed >= delay;
    }
}
