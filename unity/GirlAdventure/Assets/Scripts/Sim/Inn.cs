namespace GirlAdventure.Sim
{
    /// <summary>별빛 마을 여관: 골드를 내고 하룻밤 자면 HP·MP 가득 (사탕은 샘물처럼 채워 주지 않음).</summary>
    public static class Inn
    {
        public const int Price = 10;

        /// <summary>묵을 수 있으면 돈을 내고 회복하고 true, 골드가 모자라면 아무것도 안 하고 false.</summary>
        public static bool Stay(PlayerState p)
        {
            if (p.Gold < Price) return false;
            p.GainGold(-Price);
            p.Hp = p.MaxHp;
            p.Mp = p.MaxMp;
            return true;
        }
    }
}
