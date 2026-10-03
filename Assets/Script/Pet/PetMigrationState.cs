using EchoZone.Online.Migration;
using UnityEngine;

namespace EchoZone.Pet
{
    public sealed partial class PetStateGlue
    {
        /// <summary>진행 중인 절도는 문 앞으로 취소하고 완료된 펫 상태만 저장합니다.</summary>
        public ActorRecord CaptureMigration()
        {
            var site = heist != null && heist.IsBusy ? EchoZone.Heist.HeistWorldGlue.Instance?.Site(heist.BuildingId) : null;
            RecoveryCatalog.Load().Pet(DefinitionId);
            return new ActorRecord { id = PetId, definitionId = DefinitionId, owner = ownerPlayerId, position = site != null ? site.Entrance : transform.position,
                hasEscapeEpisode = true, escapeCount = escapeEpisode.Count, escapeGraceRemaining = escapeEpisode.GraceRemaining,
                escapeFailureRemaining = escapeEpisode.FailureRemaining, escapeFailed = escapeEpisode.Failed,
                rotation = transform.rotation, health = stats.CurrentHealth, state = (int)State, escapeCenter = escapeCenter,
                recovery = recovery, watchThreats = watchThreats, cargo = heist != null ? heist.Cargo : 0, grade = heist != null ? heist.Grade : 1 };
        }

        /// <summary>고정 ID와 소유 계정을 복원하고 도주 경로는 새 경찰 배치로 다시 계산합니다.</summary>
        public void RestoreMigration(ActorRecord record)
        {
            if (!IsServer || record == null) return;
            if (record.definitionId != DefinitionId) throw new System.InvalidOperationException("Pet definition mismatch.");
            PetId = record.id;
            heist?.RestoreMigrationCargo(record.cargo, record.grade);
            stats.SetCurrentValues(record.health, stats.CurrentStamina, stats.CurrentMana);
            ownerPlayerId = record.owner; owner.Value = default;
            state.Value = (PetBehaviourState)record.state;
            if (record.hasEscapeEpisode)
                escapeEpisode.Restore(record.escapeCount, record.escapeGraceRemaining, record.escapeFailureRemaining, record.escapeFailed);
            else
            {
                escapeEpisode.Reset();
                if (State == PetBehaviourState.Fleeing || record.watchThreats)
                    escapeEpisode.TryBegin(config.MaximumEscapeCount, config.ReclaimGraceSeconds, config.EscapeFailureSeconds);
            }
            awaitingOwner = State == PetBehaviourState.Following && !string.IsNullOrEmpty(ownerPlayerId);
            escapeCenter = record.escapeCenter; recovery = record.recovery; watchThreats = record.watchThreats;
            hasEscapeDestination = false; nextCheck = 0;
            stats.SetDamageBlocked(State == PetBehaviourState.Fleeing);
            follow.StopServer(); follow.ResetFollowTarget();
            SessionWorldMigrationGlue.Place(NetworkObject, record.position, record.rotation);
        }
    }
}
