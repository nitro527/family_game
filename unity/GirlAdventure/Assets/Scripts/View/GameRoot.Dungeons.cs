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
            if (enter && !string.IsNullOrEmpty(ch.DungeonTalk) && !P.Opened.Contains("dg_" + ch.No))
            {
                P.Opened.Add("dg_" + ch.No);
                yield return Talk(ch.DungeonTalk, ("name", P.Name));
                HideMessage();
                AutoSave();
            }
            busy = false;
        }

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
