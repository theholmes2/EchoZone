using System;
using System.Text;
using EchoZone.Pet;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace EchoZone.Heist
{
    /// <summary>별도 건물 레이와 복제된 상태를 로컬 툴팁에 연결합니다. 기존 HUD가 수동 갱신합니다.</summary>
    public sealed class BuildingHoverGlue : MonoBehaviour
    {
        /// <summary>탐색과 배치 설정입니다.</summary>
        [SerializeField] private BuildingHoverConfig config;
        /// <summary>표시 전용 컴포넌트입니다.</summary>
        [SerializeField] private BuildingHoverView view;
        /// <summary>현재 커서가 가리키는 건물입니다.</summary>
        private HeistBuildingSite hovered;
        /// <summary>문자열 갱신 제한 시각입니다.</summary>
        private float refreshAt;
        /// <summary>위치만 이동할 때 재사용할 표시 문자열입니다.</summary>
        private string cachedText;

        /// <summary>로컬 오너가 존재할 때만 별도의 건물 마우스 레이를 검사합니다.</summary>
        public void ManualUpdate()
        {
            var manager = NetworkManager.Singleton;
            var player = manager != null && manager.IsClient ? manager.LocalClient?.PlayerObject : null;
            var world = HeistWorldGlue.Instance; var camera = Camera.main; var mouse = Mouse.current;
            if (config == null || player == null || world == null || !world.IsSpawned || camera == null || mouse == null ||
                (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            { hovered = null; view?.Hide(); return; }
            Vector2 point = mouse.position.ReadValue();
            var ray = camera.ScreenPointToRay(point);
            var site = Physics.Raycast(ray, out var hit, config.RayDistance, config.BuildingLayers, QueryTriggerInteraction.Ignore)
                ? hit.collider.GetComponentInParent<HeistBuildingSite>() : null;
            if (site == null) { hovered = null; view?.Hide(); return; }
            if (site != hovered || Time.unscaledTime >= refreshAt)
            {
                hovered = site; refreshAt = Time.unscaledTime + config.RefreshSeconds;
                cachedText = Describe(world, site, player, manager.ServerTime.Time);
            }
            view?.Show(cachedText, point, config);
        }

        /// <summary>실제 입장과 출동·대기를 구분하고 기존 서버 명령 조건에 맞는 예상 가능 여부를 표시합니다.</summary>
        private static string Describe(HeistWorldGlue world, HeistBuildingSite site, NetworkObject player, double now)
        {
            if (!site.IsLootSite) return site.DisplayName + "\n일반 건물 · 절도/정기 검사 대상 아님";
            var state = world.Status(site.Id);
            string reason = "가능 (서버에서 최종 확인)";
            if (player.GetComponent<PlayerStats>().IsDead) reason = "플레이어 사망";
            else if (player.TryGetComponent<PlayerWalletGlue>(out var wallet) && wallet.IsEscaping) reason = "탈출 정산 중";
            else if (state.Money <= 0) reason = "돈 없음";
            else if (!world.CanReach(player, site.Entrance, world.Config.InteractionDistance)) reason = "문까지 거리 또는 벽 확인";
            else
            {
                bool owns = false, available = false;
                foreach (var pet in PetUpdateManager.Pets)
                {
                    if (pet == null || !pet.IsOwnedBy(player)) continue;
                    owns = true;
                    var work = pet.GetComponent<PetHeistGlue>();
                    available |= work != null && !work.IsBusy && !pet.GetComponent<PlayerStats>().IsDead &&
                        work.Cargo < world.Config.PetCapacity && Vector3.Distance(pet.transform.position, player.transform.position) <= world.Config.PetCommandDistance;
                }
                if (!owns) reason = "소속 펫 없음";
                else if (!available) reason = "펫 작업·체력·적재량·거리 확인";
            }
            var text = new StringBuilder().AppendLine(site.DisplayName).AppendLine($"보유 금액 {state.Money:N0}").AppendLine("절도: " + reason);
            if (state.Inspecting) text.AppendLine($"경찰 검사 중 · {Math.Max(0, state.InspectionEndsAt - now):0.0}초");
            else if (state.InspectionEnRoute) text.AppendLine("경찰 출동 중");
            else if (state.NextInspection <= now) text.AppendLine("검사 대기 · 담당 경찰 배정 중");
            else text.AppendLine($"다음 검사 예정 {state.NextInspection - now:0}초");
            foreach (var pet in PetUpdateManager.Pets)
            {
                var work = pet != null && pet.IsOwnedBy(player) ? pet.GetComponent<PetHeistGlue>() : null;
                if (work == null || work.BuildingId != site.Id || !work.IsBusy) continue;
                text.AppendLine(work.IsInside ? $"내 펫 절도 중 · {Math.Max(0, work.FinishAt - now):0.0}초" : "내 펫 이동 중");
            }
            if (state.Inspecting) text.Append("주의: 입장 시 펫 압수·수배");
            return text.ToString();
        }
    }
}
