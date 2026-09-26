using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>세이브 파일 한 칸의 모양 (JSON). 능력치는 레벨에서 다시 계산하므로 저장하지 않는다.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 2;        // 2: 챕터·아이템·연 상자 추가
        public int Version = CurrentVersion;
        public string Name;
        public int Lv, Exp, Hp, Mp, Candy, X, Y;
        public bool BossDown;
        public int Chapter;
        public string[] Items, Opened;
        public string SavedAt;          // 표시용 (예: "09-26 14:30")
    }

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
        public int Chapter = 1;
        public List<string> Items = new List<string>();     // 중요 아이템 id (예: "key")
        public List<string> Opened = new List<string>();    // 연 보물상자 "x,y"

        public bool HasItem(string id) => Items.Contains(id);

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

        public SaveData ToSave(string savedAt = "") => new SaveData
        {
            Name = Name, Lv = Lv, Exp = Exp, Hp = Hp, Mp = Mp, Candy = Candy, X = X, Y = Y, BossDown = BossDown, SavedAt = savedAt,
            Chapter = Chapter, Items = Items.ToArray(), Opened = Opened.ToArray(),
        };

        /// <summary>
        /// 세이브에서 복원. 최대 HP/MP/공격력은 레벨로 다시 계산하고, 값이 이상하면 안전한 값으로 고친다
        /// (막힌 칸이면 시작 지점, HP 0 이면 가득).
        /// </summary>
        public static PlayerState FromSave(SaveData s, WorldMap map)
        {
            var p = NewGame(s.Name);
            int lv = Math.Max(1, Math.Min(99, s.Lv));
            p.Lv = lv;
            p.MaxHp = StartHp + HpPerLevel * (lv - 1);
            p.MaxMp = StartMp + MpPerLevel * (lv - 1);
            p.Atk = StartAtk + AtkPerLevel * (lv - 1);
            p.Exp = Math.Max(lv > 1 ? NextExp(lv - 1) : 0, Math.Min(s.Exp, NextExp(lv) - 1));
            p.Hp = s.Hp <= 0 ? p.MaxHp : Math.Min(s.Hp, p.MaxHp);
            p.Mp = Math.Max(0, Math.Min(s.Mp, p.MaxMp));
            p.Candy = Math.Max(0, Math.Min(s.Candy, MaxCandy));
            p.BossDown = s.BossDown;
            p.Chapter = Math.Max(1, Math.Min(2, s.Chapter));   // 옛 세이브(버전 1)는 0 → 챕터 1
            if (s.Items != null) p.Items.AddRange(s.Items);
            if (s.Opened != null) p.Opened.AddRange(s.Opened);
            if (!map.Blocked(s.X, s.Y) && s.X > 0 && s.Y > 0 && s.X < WorldMap.W && s.Y < WorldMap.H)
            {
                p.X = s.X;
                p.Y = s.Y;
            }
            return p;
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
