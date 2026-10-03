using System;
using System.Collections.Generic;
using EchoZone.Heist;
using UnityEngine;

namespace EchoZone.Online.Migration
{
    /// <summary>Unity 객체를 변경하기 전에 확장 복사본의 고정 키·숫자·원장을 검사합니다.</summary>
    public static class SessionWorldSnapshotValidator
    {
        /// <summary>손상된 복사본을 부분 적용하지 않고 복구 실패로 처리합니다.</summary>
        public static void Validate(SessionWorldSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.worldId)) throw new ArgumentException("World identity is missing.");
            if (snapshot.buildings == null || snapshot.pets == null || snapshot.police == null || snapshot.players == null ||
                snapshot.wallets == null || snapshot.reports == null || snapshot.starterPets == null || snapshot.retiredPets == null || snapshot.stationSpawnIndices == null)
                throw new ArgumentException("World snapshot collections are missing.");
            var ids = new HashSet<int>();
            foreach (var building in snapshot.buildings)
                if (building == null || !ids.Add(building.id) || building.money < 0 ||
                    !Finite(building.inspectionRemaining) || !Finite(building.searchRemaining) || !Finite(building.inspectionOverdue) ||
                    !Finite(building.incomeRemaining)) throw new ArgumentException("Invalid building snapshot.");
            Actors(snapshot.players); Actors(snapshot.pets); Actors(snapshot.police);
            var accounts = new HashSet<string>();
            foreach (var wallet in snapshot.wallets)
            {
                if (wallet == null || string.IsNullOrWhiteSpace(wallet.playerId) || !accounts.Add(wallet.playerId)) throw new ArgumentException("Duplicate wallet.");
                WalletSessionBrick.Restore(wallet.json);
            }
            var ledger = new HeistLedgerBrick(); ledger.Restore(snapshot.ledgerJson);
            if (!string.IsNullOrEmpty(snapshot.retirementJournalJson))
            {
                var journal = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RetirementRequestRecord>>(snapshot.retirementJournalJson)
                    ?? throw new ArgumentException("Missing retirement journal.");
                var settlements = new HashSet<string>();
                foreach (var record in journal)
                    if (record == null || !Guid.TryParseExact(record.SettlementId, "N", out _) || !settlements.Add(record.SettlementId) ||
                        string.IsNullOrWhiteSpace(record.PetId) || !snapshot.retiredPets.Contains(record.PetId) ||
                        record.Reward < 0 || record.Balance < 0 || record.ExpectedRevision < 0 || record.Attempts < 0 ||
                        record.LedgerIds == null || record.ReturnedAmounts == null)
                        throw new ArgumentException("Invalid retirement journal.");
            }
            var investigation = new PoliceInvestigationBrick(); investigation.Restore(snapshot.investigationJson, 0);
        }
        /// <summary>같은 종류 개체의 ID 중복과 잘못된 위치를 검사합니다.</summary>
        private static void Actors(List<ActorRecord> actors)
        {
            var ids = new HashSet<string>();
            foreach (var actor in actors)
                if (actor == null || string.IsNullOrWhiteSpace(actor.id) || !ids.Add(actor.id) ||
                    !float.IsFinite(actor.position.x) || !float.IsFinite(actor.position.y) || !float.IsFinite(actor.position.z) ||
                    !float.IsFinite(actor.rotation.x) || !float.IsFinite(actor.rotation.y) || !float.IsFinite(actor.rotation.z) || !float.IsFinite(actor.rotation.w) ||
                    actor.health < 0 || actor.cargo < 0 || actor.escapeCount < 0 ||
                    !Finite(actor.escapeGraceRemaining) || !Finite(actor.escapeFailureRemaining) ||
                    !Finite(actor.pursuitRemaining) || !Finite(actor.pursuitStuckRemaining)) throw new ArgumentException("Invalid actor snapshot.");
        }
        /// <summary>상대 시간이 유효한 비음수인지 판정합니다.</summary>
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    }
}
