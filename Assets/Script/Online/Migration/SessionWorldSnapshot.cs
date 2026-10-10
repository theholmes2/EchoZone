using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoZone.Online.Migration
{
    /// <summary>고정 ID와 남은 시간으로 저장하는 확장 월드 데이터입니다.</summary>
    [Serializable] public sealed class SessionWorldSnapshot
    {
        /// <summary>사망 전리품과 랜덤 탄약 상자의 남은 내용입니다.</summary>
        public List<EchoZone.Equipment.LootBagRecord> lootBags = new();
        /// <summary>복구 중 정지할 다음 랜덤 탄약 생성까지 남은 초입니다.</summary>
        public float lootSpawnRemaining;
        /// <summary>종류 정의/설정의 콘텐츠 버전입니다. 0인 구형 월드는 명시적 변환 없이 복원하지 않습니다.</summary>
        public int catalogVersion;
        /// <summary>분기하지 않고 이어받을 월드 식별자입니다.</summary>
        public string worldId;
        /// <summary>수집 UTC입니다. 복구 중 타이머는 진행하지 않습니다.</summary>
        public string savedAtUtc;
        /// <summary>건물·펫·경찰·플레이어 확장 상태입니다.</summary>
        public List<BuildingRecord> buildings = new();
        public List<ActorRecord> pets = new(), police = new(), players = new();
        /// <summary>순수 원장의 버전 호환 JSON입니다.</summary>
        public string ledgerJson, investigationJson;
        /// <summary>회수의 고정 요청·시도 상태입니다. Cloud 체크포인트에 원장/보상과 함께 영속화됩니다.</summary>
        public string retirementJournalJson;
        /// <summary>계정별 지갑과 최초 펫 지급 이력입니다.</summary>
        public List<WalletRecord> wallets = new();
        public List<string> starterPets = new();
        /// <summary>신고·종료한 펫의 고정 ID입니다.</summary>
        public List<ReportRecord> reports = new();
        public List<string> retiredPets = new();
        /// <summary>스포너 예약 데이터입니다.</summary>
        public float nextSpawnRemaining;
        public int remainingInitial;
        /// <summary>경찰서별 다음 생성 위치 인덱스입니다.</summary>
        public List<int> stationSpawnIndices = new();
    }
    /// <summary>건물 현금과 남은 검사·수색 시간입니다.</summary>
    [Serializable] public sealed class BuildingRecord
    {
        /// <summary>보충 타이머가 없는 구형 스냅샷과 구분합니다.</summary>
        public bool hasIncomeTimer;
        /// <summary>다음 현금 보충까지 남은 세션 초입니다. 복구 중에는 진행하지 않습니다.</summary>
        public double incomeRemaining;
        public int id, money;
        public double inspectionRemaining, searchRemaining;
        /// <summary>이미 기한이 지난 검사의 누적 대기 시간입니다.</summary>
        public double inspectionOverdue;
    }
    /// <summary>네트워크 객체 ID와 독립된 개체 복원 데이터입니다.</summary>
    [Serializable] public sealed class ActorRecord
    {
        /// <summary>펫 종류 ID이며 개체 id와 별개입니다.</summary>
        public string definitionId;
        /// <summary>장착 무기 종류 ID이며 weaponJson의 변경 상태와 별개입니다.</summary>
        public string weaponDefinitionId;
        /// <summary>도주 회차 필드가 없는 구형 스냅샷과 구분합니다.</summary>
        public bool hasEscapeEpisode;
        /// <summary>새 도주 진입 횟수입니다.</summary>
        public int escapeCount;
        /// <summary>플레이어 회수 유예의 남은 초입니다.</summary>
        public float escapeGraceRemaining;
        /// <summary>현재 도주 실패 판정까지 남은 초입니다.</summary>
        public float escapeFailureRemaining;
        /// <summary>경로 실패 제한시간을 소진했는지 나타냅니다.</summary>
        public bool escapeFailed;
        /// <summary>단계별 추격 데이터가 있는 스냅샷인지 나타냅니다.</summary>
        public bool hasPursuitPhase;
        /// <summary>마지막 목격 위치 추격 회차의 활성 여부입니다.</summary>
        public bool pursuitActive;
        /// <summary>이동을 마치고 현장 수색 중인지 나타냅니다.</summary>
        public bool pursuitArrived;
        /// <summary>현재 이동 또는 수색 단계의 남은 시간입니다.</summary>
        public float pursuitRemaining;
        /// <summary>이동 정체를 허용할 남은 초입니다.</summary>
        public float pursuitStuckRemaining;
        /// <summary>지면 목적지와 별개인 마지막 조준 위치입니다.</summary>
        public Vector3 lastAim;
        /// <summary>계정 또는 개체의 영속 식별자입니다.</summary>
        public string id;
        /// <summary>펫 주인의 인증 계정입니다.</summary>
        public string owner;
        /// <summary>경찰 추격 대상의 고정 식별자입니다.</summary>
        public string target;
        /// <summary>복원할 월드 위치입니다.</summary>
        public Vector3 position;
        /// <summary>경찰이 마지막으로 목격한 위치입니다.</summary>
        public Vector3 lastKnown;
        /// <summary>펫 도주 후보 원의 중심입니다.</summary>
        public Vector3 escapeCenter;
        /// <summary>복원할 월드 회전입니다.</summary>
        public Quaternion rotation;
        /// <summary>현재 체력입니다.</summary>
        public int health;
        /// <summary>현재 구현된 경찰 무기 종류입니다.</summary>
        public int kind;
        /// <summary>해당 개체의 행동 상태 열거형 값입니다.</summary>
        public int state;
        /// <summary>경찰 소속 스폰 거점 번호입니다.</summary>
        public int station;
        /// <summary>다음 순찰 지점 번호입니다.</summary>
        public int patrolIndex;
        /// <summary>펫 운반 금액입니다. 원장과 함께 보존합니다.</summary>
        public int cargo;
        /// <summary>펫 등급입니다.</summary>
        public int grade;
        /// <summary>경찰이 대상의 신원을 확인했는지 나타냅니다.</summary>
        public bool identified;
        /// <summary>마지막 목격 위치의 유효 여부입니다.</summary>
        public bool hasLastKnown;
        /// <summary>대기 펫이 경찰 위험을 감시할지 나타냅니다.</summary>
        public bool watchThreats;
        /// <summary>수색 종료까지 남은 초입니다.</summary>
        public float searchRemaining;
        /// <summary>순찰 대기 종료까지 남은 초입니다.</summary>
        public float waitRemaining;
        /// <summary>다음 사격까지 남은 초입니다.</summary>
        public float fireRemaining;
        /// <summary>선회 방향 변경까지 남은 초입니다.</summary>
        public float orbitRemaining;
        /// <summary>펫 회복 계산의 소수 누적량입니다.</summary>
        public float recovery;
        /// <summary>플레이어 부활까지 남은 초입니다.</summary>
        public float respawnRemaining;
        /// <summary>탄창·재장전·사격 대기 데이터입니다.</summary>
        public string weaponJson;
        /// <summary>경찰 발견 게이지입니다.</summary>
        public float detection;
        /// <summary>전투 선회 방향의 부호입니다.</summary>
        public float orbitSign;
        /// <summary>피격 보복 추격의 남은 시간입니다.</summary>
        public float attackerRemaining;
        /// <summary>사망 후 풀 반환까지 남은 시간입니다.</summary>
        public float corpseRemaining;
        /// <summary>진행 중인 연사 묶음의 발사 횟수입니다.</summary>
        public int burstCount;
        /// <summary>순찰 지점 도착 후 대기 중인지 나타냅니다.</summary>
        public bool patrolWaiting;
        /// <summary>자신을 공격한 대상을 추격 중인지 나타냅니다.</summary>
        public bool pursuingAttacker;
    }
    /// <summary>세션 지갑을 순수 JSON으로 보존하는 계정 봉투입니다.</summary>
    [Serializable] public sealed class WalletRecord { public string playerId, json; }
    /// <summary>펫 고정 ID에 연결한 신고 계정입니다.</summary>
    [Serializable] public sealed class ReportRecord { public string petId, playerId; }
}
