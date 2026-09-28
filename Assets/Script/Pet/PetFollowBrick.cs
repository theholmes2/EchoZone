using UnityEngine;

namespace EchoZone.Pet
{
    /// <summary>추종 방향과 수평 이동량만 계산합니다. Physics·네트워크를 참조하지 않습니다.</summary>
    public sealed class PetFollowBrick
    {
        /// <summary>마우스 조준이 아니라 플레이어의 실제 이동 방향을 기억합니다.</summary>
        private Vector3 travelDirection = Vector3.forward;

        /// <summary>이동 중에만 방향을 갱신하여 제자리 조준 시 펫이 빙빙 돌지 않게 합니다.</summary>
        public void ObserveMovement(Vector3 displacement)
        {
            displacement.y = 0f;
            if (displacement.sqrMagnitude > 0.0001f) travelDirection = displacement.normalized;
        }

        /// <summary>생성·부활 시 플레이어 뒤에 배치할 후보 위치를 구합니다.</summary>
        public Vector3 GetSpawnPosition(Vector3 ownerPosition, float distance)
        {
            return ownerPosition - travelDirection * distance;
        }
    }
}
