using UnityEngine;

/// <summary>
/// 플레이어가 감지하고 상호작용할 수 있는 대상이 따라야 하는 공통 기반 클래스입니다.
/// </summary>
public abstract class InteractableBehaviour : MonoBehaviour
{
    /// <summary>
    /// 지정된 상호작용 주체가 현재 이 대상과 상호작용할 수 있는지 판단합니다.
    /// </summary>
    /// <param name="interactor">상호작용을 시도하는 플레이어 등의 게임 오브젝트입니다.</param>
    /// <returns>현재 상호작용할 수 있으면 <see langword="true"/>입니다.</returns>
    public abstract bool CanInteract(GameObject interactor);

    /// <summary>
    /// 지정된 상호작용 주체와 실제 상호작용을 시도합니다.
    /// </summary>
    /// <param name="interactor">상호작용을 시도하는 플레이어 등의 게임 오브젝트입니다.</param>
    /// <returns>상호작용으로 상태가 실제 변경되었으면 <see langword="true"/>입니다.</returns>
    public abstract bool TryInteract(GameObject interactor);
}
