using UnityEngine;

/// <summary>
/// 플레이어가 감지하고 상호작용할 수 있는 대상이 따라야 하는 공통 기반 클래스입니다.
/// </summary>
public abstract class InteractableBehaviour : MonoBehaviour
{
    /// <summary>가까운 로컬 플레이어에게 표시할 상호작용 안내가 있는지 제공합니다.</summary>
    /// <param name="interactor">안내를 확인하는 로컬 플레이어입니다.</param>
    /// <param name="binding">현재 상호작용 Input Action의 표시용 키 이름입니다.</param>
    /// <param name="prompt">대상 이름과 행동을 설명하는 표시 문구입니다.</param>
    /// <returns>표시할 안내가 있으면 <see langword="true"/>입니다.</returns>
    public virtual bool TryGetInteractionPrompt(GameObject interactor, string binding, out string prompt)
    {
        prompt = string.Empty;
        return false;
    }

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
