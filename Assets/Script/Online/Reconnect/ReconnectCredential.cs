using System;

namespace EchoZone.Online.Reconnect
{
    /// <summary>NGO 연결 승인에 전달할 PlayerId와 일회용 재접속 티켓입니다.</summary>
    public readonly struct ReconnectCredential
    {
        /// <summary>Separator 값을 저장합니다.</summary>
        private const char Separator = '\n';

        /// <summary>연결 승인 자격 정보를 생성합니다.</summary>
        /// <param name="playerId">Unity Authentication PlayerId입니다.</param>
        /// <param name="ticket">최초 접속이면 비어 있고 재접속이면 서버 발급 티켓입니다.</param>
        public ReconnectCredential(string playerId, string ticket)
        {
            PlayerId = playerId ?? string.Empty;
            Ticket = ticket ?? string.Empty;
        }

        /// <summary>Unity Authentication PlayerId입니다.</summary>
        public string PlayerId { get; }

        /// <summary>서버가 이전 연결에서 발급한 재접속 티켓입니다.</summary>
        public string Ticket { get; }

        /// <summary>NGO ConnectionData로 전달할 문자열로 변환합니다.</summary>
        /// <returns>PlayerId와 티켓을 순서대로 담은 문자열입니다.</returns>
        public string Serialize()
        {
            return $"{PlayerId}{Separator}{Ticket}";
        }

        /// <summary>NGO ConnectionData 문자열을 연결 승인 자격 정보로 복원합니다.</summary>
        /// <param name="serialized">클라이언트가 전달한 Payload 문자열입니다.</param>
        /// <param name="credential">복원된 연결 승인 자격 정보입니다.</param>
        /// <returns>형식과 PlayerId가 유효하면 <see langword="true"/>입니다.</returns>
        public static bool TryDeserialize(
            string serialized,
            out ReconnectCredential credential)
        {
            credential = default;
            if (string.IsNullOrEmpty(serialized))
            {
                return false;
            }

            int separatorIndex = serialized.IndexOf(Separator);
            if (separatorIndex <= 0)
            {
                return false;
            }

            string playerId = serialized.Substring(0, separatorIndex);
            string ticket = serialized.Substring(separatorIndex + 1);
            credential = new ReconnectCredential(playerId, ticket);
            return !string.IsNullOrWhiteSpace(playerId);
        }
    }

    /// <summary>현재 실행 중인 Client가 받은 최신 재접속 티켓을 메모리에만 보관합니다.</summary>
    public static class ReconnectTicketMemoryStore
    {
        /// <summary>sessionCode 값을 저장합니다.</summary>
        private static string sessionCode = string.Empty;
        /// <summary>expiresAt 값을 저장합니다.</summary>
        private static double expiresAt = double.PositiveInfinity;
        /// <summary>다음 재접속 요청에 사용할 최신 티켓입니다.</summary>
        public static string Ticket { get; private set; } = string.Empty;

        /// <summary>서버가 Owner에게 발급한 최신 티켓으로 교체합니다.</summary>
        /// <param name="ticket">새로 발급받은 티켓입니다.</param>
        public static void Replace(string ticket)
        {
            Ticket = ticket ?? string.Empty;
            expiresAt = double.PositiveInfinity;
        }

        /// <summary>다른 방에 참가할 때 이전 방 티켓을 전달하지 않습니다.</summary>
        public static void PrepareForSession(string code)
        {
            string normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.Equals(sessionCode, normalized, StringComparison.Ordinal))
            {
                Reset();
                sessionCode = normalized;
            }
        }

        /// <summary>최초 단절 시점부터 캐시 유예 시간을 계산합니다. 재시도는 만료를 연장하지 않습니다.</summary>
        public static void MarkDisconnected(double now, double retentionSeconds)
        {
            if (double.IsPositiveInfinity(expiresAt))
            {
                expiresAt = now + Math.Max(0, retentionSeconds);
            }
        }

        /// <summary>GetTicket 작업을 수행합니다.</summary>
        public static string GetTicket(double now)
        {
            if (now >= expiresAt)
            {
                Ticket = string.Empty;
            }
            return Ticket;
        }

        /// <summary>도메인 리로드를 꺼도 새 Play 실행에는 이전 티켓을 남기지 않습니다.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Ticket = string.Empty;
            sessionCode = string.Empty;
            expiresAt = double.PositiveInfinity;
        }
    }
}
