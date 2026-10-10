/// <summary>접속 화면을 한 번 거친 뒤 연결·복구·종료 작업이 끝났을 때만 첫 메뉴로 돌아갑니다.</summary>
public sealed class TitleReturnBrick
{
    private bool awaitingReturn;

    /// <summary>네트워크 종료가 완료된 첫 프레임만 반환하며 코드 입력 중에는 메뉴를 덮어쓰지 않습니다.</summary>
    public bool ShouldReturn(bool connected, bool recovering, bool shuttingDown, bool requestPending)
    {
        if (connected || recovering || shuttingDown) awaitingReturn = true;
        if (!awaitingReturn || connected || recovering || shuttingDown || requestPending) return false;
        awaitingReturn = false;
        return true;
    }
}
