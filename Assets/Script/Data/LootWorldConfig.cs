using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoZone.Equipment
{
    /// <summary>한 종류의 월드 전리품과 생성 수량·상대 확률을 정의합니다.</summary>
    [Serializable]
    public sealed class RandomLootEntry
    {
        /// <summary>바닥에 표시하고 획득할 아이템 정의입니다.</summary>
        public ItemData item;
        /// <summary>한 번 생성할 아이템 수량입니다.</summary>
        [Min(1)] public int amount = 1;
        /// <summary>다른 항목과 비교할 상대 생성 가중치입니다.</summary>
        [Min(0f)] public float weight = 1f;
    }

    /// <summary>전리품 상자와 랜덤 월드 전리품 생성 수치를 관리합니다.</summary>
    [CreateAssetMenu(menuName = "EchoZone/Equipment/Loot World Config")]
    public sealed class LootWorldConfig : ScriptableObject
    {
        /// <summary>서버가 생성할 NGO 등록 상자 프리팹입니다.</summary>
        public LootBagGlue prefab;
        /// <summary>저장한 아이템 ID를 실제 정의로 해석합니다.</summary>
        public ItemCatalog items;
        /// <summary>서버가 생성 회차마다 가중치로 선택할 월드 전리품 목록입니다.</summary>
        [SerializeField] private List<RandomLootEntry> randomDrops = new();
        /// <summary>상자 획득을 허용할 최대 거리입니다.</summary>
        [Min(.1f)] public float interactionDistance = 3;
        /// <summary>랜덤 전리품 생성 판정 간격입니다.</summary>
        [Min(1)] public float spawnInterval = 45;
        /// <summary>각 생성 회차에 전리품을 놓을 확률입니다.</summary>
        [Range(0, 1)] public float spawnChance = .6f;
        /// <summary>동시에 남아 있을 랜덤 월드 전리품 상한입니다.</summary>
        [Min(0)] public int randomBoxLimit = 8;
        /// <summary>새 방 시작 직후 맵 전역에 빠르게 채울 전리품 수입니다.</summary>
        [Min(0)] public int initialSpawnCount = 10;
        /// <summary>생성 위치 주변 장애물 검사 반지름입니다.</summary>
        [Min(.01f)] public float clearance = .35f;
        /// <summary>상자가 건물이나 다른 개체 속에 생기지 않게 검사할 레이어입니다.</summary>
        public LayerMask obstacleLayers = Physics.DefaultRaycastLayers;
        /// <summary>지붕 아래와 실내 후보를 제외하기 위해 생성 지점 위를 검사할 높이입니다.</summary>
        [Min(0f)] public float openSkyCheckHeight = 30f;
        /// <summary>바닥 전리품의 물리 중심은 유지하면서 메시와 파티클만 지면에 맞추는 위치 보정입니다.</summary>
        public Vector3 looseVisualOffset = new(0f, -0.18f, 0f);

        /// <summary>서버가 가중치 선택에 사용할 랜덤 전리품 후보입니다.</summary>
        public IReadOnlyList<RandomLootEntry> RandomDrops => randomDrops;
    }
}
