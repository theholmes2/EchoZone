using UnityEngine;
using UnityEngine.AI;
namespace EchoZone.Enemy
{
    public sealed partial class PoliceEnemyBrainGlue
    {
        /// <summary>조준 위치와 분리된 마지막 목격 이동의 단계 타이머입니다.</summary>
        private readonly PolicePursuitBrick lostPursuit = new();
        /// <summary>마지막으로 실제 보였던 대상의 피격 중심점입니다.</summary>
        private Vector3 lastAimPoint;
        /// <summary>현재 프레임 중앙 갱신 시간입니다.</summary>
        private float pursuitDelta;
        /// <summary>정체 판정 기준 위치입니다.</summary>
        private Vector3 pursuitProgress;
        /// <summary>추격 중 진전이 없는 누적 시간입니다.</summary>
        private float pursuitStall;

        /// <summary>확인된 대상 발 위치만 NavMesh로 변환해 기억합니다. 보이지 않는 대상은 읽지 않습니다.</summary>
        private void RememberGroundPosition(Vector3 observedPosition)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = navigationAgent.agentTypeID, areaMask = navigationAgent.areaMask };
            var point = NavMesh.SamplePosition(observedPosition, out var hit, config.LastKnownGroundSampleRadius, filter) ? hit.position : observedPosition;
            perceptionBrick.RememberTargetPosition(point);
        }

        /// <summary>마지막 목격 위치로 이동한 뒤부터 수색하며 경로 실패·정체·이동 만료는 순찰로 복귀합니다.</summary>
        private void TickLostPursuit(float serverTime)
        {
            if (!lostPursuit.Active)
            {
                lostPursuit.Begin(config.LostTargetTravelSeconds);
                pursuitProgress = transform.position; pursuitStall = 0;
            }
            bool arrived = patrolBrick.HasReachedPoint(transform.position, perceptionBrick.LastKnownPosition, config.LastKnownPositionArrivalDistance);
            if (HorizontalDistance(pursuitProgress, transform.position) >= config.LostTargetProgressDistance)
            { pursuitProgress = transform.position; pursuitStall = 0; }
            else if (!arrived) pursuitStall += pursuitDelta;
            bool failed = !CanMoveOnNavMesh() || pursuitStall >= config.LostTargetStuckSeconds;
            if (lostPursuit.Tick(pursuitDelta, arrived, failed, config.SearchDurationSeconds))
            {
                lostPursuit.Reset(); perceptionBrick.Reset(); currentTarget = null; pendingMigrationTarget = null;
                targetIdentified = pursuingAttacker = false; StopMoving(); ChangeState(PoliceEnemyState.Patrol, serverTime); return;
            }
            if (lostPursuit.Arrived) { ChangeState(PoliceEnemyState.Search, serverTime); StopMoving(); }
            else { navigationAgent.speed = config.ChaseMoveSpeed; MoveTo(perceptionBrick.LastKnownPosition); }
        }
    }
}
