using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 감지 범위에 들어오고 나가는 상호작용 후보를 탐지합니다.
/// 후보의 실제 상호작용 실행은 담당하지 않습니다.
/// </summary>
public class PlayerInteractionSensor : MonoBehaviour
{
    /// <summary>상호작용을 시도하는 플레이어의 최상위 게임 오브젝트입니다.</summary>
    private GameObject interactor;

    /// <summary>현재 감지 범위 안에서 발견한 상호작용 후보 목록입니다.</summary>
    private readonly List<InteractableBehaviour> candidates = new();

    /// <summary>외부에서 읽을 수 있는 상호작용 후보 목록입니다.</summary>
    public IReadOnlyList<InteractableBehaviour> Candidates => candidates;

    /// <summary>센서를 소유한 최상위 플레이어 오브젝트를 상호작용 주체로 설정합니다.</summary>
    private void Awake()
    {
        // transform.root는 나를 둘러싼 가장 최상위 부모 오브젝트의 transform을 가져옵니다.
        interactor = transform.root.gameObject;
    }


    /// <summary>콜라이더가 감지 범위에 들어왔을 때 호출됩니다.</summary>
    /// <param name="other">감지 범위에 들어온 콜라이더입니다.</param>
    private void OnTriggerEnter(Collider other)
    {
        InteractableBehaviour interactable;
        bool found = TryFindInteractable(other,out interactable);

        if (found == false)
            return;

        for (int i = 0; i < candidates.Count; i++) {
           if( candidates[i]== interactable)
            {
                return;
            }

        }

            bool canInteract = interactable.CanInteract(interactor);

            if (canInteract)
            {
                candidates.Add(interactable);

            }

      
    }

    /// <summary>콜라이더가 감지 범위에서 나갔을 때 호출됩니다.</summary>
    /// <param name="other">감지 범위에서 나간 콜라이더입니다.</param>
    private void OnTriggerExit(Collider other)
    {
        InteractableBehaviour interactable;
        bool found = TryFindInteractable(other, out interactable);

        if (found == false)
            return;

        candidates.Remove(interactable);


    }

    /// <summary>콜라이더 또는 그 부모에서 상호작용 가능한 컴포넌트를 찾습니다.</summary>
    /// <param name="other">검색을 시작할 콜라이더입니다.</param>
    /// <param name="interactable">찾은 상호작용 컴포넌트입니다.</param>
    /// <returns>상호작용 컴포넌트를 찾았으면 <see langword="true"/>입니다.</returns>
    private bool TryFindInteractable(Collider other, out InteractableBehaviour interactable)
    {
        
        interactable = other.GetComponentInParent<InteractableBehaviour>();
        if (interactable != null) {

            return true;
        }
        return false;
    }
}
