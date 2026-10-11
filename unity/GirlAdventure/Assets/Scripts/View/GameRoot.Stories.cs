using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// 이야기 장면 실행 (stories.json, 2026-10-11 "단순한 이벤트부터 장기 퀘스트까지"): 차례(Steps)를 하나씩 —
    /// 규칙 쪽 차례(표시·숫자·아이템·회복·사진)는 Sim.Stories.Apply, 화면 쪽(대사·소리·반짝·선택·알림 띠·사진 찰칵·전투)은 여기.
    /// 부르는 곳: 마을 사람 말 걸기(NpcRoutine), 칸 도착(OnArrive), 물건 살펴보기(TryMove), 전투 승리(Victory), 맵 들어감(TravelRoutine).
    /// </summary>
    public partial class GameRoot
    {
        /// <summary>프로브용: 이야기 장면이 끝까지 돈 횟수.</summary>
        public int StoryCount { get; private set; }
        public string LastStory { get; private set; } = "";

        /// <summary>필드에서 이야기 하나 (다른 장면 중이면 건너뜀, 끝나면 저장).</summary>
        IEnumerator StoryScene(StoryDef s) => Scene(RunStory(s), save: true, skipIfBusy: true);

        /// <summary>이야기 차례를 처음부터 끝까지 (Scene 으로 감싸지 않음 — 마을 사람 대화·전투 승리 안에서 그대로 부른다).</summary>
        IEnumerator RunStory(StoryDef s)
        {
            Stories.Begin(P, s);
            LastStory = s.Id;
            for (int i = 0; i < s.Steps.Length; i++)
            {
                var st = s.Steps[i];
                if (Stories.Apply(st, P)) { RefreshHud(); continue; }
                switch (st.Do)
                {
                    case "talk": yield return Talk(st.A, ("name", P.Name), ("bro", PlayerState.BrotherName)); break;
                    case "say": yield return Say(FillText(st.A, new (string, object)[0]), string.IsNullOrEmpty(st.B) ? "smile" : st.B, 0); break;
                    case "sfx": sfx.Play(st.A); break;
                    case "sparkle": if (Current == Mode.Field) RewardSparkle(P.X, P.Y); break;
                    case "note": ShowBanner(StoryCounts(st.A)); break;
                    case "flash": yield return Flash(); break;
                    case "wait": yield return new WaitForSeconds(st.N > 0 ? st.N / 10f : .5f); break;
                    case "ask":
                        if (!string.IsNullOrEmpty(st.A)) yield return Talk(st.A, ("name", P.Name));
                        ShowMessageWindow(true);
                        var opts = (string.IsNullOrEmpty(st.B) ? "응!|다음에" : st.B).Split('|');
                        choiceIndex = 0;
                        yield return Choose(opts);
                        if (choicePicked != 0) i = Jump(s, "no");
                        break;
                    case "need": if (!Npcs.Holds(st.A, P)) i = Jump(s, st.B); break;
                    case "jump": i = Jump(s, st.A); break;
                    case "end": i = s.Steps.Length; break;
                    case "photo_shot": yield return PhotoShot(); break;
                    case "battle":
                        var def = GameData.EnemyByName(st.A);
                        if (def != null && Current == Mode.Field) { HideMessage(); StoryCount++; yield return BattleRoutine(def); yield break; }
                        break;
                }
            }
            StoryCount++;
        }

        /// <summary>글 속 {cnt:이름} → 이야기 숫자, {photos} → 앨범 사진 수.</summary>
        string StoryCounts(string t)
        {
            if (t == null || t.IndexOf('{') < 0 || P == null) return t;
            if (t.Contains("{photos}")) t = t.Replace("{photos}", Stories.PhotoCount(P).ToString());
            int at;
            while ((at = t.IndexOf("{cnt:", System.StringComparison.Ordinal)) >= 0)
            {
                int end = t.IndexOf('}', at);
                if (end < 0) break;
                t = t.Substring(0, at) + Stories.Count(P, t.Substring(at + 5, end - at - 5)) + t.Substring(end + 1);
            }
            return t;
        }

        /// <summary>표시로 건너뛰기 (for 문의 i++ 를 생각해 표시 칸 그대로 돌려준다, 없으면 끝).</summary>
        static int Jump(StoryDef s, string label)
        {
            int at = string.IsNullOrEmpty(label) ? -1 : Stories.LabelAt(s, label);
            return at < 0 ? s.Steps.Length : at;
        }

        /// <summary>가족 사진 찰칵: 셔터 소리 → 하얗게 번쩍 → 모두 웃는 얼굴 (가족 앨범 장기 퀘스트).</summary>
        IEnumerator PhotoShot()
        {
            HideMessage();
            facing = "down";
            UpdatePlayerVisual();
            yield return new WaitForSeconds(.4f);
            sfx.Play("select");
            yield return Flash(.5f);
            sfx.Play("star");
            if (Current == Mode.Field) RewardSparkle(P.X, P.Y);
            yield return new WaitForSeconds(.3f);
        }

        // ---- 알림 띠 (부탁을 받거나 끝낼 때 위쪽에 잠깐)
        GameObject bannerWin;
        Text bannerText;
        float bannerT;

        void ShowBanner(string text)
        {
            if (bannerWin == null)
            {
                var w = UIKit.Window(ui, "Banner", UIKit.RefW / 2 - 210, 14, 420, 44);
                bannerWin = w.gameObject;
                bannerText = UIKit.Label(w.transform, "Text", "", 10, 4, 400, 36, 20, TextAnchor.MiddleCenter);
                bannerText.supportRichText = true;
            }
            bannerText.text = text;
            bannerWin.SetActive(true);
            bannerWin.transform.SetAsLastSibling();
            bannerT = 2.6f;
            sfx.Play("peek");
        }

        /// <summary>알림 띠 시간 (Update 에서).</summary>
        void TickBanner()
        {
            if (bannerWin == null || !bannerWin.activeSelf) return;
            bannerT -= Time.deltaTime;
            if (bannerT <= 0) bannerWin.SetActive(false);
        }

        public string BannerText => bannerWin != null && bannerWin.activeSelf ? bannerText.text : "";

        // ---- 부탁 수첩 · 가족 앨범 (상태 메뉴)
        /// <summary>하는 중인 부탁 수 (메뉴 옆 숫자).</summary>
        int OpenErrands()
        {
            int c = 0;
            foreach (var e in Stories.Notebook(P)) if (!Stories.Finished(e, P)) c++;
            return c;
        }

        IEnumerator NotebookRoutine()
        {
            var list = Stories.Notebook(P);
            var lines = new List<string>();
            foreach (var e in list)
                lines.Add(Stories.Finished(e, P) ? $"<color=#7A80A0>{Stories.Line(e, P)}</color>"
                                                 : $"<color=#F2C443>{e.Kind}</color> {Stories.Line(e, P)}");
            lines.Add("닫기");
            ShowMessageWindow(true);
            SetFace("smile");
            int open = 0;
            foreach (var e in list) if (!Stories.Finished(e, P)) open++;
            msgText.text = list.Count == 0 ? "아직 받은 부탁이 없어요. 마을 사람들에게 말을 걸어 봐요!" : $"부탁 수첩 — 하는 중 {open}개";
            choiceIndex = 0;
            yield return Choose(lines.ToArray());
        }

        IEnumerator AlbumRoutine()
        {
            var lines = new List<string>();
            foreach (var ph in Stories.Photos)
                lines.Add(Stories.HasPhoto(P, ph.Id) ? $"<color=#FFE27A>♥</color> {ph.Title} <size=15><color=#9AA3C7>{ph.Caption}</color></size>"
                                                     : $"<color=#7A80A0>??? <size=15>챕터 {ph.Chapter} 의 경치 좋은 곳</size></color>");
            lines.Add("닫기");
            ShowMessageWindow(true);
            SetFace("laugh");
            msgText.text = $"가족 앨범 {Stories.PhotoCount(P)}/{Stories.Photos.Count} — 경치 좋은 곳에서 찰칵!";
            choiceIndex = 0;
            yield return Choose(lines.ToArray());
        }
    }
}
