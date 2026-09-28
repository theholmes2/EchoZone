using System;
using Unity.Netcode;

namespace EchoZone.Heist
{
    /// <summary>건물별 돈과 검사 예정 시각을 모든 피어에 복제합니다.</summary>
    public struct HeistBuildingState : INetworkSerializable, IEquatable<HeistBuildingState>
    {
        /// <summary>씬 건물 식별자입니다.</summary>
        public int Id;
        /// <summary>현재 남아 있는 현금입니다.</summary>
        public int Money;
        /// <summary>다음 검사 작업이 배정될 수 있는 서버 시각입니다.</summary>
        public double NextInspection;
        /// <summary>경찰이 실제로 내부 검사 중인지 나타냅니다.</summary>
        public bool Inspecting;
        /// <summary>검사 경찰이 배정되어 문 앞으로 이동 중인지 나타냅니다.</summary>
        public bool InspectionEnRoute;
        /// <summary>실제 입장 후 검사 종료 서버 시각입니다.</summary>
        public double InspectionEndsAt;
        /// <summary>도난 현장 주변 수색 명령의 서버 만료 시각입니다.</summary>
        public double SearchUntil;
        /// <summary>NGO 전송 필드 순서입니다.</summary>
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Id); s.SerializeValue(ref Money); s.SerializeValue(ref NextInspection); s.SerializeValue(ref Inspecting); s.SerializeValue(ref SearchUntil); s.SerializeValue(ref InspectionEnRoute); s.SerializeValue(ref InspectionEndsAt); }
        /// <summary>변경 감지 비교입니다.</summary>
        public bool Equals(HeistBuildingState other) => Id == other.Id && Money == other.Money && NextInspection == other.NextInspection && Inspecting == other.Inspecting && SearchUntil == other.SearchUntil && InspectionEnRoute == other.InspectionEnRoute && InspectionEndsAt == other.InspectionEndsAt;
    }
}
