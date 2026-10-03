using UnityEngine;

namespace EchoZone.Pet
{
        /// <summary>도주 후보·경찰 회피·대기 회복·주인 변경의 조절값입니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Pet/Behaviour Config")]
    public sealed class PetBehaviourConfig : ScriptableObject
    {
        /// <summary>도주 시작 당시 플레이어를 중심으로 탐색할 첫 원의 반지름입니다.</summary>
        [Min(1f)] public float EscapeRadius = 25f;
        /// <summary>한 회차에 허용하는 실제 도주 진입 횟수입니다.</summary>
        [Min(1)] public int MaximumEscapeCount = 5;
        /// <summary>첫 도주부터 경찰 수거를 유예하는 최소 시간입니다.</summary>
        [Min(0)] public float ReclaimGraceSeconds = 30f;
        /// <summary>안전 지점에 도착하지 못한 도주 시도의 제한시간입니다.</summary>
        [Min(1)] public float EscapeFailureSeconds = 20f;
        /// <summary>첫 원에 후보가 없을 때 다음 원을 넓힐 거리입니다.</summary>
        [Min(1f)] public float RingSpacing = 10f;
        /// <summary>한 번의 탐색에서 검사할 동심원 수입니다.</summary>
        [Range(1, 5)] public int RingCount = 3;
        /// <summary>원마다 검사할 각도 후보 수입니다.</summary>
        [Range(8, 64)] public int CandidateCount = 24;
        /// <summary>NavMesh 투영 후에도 반드시 유지할 플레이어와의 최소 거리입니다.</summary>
        [Min(1f)] public float MinimumPlayerDistance = 20f;
        /// <summary>원 위 후보를 NavMesh에 투영할 최대 오차입니다.</summary>
        [Min(0.1f)] public float SampleRadius = 1.5f;
        /// <summary>경찰 시야거리 바깥에 더 확보할 여유 거리입니다.</summary>
        [Min(0f)] public float PoliceMargin = 3f;
        /// <summary>현재 경찰과 순찰 경로를 다시 검사할 간격입니다.</summary>
        [Min(0.1f)] public float RecheckSeconds = 1f;
        /// <summary>도주 이동 속도입니다.</summary>
        [Min(0.1f)] public float EscapeSpeed = 9f;
        /// <summary>도주 목적지 도착 허용 거리입니다.</summary>
        [Min(0.05f)] public float ArrivalDistance = 0.4f;
        /// <summary>위험 경로 검사 간격입니다. 반 간격만큼 안전 여유를 추가합니다.</summary>
        [Min(0.1f)] public float PathSampleSpacing = 1f;
        /// <summary>후보당 경로 검사 상한입니다. 초과 경로는 승인하지 않습니다.</summary>
        [Range(16, 1024)] public int MaximumPathSamples = 256;
        /// <summary>대기 중 초당 회복하는 체력입니다.</summary>
        [Min(0f)] public float RecoveryPerSecond = 5f;
        /// <summary>서버가 주인 변경을 허용하는 최대 거리입니다.</summary>
        [Min(0.1f)] public float ClaimDistance = 3f;
        /// <summary>상호작용 LOS를 막는 지형·건물 레이어입니다.</summary>
        public LayerMask ClaimBlockingLayers = ~0;
        /// <summary>상호작용 LOS 시작 높이입니다.</summary>
        [Min(0f)] public float ClaimRayHeight = 0.8f;
    }
}
