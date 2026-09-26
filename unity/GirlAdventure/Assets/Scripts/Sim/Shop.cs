using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    public enum BuyResult { Bought, NoGold, Full }

    /// <summary>
    /// 도구 가게: 샘물 옆 가게에서 골드로 소모품을 산다. 가격과 파는 곳(Shops: "field"/"cave")은 items.json.
    /// 샘물은 사탕을 3개까지만 채워 주므로, 먼 길(성·기지)을 떠나기 전에 더 사 두는 곳.
    /// </summary>
    public static class Shop
    {
        public const string CandyId = "candy";

        public static int CandyPrice => GameData.ItemPrice(CandyId);

        /// <summary>이 맵 가게에서 파는 물건 id (데이터 순서).</summary>
        public static List<string> Goods(MapKind kind)
        {
            string where = kind == MapKind.Cave ? "cave" : "field";
            var list = new List<string>();
            foreach (var d in GameData.Consumables)
                if (d.Price > 0 && d.Shops != null && d.Shops.Split(',').Contains(where)) list.Add(d.Id);
            return list;
        }

        public static BuyResult CanBuy(PlayerState p, string id)
        {
            if (p.Count(id) >= Items.BagMax) return BuyResult.Full;
            if (p.Gold < GameData.ItemPrice(id)) return BuyResult.NoGold;
            return BuyResult.Bought;
        }

        /// <summary>하나 산다. 살 수 없으면 아무것도 바뀌지 않는다.</summary>
        public static BuyResult Buy(PlayerState p, string id)
        {
            var r = CanBuy(p, id);
            if (r != BuyResult.Bought) return r;
            p.Gold -= GameData.ItemPrice(id);
            p.AddItem(id, 1);
            return r;
        }

        public static BuyResult BuyCandy(PlayerState p) => Buy(p, CandyId);

        static bool Contains(this string[] a, string s) => System.Array.IndexOf(a, s) >= 0;
    }
}
