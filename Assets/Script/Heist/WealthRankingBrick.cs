using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EchoZone.Heist
{
    /// <summary>인증 정보 대신 공개용 별칭과 확정 보유금만 전달하는 랭킹 행입니다.</summary>
    public sealed class WealthRankingEntry
    {
        /// <summary>동일 금액은 같은 순위로 표시합니다.</summary>
        public int Rank { get; set; }
        /// <summary>로그인 아이디를 노출하지 않는 임시 공개 별칭입니다.</summary>
        public string DisplayName { get; set; } = "";
        /// <summary>탈출 정산으로 Cloud에 확정된 보유금입니다.</summary>
        public long Balance { get; set; }
        /// <summary>조회한 본인의 행인지 표시합니다.</summary>
        public bool IsSelf { get; set; }
    }

    /// <summary>공개 랭킹 목록과 순위 밖에서도 볼 수 있는 본인 확정 잔액입니다.</summary>
    public sealed class WealthRankingResult
    {
        /// <summary>서버가 반환한 보유금 상위 목록입니다.</summary>
        public WealthRankingEntry[] Entries { get; set; } = Array.Empty<WealthRankingEntry>();
        /// <summary>계정 지갑이 아직 준비되지 않았으면 null입니다.</summary>
        public long? OwnBalance { get; set; }
    }

    /// <summary>Cloud 조회 결과의 정렬·동점 순위·임시 별칭을 계산하는 순수 규칙입니다.</summary>
    public static class WealthRankingBrick
    {
        /// <summary>한 번 조회하는 최대 인원으로 전체 계정을 클라이언트에 내리지 않습니다.</summary>
        public const int Limit = 20;
        /// <summary>원본 PlayerId나 로그인 이름 대신 안정적인 해시 별칭을 만듭니다.</summary>
        public static string Alias(string playerId)
        {
            using var sha = SHA256.Create();
            return "도둑-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(playerId))).Replace("-", "").Substring(0, 12);
        }
        /// <summary>이미 서버에서 조회한 상위 후보만 정렬하고 같은 금액에 동일 순위를 줍니다.</summary>
        public static WealthRankingEntry[] Build(IEnumerable<KeyValuePair<string, long>> balances, string self)
        {
            var sorted = balances.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Take(Limit).ToArray();
            var rows = new WealthRankingEntry[sorted.Length];
            for (int i = 0; i < sorted.Length; i++)
            {
                if (sorted[i].Value < 0) throw new InvalidOperationException("Invalid ranking balance.");
                rows[i] = new WealthRankingEntry { Rank = i > 0 && sorted[i - 1].Value == sorted[i].Value ? rows[i - 1].Rank : i + 1,
                    DisplayName = Alias(sorted[i].Key), Balance = sorted[i].Value, IsSelf = sorted[i].Key == self };
            }
            return rows;
        }
    }
}
