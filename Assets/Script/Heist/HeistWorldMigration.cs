using System;
using System.Collections.Generic;
using EchoZone.Online.Migration;

namespace EchoZone.Heist
{
    public sealed partial class HeistWorldGlue
    {
        /// <summary>이번 Session의 월드 ID입니다. 호스트 교체 때 새로 생성하지 않습니다.</summary>
        private string worldId;
        /// <summary>새 호스트에서 아직 Cloud 버전을 대조하지 않은 지갑 계정입니다.</summary>
        private readonly HashSet<string> walletsToVerify = new();
        /// <summary>늦게 재접속하는 계정도 지갑을 사용하기 전에 검증합니다.</summary>
        public bool RequiresWalletVerification(string id) => walletsToVerify.Contains(id);
        /// <summary>Cloud Revision 확인이 성공한 계정만 사용을 승인합니다.</summary>
        public void ConfirmWalletVerification(string id) => walletsToVerify.Remove(id);

        /// <summary>금액·원장·신고·수배와 상대 타이머를 하나의 복사본으로 만듭니다.</summary>
        public SessionWorldSnapshot CaptureMigration(string runId, double now)
        {
            if (string.IsNullOrEmpty(worldId)) worldId = runId;
            var snapshot = new SessionWorldSnapshot { worldId = worldId, savedAtUtc = DateTime.UtcNow.ToString("O"),
                ledgerJson = ledger.Export(), investigationJson = investigation.Export(now),
                retiredPets = new List<string>(retiredPets), starterPets = new List<string>(starterPets) };
            foreach (var value in Buildings)
                snapshot.buildings.Add(new BuildingRecord { id = value.Id, money = value.Money,
                    inspectionRemaining = value.Inspecting || value.InspectionEnRoute ? 0 : Math.Max(0, value.NextInspection - now),
                    searchRemaining = Math.Max(0, value.SearchUntil - now) });
            foreach (var pair in wallets) snapshot.wallets.Add(new WalletRecord { playerId = pair.Key, json = pair.Value.Export() });
            foreach (var pair in reports) snapshot.reports.Add(new ReportRecord { petId = pair.Key, playerId = pair.Value });
            return snapshot;
        }

        /// <summary>이미 완료된 돈 이동을 보존하고 검사 담당 예약은 새 서버에서 다시 배정합니다.</summary>
        public void RestoreMigration(SessionWorldSnapshot snapshot, double now)
        {
            if (!IsServer) return;
            worldId = snapshot.worldId; inspections.Clear(); recoveries.Clear(); reports.Clear();
            ledger.Restore(snapshot.ledgerJson); investigation.Restore(snapshot.investigationJson, now);
            wallets.Clear(); foreach (var value in snapshot.wallets) wallets.Add(value.playerId, WalletSessionBrick.Restore(value.json));
            walletsToVerify.Clear(); walletsToVerify.UnionWith(wallets.Keys);
            retiredPets.Clear(); retiredPets.UnionWith(snapshot.retiredPets);
            starterPets.Clear(); starterPets.UnionWith(snapshot.starterPets);
            foreach (var value in snapshot.reports) if (!retiredPets.Contains(value.petId)) reports[value.petId] = value.playerId;
            foreach (var value in snapshot.buildings)
                Write(new HeistBuildingState { Id = value.id, Money = value.money,
                    NextInspection = now + value.inspectionRemaining, SearchUntil = now + value.searchRemaining });
            PublishWanted();
        }

        /// <summary>이미 반환 또는 탈출 완료된 펫의 복원을 금지합니다.</summary>
        public bool IsRetiredPet(string id) => retiredPets.Contains(id);
        /// <summary>복구된 펫에 신고 표시를 다시 연결합니다.</summary>
        public bool IsReportedPet(string id) => reports.ContainsKey(id);
    }
}
