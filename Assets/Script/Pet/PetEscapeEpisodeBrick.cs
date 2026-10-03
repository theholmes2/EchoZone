using System;

namespace EchoZone.Pet
{
    /// <summary>시간과 횟수만으로 한 번의 펫 도주 회차를 판정합니다. Unity 시계와 이동을 사용하지 않습니다.</summary>
    public sealed class PetEscapeEpisodeBrick
    {
        /// <summary>실제로 새 도주에 진입한 횟수입니다.</summary>
        public int Count { get; private set; }
        /// <summary>플레이어 회수 기회를 보장할 남은 시간입니다.</summary>
        public float GraceRemaining { get; private set; }
        /// <summary>현재 도주 시도의 최대 남은 시간입니다.</summary>
        public float FailureRemaining { get; private set; }
        /// <summary>제한시간 내 안전 위치에 도착하지 못한 결정적 실패입니다.</summary>
        public bool Failed { get; private set; }
        /// <summary>회차가 시작됐는지 나타냅니다.</summary>
        public bool Active => Count > 0;
        /// <summary>획득으로 새 회차를 준비합니다. 재접속에는 호출하지 않습니다.</summary>
        public void Reset() { Count = 0; GraceRemaining = FailureRemaining = 0; Failed = false; }
        /// <summary>새 도주 진입만 세며 경로 재계산에는 호출하지 않습니다.</summary>
        public bool TryBegin(int maximum, float grace, float failure)
        {
            if (Failed || Count >= Math.Max(1, maximum)) return false;
            if (!Active) GraceRemaining = Math.Max(0, grace);
            Count++; FailureRemaining = Math.Max(0, failure); return true;
        }
        /// <summary>중앙 서버 루프에서만 남은 시간을 소모하므로 복구 장벽에서는 정지합니다.</summary>
        public void Tick(float delta, bool fleeing)
        {
            if (!Active) return;
            GraceRemaining = Math.Max(0, GraceRemaining - Math.Max(0, delta));
            if (!fleeing) return;
            FailureRemaining = Math.Max(0, FailureRemaining - Math.Max(0, delta));
            if (FailureRemaining <= 0) Failed = true;
        }
        /// <summary>횟수 소진 또는 결정적 경로 실패 후에도 최소 회수 시간은 보장합니다.</summary>
        public bool CanCollect(int maximum) => Active && GraceRemaining <= 0 && (Count >= Math.Max(1, maximum) || Failed);
        /// <summary>남은 시간으로 복원하며 누락되거나 음수인 값을 안전하게 보정합니다.</summary>
        public void Restore(int count, float grace, float failure, bool failed)
        { Count = Math.Max(0, count); GraceRemaining = Math.Max(0, grace); FailureRemaining = Math.Max(0, failure); Failed = failed; }
    }
}
