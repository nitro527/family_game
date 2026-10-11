using System.Collections.Generic;
using GirlAdventure.Sim;

namespace GirlAdventure.EditorTools
{
    /// <summary>이야기 장면 표 (stories.json, 2026-10-11): 표가 맞는지 + 사냥·배달·앨범·우체부 흐름을 규칙 쪽에서 끝까지.</summary>
    public static partial class SmokeTest
    {
        static readonly HashSet<string> storyDos = new HashSet<string>
        {
            "talk", "say", "flag", "unflag", "count", "give", "gold", "candy", "heal", "sfx", "sparkle", "note", "photo", "photo_shot",
            "ask", "need", "label", "jump", "end", "battle", "flash", "wait", "cutin",
        };

        /// <summary>조건 글의 낱말이 모두 아는 낱말인지 (모르는 낱말은 늘 틀림이 되어 이야기가 영영 안 나온다).</summary>
        static bool KnownCond(string cond, PlayerState p)
        {
            if (string.IsNullOrWhiteSpace(cond)) return true;
            foreach (var raw in cond.Split(','))
            {
                string w = raw.Trim().TrimStart('!');
                if (Npcs.Word(w, p) == null) return false;
            }
            return true;
        }

        static void StoryTableTests()
        {
            var p = new PlayerState();
            string bad = "";
            var npcIds = new HashSet<string>();
            foreach (var n in Npcs.All) npcIds.Add(n.Id);
            var photoIds = new HashSet<string>();
            foreach (var ph in Stories.Photos) photoIds.Add(ph.Id);
            var ids = new HashSet<string>();
            foreach (var s in Stories.All)
            {
                if (!ids.Add(s.Id)) bad += $"{s.Id}:dup ";
                if (!KnownCond(s.Need, p)) bad += $"{s.Id}:need ";
                switch (s.Kind)
                {
                    case "talk": if (!npcIds.Contains(s.Key)) bad += $"{s.Id}:npc "; break;
                    case "win": if (GameData.EnemyByName(s.Key) == null) bad += $"{s.Id}:enemy "; break;
                    case "step":
                    case "bump":
                    case "enter":
                        var k = Quests.KindOf(s.MapName);
                        if (k == MapKind.Field && s.MapName != "field") { bad += $"{s.Id}:map "; break; }
                        if (s.Kind == "enter") break;
                        var m = WorldMap.Build(k);
                        Npcs.Place(m);
                        if (s.Kind == "step" && Stories.StepSpot(s, m).x < 0) bad += $"{s.Id}:spot ";
                        if (s.Kind == "bump" && !m.Blocked(s.X, s.Y)) bad += $"{s.Id}:notblocked ";
                        break;
                    default: bad += $"{s.Id}:on "; break;
                }
                for (int i = 0; i < s.Steps.Length; i++)
                {
                    var st = s.Steps[i];
                    if (!storyDos.Contains(st.Do)) { bad += $"{s.Id}:do={st.Do} "; continue; }
                    if (st.Do == "talk" && !GameData.HasTalk(st.A)) bad += $"{s.Id}:talk={st.A} ";
                    if (st.Do == "ask" && !string.IsNullOrEmpty(st.A) && !GameData.HasTalk(st.A)) bad += $"{s.Id}:ask={st.A} ";
                    if (st.Do == "give" && GameData.Item(st.A) == null) bad += $"{s.Id}:item={st.A} ";
                    if (st.Do == "photo" && !photoIds.Contains(st.A)) bad += $"{s.Id}:photo={st.A} ";
                    if (st.Do == "need" && !KnownCond(st.A, p)) bad += $"{s.Id}:needstep ";
                    if ((st.Do == "need" && !string.IsNullOrEmpty(st.B) || st.Do == "jump") && Stories.LabelAt(s, st.Do == "jump" ? st.A : st.B) < 0) bad += $"{s.Id}:label ";
                    if (st.Do == "battle" && (GameData.EnemyByName(st.A) == null || i != s.Steps.Length - 1)) bad += $"{s.Id}:battle ";
                }
            }
            foreach (var e in Stories.Errands)
            {
                if (!KnownCond(e.Show, p) || !KnownCond(e.Done, p)) bad += $"errand {e.Id}:cond ";
                if (!string.IsNullOrEmpty(e.Reward) && GameData.Item(e.Reward) == null) bad += $"errand {e.Id}:reward ";
            }
            foreach (var t in Treasures.All())
                if (Treasures.ChapterOf(t.Id) == 0 && Stories.ErrandOfReward(t.Id) == null) bad += $"treasure {t.Id}:nohint ";
            Check("story table: triggers, steps, talks, items, labels, conditions", bad == "", bad);

            // 우체부 젤리가 서 있는 칸이 바닥이고 길(입구)을 막지 않는지는 프로브가 본다 — 여기서는 바닥인지만
            string npcBad = "";
            foreach (var n in Npcs.All)
            {
                if (!n.Id.StartsWith("post")) continue;
                var m = WorldMap.Build(Quests.KindOf(n.Map));
                if (m.At(n.X, n.Y) != '.') npcBad += n.Id + " ";
            }
            Check("postman jellies stand on open ground", npcBad == "", npcBad);
        }

        /// <summary>이야기 흐름을 규칙 쪽에서 끝까지: 사냥(받기 → 세기 → 알리기), 배달(받기 → 전하기 → 답례), 앨범 7장, 우체부 도장 4개.</summary>
        static void StoryFlowTests()
        {
            StoryDef Talk(string npc, PlayerState q) { var s = Stories.OnTalk(npc, q); if (s != null) Run(s, q, true); return s; }
            void Run(StoryDef s, PlayerState q, bool yes)
            {
                Stories.Begin(q, s);
                for (int i = 0; i < s.Steps.Length; i++)
                {
                    var st = s.Steps[i];
                    if (Stories.Apply(st, q)) continue;
                    if (st.Do == "ask" && !yes) i = Mathf0(Stories.LabelAt(s, "no"), s);
                    else if (st.Do == "need" && !Npcs.Holds(st.A, q)) i = Mathf0(string.IsNullOrEmpty(st.B) ? -1 : Stories.LabelAt(s, st.B), s);
                    else if (st.Do == "end") i = s.Steps.Length;
                }
            }

            // 사냥: 리본 젤리 (챕터 1, 뿔토끼 3)
            var p = new PlayerState { Chapter = 1 };
            var ask = Stories.OnTalk("tips", p);
            Run(ask, p, false);
            Check("hunt: saying no keeps the request open", !Stories.Flag(p, "rabbit_on") && Stories.OnTalk("tips", p) == ask);
            Talk("tips", p);
            var rabbit = GameData.EnemyByName("뿔토끼");
            Check("hunt: accepted -> notebook shows 0/3", Stories.Flag(p, "rabbit_on") && Stories.Line(FindErrand("rabbit"), p) == "뿔토끼 혼내 주기 (0/3)", Stories.Line(FindErrand("rabbit"), p));
            Check("hunt: other enemies don't count", Stories.OnWin(GameData.EnemyByName("파랑 젤리"), p).Count == 0);
            for (int i = 0; i < 5; i++) foreach (var s in Stories.OnWin(rabbit, p)) Run(s, p, true);
            Check("hunt: counts stop at goal", Stories.Count(p, "rabbit") == 3, Stories.Count(p, "rabbit").ToString());
            int gold = p.Gold, potions = p.Count("potion");
            Talk("tips", p);
            Check("hunt: report gives reward once and finishes", p.Gold == gold + 40 && p.Count("potion") == potions + 2 && Stories.Finished(FindErrand("rabbit"), p));
            Check("hunt: after done, normal talk again", Stories.OnTalk("tips", p) == null);
            var p2 = new PlayerState { Chapter = 2 };
            Check("hunt: chapter-bound", Stories.OnTalk("tips", p2) == null);

            // 배달: 촌장 젤리 → 밀짚모자 젤리 → 촌장 젤리
            p = new PlayerState { Chapter = 1 };
            Check("deliver: recipient silent before accepting", Stories.OnTalk("guide", p) == null);
            Talk("chief", p);
            Check("deliver: giver waits until delivered", Stories.OnTalk("chief", p)?.Id == "seed_wait");
            Talk("guide", p);
            int candy = p.Candy;
            Talk("chief", p);
            Check("deliver: thanks after delivery", Stories.Flag(p, "seed_done") && p.Candy == System.Math.Min(PlayerState.BagCandy, candy + 3));

            // 가족 앨범: 7장 → 로켓 한 번
            p = new PlayerState();
            foreach (var s in Stories.All)
                if (s.Id.StartsWith("photo_")) 
                {
                    p.Chapter = s.Chapter;
                    var m = WorldMapFor(s);
                    var c = Stories.StepSpot(s, m);
                    Check($"album: {s.Id} starts on its spot", Stories.OnStep(m, p, c.x, c.y) == s);
                    Run(s, p, true);
                }
            Check("album: all 7 photos, locket once", Stories.PhotoCount(p) == 7 && p.Gear.Contains("family_locket") && Stories.Flag(p, "album_done"));

            // 우체부: 마을 4곳 도장 → 가방
            p = new PlayerState();
            string[] to = { "kid", "elder", "lava_elder", "snow_kid" };
            int[] ch = { 1, 3, 4, 5 };
            for (int i = 0; i < 4; i++)
            {
                p.Chapter = ch[i];
                Talk("post" + (i + 1), p);
                Talk(to[i], p);
                Talk("post" + (i + 1), p);
            }
            Check("postman: 4 stamps -> mail bag", Stories.Count(p, "post") == 4 && p.Gear.Contains("mail_bag"), Stories.Count(p, "post").ToString());
            Check("postman: idle talk after", Stories.OnTalk("post4", p) == null);

            // 이어지는 부탁: 대장장이 젤리 (찾기 부탁 끝 → 불새 3 → 국자 배달 → 브로치). 온천지기의 사냥 부탁을 안 받아도 국자 전하기가 먼저
            p = new PlayerState { Chapter = 4 };
            Check("chain: waits for the find quest", Stories.OnTalk("smith", p) == null || Stories.OnTalk("smith", p).Id.StartsWith("lunch"));
            p.Opened.Add("q_lavastones_done");
            Talk("smith", p);
            var bird = GameData.EnemyByName("불새");
            for (int i = 0; i < 3; i++) foreach (var s in Stories.OnWin(bird, p)) Run(s, p, true);
            Talk("smith", p);
            Check("chain: delivery beats the springkeeper's own request", Stories.OnTalk("springkeeper", p)?.Id == "smith2_give", Stories.OnTalk("springkeeper", p)?.Id);
            Talk("springkeeper", p);
            Talk("smith", p);
            Check("chain: three requests → smith charm", p.Gear.Contains("smith_charm") && Stories.Flag(p, "smith_done") && Stories.Finished(FindErrand("smith"), p));

            // 조건 낱말
            p = new PlayerState();
            Check("story words", Npcs.Holds("!flag_x,!cnt_x>=1,photos>=0,!st_none", p) && Npcs.Word("cnt_x>=", p) == null);
        }

        static int Mathf0(int label, StoryDef s) => label < 0 ? s.Steps.Length : label;
        static ErrandDef FindErrand(string id) { foreach (var e in Stories.Errands) if (e.Id == id) return e; return null; }
        static WorldMap WorldMapFor(StoryDef s) => WorldMap.Build(Quests.KindOf(s.MapName));
    }
}
