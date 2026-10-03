using System;
namespace EchoZone.Enemy
{
    /// <summary>마지막 목격 지점까지 이동하는 시간과 도착 후 수색 시간을 분리합니다.</summary>
    public sealed class PolicePursuitBrick
    {
        /// <summary>추격 회차가 진행 중인지 나타냅니다.</summary>
        public bool Active { get; private set; }
        /// <summary>마지막 목격 지점에 도착해 수색 중인지 나타냅니다.</summary>
        public bool Arrived { get; private set; }
        /// <summary>이동 또는 도착 후 수색 단계의 남은 시간입니다.</summary>
        public float Remaining { get; private set; }
        /// <summary>대상을 다시 확인하거나 순찰로 복귀하면 회차를 초기화합니다.</summary>
        public void Reset() { Active = Arrived = false; Remaining = 0; }
        /// <summary>시야 상실 시 한 번만 이동 제한시간을 시작합니다.</summary>
        public void Begin(float travel) { if (Active) return; Active = true; Arrived = false; Remaining = Math.Max(0, travel); }
        /// <summary>도착을 확인한 프레임부터 수색 시간을 부여하고 이전 이동 시간을 사용하지 않습니다.</summary>
        public bool Tick(float delta, bool reached, bool pathFailed, float search)
        {
            if (!Active) return false;
            if (!Arrived && reached) { Arrived = true; Remaining = Math.Max(0, search); return Remaining <= 0; }
            if (!Arrived && pathFailed) return true;
            Remaining = Math.Max(0, Remaining - Math.Max(0, delta));
            return Remaining <= 0;
        }
        /// <summary>호스트의 절대 시각 없이 추격 단계를 복원합니다.</summary>
        public void Restore(bool active, bool arrived, float remaining)
        { Active = active; Arrived = arrived; Remaining = Math.Max(0, remaining); }
    }
}
