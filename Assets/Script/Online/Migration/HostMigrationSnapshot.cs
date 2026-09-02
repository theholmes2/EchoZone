using System.Collections.Generic;
using EchoZone.Online.Reconnect;

namespace EchoZone.Online.Migration
{
    /// <summary>Host Migration 때 한 플레이어의 식별자와 게임 상태를 함께 전달하는 데이터입니다.</summary>
    public sealed class HostMigrationPlayerSnapshot
    {
        /// <summary>플레이어 마이그레이션 복사본을 생성합니다.</summary>
        /// <param name="playerId">세션이 바뀌어도 동일 사용자를 찾을 인증 PlayerId입니다.</param>
        /// <param name="state">인벤토리와 체력·기력·마나를 담은 상태 복사본입니다.</param>
        public HostMigrationPlayerSnapshot(
            string playerId,
            PlayerSessionSnapshot state)
        {
            PlayerId = playerId ?? string.Empty;
            State = state;
        }

        /// <summary>세션이 바뀌어도 동일 사용자를 찾을 인증 PlayerId입니다.</summary>
        public string PlayerId { get; }

        /// <summary>인벤토리와 체력·기력·마나를 담은 상태 복사본입니다.</summary>
        public PlayerSessionSnapshot State { get; }
    }

    /// <summary>Host Migration 후 동일한 월드 아이템 상태를 복원하기 위한 데이터입니다.</summary>
    public sealed class WorldItemMigrationSnapshot
    {
        /// <summary>월드 아이템의 마이그레이션 복사본을 생성합니다.</summary>
        /// <param name="worldItemId">네트워크 세션이 바뀌어도 유지되는 월드 인스턴스 식별자입니다.</param>
        /// <param name="itemId">ItemCatalog에서 아이템 정의를 찾을 식별자입니다.</param>
        /// <param name="quantity">월드에 남아 있던 아이템 수량입니다.</param>
        /// <param name="isDepleted">수량이 소진되어 월드에서 제거된 상태인지 나타냅니다.</param>
        public WorldItemMigrationSnapshot(
            string worldItemId,
            string itemId,
            int quantity,
            bool isDepleted)
        {
            WorldItemId = worldItemId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            Quantity = quantity;
            IsDepleted = isDepleted;
        }

        /// <summary>네트워크 세션이 바뀌어도 유지되는 월드 인스턴스 식별자입니다.</summary>
        public string WorldItemId { get; }

        /// <summary>ItemCatalog에서 아이템 정의를 찾을 식별자입니다.</summary>
        public string ItemId { get; }

        /// <summary>월드에 남아 있던 아이템 수량입니다.</summary>
        public int Quantity { get; }

        /// <summary>수량이 소진되어 월드에서 제거된 상태인지 나타냅니다.</summary>
        public bool IsDepleted { get; }
    }

    /// <summary>새 Host가 게임 상태를 복원할 때 사용할 플레이어와 월드 아이템의 전체 복사본입니다.</summary>
    public sealed class HostMigrationSnapshot
    {
        /// <summary>Host Migration 전체 복사본을 생성하고 전달받은 목록을 내부 목록으로 복사합니다.</summary>
        /// <param name="runId">이 Snapshot이 속한 한 번의 게임 진행 식별자입니다.</param>
        /// <param name="snapshotVersion">같은 Run 안에서 최신 순서를 구분할 증가 버전입니다.</param>
        /// <param name="players">현재 Session에 존재하는 플레이어 상태 목록입니다.</param>
        /// <param name="worldItems">현재 월드 아이템 상태 목록입니다.</param>
        public HostMigrationSnapshot(
            string runId,
            long snapshotVersion,
            IReadOnlyList<HostMigrationPlayerSnapshot> players,
            IReadOnlyList<WorldItemMigrationSnapshot> worldItems)
        {
            RunId = runId ?? string.Empty;
            SnapshotVersion = snapshotVersion;
            Players = players != null
                ? new List<HostMigrationPlayerSnapshot>(players)
                : new List<HostMigrationPlayerSnapshot>();
            WorldItems = worldItems != null
                ? new List<WorldItemMigrationSnapshot>(worldItems)
                : new List<WorldItemMigrationSnapshot>();
        }

        /// <summary>이 Snapshot이 속한 한 번의 게임 진행 식별자입니다.</summary>
        public string RunId { get; }

        /// <summary>같은 Run 안에서 최신 Snapshot을 구분할 증가 버전입니다.</summary>
        public long SnapshotVersion { get; }

        /// <summary>현재 Session에 존재하는 플레이어 상태 목록입니다.</summary>
        public IReadOnlyList<HostMigrationPlayerSnapshot> Players { get; }

        /// <summary>현재 월드 아이템 상태 목록입니다.</summary>
        public IReadOnlyList<WorldItemMigrationSnapshot> WorldItems { get; }
    }
}
