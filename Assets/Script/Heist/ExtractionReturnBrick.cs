using System;

namespace EchoZone.Heist
{
    /// <summary>탈출할 때 반납하는 코인·탄약·총기의 환불 금액을 Unity 참조 없이 계산합니다.</summary>
    public static class ExtractionReturnBrick
    {
        /// <summary>상점 판매 금액을 구매 기준가에 비례해 계산하며 소수 금액은 버립니다.</summary>
        public static long SaleCredit(int quantity, int bundleQuantity, int bundlePrice, int percent)
        {
            if (quantity < 0 || bundleQuantity <= 0 || bundlePrice < 0 || percent < 0 || percent > 100)
                throw new System.ArgumentOutOfRangeException();
            return checked((long)quantity * bundlePrice * percent) / (100L * bundleQuantity);
        }
        /// <summary>코인의 액면가와 수량을 곱해 지갑에 반영할 금액을 계산합니다.</summary>
        public static long CoinCredit(int quantity, int unitValue)
        {
            if (quantity < 0 || unitValue < 0) throw new ArgumentOutOfRangeException();
            return checked((long)quantity * unitValue);
        }

        /// <summary>탄약 묶음 가격을 실제 반납 탄수에 비례시키고 소수 금액은 버립니다.</summary>
        public static long AmmunitionCredit(int rounds, int bundleRounds, int bundlePrice)
        {
            if (rounds < 0 || bundleRounds <= 0 || bundlePrice < 0) throw new ArgumentOutOfRangeException();
            return checked((long)rounds * bundlePrice / bundleRounds);
        }

        /// <summary>무료 획득 총기는 0원, 구매 총기는 기록된 실제 구매가만 돌려줍니다.</summary>
        public static long WeaponCredit(int purchaseValue)
        {
            if (purchaseValue < 0) throw new ArgumentOutOfRangeException(nameof(purchaseValue));
            return purchaseValue;
        }
    }
}
