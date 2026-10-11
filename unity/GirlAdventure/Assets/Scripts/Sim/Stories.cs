using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 이야기 장면의 한 차례 (stories.json Steps). Do 가 할 일, A·B·N 이 그 재료:
    /// talk A(대사 id) · say A(바로 쓴 글) · flag A / unflag A · count A N(숫자 N 올리기, 0 이면 1) · give A N(아이템 id: 장비면 장비, 소모품이면 N 개) ·
    /// gold N · candy N · heal · sfx A · sparkle · note A(알림 띠 글) · photo A(가족 사진) · ask A B(대사 A 뒤 "예|아니요" B, 아니요면 "no" 표시로) ·
    /// need A B(조건 A 가 아니면 표시 B 로, B 가 없으면 끝) · label A(표시) · jump A · end · battle A(적 이름 — 반드시 마지막 차례).
    /// </summary>
    [Serializable]
    public class StoryStep
    {
        public string Do, A, B;
        public int N;
    }

    /// <summary>
    /// 이야기 장면 하나 = 언제(On) + 조건(Need) + 차례(Steps). 2026-10-11 사용자 "단순한 이벤트부터 장기 퀘스트까지 짜임새 있게".
    /// On: "talk:마을사람Id" · "step:맵:x,y"(그 근처 사방 한 칸) · "bump:맵:x,y"(그 물건 칸을 살펴봄) · "win:적 이름" · "enter:맵".
    /// Need: 마을 사람 대사 조건 낱말 + flag_이름 · cnt_이름>=N · st_이야기Id(본 이야기). Chapter = 그 챕터에서만 (0 이면 언제나).
    /// Repeat 가 아니면 한 번 보고 나면 끝 (세이브 Opened 에 "st_Id").
    /// </summary>
    [Serializable]
    public class StoryDef
    {
        public string Id;
        public string On;
        public string Need;
        public int Chapter;
        public bool Repeat;
        public int Priority;                    // 같은 사람에게 여러 장면이 맞으면 큰 것부터 (전하기·답례 장면이 새 부탁보다 먼저)
        public StoryStep[] Steps = new StoryStep[0];

        [NonSerialized] public string Kind, Key, MapName;
        [NonSerialized] public int X = -1, Y = -1;
    }

    /// <summary>부탁 수첩 한 줄 (상태 메뉴): Show 조건이 맞으면 보이고, Done 이 맞으면 끝(✓), Count 가 있으면 (n/Goal).</summary>
    [Serializable]
    public class ErrandDef
    {
        public string Id, Title, Show, Done, Count;
        public int Goal;
        public string Reward;                     // 끝나면 받는 특별 아이템 (보물 모음 힌트에 쓴다)
        public string Kind = "부탁";              // 부탁·사냥·배달·앨범·편지 (수첩 머리)
    }

    /// <summary>가족 앨범 한 장: photo 차례로 찍히면 앨범에 (Title·Caption 은 앨범 화면 글).</summary>
    [Serializable]
    public class PhotoDef
    {
        public string Id, Title, Caption;
        public int Chapter;
    }

    [Serializable]
    public class StoryTable
    {
        public StoryDef[] Stories;
        public ErrandDef[] Errands;
        public PhotoDef[] Photos;
        public DialogueEntry[] Talks;          // 이야기 대사 (dialogue.json 과 같은 모양 — 읽을 때 대사 표에 합친다)
    }

    public static class Stories
    {
        static StoryDef[] stories = new StoryDef[0];
        static ErrandDef[] errands = new ErrandDef[0];
        static PhotoDef[] photos = new PhotoDef[0];

        public static IReadOnlyList<StoryDef> All => stories;
        public static IReadOnlyList<ErrandDef> Errands => errands;
        public static IReadOnlyList<PhotoDef> Photos => photos;

        public static void Set(StoryTable t)
        {
            stories = t?.Stories ?? new StoryDef[0];
            errands = t?.Errands ?? new ErrandDef[0];
            photos = t?.Photos ?? new PhotoDef[0];
            foreach (var s in stories)
            {
                s.Steps ??= new StoryStep[0];
                var parts = (s.On ?? "").Split(new[] { ':' }, 3);
                s.Kind = parts[0];
                s.Key = parts.Length > 1 ? parts[1] : "";
                s.MapName = s.Kind == "step" || s.Kind == "bump" || s.Kind == "enter" ? s.Key : null;
                if (parts.Length > 2)
                {
                    var xy = parts[2].Split(',');
                    if (xy.Length == 2 && int.TryParse(xy[0], out int x) && int.TryParse(xy[1], out int y)) { s.X = x; s.Y = y; }
                }
            }
        }

        // ---- 진행 표시 (세이브 Opened: "flag_이름", "cnt_이름=3", "st_이야기", "photo_사진")
        public static bool Flag(PlayerState p, string name) => p.Opened.Contains("flag_" + name);
        public static void SetFlag(PlayerState p, string name, bool on = true)
        {
            if (on) { if (!Flag(p, name)) p.Opened.Add("flag_" + name); }
            else p.Opened.Remove("flag_" + name);
        }

        public static int Count(PlayerState p, string name)
        {
            string pre = "cnt_" + name + "=";
            foreach (var o in p.Opened) if (o.StartsWith(pre) && int.TryParse(o.Substring(pre.Length), out int n)) return n;
            return 0;
        }

        public static void AddCount(PlayerState p, string name, int n)
        {
            string pre = "cnt_" + name + "=";
            int now = Count(p, name);
            p.Opened.RemoveAll(o => o.StartsWith(pre));
            p.Opened.Add(pre + Math.Max(0, now + n));
        }

        public static bool Seen(PlayerState p, StoryDef s) => p.Opened.Contains("st_" + s.Id);
        public static bool HasPhoto(PlayerState p, string id) => p.Opened.Contains("photo_" + id);
        public static int PhotoCount(PlayerState p) { int c = 0; foreach (var ph in photos) if (HasPhoto(p, ph.Id)) c++; return c; }

        /// <summary>조건 낱말 (Npcs.Word 가 모르는 것): flag_이름 · cnt_이름>=N · st_이야기 · photo_사진 · photos>=N · gold>=N · opened_표시. 모르면 null.</summary>
        public static bool? Word(string w, PlayerState p)
        {
            if (w.StartsWith("flag_")) return Flag(p, w.Substring(5));
            if (w.StartsWith("st_")) return p.Opened.Contains(w);
            if (w.StartsWith("photo_")) return p.Opened.Contains(w);
            if (w.StartsWith("opened_")) return p.Opened.Contains(w.Substring(7));      // 세이브에 남은 아무 표시 (예: opened_q_crystals = 찾기 부탁 받음)
            int ge = w.IndexOf(">=", StringComparison.Ordinal);
            if (ge > 0 && int.TryParse(w.Substring(ge + 2), out int n))
            {
                string left = w.Substring(0, ge);
                if (left.StartsWith("cnt_")) return Count(p, left.Substring(4)) >= n;
                if (left == "photos") return PhotoCount(p) >= n;
                if (left == "gold") return p.Gold >= n;
            }
            return null;
        }

        static bool Ready(StoryDef s, PlayerState p) =>
            (s.Repeat || !Seen(p, s)) && (s.Chapter == 0 || s.Chapter == p.Chapter) && Npcs.Holds(s.Need, p);

        /// <summary>이야기를 시작한 것으로 표시 (한 번짜리).</summary>
        public static void Begin(PlayerState p, StoryDef s)
        {
            if (!s.Repeat && !Seen(p, s)) p.Opened.Add("st_" + s.Id);
        }

        /// <summary>마을 사람에게 말 걸 때 볼 이야기 (평소 대사보다 먼저).</summary>
        public static StoryDef OnTalk(string npcId, PlayerState p)
        {
            StoryDef best = null;
            foreach (var s in stories) if (s.Kind == "talk" && s.Key == npcId && Ready(s, p) && (best == null || s.Priority > best.Priority)) best = s;
            return best;
        }

        /// <summary>이 적을 이겼을 때 볼 이야기들 (사냥 부탁 숫자 세기 등 — 여러 개일 수 있다).</summary>
        public static List<StoryDef> OnWin(EnemyDef def, PlayerState p)
        {
            var list = new List<StoryDef>();
            if (def == null) return list;
            foreach (var s in stories) if (s.Kind == "win" && s.Key == def.Name && Ready(s, p)) list.Add(s);
            return list;
        }

        /// <summary>맵에 들어왔을 때 볼 이야기.</summary>
        public static StoryDef OnEnter(WorldMap m, PlayerState p)
        {
            foreach (var s in stories) if (s.Kind == "enter" && Quests.KindOf(s.MapName) == m.Kind && Ready(s, p)) return s;
            return null;
        }

        /// <summary>그 칸에 도착했을 때 볼 이야기 (중심 칸은 근처 바닥 — 맵 이벤트와 같은 규칙, 사방 한 칸 안).</summary>
        public static StoryDef OnStep(WorldMap m, PlayerState p, int x, int y)
        {
            foreach (var s in stories)
            {
                if (s.Kind != "step" || Quests.KindOf(s.MapName) != m.Kind || !Ready(s, p)) continue;
                var c = StepSpot(s, m);
                if (c.x >= 0 && Math.Abs(c.x - x) <= 1 && Math.Abs(c.y - y) <= 1) return s;
            }
            return null;
        }

        public static (int x, int y) StepSpot(StoryDef s, WorldMap m) =>
            Quests.Spots(new QuestDef { Id = "st_" + s.Id, Wish = s.X + "," + s.Y }, m)[0];

        /// <summary>물건 칸에 부딪혔을 때 볼 이야기 (정확히 그 칸).</summary>
        public static StoryDef OnBump(WorldMap m, PlayerState p, int x, int y)
        {
            foreach (var s in stories)
                if (s.Kind == "bump" && Quests.KindOf(s.MapName) == m.Kind && s.X == x && s.Y == y && Ready(s, p)) return s;
            return null;
        }

        /// <summary>
        /// 화면이 필요 없는 차례를 처리한다 (flag·count·give·gold·candy·heal·photo). 처리했으면 true.
        /// 화면 쪽(GameRoot.Stories)은 이것을 먼저 부르고, 아니면 대사·소리·선택·전투를 맡는다.
        /// </summary>
        public static bool Apply(StoryStep st, PlayerState p)
        {
            int n = st.N == 0 ? 1 : st.N;
            switch (st.Do)
            {
                case "flag": SetFlag(p, st.A); return true;
                case "unflag": SetFlag(p, st.A, false); return true;
                case "count": AddCount(p, st.A, n); return true;
                case "gold": p.GainGold(n); return true;
                case "candy": p.AddItem("candy", n); return true;
                case "heal": p.RestoreAll(); return true;
                case "photo": if (!HasPhoto(p, st.A)) p.Opened.Add("photo_" + st.A); return true;
                case "give":
                    var it = GameData.Item(st.A);
                    if (it == null) return true;
                    if (it.Kind == "use") p.AddItem(st.A, n);
                    else if (it.Kind == "key") { if (!p.Items.Contains(st.A)) p.Items.Add(st.A); }
                    else if (!p.Gear.Contains(st.A)) p.Gear.Add(st.A);
                    return true;
            }
            return false;
        }

        /// <summary>표시 A 의 차례 번호 (없으면 -1 = 끝).</summary>
        public static int LabelAt(StoryDef s, string label)
        {
            for (int i = 0; i < s.Steps.Length; i++) if (s.Steps[i].Do == "label" && s.Steps[i].A == label) return i;
            return -1;
        }

        /// <summary>이 특별 아이템을 주는 부탁 (보물 모음 힌트, 없으면 null).</summary>
        public static ErrandDef ErrandOfReward(string item)
        {
            foreach (var e in errands) if (e.Reward == item) return e;
            return null;
        }

        // ---- 부탁 수첩
        public static bool Shown(ErrandDef e, PlayerState p) => !string.IsNullOrEmpty(e.Show) && Npcs.Holds(e.Show, p);
        public static bool Finished(ErrandDef e, PlayerState p) => !string.IsNullOrEmpty(e.Done) && Npcs.Holds(e.Done, p);

        /// <summary>수첩 한 줄 글: "✓ 제목" 또는 "제목 (3/5)".</summary>
        public static string Line(ErrandDef e, PlayerState p)
        {
            if (Finished(e, p)) return "✓ " + e.Title;
            if (!string.IsNullOrEmpty(e.Count) && e.Goal > 0) return $"{e.Title} ({Math.Min(e.Goal, Count(p, e.Count))}/{e.Goal})";
            return e.Title;
        }

        /// <summary>지금 수첩에 보일 줄들 (진행 중 먼저, 끝난 것 뒤).</summary>
        public static List<ErrandDef> Notebook(PlayerState p)
        {
            var open = new List<ErrandDef>();
            var done = new List<ErrandDef>();
            foreach (var e in errands) if (Shown(e, p)) (Finished(e, p) ? done : open).Add(e);
            open.AddRange(done);
            return open;
        }
    }
}
