using System.Collections.Generic;
using UnityEngine;

namespace EchoZone.Equipment
{
    /// <summary>씬의 고정 총기상점 위치를 서버 검증과 로컬 UI에 제공합니다.</summary>
    public sealed class EquipmentShopGlue : MonoBehaviour
    {
        /// <summary>씬 안에서 중복되지 않는 상점 번호입니다.</summary>
        [SerializeField] private int shopId = 1;
        private static readonly HashSet<EquipmentShopGlue> shops = new();
        /// <summary>구매 요청이 대상으로 지정하는 상점 번호입니다.</summary>
        public int ShopId => shopId;
        private void OnEnable() => shops.Add(this);
        private void OnDisable() => shops.Remove(this);

        /// <summary>동일 ID가 둘 이상이면 잘못된 설정으로 간주하여 거절합니다.</summary>
        public static EquipmentShopGlue Find(int id)
        {
            EquipmentShopGlue found = null;
            foreach (var shop in shops)
                if (shop != null && shop.shopId == id)
                {
                    if (found != null) return null;
                    found = shop;
                }
            return found;
        }

        /// <summary>플레이어가 접근 가능한 가장 가까운 상점을 찾습니다.</summary>
        public static EquipmentShopGlue Nearest(Vector3 position, float radius)
        {
            EquipmentShopGlue found = null;
            float distance = radius * radius;
            foreach (var shop in shops)
            {
                if (shop == null) continue;
                float candidate = (shop.transform.position - position).sqrMagnitude;
                if (candidate > distance) continue;
                distance = candidate; found = shop;
            }
            return found;
        }
    }
}
