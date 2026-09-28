using System.Collections.Generic;
using System.Threading.Tasks;
using EchoZone.Online.Migration;
using EchoZone.Pet;
using UnityEngine;

namespace EchoZone.Heist
{
    public sealed partial class HeistWorldGlue
    {
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
                if (checkpoint == null || !await checkpoint.SaveCurrentCheckpointAsync()) return;
                if (this == null || generation != retirementGeneration || !IsSpawned || !IsServer || SessionWorldMigrationGlue.IsRestoring) return;
                foreach (var pet in new List<PetStateGlue>(PetUpdateManager.Pets))
                    if (pet != null && pet.IsSpawned && ids.Contains(pet.PetId)) pet.NetworkObject.Despawn(true);
                pendingRetirements.ExceptWith(ids);
            }
            finally
            {
                if (this != null && generation == retirementGeneration)
                { retirementSaving = false; nextRetirementSave = Time.unscaledTime + config.InspectionRetrySeconds; }
            }
        }
    }
}
