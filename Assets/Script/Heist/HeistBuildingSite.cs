using UnityEngine;

namespace EchoZone.Heist
{
    /// <summary>건물의 식별자와 외부 출입구를 제공하는 씬 데이터입니다. 내부는 현재 시간 경과로 표현합니다.</summary>
    public sealed class HeistBuildingSite : MonoBehaviour
    {
        /// <summary>모든 피어에서 동일한 건물 번호입니다.</summary>
        [SerializeField] private int buildingId;
        /// <summary>펫·경찰이 접근할 NavMesh 위 출입구입니다.</summary>
        [SerializeField] private Transform entrance;
        /// <summary>HUD에서 표시할 이름입니다.</summary>
        [SerializeField] private string displayName;
        /// <summary>절도와 정기 검사를 허용하는 건물입니다. 끄더라도 저장 원장의 식별자는 유지합니다.</summary>
        [SerializeField] private bool lootEnabled = true;
        /// <summary>이번 맵에서 돈을 훔칠 수 있는 건물인지 나타냅니다.</summary>
        public bool IsLootSite => lootEnabled;
        /// <summary>서버 원장과 동기화 목록의 키입니다.</summary>
        public int Id => buildingId;
        /// <summary>건물 표시 이름입니다.</summary>
        public string DisplayName => displayName;
        /// <summary>검사와 절도의 도착 지점입니다.</summary>
        public Vector3 Entrance => entrance != null ? entrance.position : transform.position;
    }
}
