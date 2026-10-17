namespace EchoZone.Heist
{
    /// <summary>화면 닫기·계정 전환·중복 조회에서 늦은 응답을 차단하는 순수 요청 상태입니다.</summary>
    public sealed class PlayHistoryQueryBrick
    {
        /// <summary>현재 화면을 연 계정입니다.</summary>
        public string PlayerId { get; private set; } = "";
        /// <summary>목록이 열린 상태인지 나타냅니다.</summary>
        public bool Opened { get; private set; }
        /// <summary>현재 화면 세대의 조회가 진행 중인지 나타냅니다.</summary>
        public bool Busy { get; private set; }
        private int generation;
        /// <summary>계정별 새 화면을 시작하여 이전 응답을 무효화합니다.</summary>
        public void Open(string player) { Close(); PlayerId = player; Opened = true; }
        /// <summary>닫힌 화면에 도착하는 응답을 무효화합니다.</summary>
        public void Close() { generation++; Opened = Busy = false; PlayerId = ""; }
        /// <summary>중복 조회를 거절하며 응답 검증용 세대를 발급합니다.</summary>
        public bool TryBegin(out int token)
        { token = generation; if (!Opened || Busy) return false; Busy = true; token = ++generation; return true; }
        /// <summary>현재 화면과 로그인 계정이 동일한 응답만 승인합니다.</summary>
        public bool Accepts(int token, string player) => Opened && Busy && generation == token && PlayerId == player;
        /// <summary>유효 응답만 현재 조회를 종료합니다.</summary>
        public void Complete(int token, string player) { if (Accepts(token, player)) Busy = false; }
    }
}
