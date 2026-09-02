using System.Text;
using UnityEngine;

namespace EchoZone.Online.Migration
{
    /// <summary>월드 아이템 Brick의 현재 상태와 세션 독립 식별자를 Collector에 제공하는 컴포넌트입니다.</summary>
    [RequireComponent(typeof(ItemPickup))]
    public sealed class WorldItemMigrationSource : MonoBehaviour
    {
        [SerializeField] private string worldItemId = string.Empty;

        private ItemPickup itemPickup;

        /// <summary>세션이 바뀌어도 동일 월드 아이템을 찾을 식별자입니다.</summary>
        public string WorldItemId => worldItemId;

        /// <summary>월드 아이템 Brick을 찾고 ID가 비어 있으면 현재 씬 계층에서 안정적인 ID를 만듭니다.</summary>
        private void Awake()
        {
            itemPickup = GetComponent<ItemPickup>();

            if (string.IsNullOrWhiteSpace(worldItemId))
            {
                worldItemId = BuildSceneHierarchyId(transform);
            }
        }

        /// <summary>동적으로 생성된 아이템에 외부 저장소가 관리하는 영구 월드 ID를 지정합니다.</summary>
        /// <param name="persistentWorldItemId">세션을 넘어서 유지할 월드 아이템 식별자입니다.</param>
        public void AssignWorldItemId(string persistentWorldItemId)
        {
            if (!string.IsNullOrWhiteSpace(persistentWorldItemId))
            {
                worldItemId = persistentWorldItemId;
            }
        }

        /// <summary>현재 월드 아이템 상태를 Host Migration 데이터로 복사합니다.</summary>
        /// <returns>필수 식별자와 ItemData가 준비되었으면 복사본이며 아니면 null입니다.</returns>
        public WorldItemMigrationSnapshot CreateSnapshot()
        {
            if (itemPickup == null ||
                itemPickup.ItemDefinition == null ||
                string.IsNullOrWhiteSpace(worldItemId))
            {
                return null;
            }

            return new WorldItemMigrationSnapshot(
                worldItemId,
                itemPickup.ItemDefinition.ItemId,
                itemPickup.Quantity,
                itemPickup.IsEmpty);
        }

        /// <summary>새 Host가 받은 수량과 고갈 상태를 현재 월드 아이템 Brick에 적용합니다.</summary>
        public void ApplySnapshot(WorldItemMigrationSnapshot snapshot)
        {
            if (snapshot == null ||
                itemPickup == null ||
                !string.Equals(worldItemId, snapshot.WorldItemId, System.StringComparison.Ordinal) ||
                itemPickup.ItemDefinition == null ||
                !string.Equals(
                    itemPickup.ItemDefinition.ItemId,
                    snapshot.ItemId,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            itemPickup.SetQuantity(snapshot.IsDepleted ? 0 : snapshot.Quantity);
        }

        /// <summary>씬에 직접 배치된 오브젝트를 다시 찾을 수 있도록 씬 경로와 형제 순서로 ID를 만듭니다.</summary>
        private static string BuildSceneHierarchyId(Transform target)
        {
            StringBuilder builder = new(target.gameObject.scene.path);
            BuildTransformPath(target, builder);
            return builder.ToString();
        }

        /// <summary>부모부터 현재 Transform까지 이름과 형제 순서를 ID 문자열에 추가합니다.</summary>
        private static void BuildTransformPath(Transform target, StringBuilder builder)
        {
            if (target.parent != null)
            {
                BuildTransformPath(target.parent, builder);
            }

            builder.Append('/');
            builder.Append(target.name);
            builder.Append('[');
            builder.Append(target.GetSiblingIndex());
            builder.Append(']');
        }
    }
}
