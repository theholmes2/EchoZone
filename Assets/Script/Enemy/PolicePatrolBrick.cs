using UnityEngine;

namespace EchoZone.Enemy
{
    /// <summary>Unity 이동·NavMesh를 모르는 순찰 지점 순서와 대기시간 계산 Brick입니다.</summary>
    public sealed class PolicePatrolBrick
    {
        /// <summary>현재 목표로 삼을 순찰 지점 인덱스입니다.</summary>
        private int currentPointIndex;

        /// <summary>현재 순찰 지점에서 대기가 끝날 게임 시각입니다.</summary>
        private float waitEndTime;

        /// <summary>현재 순찰 지점 인덱스를 읽기 전용으로 제공합니다.</summary>
        public int CurrentPointIndex => currentPointIndex;
        /// <summary>순찰 대기 타이머의 남은 시간을 제공합니다.</summary>
        public float RemainingWait(float now) => Mathf.Max(0, waitEndTime - now);
        /// <summary>지점 인덱스와 대기 시간을 새 서버 시간에 맞춰 복원합니다.</summary>
        public void Restore(int index, int count, float remaining, float now)
        { currentPointIndex = count > 0 ? Mathf.Clamp(index, 0, count - 1) : 0; waitEndTime = now + Mathf.Max(0, remaining); }

        /// <summary>새 생애의 순찰을 첫 지점부터 시작합니다.</summary>
        public void Reset() { currentPointIndex = 0; waitEndTime = 0f; }

        /// <summary>1→2→3→1 순서로 다음 순찰 지점을 선택합니다.</summary>
        public void AdvancePoint(int pointCount)
        {
            if (pointCount <= 0) return;
            currentPointIndex = (currentPointIndex + 1) % pointCount;
        }

        /// <summary>순찰 지점에 도착했을 때 대기 종료 시각을 기록합니다.</summary>
        public void BeginWait(float currentTime, float waitSeconds)
        {
            waitEndTime = currentTime + waitSeconds;
        }

        /// <summary>현재 순찰 지점에서의 대기가 끝났는지 판정합니다.</summary>
        public bool IsWaitComplete(float currentTime)
        {
            if(currentTime>= waitEndTime)
            {
                return true;
            }

            return false;
        }

        /// <summary>현재 위치가 목표 지점에 충분히 가까운지 순수 거리로 판정합니다.</summary>
        public bool HasReachedPoint(Vector3 currentPosition, Vector3 targetPosition, float arrivalDistance)
        {
            if((currentPosition - targetPosition).sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                return true;
            }

            return false;
        }
    }
}
