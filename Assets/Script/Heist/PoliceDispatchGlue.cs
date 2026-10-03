using System.Collections.Generic;
using EchoZone.Enemy;
using UnityEngine;
namespace EchoZone.Heist
{
    public sealed partial class HeistWorldGlue
    {
        /// <summary>취소된 작업의 원래 기한을 보존하면서 재시도 폭주만 막는 시각입니다.</summary>
        private readonly Dictionary<int, double> inspectionRetryAt = new();
        /// <summary>배정 시 한 번 조사한 경로 길이와 건물입니다.</summary>
        private readonly struct InspectionCandidate
        {
            /// <summary>검사할 건물입니다.</summary>
            public readonly HeistBuildingSite Site;
            /// <summary>첫 검사 예정 시각입니다.</summary>
            public readonly double Due;
            /// <summary>완전 경로를 따라 이동할 거리입니다.</summary>
            public readonly float Length;
            /// <summary>경로 검사 결과를 정렬용 데이터로 보관합니다.</summary>
            public InspectionCandidate(HeistBuildingSite site, double due, float length) { Site = site; Due = due; Length = length; }
        }
        /// <summary>가용 순찰만 세므로 전투·수색·기존 예약은 신규 검사 여유로 세지 않습니다.</summary>
        public bool CanDispatchDuty(PoliceHeistDutyGlue officer)
        {
            if (officer == null || !officer.CanAcceptInspection) return false;
            int district = officer.GetComponent<PoliceEnemyBrainGlue>().StationIndex;
            int free = 0, assigned = 0;
            foreach (var other in PoliceHeistDutyGlue.ActiveOfficers)
            {
                if (other == null || !other.IsSpawned || other.GetComponent<PlayerStats>().IsDead ||
                    other.GetComponent<PoliceEnemyBrainGlue>().StationIndex != district) continue;
                if (other.HasDuty) assigned++;
                else if (other.CanAcceptInspection) free++;
            }
            return PoliceInspectionPriorityBrick.CanDispatch(free, assigned, config.MinimumDistrictPatrol, config.MaximumDistrictDuties);
        }
        /// <summary>예약 불가 후보는 건너뛰어 다음 후보를 시도하며 오래 밀린 요청부터 공정하게 배정합니다.</summary>
        private HeistBuildingSite SelectInspection(PoliceHeistDutyGlue officer)
        {
            double now = NetworkManager.ServerTime.Time;
            var candidates = new List<InspectionCandidate>();
            foreach (var site in sites.Values)
            {
                if (!site.IsLootSite) continue;
                var status = Status(site.Id);
                if (status.NextInspection > now || inspections.ContainsKey(site.Id) ||
                    (inspectionRetryAt.TryGetValue(site.Id, out double retry) && now < retry)) continue;
                if (officer.TryNavigationLength(site.Entrance, out float length)) candidates.Add(new(site, status.NextInspection, length));
            }
            candidates.Sort((a,b) => PoliceInspectionPriorityBrick.Compare(a.Due, a.Length, a.Site.Id, b.Due, b.Length, b.Site.Id, now, config.InspectionAgingSeconds));
            foreach (var candidate in candidates)
            {
                if (!PoliceDestinationBrick.Shared.TryClaim(officer.NetworkObjectId, candidate.Site.Entrance,
                    officer.GetComponent<PoliceEnemyBrainGlue>().DestinationSpacing)) continue;
                inspections[candidate.Site.Id] = officer;
                var state = Status(candidate.Site.Id); state.InspectionEnRoute = true; Write(state);
                return candidate.Site;
            }
            return null;
        }
    }
}
