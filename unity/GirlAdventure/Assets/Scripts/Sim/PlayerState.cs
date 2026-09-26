namespace GirlAdventure.Sim
{
    /// <summary>주인공(첫째)의 능력치와 진행 상태. UnityEngine 의존 없음.</summary>
    public class PlayerState
    {
        public const int StartHp = 22, StartMp = 10, StartAtk = 5, MaxCandy = 3;
        public const int HpPerLevel = 6, MpPerLevel = 3, AtkPerLevel = 2;

        public string Name = "첫째";
        public int Lv = 1;
        public int Hp = StartHp, MaxHp = StartHp;
        public int Mp = StartMp, MaxMp = StartMp;
        public int Atk = StartAtk;
        public int Exp;
        public int Candy = MaxCandy;
        public int X, Y;
        public bool BossDown;

        /// <summary>다음 레벨까지 필요한 누적 경험치.</summary>
        public static int NextExp(int lv) => 5 * lv * (lv + 1);

        public static PlayerState NewGame(string name)
        {
            var p = new PlayerState { Name = string.IsNullOrWhiteSpace(name) ? "첫째" : name.Trim() };
            p.X = WorldMap.Start.x;
            p.Y = WorldMap.Start.y;
            return p;
        }

        /// <summary>경험치를 얻고, 오른 레벨 수를 돌려준다. 레벨업 시 HP/MP 완전 회복.</summary>
        public int GainExp(int amount)
        {
            Exp += amount;
            int ups = 0;
            while (Exp >= NextExp(Lv))
            {
                Lv++;
                MaxHp += HpPerLevel;
                MaxMp += MpPerLevel;
                Atk += AtkPerLevel;
                Hp = MaxHp;
                Mp = MaxMp;
                ups++;
            }
            return ups;
        }

        /// <summary>샘물: HP/MP/사탕 전부 회복.</summary>
        public void RestoreAll()
        {
            Hp = MaxHp;
            Mp = MaxMp;
            Candy = MaxCandy;
        }
    }
}
