using System;

/// <summary>
/// 하나의 상호작용 대상에 둘 이상의 실행이 동시에 진입하지 못하도록 하는 순수 실행 잠금 Brick입니다.
/// </summary>
public sealed class InteractionExecutionLock
{
    /// <summary>상호작용 임계 구역에 한 실행만 진입하도록 동기화하는 전용 객체입니다.</summary>
    private readonly object syncRoot = new();

    /// <summary>
    /// 다른 실행이 끝날 때까지 기다린 뒤 상호작용 작업을 잠금 안에서 실행합니다.
    /// </summary>
    /// <param name="interaction">잠금을 획득한 뒤 실행할 상호작용 작업입니다.</param>
    /// <returns>상호작용 작업이 성공했으면 <see langword="true"/>입니다.</returns>
    public bool Execute(Func<bool> interaction)
    {
        if (interaction == null)
        {
            return false;
        }

        lock (syncRoot)
        {
            return interaction.Invoke();
        }
    }
}
