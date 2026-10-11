using System.Collections;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 챕터 던전 드나들기 (2026-10-11 사용자 "보스전은 바로 하지 말고 던전을 하나 — 성 안을 돌아다니다가 보스를 만나게"):
    /// 바깥 맵 입구 칸 → 어두워졌다 던전 출발 칸 (처음 들어가면 던전 대사), 던전 'E' → 바깥 입구 앞 칸.
    /// 보스는 던전 맨 안쪽 'B' 칸에 서 있다 (props.json Objects 의 보스 그림), 밟으면 BossRoutine.
    /// </summary>
    public partial class GameRoot
    {
        /// <summary>프로브용: 던전에 들어간 횟수.</summary>
        public int DungeonEnterCount { get; private set; }

        IEnumerator DungeonRoutine(ChapterDef ch, bool enter)
        {
            busy = true;
            sfx.Play("step");
            yield return Fade(0, 1, .35f);
            P.InDungeon = enter;
            noticedIn = null;
            if (enter)
            {
                DungeonEnterCount++;
                (P.X, P.Y) = MapPoints.Of(ch.DungeonKind.Value).spawn;
                facing = "up";
            }
            else
            {
                (P.X, P.Y) = Dungeons.XY(ch.DungeonExit);
                facing = ch.ExitFacing;
            }
            moveT = -1;
            ApplyMap();
            ResetBrother();
            bgm.Play(MapSong);
            RefreshQuestProps();
            UpdatePlayerVisual();
            mobs.Grace = 2f;
            AutoSave();
            yield return Fade(1, 0, .35f);
            HideSwirl();
            if (enter && Dungeons.Of(ch.DungeonKind.Value) is DungeonDef dd) ShowBanner("<color=#F2C443>" + dd.Name + "</color>");   // 던전 이름 띠
            if (enter && !string.IsNullOrEmpty(ch.DungeonTalk) && !P.Opened.Contains("dg_" + ch.No))
            {
                P.Opened.Add("dg_" + ch.No);
                yield return Talk(ch.DungeonTalk, ("name", P.Name));
                HideMessage();
                AutoSave();
            }
            busy = false;
        }

        // ---- 보스가 알아챔: 던전 보스 세 칸 안으로 다가가면 보스 머리 위에 "!" (한 번 소리)
        SpriteRenderer bossAlert;
        bool bossNoticed;
        MapKind? noticedIn;

        /// <summary>프로브용: 보스 포효 연출이 나온 횟수.</summary>
        public int BossRoarCount { get; private set; }

        /// <summary>던전 보스 그림 (props.json Objects "boss_던전").</summary>
        SpriteRenderer BossSprite(ChapterDef ch)
        {
            string id = "boss_" + ch.DungeonKind.Value.ToString().ToLower();
            foreach (var v in objViews) if (v.o.Id == id && v.sr) return v.sr;
            return null;
        }

        /// <summary>보스 만남 연출: 음악이 뚝 → 잠깐 정적 → 포효 (보스가 부풀었다 줄고, 화면이 붉게 번쩍·흔들) → 만남 대사로.</summary>
        IEnumerator BossRoar(ChapterDef ch)
        {
            BossRoarCount++;
            bgm.Stop();
            facing = "up";
            UpdatePlayerVisual();
            var sr = BossSprite(ch);
            yield return new WaitForSeconds(.45f);
            sfx.Play("roar");
            Vector3 s0 = sr ? sr.transform.localScale : Vector3.one;
            const float dur = .9f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / dur * Mathf.PI);
                if (sr) sr.transform.localScale = s0 * (1 + .18f * k);
                flashImg.color = new Color(1, .2f, .15f, .32f * k);
                if (Settings.Flash) fieldRoot.localPosition = (Vector3)(Random.insideUnitCircle * .12f * k);
                yield return null;
            }
            if (sr) sr.transform.localScale = s0;
            flashImg.color = new Color(1, 1, 1, 0);
            fieldRoot.localPosition = Vector3.zero;
            yield return new WaitForSeconds(.25f);
        }

        void TickDungeonBoss()
        {
            var ch = P != null ? Chapters.Get(P.Chapter) : null;
            bool on = false;
            Vector3 at = default;
            if (ch != null && ch.HasDungeon && P.InDungeon && map != null && map.Kind == ch.DungeonKind && !P.BossBeaten(ch.No))
            {
                (int x, int y) b = (-1, -1);
                for (int y = 0; y < WorldMap.H && b.x < 0; y++) for (int x = 0; x < WorldMap.W && b.x < 0; x++) if (map.At(x, y) == ch.BossChar) b = (x, y);
                if (b.x >= 0 && Mathf.Abs(b.x - P.X) + Mathf.Abs(b.y - P.Y) <= 3)
                {
                    on = true;
                    at = FeetAt(b.x + .5f, b.y) + new Vector3(0, 2.5f + Mathf.Abs(Mathf.Sin(Time.time * 5)) * .12f, -.5f);
                }
            }
            if (on && bossAlert == null)
            {
                bossAlert = new GameObject("BossAlert").AddComponent<SpriteRenderer>();
                bossAlert.transform.SetParent(fieldRoot, false);
                bossAlert.sprite = alertSprite ??= Art.FromTexture(AlertTexture(), new Vector2(.5f, 0), 96);
                bossAlert.sortingOrder = 8;
            }
            if (bossAlert != null)
            {
                bossAlert.enabled = on;
                if (on) bossAlert.transform.localPosition = at;
            }
            if (on && !bossNoticed)
            {
                sfx.Play("peek");
                if (noticedIn != map.Kind && ch.BossDef() is EnemyDef bd) ShowBanner(Josa.With(bd.Name, "이/가") + " 이쪽을 노려본다!");   // 던전에 들어올 때마다 한 번
                noticedIn = map.Kind;
            }
            bossNoticed = on;
        }

        public bool BossAlertShown => bossAlert != null && bossAlert.enabled;

        /// <summary>던전 보물상자: 처음이면 아이템·골드 (dungeons.json Chests), 열린 그림은 props.json Objects 가 "Opened:dch_…" 로.</summary>
        IEnumerator DungeonChestRoutine(int x, int y)
        {
            IEnumerator Body()
            {
                var c = Dungeons.ChestAt(map.Kind, x, y);
                string what = Dungeons.OpenChest(P, map.Kind, c);
                if (what != null)
                {
                    sfx.Play("chest");
                    RefreshQuestProps();
                    RewardSparkle(x, y);
                    RefreshHud();
                    yield return Talk("chest_open", ("item", what));
                }
                else yield return Talk("chest_empty");
            }
            return Scene(Body(), save: true, skipIfBusy: true);
        }
    }
}
