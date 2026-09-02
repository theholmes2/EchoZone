using System;
using System.Text;
using Unity.Services.Multiplayer;

namespace EchoZone.Online.Migration
{
    /// <summary>
    /// MPS Session의 주기적 마이그레이션 데이터와 EchoZone Snapshot을 연결합니다.
    /// Relay, Cloud, 화면 상태는 알지 못합니다.
    /// </summary>
    public sealed class HostMigrationSessionDataHandler : IMigrationDataHandler
    {
        private readonly HostMigrationSnapshotCollector collector;
        private readonly HostMigrationSnapshotJsonSerializer serializer;
        private readonly Action<HostMigrationSnapshot> snapshotReceived;

        public HostMigrationSessionDataHandler(
            HostMigrationSnapshotCollector collector,
            Action<HostMigrationSnapshot> snapshotReceived)
        {
            this.collector = collector;
            this.snapshotReceived = snapshotReceived;
            serializer = new HostMigrationSnapshotJsonSerializer();
        }

        /// <summary>현재 Host의 권위 상태를 UTF-8 JSON 바이트로 생성합니다.</summary>
        public byte[] Generate()
        {
            if (collector == null ||
                !collector.TryCollect(out HostMigrationSnapshot snapshot))
            {
                return Array.Empty<byte>();
            }

            string json = serializer.Serialize(snapshot);
            return string.IsNullOrEmpty(json)
                ? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// 새 Host가 받은 데이터를 검증한 뒤 적용 준비 콜백에 전달합니다.
        /// 실제 Unity 상태 적용은 새 NGO Host가 시작된 뒤 Glue가 수행합니다.
        /// </summary>
        public void Apply(byte[] migrationData)
        {
            if (migrationData == null || migrationData.Length == 0)
            {
                return;
            }

            string json = Encoding.UTF8.GetString(migrationData);
            if (serializer.TryDeserialize(json, out HostMigrationSnapshot snapshot))
            {
                snapshotReceived?.Invoke(snapshot);
            }
        }
    }
}
