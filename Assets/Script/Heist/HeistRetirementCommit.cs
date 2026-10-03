using System.Collections.Generic;
using System.Threading.Tasks;
using EchoZone.Online.Migration;
using EchoZone.Pet;
using UnityEngine;

namespace EchoZone.Heist
{
    public sealed partial class HeistWorldGlue
    {
        /// <summary>월드 내 회수 정산을 고정 ID로 추적하는 요청 이력입니다. 개인 Cloud 현금 정산과는 다릅니다.</summary>
        private readonly List<RetirementRequestRecord> retirementJournal = new();
        /// <summary>원장 반영 후 Cloud 체크포인트 확인을 기다리는 펫입니다.</summary>
        private readonly HashSet<string> pendingRetirements = new();
        /// <summary>같은 회수 완료 기록을 동시에 저장하지 않습니다.</summary>
        private bool retirementSaving;
        /// <summary>Cloud 오류 재시도 시각입니다.</summary>
        private float nextRetirementSave;
        /// <summary>이전 세션의 비동기 응답을 새 세션에 적용하지 않는 세대입니다.</summary>
        private int retirementGeneration;

        /// <summary>기존 서버 루프에서 반환·보상·펫 종료를 하나의 durable 체크포인트로 확정합니다.</summary>
        private void FlushRetirements()
        {
            if (retirementSaving || pendingRetirements.Count == 0 || Time.unscaledTime < nextRetirementSave || SessionWorldMigrationGlue.IsRestoring) return;
            _ = CommitRetirements();
        }

        /// <summary>저장이 확인된 뒤에만 외형을 디스폰합니다. 실패 시 펫은 상호작용 불가로 보관합니다.</summary>
        private async Task CommitRetirements()
        {
            retirementSaving = true;
            int generation = retirementGeneration;
            var ids = new HashSet<string>(pendingRetirements);
            var checkpoint = FindFirstObjectByType<HostMigrationCloudCheckpointGlue>();
            try
            {
                foreach (var record in retirementJournal)
                    if (ids.Contains(record.PetId)) { record.Attempts++; record.State = "AwaitingCheckpoint"; }
                if (checkpoint == null || !await checkpoint.SaveCurrentCheckpointAsync()) return;
                if (this == null || generation != retirementGeneration || !IsSpawned || !IsServer || SessionWorldMigrationGlue.IsRestoring) return;
                foreach (var pet in new List<PetStateGlue>(PetUpdateManager.Pets))
                    if (pet != null && pet.IsSpawned && ids.Contains(pet.PetId)) pet.NetworkObject.Despawn(true);
                pendingRetirements.ExceptWith(ids);
                foreach (var record in retirementJournal) if (ids.Contains(record.PetId)) record.State = "Confirmed";
            }
            finally
            {
                if (this != null && generation == retirementGeneration)
                {
                    retirementSaving = false;
                    int attempts = 1;
                    foreach (var record in retirementJournal) if (ids.Contains(record.PetId)) attempts = System.Math.Max(attempts, record.Attempts);
                    nextRetirementSave = Time.unscaledTime + (float)SettlementJournalBrick.RetryDelay(attempts,
                        config.InspectionRetrySeconds, ExtractionPoint.Instance?.Config?.MaximumRetrySeconds ?? 60);
                }
            }
        }
    }

    /// <summary>회수 요청의 금액·대상과 재시도 상태입니다. Snapshot의 원장/건물/지갑과 같은 Cloud 항목에 저장합니다.</summary>
    public sealed class RetirementRequestRecord
    {
        /// <summary>회수 승인마다 한 번 만드는 멱등성 키입니다.</summary>
        public string SettlementId;
        /// <summary>최초 방과 실행입니다.</summary>
        public string SessionId, RunId;
        /// <summary>감사비 대상 계정입니다. 자동 회수는 빈 값입니다.</summary>
        public string PlayerId;
        /// <summary>세션 소멸 후에도 요청을 찾을 계정입니다. 자동 회수는 최초 승인 호스트로 고정합니다.</summary>
        public string CustodianPlayerId;
        /// <summary>종료할 펫과 반환한 장물 건들입니다.</summary>
        public string PetId;
        public string[] LedgerIds;
        /// <summary>수동 정합성 복구에도 원래 건물별 반환 금액을 재구성할 수 있도록 보관합니다.</summary>
        public Dictionary<int, int> ReturnedAmounts;
        /// <summary>계정 지갑 기준 버전과 이번 감사비 반영 후 세션 잔액입니다. Cloud 확정 잔액으로 쓰지 않습니다.</summary>
        public long ExpectedRevision, Balance;
        /// <summary>로드 전 보상 지갑인지 구분합니다.</summary>
        public bool WalletLoaded;
        /// <summary>검증된 신고 감사비입니다.</summary>
        public int Reward;
        /// <summary>승인 UTC와 체크포인트 대기 상태입니다.</summary>
        public string CreatedAtUtc, State;
        /// <summary>체크포인트 저장 시도 횟수입니다.</summary>
        public int Attempts;
    }
}
