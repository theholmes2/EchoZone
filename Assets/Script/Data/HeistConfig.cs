using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>건물 절도·정기 검사·장물 회수의 서버 규칙입니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Heist/Config")]
    public sealed class HeistConfig : ScriptableObject
    {
        /// <summary>이 시간만큼 더 오래 밀린 요청은 경로 길이보다 우선합니다.</summary>
        [Min(1)] public float InspectionAgingSeconds = 30f;
        /// <summary>구역별 검사·회수·현장 수색 배정 후 남겨둘 비전투 순찰 인원입니다.</summary>
        [Min(0)] public int MinimumDistrictPatrol = 2;
        /// <summary>한 구역에서 동시에 일반 작업으로 빠질 최대 인원입니다.</summary>
        [Min(1)] public int MaximumDistrictDuties = 4;
        /// <summary>새 세션에서 각 건물에 배치할 돈입니다.</summary>
        [Min(1)] public int InitialBuildingMoney = 10000;
        /// <summary>절도 대상 건물의 현금 보충 주기입니다. 실행 중인 세션 시간만 진행합니다.</summary>
        [Min(1f)] public float BuildingIncomeInterval = 30f;
        /// <summary>보충 주기마다 생성할 현금입니다. 0이면 보충하지 않습니다.</summary>
        [Min(0)] public int BuildingIncomeAmount = 500;
        /// <summary>자동 보충으로 늘어날 수 있는 상한입니다. 장물 반환은 이 상한 때문에 소실되지 않습니다.</summary>
        [Min(1)] public int BuildingIncomeCap = 20000;
        /// <summary>한 번의 절도에서 펫이 가져오는 최대 금액입니다.</summary>
        [Min(1)] public int MoneyPerTheft = 1000;
        /// <summary>펫 한 마리의 운반 한도입니다.</summary>
        [Min(1)] public int PetCapacity = 5000;
        /// <summary>운반 한도에서 보통 등급 펫에게 적용할 최대 감속 비율입니다.</summary>
        [Range(0f, 0.95f)] public float MaximumLoadSlowdown = 0.65f;
        /// <summary>펫 등급별 힘입니다. 1등급은 첫 값이며 힘이 높을수록 덜 느려집니다.</summary>
        public float[] PetGradeStrength = { 1f, 1.5f, 2f };
        /// <summary>도난 발견 후 주변을 수색할 시간입니다.</summary>
        [Min(1f)] public float CrimeSearchSeconds = 60f;
        /// <summary>검사로 도난을 발견한 뒤 범인의 수배를 확정할 시간입니다.</summary>
        [Min(0f)] public float InvestigationDelaySeconds = 15f;
        /// <summary>도난 건물 출입구 주변 수색 반경입니다.</summary>
        [Min(1f)] public float CrimeSearchRadius = 20f;
        /// <summary>수색 원 위의 후보 지점 개수입니다.</summary>
        [Min(3)] public int CrimeSearchPointCount = 8;
        /// <summary>수색 지점에서 주변을 둘러보는 시간입니다.</summary>
        [Min(0.1f)] public float CrimeSearchWaitSeconds = 3f;
        /// <summary>수색 지점에서 주변을 천천히 둘러볼 초당 회전 각도입니다.</summary>
        [Min(0f)] public float CrimeSearchTurnSpeed = 60f;
        /// <summary>수색 지점 도착 방향을 기준으로 좌우를 살필 최대 각도입니다.</summary>
        [Range(0f, 180f)] public float CrimeSearchLookAngle = 55f;
        /// <summary>수색 후보를 같은 Agent 종류의 NavMesh에 투영할 최대 거리입니다.</summary>
        [Min(0.1f)] public float CrimeSearchSampleRadius = 2f;
        /// <summary>수배자를 쓰러뜨린 다른 플레이어에게 지급할 현상금입니다.</summary>
        [Min(0)] public int CaptureBounty = 500;
        /// <summary>펫이 건물 안에서 돈을 꺼내는 시간입니다.</summary>
        [Min(0.1f)] public float StealSeconds = 8f;
        /// <summary>검사 완료 후 다음 검사까지의 최소 간격입니다.</summary>
        [Min(1f)] public float InspectionInterval = 180f;
        /// <summary>세션 시작 후 첫 검사까지의 유예입니다.</summary>
        [Min(1f)] public float FirstInspectionDelay = 120f;
        /// <summary>건물별 검사 시작 시각을 분산할 간격입니다.</summary>
        [Min(0f)] public float InspectionStagger = 3f;
        /// <summary>경찰이 건물 안에서 검사하는 시간입니다.</summary>
        [Min(0.1f)] public float InspectionSeconds = 1f;
        /// <summary>도달 불가 작업을 취소하는 시간입니다.</summary>
        [Min(1f)] public float TravelTimeout = 60f;
        /// <summary>경찰 작업 탐색 및 경로 갱신 간격입니다.</summary>
        [Min(0.1f)] public float JobSearchInterval = 1f;
        /// <summary>출입구에서 작업을 요청할 수 있는 거리입니다.</summary>
        [Min(0.5f)] public float InteractionDistance = 4f;
        /// <summary>플레이어가 절도를 지시할 펫의 최대 거리입니다.</summary>
        [Min(1f)] public float PetCommandDistance = 15f;
        /// <summary>출입구 도착 판정 거리입니다.</summary>
        [Min(0.1f)] public float ArrivalDistance = 0.6f;
        /// <summary>펫과 경찰이 작업 목적지로 이동하는 속도입니다.</summary>
        [Min(0.1f)] public float WorkMoveSpeed = 5f;
        /// <summary>예정된 건물 검사로 출동하는 경찰의 달리기 속도입니다.</summary>
        [Min(0.1f)] public float InspectionRunSpeed = 6f;
        /// <summary>검사·회수 이동 중 진전이 없을 때 작업을 재배정할 시간입니다.</summary>
        [Min(1f)] public float WorkStuckSeconds = 6f;
        /// <summary>작업 이동이 진전됐다고 인정할 거리입니다.</summary>
        [Min(0.01f)] public float WorkProgressDistance = 0.25f;
        /// <summary>중단·도달 불가 작업의 재배정 대기시간입니다.</summary>
        [Min(0.1f)] public float InspectionRetrySeconds = 3f;
        /// <summary>경찰이 장물을 인수하는 근접 거리입니다.</summary>
        [Min(0.1f)] public float ConfiscationDistance = 1.8f;
        /// <summary>회수 금액 중 감사비 비율입니다. 금액은 내림합니다.</summary>
        [Range(0f, 1f)] public float ReportRewardRate = 0.05f;
        /// <summary>상호작용을 차단하는 건물·지형 레이어입니다.</summary>
        public LayerMask BlockingLayers = ~0;
        /// <summary>반복 RPC 요청의 최소 간격입니다.</summary>
        [Min(0.05f)] public float RequestInterval = 0.25f;
        /// <summary>로컬 HUD 문자열 갱신 간격입니다.</summary>
        [Min(0.05f)] public float HudRefreshSeconds = 0.15f;
        /// <summary>복구 시 겹친 펫·경찰 사이에 확보할 최소 간격입니다.</summary>
        [Min(0.1f)] public float RestoreSpacing = 1.1f;
        /// <summary>복구 위치 주변 NavMesh 후보 검색 반경입니다.</summary>
        [Min(0.1f)] public float RestoreSampleRadius = 0.5f;
        /// <summary>문 앞 복귀 위치가 겹칠 때 검사할 분산 후보 수입니다.</summary>
        [Min(4)] public int RestorePositionCandidates = 32;
    }
}
