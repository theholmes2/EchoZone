using UnityEngine;

namespace EchoZone.Pet
{
    /// <summary>펫 추종과 임시 짐 연출 설정입니다. 등급·운반량 규칙은 아직 적용하지 않습니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Pet/Follow Config")]
    public sealed class PetFollowConfig : ScriptableObject
    {
        /// <summary>생성 시 Agent의 정지 허용 반경에 적용할 거리입니다. 몸체 표면 사이의 간격이 아닌 목적지 기준 거리입니다.</summary>
        [Min(0.1f)] public float FollowDistance = 1f;
        /// <summary>서버 NavMesh 이동 속도입니다.</summary>
        [Min(0.1f)] public float MoveSpeed = 6f;
        /// <summary>서버 이동 가속도입니다.</summary>
        [Min(0.1f)] public float Acceleration = 16f;
        /// <summary>초당 회전 각도입니다.</summary>
        [Min(1f)] public float AngularSpeed = 360f;
        /// <summary>경로 재요청 간격입니다.</summary>
        [Min(0.02f)] public float RepathSeconds = 0.2f;
        /// <summary>목적지 근처 NavMesh 검색 반경입니다.</summary>
        [Min(0.1f)] public float SampleRadius = 3f;
        /// <summary>플레이어가 부활 등으로 순간 이동했을 때 펫을 재배치할 이동량입니다.</summary>
        [Min(1f)] public float OwnerTeleportDistance = 15f;
        /// <summary>임시 상자의 펫 기준 뒤쪽 위치입니다.</summary>
        public Vector3 CargoOffset = new(0f, 0.35f, -1.7f);
        /// <summary>걷기 애니메이션으로 판단할 최소 관측 속도입니다.</summary>
        [Min(0f)] public float AnimationMoveThreshold = 0.1f;
    }
}
