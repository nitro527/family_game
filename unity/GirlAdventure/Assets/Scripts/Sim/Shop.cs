namespace GirlAdventure.Sim
{
    public enum BuyResult { Bought, NoGold, Full }

    /// <summary>
    /// 사탕 가게: 샘물 옆 가게에서 골드로 사탕을 산다. 가격은 items.json 의 "candy" Price.
    /// 샘물은 사탕을 3개까지만 채워 주므로, 먼 길(성·기지)을 떠나기 전에 더 사 두는 곳.
    /// </summary>
    public static class Shop
    {
        public const string CandyId = "candy";

        public static int CandyPrice => GameData.ItemPrice(CandyId);

        public static BuyResult CanBuyCandy(PlayerState p)
        {
            if (p.Candy >= PlayerState.BagCandy) return BuyResult.Full;
            if (p.Gold < CandyPrice) return BuyResult.NoGold;
            return BuyResult.Bought;
        }

        /// <summary>사탕 하나를 산다. 살 수 없으면 아무것도 바뀌지 않는다.</summary>
        public static BuyResult BuyCandy(PlayerState p)
        {
            var r = CanBuyCandy(p);
            if (r != BuyResult.Bought) return r;
            p.Gold -= CandyPrice;
            p.Candy++;
            return r;
        }
    }
}
