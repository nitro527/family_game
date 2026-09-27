using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GirlAdventure.Sim;
using GirlAdventure.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GirlAdventure.EditorTools
{
    /// <summary>
    /// 자동 플레이테스트: 가상 키 입력으로 실제 플레이처럼 진행하며
    /// 스크린샷(unity/playtest/*.png)과 상태 로그("PT:" 줄)를 남긴다.
    /// 실행: Unity.exe -batchmode -projectPath &lt;path&gt; -executeMethod GirlAdventure.EditorTools.PlaytestProbe.Run -logFile -
    /// </summary>
    public static class PlaytestProbe
    {
        static IEnumerator script;
        static double waitUntil;
        static Func<bool> waitCond;
        static double condDeadline;
        static string outDir;
        static int shotNo;
        static int fails;
        static GameRoot G => GameRoot.Instance;

        public static void Run()
        {
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../playtest"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneBuilder.ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            script = null;
            shotNo = 0;
            fails = 0;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying || G == null) return;
            if (script == null) { script = Scenario(); waitUntil = 0; waitCond = null; Camera.main.aspect = 16f / 9f; }
            double now = EditorApplication.timeSinceStartup;
            if (now < waitUntil) return;
            if (waitCond != null)
            {
                if (!waitCond() && now < condDeadline) return;
                if (!waitCond()) Log("WAIT TIMEOUT");
                waitCond = null;
            }
            try
            {
                if (!script.MoveNext()) { Log("DONE"); Debug.Log(fails == 0 ? "PLAYTEST_RESULT: OK" : $"PLAYTEST_RESULT: {fails}_FAILURES"); Finish(); return; }
                switch (script.Current)
                {
                    case float s: waitUntil = now + s; break;
                    case Func<bool> c: waitCond = c; condDeadline = now + 25; break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("PLAYTEST_RESULT: EXCEPTION " + e);
                Finish();
            }
        }

        static void Log(string s) => Debug.Log($"PT: {s} | {G.DebugState()}");

        static void Expect(string what, bool ok)
        {
            if (!ok) fails++;
            Log($"CHECK {(ok ? "OK" : "FAIL")} {what}");
        }

        /// <summary>기대한 배경음악이 나오는지 확인 (틀리면 실패로 센다).</summary>
        static void ExpectBgm(string name)
        {
            bool ok = G.BgmName == name;
            if (!ok) fails++;
            Log($"BGM {(ok ? "OK" : "FAIL")} expect={name} actual={G.BgmName}");
        }

        static void Shot(string name)
        {
            shotNo++;
            var b = G.PlayerBounds;
            ProbeUtil.RenderToPng(Path.Combine(outDir, $"{shotNo:D2}_{name}.png"));
            Log($"SHOT {shotNo:D2}_{name} playerSize={b.size.x:F2}x{b.size.y:F2}");
        }

        /// <summary>대사가 끝날 때까지 Z 를 눌러 넘긴다 (명령창이 뜨거나 한가해지면 멈춤).</summary>
        static IEnumerable SkipTalk(Func<bool> until)
        {
            for (int i = 0; i < 80 && !until(); i++)
            {
                Keys.Press(KeyCode.Z);
                yield return .25f;
            }
        }

        /// <summary>명령창에서 idx 번째(0 파이어,1 주문,2 도구,3 도망)로 커서를 옮겨 결정.</summary>
        static IEnumerable Choose(int idx, string shotBeforeConfirm = null)
        {
            for (int k = 0; k < 4 && G.CommandIndex != idx; k++) { Keys.Press(KeyCode.DownArrow); yield return .12f; }
            if (shotBeforeConfirm != null) Shot(shotBeforeConfirm);
            Keys.Press(KeyCode.Return);
            yield return .1f;
        }

        static IEnumerable Walk(KeyCode key, string dir, float seconds, bool frames)
        {
            Keys.Hold(key, true);
            var seen = new HashSet<string>();
            if (frames)
                for (int i = 0; i < 4; i++) { yield return .09f; seen.Add(G.PlayerSpriteName); Shot($"walk_{dir}_{i}"); }
            if (frames) Expect($"walk {dir} animates ({string.Join(",", seen)})", seen.Count >= 2);
            yield return seconds;
            Keys.Hold(key, false);
            yield return .35f;
        }

        static IEnumerator Scenario()
        {
            yield return 2f;
            G.NoGroups = true;                        // 필드 전투는 늘 한 마리 (무리는 아래에서 따로 검사)
            G.DebugDeleteSave();                      // 새 게임부터 (에디터 PlayerPrefs 만 지움)
            Settings.ResetDefaults();                 // 지난 실행이 설정을 바꾼 채 멈췄어도 기본값에서 시작
            G.DebugToTitle();
            yield return .3f;
            Expect("title has no continue without save", !G.CanContinue && G.TitleIndex == 0);
            Shot("title");
            ExpectBgm("title");
            // 음량 4단계를 한 바퀴 돌려 원래대로
            int vol0 = G.BgmLevel;
            Keys.Press(KeyCode.M); yield return .2f;
            if (G.BgmLevel == vol0) { fails++; Log("BGM VOLUME FAIL (M 키로 안 바뀜)"); }
            for (int i = 0; i < 3; i++) { Keys.Press(KeyCode.M); yield return .2f; }
            if (G.BgmLevel != vol0) { fails++; Log("BGM VOLUME FAIL (한 바퀴 뒤 원래대로 안 돌아옴)"); }
            // 타이틀 설정: ↓ → 설정 → 효과음 한 번 바꾸고 → 되돌리기 → 닫기
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Expect("title cursor reaches settings", G.TitleIndex == 2);
            Keys.Press(KeyCode.Return);
            yield return .4f;
            Expect("title opens settings", G.Busy && G.MessageText.Contains("고를 때마다"));
            int titleSfx0 = Settings.SfxPercent;
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return); yield return .3f;
            Shot("title_settings");
            Expect("title settings change sfx", Settings.SfxPercent != titleSfx0);
            for (int i = 0; i < 3 && Settings.SfxPercent != titleSfx0; i++) { Keys.Press(KeyCode.Return); yield return .3f; }
            Keys.Press(KeyCode.Escape);
            yield return .4f;
            Expect("title settings close", G.Current == GameRoot.Mode.Title && !G.Busy && Settings.SfxPercent == titleSfx0);
            Keys.Press(KeyCode.UpArrow); yield return .15f;
            Expect("title cursor back to start", G.TitleIndex == 0);
            yield return .5f;
            Keys.Press(KeyCode.Return);
            yield return 1.2f;
            Shot("intro_first_line");                 // 첫 대사가 Enter 로 건너뛰어지지 않았는지
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            yield return .5f;
            Shot("field_start");
            ExpectBgm("field");

            // 네 방향 걷기 — 방향별 캐릭터 크기 비교 (몬스터가 다가와 전투가 끼어들면 걷기 검사가 흔들려서 멈춤)
            G.DebugMobs(true, false);
            var open = G.DebugOpenSpot(6);            // 걷는 거리가 프레임마다 조금씩 달라서, 사방이 트인 곳에서 걷는다
            G.DebugTeleport(open.x, open.y, "down");
            yield return .3f;
            G.DebugClearMobsNear(8);                  // 멈춘 몬스터라도 걷는 길 위에 있으면 부딪혀 전투가 됨 → 멀리 옮김
            foreach (var x in Walk(KeyCode.RightArrow, "right", .5f, true)) yield return x;
            Shot("idle_right");
            foreach (var x in Walk(KeyCode.DownArrow, "down", .4f, true)) yield return x;
            Shot("idle_down");
            foreach (var x in Walk(KeyCode.LeftArrow, "left", .3f, true)) yield return x;
            Shot("idle_left");
            foreach (var x in Walk(KeyCode.UpArrow, "up", .3f, true)) yield return x;
            Shot("idle_up");
            if (G.Current == GameRoot.Mode.Battle)
                foreach (var x in FightUntilOver(false)) yield return x;

            // 필드 연출: 연못 물결 + 샘물 반짝임 (촬영하는 동안 몬스터가 부딪혀 전투가 끼어들지 않게 멈춤)
            G.DebugMobs(true, false);
            G.DebugTeleport(8, 12, "down");
            yield return .6f;
            Shot("field_water");
            Expect($"flowers are sway sprites (n={G.FlowerCount} tilt={G.FlowerTilt:F2})", G.FlowerCount > 10 && Mathf.Abs(G.FlowerTilt) > .01f);
            // 수상한 구멍 (B-7): 초원에 4개, 보랏빛 기운이 올라오는 모습만 촬영 (들여다보기는 동굴에서 검사)
            Expect($"field has holes (n={G.HoleCount})", G.HoleCount == 4);
            var fh = G.DebugHoleApproach(true);
            if (fh.dir != null) { G.DebugTeleport(fh.x, fh.y, fh.dir); yield return 1.2f; Shot("field_hole"); }
            // 마을 사람: 밀짚모자 젤리 아래에서 위로 부딪히면 말을 건다 (초상화도 젤리)
            Expect($"npcs on field (n={G.NpcCount})", G.NpcCount == 2);
            G.DebugTeleport(7, 3, "up");
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return 1.2f;
            Shot("npc_talk");
            Expect("npc talks when bumped", G.TalkingToNpc && G.MessageText.Contains("밀짚모자"));
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            Expect("npc talk ends", !G.TalkingToNpc && G.P.X == 7 && G.P.Y == 3);
            G.DebugTeleport(5, 4, "up");
            yield return .8f;
            Shot("fountain_sparkle");

            // 터치 버튼: 화면 버튼을 실제 포인터 이벤트로 눌러서 걷기
            G.DebugTeleport(3, 6, "down");               // 샘물·표지판에 부딪히지 않는 줄
            G.ForceTouchPad = true;
            yield return .3f;
            Expect("touch pad visible in field", G.TouchPadVisible);
            Shot("touchpad");
            int tx = G.P.X;
            var btn = G.TouchButton("Right");
            var ev = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(btn, ev, ExecuteEvents.pointerDownHandler);
            yield return .45f;
            ExecuteEvents.Execute(btn, ev, ExecuteEvents.pointerUpHandler);
            yield return .4f;
            Expect("touch right moves player", G.P.X > tx);
            G.ForceTouchPad = false;
            yield return .1f;
            Expect("touch pad hidden again", !G.TouchPadVisible);
            bool dpad = true;
            try { Input.GetAxisRaw("PadDpadX"); Input.GetAxisRaw("PadDpadY"); } catch (ArgumentException) { dpad = false; }
            Expect("gamepad d-pad axes configured", dpad);
            string noSound = "";
            foreach (var m in EnemyDef.Moves) if (!G.HasSfx("mv_" + m)) noSound += m + " ";
            Expect("every attack move has its own sound " + noSound, noSound == "" && G.HasSfx("thud"));
            if (G.Current == GameRoot.Mode.Battle)
                foreach (var x in FightUntilOver(false)) yield return x;

            // 표지판 & 샘물
            G.DebugTeleport(4, 4, "up");
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return 1.5f;
            Shot("sign");
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            G.DebugTeleport(6, 4, "up");
            G.DebugSetHp(3);
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return 1.2f;
            Shot("fountain");
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;

            // 도구 가게 (백로그 40: 마을로 옮김): 사탕 하나, 마법 물약 하나 사고 Esc 로 그만두기
            G.DebugSetVillage(true);
            yield return .3f;
            ExpectBgm("village");
            G.DebugSetGold(40);
            int candy0 = G.P.Candy;
            G.DebugTeleport(WorldMap.VillageShop.x - 1, WorldMap.VillageShop.y, "right");
            yield return .3f;
            Shot("shop_front");
            Keys.Hold(KeyCode.RightArrow, true); yield return .15f; Keys.Hold(KeyCode.RightArrow, false);
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            yield return .2f;
            Shot("shop_choice");
            Keys.Press(KeyCode.Return);                // 사탕 사기
            yield return .4f;
            Shot("shop_bought");
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 마법 물약 사기
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Keys.Press(KeyCode.Escape);                // 그만두기
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            Expect("shop: bought candy + potion", G.P.Candy == candy0 + 1 && G.P.Count("potion") == 1
                   && G.P.Gold == 40 - Shop.CandyPrice - GameData.ItemPrice("potion") && !G.Busy);

            // 장비 사기: 별빛 지팡이(목록 3번째)를 사서 바로 장착, 한 번 더 고르면 "가짐"
            G.DebugSetGold(100);
            int atk0 = G.P.Atk;
            G.DebugTeleport(WorldMap.VillageShop.x - 1, WorldMap.VillageShop.y, "right");
            yield return .3f;
            Keys.Hold(KeyCode.RightArrow, true); yield return .15f; Keys.Hold(KeyCode.RightArrow, false);
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            yield return .2f;
            int wandIdx = Shop.Goods(MapKind.Village).IndexOf("star_wand");
            for (int k = 0; k < wandIdx; k++) { Keys.Press(KeyCode.DownArrow); yield return .15f; }
            Shot("shop_gear_list");
            Keys.Press(KeyCode.Return);                // 별빛 지팡이 사기
            yield return (Func<bool>)(() => G.ChoiceOpen);   // "지금 장착할까?" 예/나중에
            yield return .3f;
            Shot("shop_gear_ask");
            Keys.Press(KeyCode.Return);                // 지금 장착하기
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Expect("shop: bought & equipped wand", G.P.Gear.Contains("star_wand") && Equipment.IsEquipped(G.P, "star_wand")
                   && G.P.Atk == atk0 + GameData.Item("star_wand").Atk && G.P.Gold == 100 - GameData.ItemPrice("star_wand"));
            Shot("shop_gear_owned_label");
            Keys.Press(KeyCode.Return);                // 커서가 그대로 지팡이 → 이미 가짐
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Expect("shop: gear not bought twice", G.P.Gold == 100 - GameData.ItemPrice("star_wand"));
            Keys.Press(KeyCode.Escape);
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            G.DebugSetVillage(false);
            G.DebugTeleport(6, 5, "down");
            yield return .3f;
            Expect("field has no shop tile", G.MapKind == MapKind.Field);
            ExpectBgm("field");

            // 상태 창: Esc 로 열고 닫기
            Keys.Press(KeyCode.Escape);
            yield return .4f;
            Shot("status");
            Expect("status opens with Esc", G.StatusOpen && G.Busy && G.StatusText.Contains("다음 레벨까지") && G.StatusText.Contains("사탕")
                   && G.StatusText.Contains("별빛 지팡이") && G.StatusText.Contains("장비"));
            Log("STATUS " + G.StatusText);
            // 상태 창에서 도구 쓰기: MP 를 줄여 두고 마법 물약 (HP 는 가득이라 사탕은 못 씀). Z → 메뉴 → 도구 쓰기
            G.DebugSetMp(2);
            Keys.Press(KeyCode.Return);
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Shot("status_menu");
            Keys.Press(KeyCode.Return);                // 도구 쓰기
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Shot("status_items");
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);
            yield return .6f;
            Shot("status_item_used");
            yield return (Func<bool>)(() => G.MessageText == "");
            yield return .2f;
            Expect("status: potion used", G.StatusOpen && G.P.Count("potion") == 0 && G.P.Mp == Math.Min(2 + GameData.Item("potion").Mp, G.P.MaxMp));
            // 장비 바꾸기: 메뉴 → 장비 바꾸기 → 무기 → 벗기 → 그만두기
            Keys.Press(KeyCode.Return);
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 장비 바꾸기
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Shot("status_gear_slots");
            Keys.Press(KeyCode.Return);                // 무기
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Shot("status_gear_weapon");
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 벗기
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Expect("status: wand unequipped", !Equipment.IsEquipped(G.P, "star_wand") && G.P.Atk == atk0 && G.P.Gear.Contains("star_wand"));
            Keys.Press(KeyCode.Return);                // 다시 무기 → 지팡이 장착
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Keys.Press(KeyCode.Return);
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Expect("status: wand re-equipped", Equipment.IsEquipped(G.P, "star_wand"));
            Keys.Press(KeyCode.Escape);                // 부위 고르기 그만두기
            yield return (Func<bool>)(() => G.MessageText == "");
            yield return .3f;
            Shot("status_after_gear");
            Expect("status still open after gear", G.StatusOpen);
            // 설정: 메뉴 → 설정 → 효과음·글자 속도·번쩍임을 한 단계씩 바꾸고, 끝나면 처음 값으로 되돌린다
            Keys.Press(KeyCode.Return);
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            for (int k = 0; k < 3; k++) { Keys.Press(KeyCode.DownArrow); yield return .15f; }
            Keys.Press(KeyCode.Return);                // 설정
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            int sfx0 = Settings.SfxPercent, speed0 = Settings.TextSpeed;
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 효과음
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 글자 속도 (예시 문장이 나온다)
            yield return .3f;
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Keys.Press(KeyCode.Return);                // 화면 번쩍임
            yield return .3f;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Shot("status_options");
            Expect("options: sfx, text speed, flash changed", Settings.SfxPercent != sfx0 && Settings.TextSpeed != speed0 && !Settings.Flash);
            Keys.Press(KeyCode.Escape);                // 설정 닫기
            yield return (Func<bool>)(() => G.MessageText == "");
            yield return .3f;
            Expect("status still open after options", G.StatusOpen);
            Settings.ResetDefaults();
            Expect("options reset", Settings.Flash && Settings.SfxPercent == 100 && Settings.TextSpeed == 1);
            // 몬스터 도감 (45): 메뉴 → 도감. 만난 젤리는 그림·이름, 못 만난 젤리는 그림자·???, 다 잡으면 칭호
            Keys.Press(KeyCode.Return);
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            for (int k = 0; k < 2; k++) { Keys.Press(KeyCode.DownArrow); yield return .15f; }
            Keys.Press(KeyCode.Return);                // 도감
            yield return .4f;
            Shot("book");
            Log("BOOK " + G.BookText);
            Expect("book opens from status menu (no battle yet → all hidden)", G.BookOpen && G.BookText.Contains("몬스터 도감") && G.BookText.Contains("만남 0"));
            Keys.Press(KeyCode.RightArrow); yield return .15f;
            Keys.Press(KeyCode.DownArrow); yield return .15f;       // 여섯째 칸 (킹 젤리 — 아직 못 만남)
            yield return .2f;
            Shot("book_unseen");
            Expect("book: unseen entry hidden", G.BookText.Contains("???") && G.BookText.Contains("아직 못 만났어요"));
            var beaten0 = new List<string>(G.P.Beaten);
            var seen0 = new List<string>(G.P.Seen);
            foreach (var d in Bestiary.Entries) Bestiary.Beat(G.P, d);
            G.DebugBookSelect(5);
            yield return .2f;
            Shot("book_complete");
            Expect("book: all beaten → title", G.BookText.Contains(Bestiary.Title) && G.BookText.Contains("★ 쓰러뜨렸어요"));
            G.P.Beaten.Clear(); G.P.Beaten.AddRange(beaten0);
            G.P.Seen.Clear(); G.P.Seen.AddRange(seen0);
            Keys.Press(KeyCode.Escape);
            yield return .3f;
            Expect("book closes back to status", !G.BookOpen && G.StatusOpen);
            Keys.Press(KeyCode.Escape);
            yield return .3f;
            Expect("status closes with Esc", !G.StatusOpen && !G.Busy);

            // 보이는 몬스터: 필드에 돌아다니는 모습 → 앞 칸의 몬스터에게 걸어 들어가면 전투
            Expect("mobs spawned on field", G.MobCount >= FieldMonsters.StartCount);
            G.DebugMobs(true, false);                 // 이후 시나리오가 흔들리지 않게 멈춤
            // 대화창 자리: 주인공이 화면 아래쪽(맵 아래 끝)이면 위로, 가운데면 아래로
            G.DebugTeleport(20, 22, "down");
            yield return .3f;
            G.DebugShowMessage("주인공이 아래에 있으면 대화창이 위로 올라가요.");
            yield return .2f;
            Shot("message_top");
            Expect("message moves up when player is low", G.MessageAtTop);
            G.DebugHideMessage();
            G.DebugTeleport(20, 12, "down");
            yield return .3f;
            G.DebugShowMessage("가운데에 있으면 대화창은 아래에 있어요.");
            yield return .1f;
            Expect("message stays down normally", !G.MessageAtTop && G.MessageText != "");
            // 글자 크기 옵션: 크게로 바꾸면 대화창 글씨가 커진다 (3줄 문장이 창 안에 들어가는지 촬영)
            Settings.ToggleBigText();
            G.DebugShowMessage("글자 크기를 크게 하면 이렇게 보여요.\n두 번째 줄도 잘 보이나요?\n세 번째 줄까지 창 안에 들어가요.");
            yield return .1f;
            Shot("message_big_text");
            Expect("big text option enlarges message", G.MessageFontSize == 26);
            Settings.ToggleBigText();
            G.DebugShowMessage("가운데에 있으면 대화창은 아래에 있어요.");
            yield return .1f;
            Expect("text size back to normal", G.MessageFontSize == 23);
            G.DebugHideMessage();
            G.DebugTeleport(16, 10, "right");
            yield return .6f;
            Shot("field_mobs");
            // 앞뒤 정렬: 한 칸 아래(앞)의 몬스터는 주인공 위에, 한 칸 위(뒤)의 몬스터는 주인공 뒤에 그려진다
            G.DebugMobs(true, true);
            G.DebugTeleport(8, 12, "right");
            G.DebugSpawnMob(8, 13, 0);
            G.DebugSpawnMob(8, 11, 0);
            yield return .3f;
            Shot("mob_depth");
            Expect("mob depth sorting (front over, back under)", G.DebugMobDepthOk());
            // 도감 그림 파랑 젤리 (B-5a): 필드(위 mob_depth)와 전투 모두 그림으로
            Expect("blue jelly uses dogam art", G.UsesArt(GameData.Normal[0]));
            G.DebugMobs(true, true);
            G.DebugSpawnMob(9, 12, 0);
            yield return .3f;
            Keys.Hold(KeyCode.RightArrow, true); yield return .15f; Keys.Hold(KeyCode.RightArrow, false);
            yield return (Func<bool>)(() => G.CommandOpen);
            Expect("art jelly battle", G.BattleEnemy == GameData.Normal[0]);
            Shot("art_blue_battle");
            foreach (var x in FightUntilOver(false)) yield return x;
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;

            // 별빛 마을 (백로그 38): 초원 입구 → 마을 → 촌장 젤리와 이야기 → 출구로 초원에
            G.DebugTeleport(WorldMap.VillageGate.x, WorldMap.VillageGate.y + 1, "up");
            yield return .3f;
            Shot("village_gate");
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return (Func<bool>)(() => G.MapKind == MapKind.Village);
            yield return .5f;
            Expect("entered village", G.MapKind == MapKind.Village && (G.P.X, G.P.Y) == WorldMap.VillageStart && G.P.InVillage && G.MobCount == 0);
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            ExpectBgm("village");
            Shot("village");
            G.DebugTeleport(16, 13, "up");
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return 1.2f;
            Shot("village_chief_talk");
            Expect("village chief talks", G.MessageText.Contains("촌장 젤리"));
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            // 여관 (백로그 39): 다친 채로 묵으면 10G 내고 밤 → 아침에 가득
            G.P.Hp = 3;
            int innGold = G.P.Gold;
            G.DebugTeleport(25, 8, "left");            // 옆에서 말을 건다 (아래에서 걸면 주인공 머리가 주인을 가림)
            yield return .3f;
            Keys.Hold(KeyCode.LeftArrow, true); yield return .15f; Keys.Hold(KeyCode.LeftArrow, false);
            foreach (var x in SkipTalk(() => G.ChoiceOpen)) yield return x;
            Shot("inn_ask");
            Keys.Press(KeyCode.Return);
            yield return (Func<bool>)(() => G.InnNight);
            yield return .6f;
            Shot("inn_night");
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            Expect("inn heals for 10G", G.P.Hp == G.P.MaxHp && G.P.Gold == innGold - Inn.Price);
            G.DebugTeleport(WorldMap.VillageStart.x, WorldMap.VillageStart.y, "down");
            yield return .3f;
            Keys.Hold(KeyCode.DownArrow, true); yield return .15f; Keys.Hold(KeyCode.DownArrow, false);
            yield return (Func<bool>)(() => G.MapKind == MapKind.Field && !G.Busy);
            Expect("left village to field", G.MapKind == MapKind.Field && !G.P.InVillage && (G.P.X, G.P.Y) == (WorldMap.VillageGate.x, WorldMap.VillageGate.y + 1));

            G.DebugTeleport(8, 12, "right");
            G.DebugMobs(true, true);
            G.DebugSpawnMob(9, 12, 1);
            yield return .3f;
            Shot("mob_ahead");
            Expect("nearby mob shows '!'", G.AlertCount == 1);
            Keys.Hold(KeyCode.RightArrow, true); yield return .15f; Keys.Hold(KeyCode.RightArrow, false);
            yield return .4f;
            Expect("walking into a mob starts battle", G.Current == GameRoot.Mode.Battle && G.MobCount == 0);

            // 일반 전투: 명령 하나씩
            if (G.Current != GameRoot.Mode.Battle) G.DebugBattle(false);
            yield return (Func<bool>)(() => G.CommandOpen);
            Shot("battle_command");
            Expect("swirl gone when command opens", !G.SwirlVisible);
            Expect("enemy hp bar shown", G.EnemyHpVisible && G.EnemyHpShown > .99f);
            ExpectBgm("battle");
            foreach (var x in Choose(0)) yield return x;    // 리본 파이어
            yield return (Func<bool>)(() => G.SpellFx == "fire");
            yield return .22f;
            Shot("battle_fireball");
            Expect("fireball fx plays", G.SpellFx == "fire" || G.FxCount > 10);
            yield return (Func<bool>)(() => G.CommandOpen || G.Current == GameRoot.Mode.Field);
            if (G.CommandOpen) { Shot("enemy_hp_after_hit"); Log($"ENEMY HP shown={G.EnemyHpShown:F2}"); }
            yield return (Func<bool>)(() => G.CommandOpen || G.Current == GameRoot.Mode.Field);
            if (G.CommandOpen)
            {
                foreach (var x in Choose(1)) yield return x;    // 주문 → 목록
                yield return (Func<bool>)(() => G.ChoiceOpen);
                yield return .2f;
                Shot("battle_spell_list");
                Expect("spell list: only star at Lv1", G.ChoiceCount == 2);
                Keys.Press(KeyCode.Return);                     // 별빛 폭풍 (목록 맨 위)
                yield return 1.0f;
                Shot("battle_cutin");
                yield return (Func<bool>)(() => G.SpellFx == "star");
                yield return .4f;
                Shot("battle_starstorm");
                Expect("star storm meteors", G.SpellFx == "star" && G.FxCount > 30);
            }
            foreach (var x in FightUntilOver(false)) yield return x;
            Shot("after_battle_field");
            Expect($"book records the battle (seen {Bestiary.SeenCount(G.P)} beaten {Bestiary.BeatenCount(G.P)})",
                   Bestiary.BeatenCount(G.P) >= 1 && Bestiary.SeenCount(G.P) >= Bestiary.BeatenCount(G.P));
            Expect("enemy hp bar hidden in field", !G.EnemyHpVisible);
            ExpectBgm("field");

            // 몬스터 무리 (백로그 44): 파랑 젤리 3마리 — 대상 고르기 ▼, 별빛 폭풍 전체, 경험치 합계
            G.DebugSetHp(G.P.MaxHp);
            int groupExp0 = G.P.Exp;
            G.DebugGroupBattle(3, 0);
            yield return (Func<bool>)(() => G.CommandOpen);
            yield return .3f;
            Shot("group_battle");
            Expect("group of 3 on stage", G.FoeCount == 3 && G.GroupTagsShown && !G.EnemyHpVisible);
            foreach (var x in Choose(0)) yield return x;       // 리본 파이어 → 대상 고르기
            yield return (Func<bool>)(() => G.ChoiceOpen);
            Keys.Press(KeyCode.DownArrow); yield return .25f;
            Shot("group_target");
            Expect("target list + arrow", G.TargetArrowShown && G.ChoiceCount == 4);
            Keys.Press(KeyCode.Return);
            yield return .7f;
            Shot("group_fire");
            yield return (Func<bool>)(() => G.CommandOpen || G.Current == GameRoot.Mode.Field);
            if (G.CommandOpen)
            {
                G.DebugSetMp(G.P.MaxMp);
                foreach (var x in Choose(1)) yield return x;    // 주문 → 별빛 폭풍 (모두에게)
                yield return (Func<bool>)(() => G.ChoiceOpen);
                Keys.Press(KeyCode.Return);
                yield return 2.0f;
                Shot("group_starstorm");
            }
            foreach (var x in FightUntilOver(false)) yield return x;
            Log($"GROUP exp {groupExp0}->{G.P.Exp}");
            Expect("group win gives total exp", G.Current == GameRoot.Mode.Field && G.P.Exp == groupExp0 + 3 * GameData.Normal[0].Exp);

            // 사탕 / 도망 (들어갈 때 소용돌이 전환도 찍는다)
            G.DebugBattle(false);
            yield return .3f;
            Shot("encounter_swirl");
            yield return (Func<bool>)(() => G.EnemyAppearing);
            yield return .55f;
            Shot("enemy_appear");                     // 위에서 통 떨어지는 중
            yield return (Func<bool>)(() => G.CommandOpen);
            Expect("enemy appear finished", !G.EnemyAppearing);

            // 적마다 다른 공격 모션: 모든 적 모습으로 모션 중간을 멈춰서 찍는다
            foreach (var d in GameData.AllEnemies())
            {
                G.DebugFreezeMove(d, d.Move == "slam" ? .45f : d.Move == "charge" ? .6f : d.Move == "shard" ? .5f : .4f);
                yield return .2f;
                Shot("move_" + d.Move + "_" + Array.IndexOf(EnemyDef.Moves, d.Move));
            }
            G.DebugFreezeMove(null, 1);
            yield return .1f;
            G.DebugSetHp(4);
            yield return .2f;
            foreach (var x in Choose(2, "battle_candy_select")) yield return x;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .15f;
            Shot("battle_item_list");
            Keys.Press(KeyCode.Return);                // 사탕 (목록 맨 위)
            yield return .9f;
            Shot("battle_candy");
            yield return (Func<bool>)(() => G.CommandOpen || G.Current == GameRoot.Mode.Field);
            for (int i = 0; i < 6 && G.Current == GameRoot.Mode.Battle; i++)
            {
                yield return (Func<bool>)(() => G.CommandOpen || G.Current == GameRoot.Mode.Field);
                if (!G.CommandOpen) break;
                foreach (var x in Choose(3)) yield return x;    // 도망가기
                yield return 1.1f;
                if (i == 0) Shot("battle_run");
                foreach (var x in SkipTalk(() => G.CommandOpen || G.Current == GameRoot.Mode.Field)) yield return x;
            }

            // 패배 → 부활
            G.DebugBattle(false);
            yield return (Func<bool>)(() => G.CommandOpen);
            G.DebugSetEnemyHp(9999);                   // 먼저 이겨 버리지 않게
            bool sawDefeat = false, sawRevive = false, sawHurt = false;
            for (int i = 0; i < 30 && G.Current == GameRoot.Mode.Battle; i++)
            {
                if (G.CommandOpen) { G.DebugSetHp(1); foreach (var x in Choose(0)) yield return x; }   // 매 턴 HP 1 → 반드시 쓰러진다
                else Keys.Press(KeyCode.Z);
                for (int j = 0; j < 10; j++)
                {
                    yield return .05f;
                    if (!sawHurt && G.HurtFlashing) { sawHurt = true; Shot("hurt_flash"); }
                }
                if (!sawDefeat && G.MessageText.Contains("쓰러지")) { sawDefeat = true; Shot("defeat"); }
            }
            for (int i = 0; i < 20 && !(G.Current == GameRoot.Mode.Field && !G.Busy); i++)
            {
                if (!sawRevive && G.MessageText.Contains("다시 일어났다")) { sawRevive = true; Shot("revive"); }
                Keys.Press(KeyCode.Z);
                yield return .3f;
            }
            Shot("after_revive");
            Expect("defeat → revive at fountain", sawDefeat && G.P.X == 5 && G.P.Y == 4);
            Expect("red flash when hurt", sawHurt);
            ExpectBgm("field");

            // 세이브 → 타이틀 → 이어하기
            int savedLv = G.P.Lv, savedX = G.P.X, savedY = G.P.Y;
            G.DebugToTitle();
            yield return .5f;
            Expect("title offers continue", G.CanContinue && G.TitleIndex == 1);
            Shot("title_continue");
            Keys.Press(KeyCode.Return);
            yield return 1.2f;
            Shot("continue_msg");
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            Expect("continue restores progress", G.Current == GameRoot.Mode.Field && G.P.Lv == savedLv && G.P.X == savedX && G.P.Y == savedY);
            ExpectBgm("field");

            // 세이브 3칸: 3번 칸에도 저장 → 이어하기에서 칸 고르기
            G.DebugSaveTo(2);
            G.DebugToTitle();
            yield return .5f;
            Keys.Press(KeyCode.Return);
            yield return .4f;
            Expect("continue with 2 saves opens slot picker", G.SlotPickerOpen);
            Shot("slot_load");
            Keys.Press(KeyCode.Return);                 // 기본 = 마지막으로 저장한 3번 칸
            yield return 1.2f;
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            Expect("continue from slot 3", G.Current == GameRoot.Mode.Field && G.CurrentSlot == 2 && G.P.Lv == savedLv);
            // 새로 시작: 칸 고르기 → 그만두기 / 찬 칸은 덮어쓰기 확인(기본 그만두기)
            G.DebugToTitle();
            yield return .5f;
            Keys.Press(KeyCode.UpArrow); yield return .15f;
            Keys.Press(KeyCode.Return);
            yield return .4f;
            Expect("new game with saves opens slot picker", G.SlotPickerOpen);
            Shot("slot_new");
            Keys.Press(KeyCode.Escape);
            yield return .4f;
            Expect("slot picker cancel stays on title", G.Current == GameRoot.Mode.Title && !G.Busy);
            Keys.Press(KeyCode.Return);
            yield return .4f;
            Keys.Press(KeyCode.UpArrow); yield return .15f;    // 2번(빈 칸) → 1번(찬 칸)
            Keys.Press(KeyCode.Return);
            yield return .4f;
            Shot("slot_overwrite");
            Expect("overwrite asks first", G.MessageText.Contains("지우고"));
            Keys.Press(KeyCode.Return);                 // 기본 = 그만두기
            yield return .4f;
            Expect("overwrite cancel keeps title", G.Current == GameRoot.Mode.Title && !G.Busy);
            Keys.Press(KeyCode.DownArrow); yield return .15f;  // 이어하기로
            Keys.Press(KeyCode.Return);
            yield return .4f;
            Keys.Press(KeyCode.Return);
            yield return 1.2f;
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            Expect("back to field after slot tests", G.Current == GameRoot.Mode.Field && G.P.Lv == savedLv);

            // 보스 → 엔딩
            G.DebugBattle(true);
            yield return (Func<bool>)(() => G.EnemyAppearing);
            yield return .5f;
            Shot("boss_appear");                      // 땅에서 불쑥 커지는 중
            yield return (Func<bool>)(() => G.CommandOpen);
            Shot("boss_command");
            ExpectBgm("boss");
            // 반짝 힐 (백로그 42, Lv4 에 배움): 다친 채로 주문 → 목록 두 번째
            G.DebugSetHp(G.P.MaxHp / 3);
            int healHp0 = G.P.Hp, healMp0 = G.P.Mp;
            foreach (var x in Choose(1)) yield return x;
            yield return (Func<bool>)(() => G.ChoiceOpen);
            yield return .2f;
            Keys.Press(KeyCode.DownArrow); yield return .15f;
            Shot("battle_heal_select");
            Keys.Press(KeyCode.Return);
            yield return (Func<bool>)(() => G.SpellFx == "heal");
            yield return .45f;
            Shot("battle_heal");
            Expect("heal pillar plays", G.SpellFx == "heal" && G.FxCount > 5);
            Log($"HEAL hp {healHp0}->{G.P.Hp} mp {healMp0}->{G.P.Mp}");
            Expect("heal spell restores hp for mp", G.P.Lv >= 4 && G.P.Hp > healHp0 && G.P.Mp == healMp0 - GameData.Skill(Cmd.Heal).Mp);
            G.DebugSetHp(G.P.MaxHp);                  // 힐 확인용으로 깎은 HP 는 되돌린다 (보스전 결과가 운에 달리지 않게)
            G.DebugSetMp(healMp0);
            foreach (var x in FightUntilOver(true)) yield return x;
            yield return (Func<bool>)(() => G.Current == GameRoot.Mode.Ending);
            yield return 1f;
            Shot("ending");
            ExpectBgm("ending");

            // 챕터 2 (동굴): 엔딩에서 Enter → 동굴 입구 → 잠긴 문 → 보물상자(열쇠) → 문 열고 기지 안 → 젤리 장군 → 챕터 2 완료
            Keys.Press(KeyCode.Z);                     // 엔딩 직후 연타는 무시돼야 한다
            yield return .2f;
            Expect("ending ignores instant mash", G.Current == GameRoot.Mode.Ending);
            yield return .8f;
            Keys.Press(KeyCode.Return);
            yield return 1.2f;
            Shot("ch2_intro");
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            Expect("chapter 2 in cave", G.P.Chapter == 2 && G.Current == GameRoot.Mode.Field && G.MapKind == MapKind.Cave);
            ExpectBgm("cave");
            Expect("cave has mobs", G.MobCount >= FieldMonsters.StartCount);
            G.DebugMobs(true, false);
            yield return .5f;
            Shot("cave_start");
            // 광부 젤리 (백로그 41): 동굴 입구에서 열쇠 이야기
            G.DebugTeleport(3, 20, "left");
            yield return .3f;
            Keys.Hold(KeyCode.LeftArrow, true); yield return .15f; Keys.Hold(KeyCode.LeftArrow, false);
            yield return 1.2f;
            Shot("cave_miner_talk");
            Expect("cave miner talks about the key", G.MessageText.Contains("광부 젤리"));
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            G.DebugTeleport(16, 11, "down");
            yield return .5f;
            Shot("cave_pool");
            G.DebugTeleport(12, 5, "up");
            yield return .5f;
            Shot("cave_crystals");

            // 동굴 전투 (배경 확인)
            G.DebugMobs(true, true);
            G.DebugTeleport(15, 19, "right");
            G.DebugSpawnMob(16, 19, 1);
            yield return .2f;
            Keys.Hold(KeyCode.RightArrow, true); yield return .15f; Keys.Hold(KeyCode.RightArrow, false);
            yield return (Func<bool>)(() => G.CommandOpen);
            Shot("cave_battle");
            foreach (var x in FightUntilOver(false)) yield return x;
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;

            // 2단 "큰 돌멩이 젤리" (딸의 노트): 필드에서 옆 칸 작은 젤리보다 크게 보이고, 전투에서도 크게
            G.DebugMobs(true, true);
            G.DebugTeleport(15, 19, "right");
            G.DebugSpawnMob(16, 19, 3);
            G.DebugSpawnMob(14, 18, 0);
            yield return .4f;
            Shot("cave_big_jelly_field");
            Keys.Hold(KeyCode.RightArrow, true); yield return .15f; Keys.Hold(KeyCode.RightArrow, false);
            yield return (Func<bool>)(() => G.CommandOpen);
            Expect("big jelly battle", G.BattleEnemy != null && G.BattleEnemy.Tier == 2 && G.BattleEnemy.Name.StartsWith("큰 "));
            Shot("cave_big_jelly_battle");
            foreach (var x in FightUntilOver(false)) yield return x;
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;

            // 수상한 구멍 (B-7, 서진이 노트 3): 사탕 → 2단 괴물 → 빈 구멍
            Expect("cave has holes", G.HoleCount == 3);
            foreach (var (roll, what) in new[] { (0.0, "candy"), (.99, "monster") })
            {
                var h = G.DebugHoleApproach(true);
                Expect($"hole approachable ({what})", h.dir != null);
                if (h.dir == null) break;
                G.DebugMobs(true, true);
                G.DebugTeleport(h.x, h.y, h.dir);
                G.DebugHoleRoll = roll;
                yield return .4f;
                Shot("hole_" + what + "_before");
                int holeCandy0 = G.P.Candy;
                var key = h.dir == "up" ? KeyCode.UpArrow : h.dir == "down" ? KeyCode.DownArrow : h.dir == "left" ? KeyCode.LeftArrow : KeyCode.RightArrow;
                Keys.Hold(key, true); yield return .15f; Keys.Hold(key, false);
                if (what == "candy")
                {
                    yield return .6f;
                    foreach (var x in SkipTalk(() => G.MessageText.Contains("사탕"))) yield return x;
                    yield return .5f;
                    Shot("hole_candy");
                    Expect("hole gives candy", G.P.Candy == Math.Min(PlayerState.BagCandy, holeCandy0 + Holes.CandyGain));
                    foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
                }
                else
                {
                    yield return .6f;
                    foreach (var x in SkipTalk(() => G.MessageText.Contains("이단 괴물"))) yield return x;
                    yield return .3f;
                    Shot("hole_monster");
                    foreach (var x in SkipTalk(() => G.CommandOpen)) yield return x;
                    Expect("hole monster battle is tier 2", G.Current == GameRoot.Mode.Battle && G.BattleEnemy != null && G.BattleEnemy.Tier == 2);
                    Shot("hole_monster_battle");
                    foreach (var x in FightUntilOver(false)) yield return x;
                    foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
                }
            }
            G.DebugHoleRoll = -1;
            var used = G.DebugHoleApproach(false);
            Expect("looked hole approachable", used.dir != null);
            if (used.dir != null)
            {
                G.DebugTeleport(used.x, used.y, used.dir);
                yield return .3f;
                var key = used.dir == "up" ? KeyCode.UpArrow : used.dir == "down" ? KeyCode.DownArrow : used.dir == "left" ? KeyCode.LeftArrow : KeyCode.RightArrow;
                Keys.Hold(key, true); yield return .15f; Keys.Hold(key, false);
                yield return 1.0f;
                Shot("hole_empty");
                Expect("looked hole is empty", G.MessageText.Contains("텅 빈"));
                foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            }

            G.DebugTeleport(28, 9, "up");
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return 1.3f;
            Shot("door_locked");
            Expect("door locked without key", G.MessageText.Contains("잠겨"));
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            // 보물상자 지킴이(중간 보스): 상자에 부딪히면 싸움 → 이기면 필드로 돌아와 상자를 연다
            G.DebugTeleport(4, 8, "left");
            yield return .5f;
            Shot("chest_guard");
            Expect("guard stands by the chest", G.GuardVisible);
            Expect("guard alerts when near", G.GuardAlerted);
            Keys.Hold(KeyCode.LeftArrow, true); yield return .15f; Keys.Hold(KeyCode.LeftArrow, false);
            foreach (var x in SkipTalk(() => G.CommandOpen)) yield return x;
            Expect("guard battle starts at chest", G.Current == GameRoot.Mode.Battle && G.BgmName == "boss" && !G.P.HasItem("key"));
            Shot("guard_battle");
            G.DebugSetEnemyHp(25);
            foreach (var x in FightUntilOver(true)) yield return x;
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field)) yield return x;
            yield return .8f;
            Shot("guard_roll_away");
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Field && !G.Busy)) yield return x;
            Expect("guard down, back in cave (no ending)", G.P.GuardDown && G.Current == GameRoot.Mode.Field && G.P.Chapter == 2 && !G.P.Boss2Down && !G.GuardVisible);
            G.DebugTeleport(4, 8, "left");
            yield return .3f;
            Shot("chest_no_guard");
            Keys.Hold(KeyCode.LeftArrow, true); yield return .15f; Keys.Hold(KeyCode.LeftArrow, false);
            yield return 1.4f;
            Shot("chest_open");
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            Expect("got key from chest", G.P.HasItem("key"));
            G.DebugTeleport(28, 9, "up");
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            yield return 1.3f;
            Shot("door_open");
            Expect("door opens with key", G.MessageText.Contains("열렸다"));
            foreach (var x in SkipTalk(() => !G.Busy)) yield return x;
            Keys.Hold(KeyCode.UpArrow, true); yield return .5f; Keys.Hold(KeyCode.UpArrow, false);
            yield return .4f;
            Expect("walked through the open gate", G.P.Y <= 6);
            Shot("inside_base");

            G.DebugTeleport(28, 4, "up");
            yield return .3f;
            Keys.Hold(KeyCode.UpArrow, true); yield return .15f; Keys.Hold(KeyCode.UpArrow, false);
            foreach (var x in SkipTalk(() => G.CommandOpen)) yield return x;
            Shot("boss2_command");
            G.DebugSetEnemyHp(25);                     // 흐름만 확인 (난이도는 SmokeTest 밸런스로)
            foreach (var x in FightUntilOver(true)) yield return x;
            foreach (var x in SkipTalk(() => G.Current == GameRoot.Mode.Ending)) yield return x;
            yield return 1f;
            Shot("ch2_clear");
            Expect("chapter 2 cleared", G.P.Boss2Down && G.Current == GameRoot.Mode.Ending);
            ExpectBgm("ending");
        }

        /// <summary>전투가 끝날 때까지: HP 35% 이하면 사탕, 보스면 별빛 폭풍 우선, 아니면 리본 파이어 (SmokeTest 밸런스 AI 와 같은 규칙).</summary>
        static IEnumerable FightUntilOver(bool boss)
        {
            int lvShot = 0;
            for (int i = 0; i < 200 && G.Current == GameRoot.Mode.Battle; i++)
            {
                if (G.CommandOpen)
                {
                    int pick = G.P.Hp <= G.P.MaxHp * .35f && G.P.Candy > 0 ? 2 : boss && G.P.Mp >= 6 ? 1 : 0;
                    foreach (var x in Choose(pick)) yield return x;
                    if (pick == 1)
                    {
                        yield return (Func<bool>)(() => G.ChoiceOpen);
                        Keys.Press(KeyCode.Return);    // 주문 목록 맨 위 = 별빛 폭풍
                    }
                    if (pick == 2)
                    {
                        yield return (Func<bool>)(() => G.ChoiceOpen);
                        Keys.Press(KeyCode.Return);    // 도구 목록 맨 위 = 사탕
                    }
                }
                else Keys.Press(KeyCode.Z);
                yield return .3f;
                if (G.MessageText.Contains("레벨이 올랐다") && lvShot++ == 0) Shot("levelup");
            }
        }

        static void Finish()
        {
            Keys.ReleaseAll();
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }
}
