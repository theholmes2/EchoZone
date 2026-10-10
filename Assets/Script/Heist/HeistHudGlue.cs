using System.Collections.Generic;
using System.Text;
using EchoZone.Pet;
using Unity.Netcode;
using UnityEngine;
using EchoZone.Player.Input;

namespace EchoZone.Heist
{
    /// <summary>복제된 범죄 상태와 가까운 대상 정보를 HUD에 전달합니다. 독립 Update는 사용하지 않습니다.</summary>
    public sealed class HeistHudGlue : MonoBehaviour
    {
        /// <summary>씬 Canvas 표시 컴포넌트입니다.</summary>
        [SerializeField] private HeistHudView view;
        /// <summary>동일 HUD의 독립 입력 없는 건물 툴팁입니다.</summary>
        [SerializeField] private BuildingHoverGlue buildingHover;
        /// <summary>현재 로컬 플레이어의 요청 통로입니다.</summary>
        private PlayerHeistGlue player;
        /// <summary>버튼이 가리키는 가까운 건물입니다.</summary>
        private HeistBuildingSite building;
        /// <summary>버튼이 가리키는 가까운 대기 펫입니다.</summary>
        private PetStateGlue pet;
        /// <summary>HUD 문자열 갱신 제한 시각입니다.</summary>
        private float nextRefresh;
        /// <summary>클릭 이벤트는 자신의 NetworkBehaviour로만 보냅니다.</summary>
        private void Awake() => view.Bind(
            () => { if (player != null && building != null) player.RequestSteal(building.Id); },
            () => { if (player != null && pet != null) player.RequestPet(pet.NetworkObjectId, false); },
            () => { if (player != null && pet != null) player.RequestPet(pet.NetworkObjectId, true); });
        /// <summary>기존 씬 런타임 갱신 순서에서 로컬 HUD만 갱신합니다.</summary>
        public void ManualUpdate()
        {
            buildingHover?.ManualUpdate();
            var nm = NetworkManager.Singleton; var world = HeistWorldGlue.Instance;
            var local = nm != null && nm.IsClient ? nm.LocalClient?.PlayerObject : null;
            bool visible = local != null && world != null && world.IsSpawned;
            view.SetVisible(visible);
            if (!visible) { view.ShowItemPrompt(null); return; }
            UpdateItemPrompt(local);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + world.Config.HudRefreshSeconds;
            player = local.GetComponent<PlayerHeistGlue>(); if (player == null) return;
            bool alive = !local.GetComponent<PlayerStats>().IsDead &&
                !(local.TryGetComponent<PlayerWalletGlue>(out var wallet) && wallet.IsEscaping);
            float best = world.Config.InteractionDistance; building = null;
            foreach (var site in world.Sites)
            { if (!site.IsLootSite) continue; float distance = Vector3.Distance(local.transform.position, site.Entrance); if (distance < best) { best = distance; building = site; } }
            pet = null; best = world.Config.InteractionDistance;
            var ownedPets = new List<PetStateGlue>();
            bool available = false;
            foreach (var item in PetUpdateManager.Pets)
            {
                if (item == null || !item.IsSpawned) continue;
                var job = item.GetComponent<PetHeistGlue>();
                if (item.IsOwnedBy(local))
                {
                    ownedPets.Add(item);
                    if (job != null) available |= !job.IsBusy && job.Cargo < world.Config.PetCapacity;
                }
                if (item.State != PetBehaviourState.Waiting && !item.IsCollectionWaiting) continue;
                float distance = Vector3.Distance(local.transform.position, item.transform.position);
                if (distance < best) { best = distance; pet = item; }
            }
            ownedPets.Sort((left, right) => left.NetworkObjectId.CompareTo(right.NetworkObjectId));
            var wanted = new StringBuilder("수배 명단\n");
            if (world.WantedPlayers.Count == 0) wanted.Append("수배자 없음");
            foreach (ulong id in world.WantedPlayers) wanted.Append($"플레이어 {id + 1}{(id == local.OwnerClientId ? " (나)" : "")} · 현상금 {world.Config.CaptureBounty:N0}\n");
            int searches = 0;
            foreach (var status in world.Buildings) if (status.SearchUntil > nm.ServerTime.Time) searches++;
            if (searches > 0) wanted.Append($"\n도난 현장 {searches}곳 수색 중");
            string siteInfo = "금빛 포탈이 있는 출입구 가까이에서 절도 지시";
            bool canSteal = false;
            if (building != null)
            {
                var state = world.Status(building.Id);
                siteInfo = $"{building.DisplayName}  현금 {state.Money:N0}\n" + (state.Inspecting ? "경찰이 내부 검사 중" : $"다음 검사 배정 {System.Math.Max(0, state.NextInspection - nm.ServerTime.Time):0}초 후");
                if (state.SearchUntil > nm.ServerTime.Time) siteInfo += $" · 수색 {state.SearchUntil - nm.ServerTime.Time:0}초";
                canSteal = state.Money > 0 && available && alive;
            }
            var nearbyJob = pet != null ? pet.GetComponent<PetHeistGlue>() : null;
            var petInfo = new StringBuilder();
            if (ownedPets.Count == 0)
            {
                petInfo.Append("내 펫 없음");
            }
            else
            {
                for (int i = 0; i < ownedPets.Count; i++)
                {
                    if (i > 0) petInfo.Append('\n');
                    var ownedJob = ownedPets[i].GetComponent<PetHeistGlue>();
                    int ownedCargo = ownedJob != null ? ownedJob.Cargo : 0;
                    petInfo.Append($"펫 {i + 1} · 장물 {ownedCargo:N0} · {PetStatus(ownedJob, world, nm)}");
                }
            }
            var feedback = new StringBuilder(player.Feedback);
            if (pet != null)
            {
                if (feedback.Length > 0) feedback.Append('\n');
                feedback.Append($"주변 대기 펫 · 장물 {nearbyJob?.Cargo ?? 0:N0}");
                if (nearbyJob != null && nearbyJob.Reported) feedback.Append(" · 신고 접수됨");
            }
            view.Show(wanted.ToString(), siteInfo, petInfo.ToString(), feedback.ToString(), canSteal,
                alive && pet != null && pet.CanBeClaimed, alive && nearbyJob != null && nearbyJob.IsAbandonedCargo && !nearbyJob.Reported);
        }

        /// <summary>펫 한 마리의 현재 지시 진행 상태를 우측 하단 한 줄용 문구로 바꿉니다.</summary>
        private static string PetStatus(PetHeistGlue job, HeistWorldGlue world, NetworkManager networkManager)
        {
            if (job == null) return "상태 확인 불가";
            if (job.IsBusy)
                return job.IsInside
                    ? $"절도 중 {System.Math.Max(0, job.FinishAt - networkManager.ServerTime.Time):0.0}초"
                    : "건물로 이동 중";
            return job.Cargo >= world.Config.PetCapacity ? "장물 가득" : "지시 대기";
        }

        /// <summary>센서가 이미 감지한 후보 중 실제 입력과 동일한 가장 가까운 아이템 안내를 표시합니다.</summary>
        private void UpdateItemPrompt(NetworkObject local)
        {
            var sensor = local.GetComponentInChildren<PlayerInteractionSensor>();
            var input = local.GetComponentInChildren<PlayerInputReader>();
            if (sensor == null) { view.ShowItemPrompt(null); return; }
            sensor.RemoveMissingCandidates();
            InteractableBehaviour nearest = null;
            float bestDistanceSquared = float.PositiveInfinity;
            foreach (var candidate in sensor.Candidates)
            {
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    !candidate.TryGetInteractionPrompt(local.gameObject,
                        input != null ? input.InteractBindingDisplayName : "E", out _)) continue;
                float distanceSquared = (candidate.transform.position - local.transform.position).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                nearest = candidate;
            }
            string prompt = null;
            nearest?.TryGetInteractionPrompt(local.gameObject,
                input != null ? input.InteractBindingDisplayName : "E", out prompt);
            view.ShowItemPrompt(prompt);
        }
    }
}
