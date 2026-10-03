namespace EchoZone.Player.View
{
    /// <summary>서로 독립적인 숨김 사유입니다.</summary>
    [System.Flags] public enum HealthBarHiddenReason { None = 0, Interior = 1, Sight = 2, Lifecycle = 4 }
    /// <summary>한 사유 해제가 다른 숨김 사유를 지우지 않도록 표시 여부만 계산합니다.</summary>
    public sealed class HealthBarVisibilityBrick
    {
        /// <summary>현재 적용된 숨김 사유입니다.</summary>
        private HealthBarHiddenReason reasons;
        /// <summary>숨김 사유가 모두 해제되어야 표시합니다.</summary>
        public bool Visible => reasons == HealthBarHiddenReason.None;
        /// <summary>지정한 사유만 설정 또는 해제합니다.</summary>
        public void Set(HealthBarHiddenReason reason, bool hidden)
        { if (hidden) reasons |= reason; else reasons &= ~reason; }
        /// <summary>풀 반환 때 이전 개체의 사유를 제거합니다.</summary>
        public void Reset() => reasons = HealthBarHiddenReason.None;
    }
}
