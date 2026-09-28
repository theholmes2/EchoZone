using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace EchoZone.Pet
{
    /// <summary>주인 유무와 무관하게 모든 펫을 기존 씬 런타임 루프에서 갱신합니다.</summary>
    public static class PetUpdateManager
    {
        /// <summary>각 피어의 현재 생성된 펫입니다.</summary>
        private static readonly List<PetStateGlue> pets = new();
        /// <summary>서버 작업과 로컬 HUD가 읽는 활성 펫 목록입니다.</summary>
        public static IReadOnlyList<PetStateGlue> Pets => pets;
        /// <summary>도메인 리로드를 끈 에디터 실행에서도 목록을 초기화합니다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => pets.Clear();
        /// <summary>중복 없이 생성된 펫을 등록합니다.</summary>
        public static void Register(PetStateGlue pet) { if (!pets.Contains(pet)) pets.Add(pet); }
        /// <summary>디스폰된 펫을 제거합니다.</summary>
        public static void Unregister(PetStateGlue pet) => pets.Remove(pet);
        /// <summary>기존 런타임 Glue에서 피어당 한 번 호출합니다.</summary>
        public static void ManualUpdate(float deltaTime)
        {
            for (int i = pets.Count - 1; i >= 0; i--)
                if (pets[i] != null && pets[i].IsSpawned) pets[i].ManualUpdate(deltaTime);
        }
        /// <summary>주인 사망·퇴장 시 소속된 모든 펫을 현재 위치에서 대기시킵니다.</summary>
        public static void ReleaseOwned(NetworkObject owner)
        {
            foreach (var pet in pets)
                if (pet != null && pet.IsSpawned && pet.IsServer && pet.IsOwnedBy(owner)) pet.WaitServer();
        }
        /// <summary>다중 소유 확인용 현재 소속 펫 수입니다.</summary>
        public static int CountOwned(NetworkObject owner)
        {
            int count = 0;
            foreach (var pet in pets) if (pet != null && pet.IsSpawned && pet.IsOwnedBy(owner)) count++;
            return count;
        }
    }
}
