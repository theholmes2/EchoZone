using EchoZone.Online.Migration;
using UnityEngine;

namespace EchoZone.Enemy
{
    public sealed partial class PoliceEnemyBrainGlue
    {
        /// <summary>아직 재접속하지 않은 추격 대상의 고정 ID입니다.</summary>
        private string pendingMigrationTarget;

        /// <summary>진행 중 검사는 취소 대상으로 저장하고 전투·순찰 상태는 보존합니다.</summary>
        public ActorRecord CaptureMigration(float now)
        {
            var site = heistDuty != null ? EchoZone.Heist.HeistWorldGlue.Instance?.Site(heistDuty.InspectionBuildingId) : null;
            return new ActorRecord { id = PoliceId, position = site != null ? site.Entrance : transform.position,
                hasPursuitPhase = true, pursuitActive = lostPursuit.Active, pursuitArrived = lostPursuit.Arrived,
                pursuitRemaining = lostPursuit.Remaining, pursuitStuckRemaining = Mathf.Max(0, config.LostTargetStuckSeconds - pursuitStall), lastAim = lastAimPoint,
                rotation = transform.rotation, health = GetComponent<PlayerStats>().CurrentHealth,
                kind = (int)equippedWeaponType.Value, station = stationIndex, state = (int)currentState,
                patrolIndex = patrolBrick.CurrentPointIndex, patrolWaiting = isWaitingAtPatrolPoint,
                waitRemaining = patrolBrick.RemainingWait(now), target = currentTarget != null ? SessionWorldMigrationGlue.TargetId(currentTarget) : pendingMigrationTarget,
                identified = targetIdentified, hasLastKnown = perceptionBrick.HasLastKnownPosition,
                lastKnown = perceptionBrick.LastKnownPosition, detection = perceptionBrick.DetectionProgress,
                searchRemaining = lostPursuit.Arrived ? lostPursuit.Remaining : 0, fireRemaining = Mathf.Max(0, nextFireTime - now),
                orbitRemaining = Mathf.Max(0, nextOrbitSwitchTime - now), orbitSign = orbitSign, burstCount = currentBurstShotCount,
                pursuingAttacker = pursuingAttacker,
                corpseRemaining = deathGlue != null ? deathGlue.CaptureReturn(now) : -1,
                weaponDefinitionId = weaponFireGlue != null ? weaponFireGlue.DefinitionId : null,
                weaponJson = weaponFireGlue != null ? weaponFireGlue.CaptureMigrationWeapon(now) : null };
        }

        /// <summary>생성된 경찰에 저장 상태를 적용하고 NavMesh 경로·점유는 다시 계산합니다.</summary>
        public void RestoreMigration(ActorRecord record, float now)
        {
            if (!IsServer) return;
            heistDuty?.CancelServer(); ReleaseDestination(); PoliceId = record.id;
            ConfigureWeaponType((PoliceWeaponType)record.kind);
            if (weaponFireGlue == null || weaponFireGlue.DefinitionId != record.weaponDefinitionId)
                throw new System.InvalidOperationException("Police weapon kind/definition mismatch.");
            var stats = GetComponent<PlayerStats>(); stats.SetCurrentValues(record.health, stats.CurrentStamina, stats.CurrentMana);
            patrolBrick.Restore(record.patrolIndex, patrolPoints != null ? patrolPoints.Length : 0, record.waitRemaining, now);
            isWaitingAtPatrolPoint = record.patrolWaiting;
            perceptionBrick.Restore(record.detection, record.hasLastKnown, record.lastKnown);
            if (record.hasPursuitPhase)
            {
                lostPursuit.Restore(record.pursuitActive, record.pursuitArrived, record.pursuitRemaining);
                pursuitStall = Mathf.Max(0, config.LostTargetStuckSeconds - record.pursuitStuckRemaining);
                lastAimPoint = record.lastAim;
            }
            else
            {
                lostPursuit.Reset(); pursuitStall = 0; lastAimPoint = record.lastKnown;
                if (record.hasLastKnown) RememberGroundPosition(record.lastKnown);
            }
            pursuitProgress = record.position;
            targetIdentified = record.identified; currentState = (PoliceEnemyState)record.state;
            SetPlayerBlockingServer(currentState == PoliceEnemyState.Combat);
            pendingMigrationTarget = record.target; currentTarget = null;
            nextFireTime = now + record.fireRemaining;
            nextOrbitSwitchTime = now + record.orbitRemaining; orbitSign = record.orbitSign;
            currentBurstShotCount = record.burstCount; pursuingAttacker = record.pursuingAttacker;
            weaponFireGlue?.RestoreDefinition(record.weaponDefinitionId, record.weaponJson, RecoveryCatalog.Load().Version, now);
            deathGlue?.RestoreReturn(record.corpseRemaining, now);
            SessionWorldMigrationGlue.Place(NetworkObject, record.position, record.rotation);
            ResolveMigrationTarget();
        }

        /// <summary>늦게 재접속한 추격 대상을 기존 루프에서 한 번만 다시 연결합니다.</summary>
        public void ResolveMigrationTarget()
        {
            if (string.IsNullOrEmpty(pendingMigrationTarget)) return;
            var target = SessionWorldMigrationGlue.ResolveTarget(pendingMigrationTarget);
            if (target == null) return;
            currentTarget = target; pendingMigrationTarget = null;
        }
    }
}
