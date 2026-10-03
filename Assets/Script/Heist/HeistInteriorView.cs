using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>내부 공간을 따로 만들기 전 입장 동안 외형과 피격 Collider를 감춥니다.</summary>
    public sealed class HeistInteriorView : MonoBehaviour
    {
        /// <summary>다른 시야 표시와 충돌하지 않도록 forceRenderingOff만 관리합니다.</summary>
        private Renderer[] renderers;
        /// <summary>내부에서 외부 총알을 맞지 않게 할 비트리거 충돌체입니다.</summary>
        private Collider[] colliders;
        /// <summary>입장 전 충돌체 활성 상태입니다.</summary>
        private bool[] enabledBefore;
        /// <summary>현재 내부 표시 상태입니다.</summary>
        private bool inside;
        /// <summary>외형과 충돌체를 한 번 수집합니다.</summary>
        private void Awake()
        { renderers = GetComponentsInChildren<Renderer>(true); colliders = GetComponentsInChildren<Collider>(true); enabledBefore = new bool[colliders.Length]; }
        /// <summary>입장 상태가 바뀔 때만 표시와 충돌을 전환합니다.</summary>
        public void SetInside(bool value)
        {
            if (inside == value) return;
            inside = value;
            GetComponent<EchoZone.Player.View.PlayerHealthBarGlue>()?.SetHidden(EchoZone.Player.View.HealthBarHiddenReason.Interior, value);
            foreach (var r in renderers) if (r != null) r.forceRenderingOff = value;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null || colliders[i].isTrigger) continue;
                if (value) { enabledBefore[i] = colliders[i].enabled; colliders[i].enabled = false; }
                else colliders[i].enabled = enabledBefore[i];
            }
        }
        /// <summary>풀 반환이나 종료 때 숨김을 복구합니다.</summary>
        private void OnDisable() => SetInside(false);
    }
}
