using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 필드: 맵(챕터 1 초원 / 챕터 2 동굴), 격자 이동, 보이는 몬스터(심볼 인카운트),
    /// 샘물/표지판/사탕 가게/보물상자/기지 문/수상한 구멍/보스 이벤트.
    /// </summary>
    public partial class GameRoot
    {
        const float StepTime = .17f, FieldCharHeight = 1.75f, MobHeight = 1.15f;

        Transform fieldRoot;
        SpriteRenderer mapSR, playerSR, shadowSR, chestOpenSR, doorOpenSR, guardSR, guardShadowSR, wellWaterSR, sandTwisterSR, momSR;
        const float GuardHeight = 1.6f;         // 보물상자 지킴이는 보통 젤리보다 크게
        Sprite[] sideFrames;
        Sprite[] frontFrames, backFrames;       // 앞/뒤 걷기: 한 발 → 두 발 모음 → 반대 발 → 두 발 모음 (tools/make_walk_frames.py)
        string facing = "down";
        Vector2Int moveFrom;
        // 동생 지훈 (합류 뒤): DQ 대열처럼 서진이 방금 있던 칸을 한 걸음 늦게 따라온다
        /// <summary>대열의 동료 한 명 (합류 순서대로 앞사람이 있던 칸을 한 걸음 늦게 따라온다, DQ 대열).</summary>
        class Follower
        {
            public string id;
            public SpriteRenderer sr, shadow;
            public Sprite[] side;
            public Sprite front, back;
            public Vector2Int tile, from;
            public string facing = "down";
        }
        readonly List<Follower> followers = new List<Follower>();
        float moveT = -1, walkT;
        bool sliding;                           // 얼음 위를 미끄러지는 중 (C5-3): 걷는 그림 대신 선 채로 쭉
        WorldMap fieldMap, caveMap, villageMap, desertMap, townMap, underMap, lavaMap, lavaTownMap, snowMap, snowTownMap, libraryMap, forestMap;
        WorldMap[] homeMaps;                    // 별빛 마을 집 안 (P-5)
        SpriteRenderer homeChestSR, letterSR;
        readonly Dictionary<WorldMap, Sprite> mapSprites = new Dictionary<WorldMap, Sprite>();
        FieldTiles fieldTiles;
        readonly Dictionary<WorldMap, System.Threading.Tasks.Task<Raster>> mapJobs = new Dictionary<WorldMap, System.Threading.Tasks.Task<Raster>>();

        // 보이는 몬스터
        FieldMonsters mobs;
        readonly Dictionary<int, SpriteRenderer> mobAlerts = new Dictionary<int, SpriteRenderer>();
        Sprite alertSprite;
        public const int AlertDist = 3;         // 이 거리(칸) 안이면 몬스터 머리 위에 "!"
        readonly Dictionary<int, (SpriteRenderer body, SpriteRenderer shadow, float scale)> mobViews = new Dictionary<int, (SpriteRenderer, SpriteRenderer, float)>();

        void BuildField()
        {
            fieldRoot = new GameObject("Field").transform;
            fieldMap = map;
            caveMap = WorldMap.Cave();
            villageMap = WorldMap.Village();
            desertMap = WorldMap.Desert();
            townMap = WorldMap.OasisTown();
            underMap = WorldMap.Underworld();
            lavaMap = WorldMap.Lava();
            lavaTownMap = WorldMap.LavaTown();
            snowMap = WorldMap.Snow();
            libraryMap = WorldMap.Library();
            forestMap = WorldMap.Forest();
            snowTownMap = WorldMap.SnowTown();
            Npcs.Place(snowTownMap);
            homeMaps = new WorldMap[WorldMap.AllHouses.Length];
            for (int i = 0; i < homeMaps.Length; i++) { homeMaps[i] = WorldMap.Home(i); Npcs.Place(homeMaps[i]); }
            mobs = new FieldMonsters(rng);
            Npcs.Place(fieldMap);               // 마을 사람 칸('N')을 먼저 놓고 그린다
            Npcs.Place(caveMap);
            Npcs.Place(villageMap);
            Npcs.Place(desertMap);
            Npcs.Place(townMap);
            Npcs.Place(underMap);
            Npcs.Place(lavaTownMap);
            // 맵 그림은 무거워서(1초+) 타이틀을 보는 동안 백그라운드에서 미리 그린다
            // (타일 이미지는 Unity API 라 여기 메인 스레드에서 먼저 읽어 둔다)
            fieldTiles = FieldTiles.Load();
            foreach (var m in new List<WorldMap> { fieldMap, caveMap, villageMap, desertMap, townMap, underMap, lavaMap, lavaTownMap, snowMap, snowTownMap, libraryMap, forestMap })   // 집 안 18채는 미리 그리지 않고 처음 들어갈 때 그린다 (한 장 8MB, Q-16)
            {
#if !(UNITY_WEBGL && !UNITY_EDITOR)
                var mm = m;
                mapJobs[mm] = System.Threading.Tasks.Task.Run(() => MapPainter.RenderRaster(mm, fieldTiles));
#endif
            }   // 웹(WebGL, 태블릿 브라우저용)은 스레드가 없어 미리 그리지 않고 처음 들어갈 때 그린다

            mapSR = new GameObject("Map").AddComponent<SpriteRenderer>();
            mapSR.transform.SetParent(fieldRoot, false);
            mapSR.sortingOrder = 0;

            shadowSR = new GameObject("Shadow").AddComponent<SpriteRenderer>();
            shadowSR.transform.SetParent(fieldRoot, false);
            shadowSR.sprite = Art.FromTexture(Painter.Glow(), new Vector2(.5f, .5f), 64);
            shadowSR.color = new Color(0, 0, 0, .45f);
            shadowSR.transform.localScale = new Vector3(.9f, .3f, 1);
            shadowSR.sortingOrder = 1;

            playerSR = new GameObject("Player").AddComponent<SpriteRenderer>();
            playerSR.transform.SetParent(fieldRoot, false);
            playerSR.sortingOrder = 2;
            sideFrames = new[] { Art.Get("walk_side1", FieldCharHeight), Art.Get("walk_side2", FieldCharHeight),
                                 Art.Get("walk_side3", FieldCharHeight), Art.Get("walk_side4", FieldCharHeight) };
            frontFrames = new[] { Art.Get("walk_front", FieldCharHeight), Art.Get("walk_front2", FieldCharHeight), Art.Get("walk_front3", FieldCharHeight) };
            backFrames = new[] { Art.Get("walk_back", FieldCharHeight), Art.Get("walk_back2", FieldCharHeight), Art.Get("walk_back3", FieldCharHeight) };
            playerSR.sprite = frontFrames[1];


            chestOpenSR = Prop("ChestOpen", Painter.ChestTexture(true), WorldMap.Chest);
            homeChestSR = Prop("HomeChestOpen", Painter.ChestTexture(true), (0, 0));
            letterSR = Prop("Letter", Painter.LetterTexture(), WorldMap.DesertLetter);
            wellWaterSR = Prop("WellWater", MapPainter.WellWaterTexture(), WorldMap.TownWell);
            // 남쪽 바위 골짜기의 모래 회오리: PIPOYA 시트 (7,1) 회오리 그림 (대장을 이기면 사라진다)
            sandTwisterSR = new GameObject("SandTwister").AddComponent<SpriteRenderer>();
            sandTwisterSR.transform.SetParent(fieldRoot, false);
            var sheet = Resources.Load<Texture2D>("Tiles/pipo_sheet");
            if (sheet != null)
                sandTwisterSR.sprite = Sprite.Create(sheet, new Rect(7 * Painter.Tile, sheet.height - 2 * Painter.Tile, Painter.Tile, Painter.Tile), new Vector2(.5f, .05f), Painter.Tile / 1.8f);
            sandTwisterSR.sortingOrder = 2;
            sandTwisterSR.enabled = false;
            doorOpenSR = Prop("DoorOpen", MapPainter.OpenDoorTexture(), (WorldMap.BaseDoor.x, WorldMap.BaseDoor.y - 1));
            BuildFieldFx();
            BuildLighting();
            RebuildMapFx();
        }

        SpriteRenderer Prop(string name, Texture2D tex, (int x, int y) at)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(fieldRoot, false);
            sr.sprite = Art.FromTexture(tex, new Vector2(0, 1), Painter.Tile);
            sr.transform.localPosition = new Vector3(at.x, -at.y, 0);
            sr.sortingOrder = 1;
            sr.enabled = false;
            return sr;
        }

        Sprite MapSprite(WorldMap m)
        {
            if (!mapSprites.TryGetValue(m, out var s))
            {
                float t0 = Time.realtimeSinceStartup;
                var raster = mapJobs.TryGetValue(m, out var job) ? job.Result : MapPainter.RenderRaster(m, fieldTiles);   // 아직이면 기다린다
                s = Art.FromTexture(raster.ToTexture(), new Vector2(0, 1), Painter.Tile);   // 1 타일 = 1 유닛
                mapSprites[m] = s;
                mapJobs.Remove(m);
                Debug.Log($"[Map] {m.Kind} 준비 대기 {(Time.realtimeSinceStartup - t0) * 1000:F0}ms");
            }
            return s;
        }

        /// <summary>그 맵의 그림(텍스처)을 메모리에서 내린다.</summary>
        void ReleaseMapSprite(WorldMap m)
        {
            if (m == map || !mapSprites.TryGetValue(m, out var sp)) return;
            mapSprites.Remove(m);
            if (sp != null) { Destroy(sp.texture); Destroy(sp); }
        }

        /// <summary>챕터(와 마을 안인지)에 맞는 맵으로 바꾼다 (이미 그 맵이면 아무것도 안 함).</summary>
        void ApplyMap()
        {
            var want = P.Underground ? underMap : P.Home >= 0 && P.Home < homeMaps.Length ? homeMaps[P.Home] : SurfaceMap;
            if (map == want && mobs.Map == want) return;
            map = want;
            mapSR.sprite = MapSprite(map);
            RebuildMapFx();
            RebuildLights();
            RebuildNpcs();
            RebuildHoles();
            mobs.Reset(map, P.X, P.Y);
            SyncMobViews();
        }

        /// <summary>지하세계가 아닐 때 있어야 할 맵 (챕터·마을 안).</summary>
        WorldMap SurfaceMap => P.Chapter >= 7 ? forestMap : P.Chapter >= 6 ? libraryMap : P.Chapter >= 5 ? (P.InVillage ? snowTownMap : snowMap) : P.Chapter >= 4 ? (P.InVillage ? lavaTownMap : lavaMap) : P.Chapter == 3 ? (P.InVillage ? townMap : desertMap) : P.Chapter == 2 ? caveMap : P.InVillage ? villageMap : fieldMap;

        /// <summary>연 상자·열린 문·보물상자 지킴이 그림.</summary>
        void RefreshQuestProps()
        {
            bool cave = map.Kind == MapKind.Cave;
            RefreshHoles();
            RefreshQuestSpots();
            chestOpenSR.enabled = cave && P.Opened.Contains($"{WorldMap.Chest.x},{WorldMap.Chest.y}");
            doorOpenSR.enabled = cave && Quest.DoorOpen(P);
            letterSR.enabled = map.Kind == MapKind.Desert && !Quest.HasLetter(P);
            (int x, int y) hc = map.Kind == MapKind.Home || map.Kind == MapKind.Snow || map.Kind == MapKind.Library || map.Kind == MapKind.Forest ? map.Find('H') : (-1, -1);
            homeChestSR.enabled = hc.x >= 0 && P.Opened.Contains(map.Kind == MapKind.Snow ? Quest.SnowChestId : map.Kind == MapKind.Library ? Quest.LibChestId : map.Kind == MapKind.Forest ? Quest.ForChestId : Quest.HomeChestId(map.Room));
            if (homeChestSR.enabled) homeChestSR.transform.localPosition = new Vector3(hc.x, -hc.y, 0);
            wellWaterSR.enabled = map.Kind == MapKind.Oasis && P.Boss3Down;      // 사막 보스를 이기면 마른 우물에 물이 찬다
            sandTwisterSR.enabled = map.Kind == MapKind.Desert && !P.Boss3Down && sandTwisterSR.sprite != null;
            bool momHere = map.Kind == MapKind.Library && !Quest.MomJoined(P);
            if (momHere && momSR == null)
            {
                momSR = new GameObject("MomAtCircle").AddComponent<SpriteRenderer>();
                momSR.transform.SetParent(fieldRoot, false);
                momSR.sprite = Art.Get("Mom/walk_front", FieldCharHeight * 1.12f);
                momSR.sortingOrder = 2;
                momSR.transform.localPosition = FeetAt(WorldMap.MomCircle.x, WorldMap.MomCircle.y - .35f);
            }
            if (momSR != null) momSR.enabled = momHere;
            bool guard = cave && Quest.GuardAlive(P);
            if (guard && guardSR == null)
            {
                guardShadowSR = new GameObject("GuardShadow").AddComponent<SpriteRenderer>();
                guardShadowSR.transform.SetParent(fieldRoot, false);
                guardShadowSR.sprite = shadowSR.sprite;
                guardShadowSR.color = new Color(0, 0, 0, .45f);
                guardShadowSR.transform.localScale = new Vector3(1.2f, .34f, 1);
                guardShadowSR.sortingOrder = 1;
                guardSR = new GameObject("Guard").AddComponent<SpriteRenderer>();
                guardSR.transform.SetParent(fieldRoot, false);
                guardSR.sprite = JellySprite(GameData.Guard);
                guardSR.sortingOrder = 2;
            }
            // 방금 쓰러뜨렸으면 (그림이 켜진 채 죽음) 바로 치우지 않고 굴러 달아나는 연출을 기다린다 (EndBattle 이 재생)
            if (guardSR != null && guardSR.enabled && !guard && cave && Current == Mode.Field) guardRollPending = true;
            else if (guardSR != null) { guardSR.enabled = guardShadowSR.enabled = guard; TickGuard(); }
        }

        /// <summary>지킴이: 제자리에서 느릿느릿 숨 쉬듯 출렁인다.</summary>
        void TickGuard()
        {
            bool near = guardSR != null && guardSR.enabled && !guardRollPending && !guardRolling && Current == Mode.Field
                        && Mathf.Abs(P.X - WorldMap.GuardSpot.x) + Mathf.Abs(P.Y - WorldMap.GuardSpot.y) <= GuardAlertDist;
            if (guardAlert != null && !near) { Destroy(guardAlert.gameObject); guardAlert = null; }
            if (guardSR == null || !guardSR.enabled || guardRollPending || guardRolling) return;
            var feet = FeetAt(WorldMap.GuardSpot.x, WorldMap.GuardSpot.y);
            float s = GuardHeight / Mathf.Max(.01f, guardSR.sprite.bounds.size.y);     // sprite.bounds 는 크기 조절 전 값
            float squash = 1 + Mathf.Sin(Time.time * 2.2f) * .06f;
            // 가까이 오면 "!" 를 띄우고 부르르 떤다 (덤빌 준비)
            float shake = near ? Mathf.Sin(Time.time * 42) * .05f : 0;
            if (near && guardAlert == null)
            {
                guardAlert = new GameObject("GuardAlert").AddComponent<SpriteRenderer>();
                guardAlert.transform.SetParent(fieldRoot, false);
                guardAlert.sprite = alertSprite ??= Art.FromTexture(AlertTexture(), new Vector2(.5f, 0), 96);
                guardAlert.sortingOrder = 8;
                guardAlertBorn = Time.time;
                sfx.Play("select");
            }
            if (guardAlert != null)
            {
                float age = Time.time - guardAlertBorn;
                guardAlert.transform.localScale = Vector3.one * 1.2f * (age < .15f ? age / .15f * 1.3f : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((age - .15f) / .15f)));
                guardAlert.transform.localPosition = feet + new Vector3(0, GuardHeight + .05f, 0);
            }
            guardSR.transform.localScale = new Vector3(s * (2 - squash), s * squash, 1);
            guardSR.transform.localPosition = feet + new Vector3(shake, 0, 0);
            guardSR.transform.localRotation = Quaternion.identity;
            guardSR.color = Color.white;
            guardShadowSR.transform.localPosition = feet + new Vector3(0, .02f, 0);
        }

        public bool GuardAlerted => guardAlert != null;
        const int GuardAlertDist = 2;
        SpriteRenderer guardAlert;
        float guardAlertBorn;
        bool guardRollPending, guardRolling;

        /// <summary>쓰러진 지킴이가 데굴데굴 두 바퀴 굴러 남쪽으로 달아나며 사라진다 (1.3초, 흙먼지).</summary>
        IEnumerator GuardRollAway()
        {
            guardRollPending = false;
            if (guardSR == null) yield break;
            guardRolling = true;
            var from = FeetAt(WorldMap.GuardSpot.x, WorldMap.GuardSpot.y);
            float s = GuardHeight / Mathf.Max(.01f, guardSR.sprite.bounds.size.y);
            sfx.Play("mv_spin");
            const float dur = 1.3f;
            float dustT = 0;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                float k = t / dur, e = k * k;
                var pos = from + new Vector3(Mathf.Sin(k * 6) * .15f, -3.2f * e, -.03f * e);
                // 그림 기준점이 발밑이라, 몸 가운데를 축으로 돌도록 위치를 보정한다
                var rot = Quaternion.Euler(0, 0, -720 * e);
                var mid = new Vector3(0, GuardHeight * (1 - .25f * k) * .5f, 0);
                guardSR.transform.localPosition = pos + mid - rot * mid;
                guardSR.transform.localRotation = rot;
                guardSR.transform.localScale = Vector3.one * s * (1 - .25f * k);
                guardSR.color = new Color(1, 1, 1, Mathf.Clamp01((1 - k) / .4f));
                guardShadowSR.transform.localPosition = pos + new Vector3(0, .02f, 0);
                guardShadowSR.color = new Color(0, 0, 0, .45f * (1 - k));
                if ((dustT -= Time.deltaTime) <= 0)
                {
                    dustT = .08f;
                    SpawnField(puffSprite, pos + new Vector3(Random.Range(-.3f, .3f), .1f, 0), new Vector3(Random.Range(-.3f, .3f), Random.Range(.2f, .5f)),
                               .5f, Random.Range(.25f, .4f), new Color(.75f, .68f, .6f, .7f));
                }
                yield return null;
            }
            guardSR.enabled = guardShadowSR.enabled = false;
            guardSR.transform.localRotation = Quaternion.identity;
            guardSR.color = Color.white;
            guardShadowSR.color = new Color(0, 0, 0, .45f);
            guardRolling = false;
        }

        /// <summary>프로브용: 지킴이가 알맞은 크기로 보이나.</summary>
        public bool GuardVisible => guardSR != null && guardSR.enabled && guardSR.bounds.size.y > GuardHeight * .8f && guardSR.bounds.size.y < GuardHeight * 1.2f;

        /// <summary>보물상자 지킴이와 싸운다 (상자나 지킴이에게 부딪혔을 때).</summary>
        IEnumerator GuardRoutine()
        {
            busy = true;
            moveT = -1;
            yield return Talk("guard_meet");
            yield return BattleRoutine(GameData.Guard);
        }

        IEnumerator ChestRoutine(int x, int y)
        {
            if (busy) yield break;
            busy = true;
            var r = Quest.OpenChest(P, x, y, out var item);
            if (r == ChestResult.Guarded) { yield return GuardRoutine(); yield break; }
            if (r == ChestResult.Got)
            {
                sfx.Play("chest");
                RefreshQuestProps();
                var c = FeetAt(x, y) + new Vector3(0, .6f, 0);
                for (int i = 0; i < 10; i++)
                    SpawnField(sparkSprite, c, new Vector3(Random.Range(-1f, 1f), Random.Range(.8f, 2f)), .9f, Random.Range(.2f, .35f), UIKit.Gold, Random.Range(-120, 120));
                AutoSave();
                RefreshHud();
                yield return Talk("chest_open", ("item", GameData.ItemName(item)));
            }
            else yield return Talk(r == ChestResult.Empty ? "chest_empty" : "chest_sealed");
            HideMessage();
            busy = false;
        }

        IEnumerator DoorRoutine()
        {
            if (busy) yield break;
            busy = true;
            var r = Quest.TryBaseDoor(P);
            if (r == DoorResult.Opened)
            {
                sfx.Play("unlock");
                RefreshQuestProps();
                AutoSave();
                yield return Talk("door_open", ("item", GameData.ItemName(Quest.KeyItem)));
            }
            else yield return Talk(r == DoorResult.Locked ? "door_locked" : "door_sealed");
            HideMessage();
            busy = false;
        }

        /// <summary>타일 좌표 → 발밑 월드 좌표.</summary>
        /// <summary>
        /// 칸 (x, y) 의 발밑 월드 위치. z 는 발 높이에 비례 → 같은 sortingOrder 끼리는 화면 아래(앞)에 선 것이 위에 그려진다
        /// (주인공·몬스터·지킴이가 겹칠 때 앞뒤가 맞게).
        /// </summary>
        static Vector3 FeetAt(float x, float y)
        {
            float wy = -(y + 1) + .12f;
            return new Vector3(x + .5f, wy, wy * .01f);
        }

        void EnterField()
        {
            Current = Mode.Field;
            titlePanel.SetActive(false);
            endingPanel.SetActive(false);
            cmdWin.SetActive(false);
            enemyLabel.text = "";
            hudWin.SetActive(true);
            hintLabel.SetActive(true);
            ApplyMap();
            RefreshHud();
            moveT = -1;
            ResetBrother();
            UpdatePlayerVisual();
            RefreshQuestProps();
            mobs.Grace = 2f;                   // 전투 직후 바로 또 부딪히지 않게
            bgm.Play(MapSong);
            AutoSave();
        }

        /// <summary>지금 맵의 배경음악: 초원 "field", 동굴 "cave", 마을 "village", 사막 "desert".</summary>
        string MapSong => map.Kind == MapKind.Forest ? "forest" : map.Kind == MapKind.Library ? "library" : map.Kind == MapKind.Home ? "village" : map.Kind == MapKind.SnowTown ? "snowtown" : map.Kind == MapKind.Snow ? "snow" : map.Kind == MapKind.Lava ? "lava" : map.Kind == MapKind.Underworld ? "underworld" : map.IsCaveLike ? "cave" : map.IsTown ? "village" : map.Kind == MapKind.Desert ? "desert" : "field";

        /// <summary>
        /// 초원 'V' ↔ 마을 'E': 화면이 어두워졌다가 다른 맵에서 밝아진다.
        /// 마을로 들어가면 큰길 아래(위를 보고), 나오면 초원 입구 바로 아래(아래를 보고).
        /// </summary>
        IEnumerator TravelRoutine(bool toVillage)
        {
            busy = true;
            sfx.Play("step");
            yield return Fade(0, 1, .3f);
            P.InVillage = toVillage;
            bool desert = P.Chapter == 3, lava = P.Chapter == 4, snow = P.Chapter >= 5;   // 챕터 3: 사막 ↔ 오아시스 마을, 챕터 4: 용암 땅 ↔ 온천 마을 (문이 오른쪽이라 나오면 왼쪽을 본다)
            if (snow) (P.X, P.Y) = toVillage ? WorldMap.SnowTownStart : (WorldMap.SnowGate.x - 1, WorldMap.SnowGate.y);
            else if (lava) (P.X, P.Y) = toVillage ? WorldMap.LavaTownStart : (WorldMap.LavaGate.x - 1, WorldMap.LavaGate.y);
            else if (desert) (P.X, P.Y) = toVillage ? WorldMap.TownStart : (WorldMap.TownGate.x - 1, WorldMap.TownGate.y);
            else (P.X, P.Y) = toVillage ? WorldMap.VillageStart : (WorldMap.VillageGate.x, WorldMap.VillageGate.y + 1);
            facing = toVillage ? "up" : desert || lava || snow ? "left" : "down";
            moveT = -1;
            ApplyMap();
            ResetBrother();
            bgm.Play(MapSong);
            RefreshQuestProps();
            UpdatePlayerVisual();
            mobs.Grace = 2f;
            AutoSave();
            yield return Fade(1, 0, .3f);
            HideSwirl();
            if (toVillage) { yield return Talk(snow ? "snowtown_enter" : lava ? "lavatown_enter" : desert ? "oasis_enter" : "village_enter"); HideMessage(); }
            busy = false;
        }

        /// <summary>
        /// 집 드나들기 (P-5): 마을 집 문에 위로 부딪히면 그 집 안 (문 바로 위, 위를 보고), 집 안 문을 밟으면 그 집 문 앞 (아래를 보고).
        /// room = -1 이면 나가기.
        /// </summary>
        IEnumerator HomeRoutine(int room)
        {
            busy = true;
            sfx.Play("door");
            yield return Fade(0, 1, .25f);
            int from = P.Home;
            P.Home = room;
            if (room >= 0) { (P.X, P.Y) = WorldMap.HomeStart; facing = "up"; }
            else { (P.X, P.Y) = WorldMap.HomeDoorFront(Mathf.Max(0, from)); facing = "down"; }
            moveT = -1;
            ApplyMap();
            if (room < 0 && from >= 0 && from < homeMaps.Length) ReleaseMapSprite(homeMaps[from]);   // 나온 집 그림은 풀어 둔다 (다시 들어가면 또 그림, Q-16)
            ResetBrother();
            bgm.Play(MapSong);
            RefreshQuestProps();
            UpdatePlayerVisual();
            mobs.Grace = 1f;
            yield return Fade(1, 0, .25f);
            HideSwirl();
            if (room >= 0) { yield return Talk("home_enter"); HideMessage(); }
            busy = false;
        }

        /// <summary>집 안 보물상자 (P-5): 집마다 하나, 한 번만.</summary>
        IEnumerator HomeChestRoutine(int x, int y)
        {
            if (busy) yield break;
            busy = true;
            bool snow = map.Kind == MapKind.Snow, lib = map.Kind == MapKind.Library, forest = map.Kind == MapKind.Forest;
            var r = snow ? Quest.OpenSnowChest(P, out var what) : lib ? Quest.OpenLibraryChest(P, out what) : forest ? Quest.OpenForestChest(P, out what) : Quest.OpenHomeChest(P, map.Room, out what);
            if (r == ChestResult.Got)
            {
                sfx.Play("chest");
                RefreshQuestProps();
                var c = FeetAt(x, y) + new Vector3(0, .6f, 0);
                for (int i = 0; i < 10; i++)
                    SpawnField(sparkSprite, c, new Vector3(Random.Range(-1f, 1f), Random.Range(.8f, 2f)), .9f, Random.Range(.2f, .35f), UIKit.Gold, Random.Range(-120, 120));
                AutoSave();
                RefreshHud();
                yield return Talk(snow ? "snow_chest_open" : lib ? "lib_chest_open" : forest ? "for_chest_open" : "chest_open", ("item", what));
            }
            else yield return Talk("chest_empty");
            HideMessage();
            busy = false;
        }

        /// <summary>리본 편지를 줍고 바로 읽는다 (서진이 노트 3, Q-13).</summary>
        IEnumerator LetterRoutine()
        {
            if (busy) yield break;
            busy = true;
            if (Quest.TakeLetter(P))
            {
                sfx.Play("chest");
                RefreshQuestProps();
                AutoSave();
                yield return Talk("letter_find");
                yield return Talk("letter_read");
            }
            HideMessage();
            busy = false;
        }

        /// <summary>얼음 위 첫 미끄럼이면 한 번 알려 준다 (C5-3).</summary>
        IEnumerator IceHintRoutine()
        {
            busy = true;
            yield return Talk("ice_slide");
            HideMessage();
            busy = false;
        }

        /// <summary>대사 하나만 (침대·책장 같은 것에 부딪혔을 때).</summary>
        IEnumerator TalkOnce(string id)
        {
            if (busy) yield break;
            busy = true;
            yield return Talk(id);
            HideMessage();
            busy = false;
        }

        /// <summary>
        /// 지하세계 드나들기 (사용자 요청 2026-10-03): 숨겨진 구멍을 밟으면 어두워졌다가 지하세계 밧줄 아래로,
        /// 밧줄을 밟으면 위 맵(챕터의 들판·동굴·사막)의 숨겨진 구멍 옆으로 올라온다.
        /// </summary>
        IEnumerator UnderworldRoutine(bool down)
        {
            busy = true;
            sfx.Play(down ? "mv_hop" : "step");
            yield return Fade(0, 1, down ? .5f : .3f);
            P.Underground = down;
            if (down) { (P.X, P.Y) = WorldMap.UnderStart; facing = "down"; }
            else
            {
                var up = SurfaceMap;
                var (hx, hy) = up.SecretHole;
                (P.X, P.Y) = Quest.Walkable(up, P, hx, hy + 1) ? (hx, hy + 1) : (hx, hy);
                facing = "down";
            }
            moveT = -1;
            ApplyMap();
            ResetBrother();
            bgm.Play(MapSong);
            RefreshQuestProps();
            UpdatePlayerVisual();
            mobs.Grace = 2f;
            AutoSave();
            yield return Fade(1, 0, .4f);
            HideSwirl();
            yield return Talk(down ? "under_enter" : "under_leave");
            HideMessage();
            busy = false;
        }

        /// <summary>화면 전체를 검게 덮는다/걷는다 (전투 소용돌이 이미지를 가득 채워 씀).</summary>
        IEnumerator Fade(float from, float to, float dur)
        {
            swirlImg.enabled = true;
            swirlImg.fillAmount = 1;
            swirlImg.rectTransform.localRotation = Quaternion.identity;
            swirlImg.rectTransform.localScale = Vector3.one * 2.2f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                swirlImg.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / dur));
                yield return null;
            }
            swirlImg.color = new Color(0, 0, 0, to);
        }

        string HeldDir()
        {
            if (Keys.Held(KeyCode.UpArrow) || Keys.Held(KeyCode.W)) return "up";
            if (Keys.Held(KeyCode.DownArrow) || Keys.Held(KeyCode.S)) return "down";
            if (Keys.Held(KeyCode.LeftArrow) || Keys.Held(KeyCode.A)) return "left";
            if (Keys.Held(KeyCode.RightArrow) || Keys.Held(KeyCode.D)) return "right";
            return null;
        }

        void TickField()
        {
            TickQuestSparkles();
            if (moveT >= 0)
            {
                moveT += Time.deltaTime / StepTime;
                if (!sliding) walkT += Time.deltaTime;
                if (moveT >= 1)
                {
                    moveT = -1;
                    OnArrive();
                }
            }
            if (moveT < 0 && !busy && Keys.Down(KeyCode.Escape)) StartCoroutine(StatusRoutine());
            if (moveT < 0 && !busy)
            {
                var d = HeldDir();
                if (d != null) TryMove(d);
            }
            if (!busy && Current == Mode.Field)
            {
                mobs.Timid = P.Lv >= FieldMonsters.TimidLevel(map.Kind);     // 훨씬 강해지면 젤리들이 도망간다
                var hit = mobs.Tick(Time.deltaTime, P.X, P.Y);
                if (hit != null) StartMobBattle(hit);
            }
            SyncMobViews();
            TickGuard();
            TickNpcs();
            TickHoles();
            UpdatePlayerVisual();
            TickFieldFx();
            TickLighting();
        }

        void TryMove(string dir)
        {
            facing = dir;
            int dx = dir == "left" ? -1 : dir == "right" ? 1 : 0, dy = dir == "up" ? -1 : dir == "down" ? 1 : 0;
            int nx = P.X + dx, ny = P.Y + dy;
            var mob = mobs.At(nx, ny);
            if (mob != null && mobs.Grace <= 0) { StartMobBattle(mob); return; }
            if (Quest.GuardAt(map, P, nx, ny)) { if (!busy) StartCoroutine(GuardRoutine()); return; }
            if (!Quest.Walkable(map, P, nx, ny))
            {
                if (Keys.AnyDown) sfx.Play("bump");
                char c = map.At(nx, ny);
                var secret = busy ? null : Quests.TrySecret(P, map, nx, ny);      // 숨겨진 이벤트 (G-4): 표시 없는 물건을 살펴보면
                if (secret != null) { StartCoroutine(SecretRoutine(secret)); return; }
                if (Keys.AnyDown && BumpObject(nx, ny, c)) return;               // 나무 흔들기·물방울 (G-3)
                if (c == 'F') StartCoroutine(FountainRoutine());
                else if (c == 'S') StartCoroutine(SignRoutine());
                else if (c == 'H') StartCoroutine(map.Kind == MapKind.Home || map.Kind == MapKind.Snow || map.Kind == MapKind.Library || map.Kind == MapKind.Forest ? HomeChestRoutine(nx, ny) : ChestRoutine(nx, ny));
                else if (c == 'h' && map.IsTown && dir == "up" && WorldMap.HouseAtDoor(map.Kind, nx, ny) >= 0) StartCoroutine(HomeRoutine(WorldMap.HouseAtDoor(map.Kind, nx, ny)));
                else if (c == 'Q' && map.Kind == MapKind.Desert) StartCoroutine(LetterRoutine());
                else if (c == 'Z' && map.Kind == MapKind.Underworld) StartCoroutine(TalkOnce("dark_box"));
                else if (c == 'O' && map.Kind == MapKind.Library) StartCoroutine(MomJoinRoutine());
                else if (c == 'B' && map.Kind == MapKind.Library) StartCoroutine(TalkOnce("lib_boss_locked"));
                else if (c == 'k' && map.Kind == MapKind.Home) StartCoroutine(TalkOnce("home_bed"));
                else if (c == 'q' && map.Kind == MapKind.Home) StartCoroutine(TalkOnce("home_shelf"));
                else if (c == 'D') StartCoroutine(DoorRoutine());
                else if (c == 'o') StartCoroutine(HoleRoutine(nx, ny));
                else if (c == 'M') StartCoroutine(ShopRoutine());
                else if (c == 'N') { var npc = Npcs.At(map, nx, ny); if (npc != null) StartCoroutine(NpcRoutine(npc)); }
                return;
            }
            moveFrom = new Vector2Int(P.X, P.Y);
            // 지훈은 서진이 떠난 칸으로 (멀리 떨어져 있었으면 그 칸에서 바로 시작)
            ShiftFollowers(moveFrom);
            P.X = nx; P.Y = ny;
            moveT = 0;
            SpawnDust(moveFrom.x, moveFrom.y, dx, dy);
        }

        void UpdatePlayerVisual()
        {
            float k = moveT >= 0 ? moveT : 1;
            float rx = moveT >= 0 ? Mathf.Lerp(moveFrom.x, P.X, k) : P.X;
            float ry = moveT >= 0 ? Mathf.Lerp(moveFrom.y, P.Y, k) : P.Y;
            playerTilePos = new Vector2(rx, ry);
            bool moving = moveT >= 0 || (!busy && HeldDir() != null);
            var feet = FeetAt(rx, ry);
            float bob = 0, sway = 0, lean = 0;
            if (facing == "left" || facing == "right")
            {
                playerSR.sprite = sideFrames[moving ? (int)(walkT / .11f) % 4 : 0];
                playerSR.flipX = facing == "left";          // 원본 프레임은 오른쪽을 본다
                bob = moving ? Mathf.Abs(Mathf.Sin(walkT * 12)) * .08f : Mathf.Sin(Time.time * 2.5f) * .02f;
            }
            else
            {
                // 앞뒤 걷기: 옆걸음처럼 4박자 (한 발 · 모음 · 반대 발 · 모음), 멈추면 두 발 모은 자세.
                // 발을 든 쪽으로 몸이 살짝 기울고 흔들리며, 발을 디딜 때 톡 튀어 오른다.
                var fr = facing == "up" ? backFrames : frontFrames;
                int f = (int)(walkT / .11f) % 4;
                playerSR.sprite = moving ? fr[f == 3 ? 1 : f] : fr[1];
                playerSR.flipX = false;
                float ph = walkT * Mathf.PI / .22f;                 // 한 걸음(.22초)마다 반 바퀴
                bob = moving ? Mathf.Abs(Mathf.Cos(ph)) * .1f : Mathf.Sin(Time.time * 2.5f) * .02f;
                lean = moving ? Mathf.Sin(ph) * 4f : 0;
                sway = moving ? Mathf.Sin(ph) * .04f : 0;
            }
            playerSR.transform.localPosition = feet + new Vector3(sway, bob, 0);
            playerSR.transform.localRotation = Quaternion.Euler(0, 0, lean);
            shadowSR.transform.localPosition = feet + new Vector3(0, .02f, 0);
            UpdateFollowers(k, moving);

            // 카메라: 맵 밖이 보이지 않게 가둔다
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            float cx = Mathf.Clamp(rx + .5f, halfW, WorldMap.W - halfW);
            // 위쪽 끝에선 상태창 높이만큼 더 내려가게 (맵 맨 윗줄이 상태창에 가리지 않게 — 사용자 "상태창이 게임 화면을 가린다")
            float hudWorld = 2 * halfH * (8 + 118 * HudScale) / UIKit.RefH;
            float cy = Mathf.Clamp(-(ry + .5f), -WorldMap.H + halfH, -halfH + hudWorld);
            if (map.Kind == MapKind.Home)           // 집 안: 방 전체가 보이게 고정 (왼쪽 위 HUD 창 옆으로 비켜서)
            {
                cx = 11.8f + halfW - 2 * halfW * .28f;
                cy = -11.2f;
            }
            cam.transform.position = new Vector3(cx, cy, -10);
        }

        Vector2 playerTilePos;

        /// <summary>동료가 바뀌면(합류) 대열 그림을 맞춘다: 그림 크기는 party.json FieldScale (지훈 0.84, 아빠 1.15).</summary>
        void SyncFollowers()
        {
            for (int i = 0; i < P.Companions.Count; i++)
            {
                var c = P.Companions[i];
                if (i < followers.Count && followers[i].id == c.Id) continue;
                var f = followers.Find(x => x.id == c.Id);
                if (f == null)
                {
                    f = new Follower { id = c.Id };
                    f.shadow = new GameObject(c.Id + "Shadow").AddComponent<SpriteRenderer>();
                    f.shadow.transform.SetParent(fieldRoot, false);
                    f.shadow.sprite = shadowSR.sprite;
                    f.shadow.color = shadowSR.color;
                    f.shadow.transform.localScale = new Vector3(.8f * Mathf.Max(1, c.Def.FieldScale), .27f, 1);
                    f.shadow.sortingOrder = 1;
                    f.sr = new GameObject(c.Id).AddComponent<SpriteRenderer>();
                    f.sr.transform.SetParent(fieldRoot, false);
                    f.sr.sortingOrder = 2;
                    float h = FieldCharHeight * c.Def.FieldScale;
                    string a = c.Def.Art;
                    f.side = new[] { Art.Get(a + "/walk_side1", h), Art.Get(a + "/walk_side2", h), Art.Get(a + "/walk_side3", h), Art.Get(a + "/walk_side4", h) };
                    f.front = Art.Get(a + "/walk_front", h);
                    f.back = Art.Get(a + "/walk_back", h);
                    f.tile = f.from = new Vector2Int(P.X, P.Y);
                }
                else followers.Remove(f);
                followers.Insert(Mathf.Min(i, followers.Count), f);
            }
            while (followers.Count > P.Companions.Count)       // 새 게임 등으로 동료가 줄면 그림을 치운다
            {
                var f = followers[followers.Count - 1];
                Destroy(f.sr.gameObject); Destroy(f.shadow.gameObject);
                followers.RemoveAt(followers.Count - 1);
            }
        }

        /// <summary>서진이 한 칸 걸으면: 첫 동료는 서진이 떠난 칸으로, 다음 동료는 앞 동료가 떠난 칸으로 (멀리 떨어져 있었으면 그 칸에서 바로).</summary>
        void ShiftFollowers(Vector2Int leaderFrom)
        {
            SyncFollowers();
            var target = leaderFrom;
            foreach (var f in followers)
            {
                var old = f.tile;
                f.from = Manhattan(f.tile, target) <= 1 ? f.tile : target;
                f.tile = target;
                target = old;
            }
        }

        /// <summary>
        /// 맵에 들어오거나 순간 이동하면 동료들은 서진 등 뒤로 줄을 선다 (막혔으면 옆 칸, 다 막혔으면 앞사람 칸에 숨어 있다가 걸으면 나온다).
        /// </summary>
        void ResetBrother()
        {
            if (P == null) return;
            SyncFollowers();
            var lead = new Vector2Int(P.X, P.Y);
            var back = facing == "up" ? Vector2Int.up : facing == "down" ? Vector2Int.down : facing == "left" ? Vector2Int.right : Vector2Int.left;   // 타일 y 는 아래로 +
            var side = new Vector2Int(back.y, back.x);
            var taken = new HashSet<Vector2Int> { lead };
            foreach (var f in followers)
            {
                f.tile = f.from = lead;
                if (map != null)
                    foreach (var d in new[] { back, side, -side })
                    {
                        var t = lead + d;
                        if (!taken.Contains(t) && Quest.Walkable(map, P, t.x, t.y) && mobs.At(t.x, t.y) == null && !map.IsRope(t.x, t.y) && !map.IsDoorway(t.x, t.y))
                        { f.tile = f.from = t; break; }   // 밧줄·출입구 칸은 비워 둔다
                    }
                taken.Add(f.tile);
                f.facing = facing;
                lead = f.tile;
            }
        }

        static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>
        /// 필드의 동료들: 서진과 같은 걸음(moveT)으로 from → tile 을 걷는다.
        /// 순간 이동·맵 이동으로 앞사람과 두 칸 넘게 떨어지면 다시 줄을 세운다.
        /// </summary>
        void UpdateFollowers(float k, bool moving)
        {
            if (P == null) return;
            if (followers.Count != P.Companions.Count) { SyncFollowers(); ResetBrother(); }
            var lead = new Vector2Int(P.X, P.Y);
            bool apart = false;
            foreach (var f in followers) { if (moveT < 0 && Manhattan(f.tile, lead) > 1) apart = true; lead = f.tile; }
            if (apart) ResetBrother();
            var here = new Vector2Int(P.X, P.Y);
            for (int i = 0; i < followers.Count; i++)
            {
                var f = followers[i];
                bool on = fieldRoot.gameObject.activeSelf;
                f.sr.enabled = f.shadow.enabled = on;
                bool walking = moveT >= 0 && f.from != f.tile;
                if (walking)
                {
                    var d = f.tile - f.from;
                    f.facing = d.x > 0 ? "right" : d.x < 0 ? "left" : d.y < 0 ? "up" : "down";
                }
                else if (f.tile == here) f.facing = facing;
                float bx = walking ? Mathf.Lerp(f.from.x, f.tile.x, k) : f.tile.x;
                float by = walking ? Mathf.Lerp(f.from.y, f.tile.y, k) : f.tile.y;
                var feet = FeetAt(bx, by) + new Vector3(0, 0, .002f * (i + 1));    // 같은 줄이면 앞사람 뒤에
                float bob = 0, lean = 0;
                float wt = walkT + .07f * (i + 1);                                   // 발걸음이 살짝씩 어긋나게
                if (f.facing == "left" || f.facing == "right")
                {
                    f.sr.sprite = f.side[moving ? (int)(wt / .11f) % 4 : 0];
                    f.sr.flipX = f.facing == "left";
                    bob = moving ? Mathf.Abs(Mathf.Sin(wt * 12)) * .08f : Mathf.Sin(Time.time * 2.5f + 1 + i) * .02f;
                }
                else
                {
                    // 앞뒤 걷기 그림은 한 장뿐이라 통통 튀고 좌우로 기우뚱하며 걷는다
                    f.sr.sprite = f.facing == "up" ? f.back : f.front;
                    f.sr.flipX = false;
                    float ph = wt * Mathf.PI / .22f;
                    bob = moving ? Mathf.Abs(Mathf.Cos(ph)) * .1f : Mathf.Sin(Time.time * 2.5f + 1 + i) * .02f;
                    lean = moving ? Mathf.Sin(ph) * 6f : 0;
                }
                f.sr.transform.localPosition = feet + new Vector3(0, bob, 0);
                f.sr.transform.localRotation = Quaternion.Euler(0, 0, lean);
                f.shadow.transform.localPosition = feet + new Vector3(0, .02f, 0);
            }
        }

        // ---- 프로브용 (첫 동료 = 지훈)
        Follower BroFollower => followers.Find(f => f.id == Party.Brother);
        public bool BroFieldVisible => BroFollower != null && BroFollower.sr.enabled;
        public Vector2Int BroTile => BroFollower?.tile ?? new Vector2Int(P.X, P.Y);
        public string BroFacing => BroFollower?.facing ?? facing;
        public Vector2Int FollowerTile(string id) => followers.Find(f => f.id == id)?.tile ?? new Vector2Int(-1, -1);
        public bool FollowerVisible(string id) => followers.Find(f => f.id == id)?.sr.enabled ?? false;

        void OnArrive()
        {
            // 얼음 위 (C5-3): 앞이 열려 있으면 같은 방향으로 한 칸 더 미끄러진다
            if (map.Slippery(P.X, P.Y))
            {
                int dx = facing == "left" ? -1 : facing == "right" ? 1 : 0, dy = facing == "up" ? -1 : facing == "down" ? 1 : 0;
                if (Quest.Walkable(map, P, P.X + dx, P.Y + dy) && mobs.At(P.X + dx, P.Y + dy) == null)
                {
                    if (!sliding) sfx.Play("slide");
                    sliding = true;
                    TryMove(facing);
                    return;
                }
            }
            bool slid = sliding;
            sliding = false;
            if (slid && !P.Opened.Contains("ice_hint")) { P.Opened.Add("ice_hint"); StartCoroutine(IceHintRoutine()); }
            sfx.Play(map.Kind == MapKind.Snow || map.Kind == MapKind.SnowTown ? "step_snow" : "step");
            char here = map.At(P.X, P.Y);
            StepFx(here);
            var found = Quests.FindAt(P, map, P.X, P.Y);                       // 찾기 부탁 (G-4): 반짝이는 칸을 밟으면
            if (found != null) { StartCoroutine(QuestFoundRoutine(found)); return; }
            if (here == 'G' && !P.BossDown && map.Kind == MapKind.Field) { StartCoroutine(BossGateRoutine()); return; }
            if (here == 'B' && !P.Boss2Down && map.Kind == MapKind.Cave) { StartCoroutine(Boss2Routine()); return; }
            if (here == 'B' && !P.Boss3Down && map.Kind == MapKind.Desert) { StartCoroutine(Boss3Routine()); return; }
            if (here == 'B' && !P.Boss4Down && map.Kind == MapKind.Lava) { StartCoroutine(Boss4Routine()); return; }
            if (here == 'B' && !P.Boss5Down && map.Kind == MapKind.Snow) { StartCoroutine(Boss5Routine()); return; }
            if (here == 'B' && !P.Boss6Down && map.Kind == MapKind.Library) { StartCoroutine(Boss6Routine()); return; }
            if (here == 'B' && !P.Boss7Down && map.Kind == MapKind.Forest) { StartCoroutine(Boss7Routine()); return; }
            if (map.IsRope(P.X, P.Y)) { StartCoroutine(UnderworldRoutine(false)); return; }
            if ((P.X, P.Y) == map.SecretHole && Holes.SecretOpen(P, map)) { StartCoroutine(UnderworldRoutine(true)); return; }
            if (map.Kind == MapKind.Home && map.At(P.X, P.Y) == 'E') { StartCoroutine(HomeRoutine(-1)); return; }
            if (map.IsDoorway(P.X, P.Y)) { StartCoroutine(TravelRoutine(!map.IsTown)); return; }
            var mob = mobs.At(P.X, P.Y);
            if (mob != null && mobs.Grace <= 0) StartMobBattle(mob);
        }

        // ------------------------------------------------------------------ 보이는 몬스터
        EnemyDef MobDef(FieldMonster m) =>
            GameData.ListFor(mobs.Map.Kind)[m.EnemyIndex % GameData.ListFor(mobs.Map.Kind).Length];

        void StartMobBattle(FieldMonster m)
        {
            if (busy) return;
            var def = MobDef(m);
            mobs.Remove(m);
            SyncMobViews();
            moveT = -1;
            // 구역 무리 확률로 동료가 따라붙는다 (부딪힌 젤리가 맨 앞). 시작 근처는 늘 한 마리
            var defs = NoGroups ? new List<EnemyDef> { def } : GameData.RollEncounter(map.Kind, P.X, P.Y, rng);
            defs[0] = def;
            StartCoroutine(BattleRoutine(defs));
        }

        /// <summary>Sim 의 몬스터 목록과 화면의 젤리 스프라이트를 맞추고, 통통 튀며 걷게 한다.</summary>
        void SyncMobViews()
        {
            var alive = new HashSet<int>();
            foreach (var m in mobs.List)
            {
                alive.Add(m.Id);
                if (!mobViews.TryGetValue(m.Id, out var v))
                {
                    var def = MobDef(m);
                    var body = new GameObject("Mob").AddComponent<SpriteRenderer>();
                    body.transform.SetParent(fieldRoot, false);
                    body.sprite = JellySprite(def);
                    body.sortingOrder = 2;
                    float s = MobHeight / JellyTexUnits * def.Size;   // 코드 젤리·도감 그림 모두 같은 배율 (그림은 ArtUnits 로 크기를 맞춰 둠)
                    body.transform.localScale = Vector3.one * s;
                    var sh = new GameObject("MobShadow").AddComponent<SpriteRenderer>();
                    sh.transform.SetParent(fieldRoot, false);
                    sh.sprite = shadowSR.sprite;
                    sh.color = new Color(0, 0, 0, .4f);
                    sh.transform.localScale = new Vector3(.8f, .26f, 1) * def.Size;
                    sh.sortingOrder = 1;
                    sh.enabled = !UsesArt(def);     // 도감 그림은 그림자가 그려져 있음
                    v = (body, sh, s);
                    mobViews[m.Id] = v;
                    Spawn(v.body, true);
                }
                float k = m.MoveT;
                float x = Mathf.Lerp(m.FromX, m.X, k), y = Mathf.Lerp(m.FromY, m.Y, k);
                var feet = FeetAt(x, y);
                float hop = k < 1 ? Mathf.Sin(k * Mathf.PI) * .22f : 0;
                float squash = 1 + Mathf.Sin(Time.time * 4 + m.Id) * .05f;
                v.body.transform.localScale = new Vector3(v.scale * (2 - squash), v.scale * squash, 1);
                v.body.transform.localPosition = feet + new Vector3(0, hop, 0);
                v.body.flipX = m.X < m.FromX;
                v.shadow.transform.localPosition = feet + new Vector3(0, .02f, 0);
                v.body.color = mobs.Grace > 0 ? new Color(1, 1, 1, .55f + .45f * Mathf.Abs(Mathf.Sin(Time.time * 10))) : Color.white;

                // 가까이 오면 "!" — 톡 튀어나왔다가 살짝 흔들린다
                bool alert = Current == Mode.Field && mobs.Grace <= 0 && Mathf.Abs(m.X - P.X) + Mathf.Abs(m.Y - P.Y) <= AlertDist;
                mobAlerts.TryGetValue(m.Id, out var mark);
                if (alert && mark == null)
                {
                    mark = new GameObject("Alert").AddComponent<SpriteRenderer>();
                    mark.transform.SetParent(fieldRoot, false);
                    mark.sprite = alertSprite ??= Art.FromTexture(AlertTexture(), new Vector2(.5f, 0), 96);
                    mark.sortingOrder = 8;
                    mobAlerts[m.Id] = mark;
                    alertBorn[m.Id] = Time.time;
                    sfx.Play("select");
                }
                else if (!alert && mark != null) { Destroy(mark.gameObject); mobAlerts.Remove(m.Id); }
                if (mark != null)
                {
                    float age = Time.time - alertBorn[m.Id];
                    float pop = age < .15f ? age / .15f * 1.3f : Mathf.Lerp(1.3f, 1f, Mathf.Clamp01((age - .15f) / .15f));
                    mark.transform.localScale = Vector3.one * pop;
                    mark.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 9) * 6);
                    mark.transform.localPosition = feet + new Vector3(0, hop + MobHeight + .05f, 0);
                }
            }
            var dead = new List<int>();
            foreach (var kv in mobViews) if (!alive.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead)
            {
                if (mobAlerts.TryGetValue(id, out var mk)) { Destroy(mk.gameObject); mobAlerts.Remove(id); }
                Destroy(mobViews[id].body.gameObject);
                Destroy(mobViews[id].shadow.gameObject);
                mobViews.Remove(id);
            }
        }

        readonly Dictionary<int, float> alertBorn = new Dictionary<int, float>();

        /// <summary>프로브용: 주인공보다 아래 칸 몬스터는 카메라에 더 가깝고(앞), 위 칸 몬스터는 더 멀다(뒤).</summary>
        public bool DebugMobDepthOk()
        {
            float pz = playerSR.transform.position.z;
            bool any = false;
            foreach (var m in mobs.List)
            {
                if (!mobViews.TryGetValue(m.Id, out var v) || m.Y == P.Y) continue;
                any = true;
                float z = v.body.transform.position.z;
                if (m.Y > P.Y ? z >= pz : z <= pz) return false;
            }
            return any;
        }

        public int AlertCount => mobAlerts.Count;

        /// <summary>말풍선 속 빨간 "!".</summary>
        static Texture2D AlertTexture()
        {
            var R = new Raster(48, 56);
            R.Polygon(new[] { new Vector2(18, 40), new Vector2(24, 54), new Vector2(30, 40) }, Painter.Ink);
            R.Circle(24, 24, 21, Painter.Ink);
            R.Polygon(new[] { new Vector2(20, 38), new Vector2(24, 50), new Vector2(28, 38) }, new Color32(255, 255, 255, 255));
            R.Circle(24, 24, 18.5f, new Color32(255, 255, 255, 255));
            R.Polygon(new[] { new Vector2(20, 9), new Vector2(28, 9), new Vector2(26.5f, 30), new Vector2(21.5f, 30) }, new Color32(235, 60, 70, 255));
            R.Circle(24, 36, 3.6f, new Color32(235, 60, 70, 255));
            return R.ToTexture();
        }

        /// <summary>새로 생긴 몬스터: 땅에서 퐁 솟아나는 반짝임.</summary>
        void Spawn(SpriteRenderer body, bool sparkle)
        {
            if (!sparkle || sparkSprite == null) return;
            var at = body.transform.localPosition;
            for (int i = 0; i < 5; i++)
                SpawnField(sparkSprite, at + new Vector3(0, .3f, 0), new Vector3(Random.Range(-.6f, .6f), Random.Range(.4f, 1f)), .5f, .18f, Color.white);
        }

        // ------------------------------------------------------------------ 이벤트
        IEnumerator FountainRoutine()
        {
            if (busy) yield break;
            busy = true;
            P.RestoreAll();
            AutoSave();
            RefreshHud();
            sfx.Play("heal");
            yield return Talk("fountain", ("candy", PlayerState.MaxCandy));
            HideMessage();
            busy = false;
        }

        IEnumerator SignRoutine()
        {
            if (busy) yield break;
            busy = true;
            yield return Talk(map.Kind == MapKind.Forest ? "sign_forest" : map.Kind == MapKind.Library ? "sign_library" : map.Kind == MapKind.SnowTown ? "sign_snowtown" : map.Kind == MapKind.Snow ? "sign_snow" : map.Kind == MapKind.LavaTown ? "sign_lavatown" : map.Kind == MapKind.Lava ? "sign_lava" : map.Kind == MapKind.Underworld ? "sign_under" : map.Kind == MapKind.Village ? "sign_village" : map.Kind == MapKind.Oasis ? "sign_oasis" : map.Kind == MapKind.Desert ? "sign_ch3" : P.Chapter >= 2 ? "sign_ch2" : "sign");
            HideMessage();
            busy = false;
        }

        /// <summary>킹 젤리에게 도전하기 좋은 레벨 (서진이 시험 플레이: "4렙에는 힘들고 5렙은 할만했음", P-4).</summary>
        public const int KingReadyLevel = 5;

        IEnumerator BossGateRoutine()
        {
            busy = true;
            if (P.Lv < KingReadyLevel)                  // 아직 약하면 물어본다 (그래도 들어갈 수 있다)
            {
                yield return Talk("boss_gate_warn", ("lv", KingReadyLevel));
                ShowMessageWindow(true);
                choiceIndex = 1;
                yield return Choose("그래도 들어간다!", "조금 더 강해지고 올래");
                if (choicePicked != 0)
                {
                    (P.X, P.Y) = (moveFrom.x, moveFrom.y);   // 한 걸음 물러난다
                    facing = "up";
                    moveT = -1;
                    UpdatePlayerVisual();
                    yield return Talk("boss_gate_later");
                    HideMessage();
                    busy = false;
                    yield break;
                }
            }
            yield return Talk("boss_gate");
            yield return BattleRoutine(GameData.King);
        }

        IEnumerator Boss2Routine()
        {
            busy = true;
            yield return Talk("boss2_meet");
            yield return BattleRoutine(GameData.General);
        }

        /// <summary>장수풍뎅이 대장 (챕터 7 ✏️): 큰 고목 구멍 앞에서 뿔이 쑥 → 보스전.</summary>
        IEnumerator Boss7Routine()
        {
            busy = true;
            yield return Talk("boss7_meet");
            yield return BattleRoutine(GameData.ForestBoss);
        }

        /// <summary>악마 대장 (챕터 6 ✏️): 책 더미 옥좌 앞에서 저주의 책이 번쩍 → 보스전.</summary>
        IEnumerator Boss6Routine()
        {
            busy = true;
            yield return Talk("boss6_meet");
            yield return BattleRoutine(GameData.LibraryBoss);
        }

        /// <summary>
        /// 엄마 합류 (챕터 6 ⭐ "현자 엄마가 도서관을 되돌리는 중, 가족이 도우러 감"): 마법진의 엄마에게 다가가면
        /// 만나는 대사 → "현자 엄마 합류!" 컷인 → 넷이 함께. 이미 합류했으면 마법진은 그냥 빛나는 바닥 그림.
        /// </summary>
        IEnumerator MomJoinRoutine()
        {
            if (busy || Quest.MomJoined(P)) yield break;
            busy = true;
            yield return Talk("mom_meet");
            Quest.TryJoinMom(P);
            yield return CutinRoutine(Art.UI("Mom/hero"), "현자 엄마 합류!", "lvup");
            RefreshHud(); ResetBrother(); UpdatePlayerVisual();
            RefreshQuestProps();
            yield return Talk("mom_joined");
            HideMessage();
            AutoSave();
            busy = false;
        }

        /// <summary>예티 (C5-5): 눈 산 동굴 입구에서 눈보라 → 예티가 걸어 나온다.</summary>
        IEnumerator Boss5Routine()
        {
            busy = true;
            yield return Talk("boss5_meet");
            yield return BattleRoutine(GameData.SnowBoss);
        }

        IEnumerator Boss4Routine()
        {
            busy = true;
            yield return Talk("boss4_meet");
            yield return BattleRoutine(GameData.LavaBoss);
        }

        IEnumerator Boss3Routine()
        {
            busy = true;
            yield return Talk("boss3_meet");
            yield return BattleRoutine(GameData.DesertBoss);
        }
    }
}
