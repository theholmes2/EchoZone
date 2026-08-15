using System;
using UnityEngine;

/// <summary>
/// GameObject에 순수 상호작용 잠금 Brick을 제공하는 Unity 연결 컴포넌트입니다.
/// </summary>
public sealed class InteractionLockBehaviour : MonoBehaviour
{
    /// <summary>이 대상의 상호작용 임계 구역을 보호하는 잠금 Brick입니다.</summary>
    private readonly InteractionExecutionLock executionLock = new();

    /// <summary>상호작용 작업을 대상별 잠금 안에서 실행합니다.</summary>
    /// <param name="interaction">잠금 안에서 실행할 상호작용 작업입니다.</param>
    /// <returns>상호작용 작업이 성공했으면 <see langword="true"/>입니다.</returns>
    public bool Execute(Func<bool> interaction)
    {
        return executionLock.Execute(interaction);
    }
}
