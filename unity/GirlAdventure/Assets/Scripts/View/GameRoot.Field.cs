using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 필드: 맵(챕터 1 초원 / 챕터 2 동굴), 격자 이동, 보이는 몬스터(심볼 인카운트),
    /// 샘물/표지판/사탕 가게/보물상자/기지 문/보스 이벤트.
    /// </summary>
    public partial class GameRoot
    {
        const float StepTime = .17f, FieldCharHeight = 1.75f, MobHeight = 1.15f;

        Transform fieldRoot;
        SpriteRenderer mapSR, playerSR, shadowSR, chestOpenSR, doorOpenSR, guardSR, guardShadowSR;
        const float GuardHeight = 1.6f;         // 보물상자 지킴이는 보통 젤리보다 크게
        Sprite[] sideFrames;
        Sprite[] frontFrames, backFrames;       // 앞/뒤 걷기: 한 발 → 두 발 모음 → 반대 발 → 두 발 모음 (tools/make_walk_frames.py)
        string facing = "down";
        Vector2Int moveFrom;
        float moveT = -1, walkT;
        WorldMap fieldMap, caveMap;
        readonly Dictionary<WorldMap, Sprite> mapSprites = new Dictionary<WorldMap, Sprite>();
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
            mobs = new FieldMonsters(rng);
            // 맵 그림은 무거워서(1초+) 타이틀을 보는 동안 백그라운드에서 미리 그린다
            foreach (var m in new[] { fieldMap, caveMap })
            {
                var mm = m;
                mapJobs[mm] = System.Threading.Tasks.Task.Run(() => MapPainter.RenderRaster(mm));
            }

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
                var raster = mapJobs.TryGetValue(m, out var job) ? job.Result : MapPainter.RenderRaster(m);   // 아직이면 기다린다
                s = Art.FromTexture(raster.ToTexture(), new Vector2(0, 1), Painter.Tile);   // 1 타일 = 1 유닛
                mapSprites[m] = s;
                mapJobs.Remove(m);
                Debug.Log($"[Map] {m.Kind} 준비 대기 {(Time.realtimeSinceStartup - t0) * 1000:F0}ms");
            }
            return s;
        }

        /// <summary>챕터에 맞는 맵으로 바꾼다 (이미 그 맵이면 아무것도 안 함).</summary>
        void ApplyMap()
        {
            var want = P.Chapter >= 2 ? caveMap : fieldMap;
            if (map == want && mobs.Map == want) return;
            map = want;
            mapSR.sprite = MapSprite(map);
            RebuildMapFx();
            RebuildLights();
            mobs.Reset(map, P.X, P.Y);
            SyncMobViews();
        }

        /// <summary>연 상자·열린 문·보물상자 지킴이 그림.</summary>
        void RefreshQuestProps()
        {
            bool cave = map.Kind == MapKind.Cave;
            chestOpenSR.enabled = cave && P.Opened.Contains($"{WorldMap.Chest.x},{WorldMap.Chest.y}");
            doorOpenSR.enabled = cave && Quest.DoorOpen(P);
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
            if (guardSR != null) { guardSR.enabled = guardShadowSR.enabled = guard; TickGuard(); }
        }

        /// <summary>지킴이: 제자리에서 느릿느릿 숨 쉬듯 출렁인다.</summary>
        void TickGuard()
        {
            if (guardSR == null || !guardSR.enabled) return;
            var feet = FeetAt(WorldMap.GuardSpot.x, WorldMap.GuardSpot.y);
            float s = GuardHeight / Mathf.Max(.01f, guardSR.sprite.bounds.size.y);     // sprite.bounds 는 크기 조절 전 값
            float squash = 1 + Mathf.Sin(Time.time * 2.2f) * .06f;
            guardSR.transform.localScale = new Vector3(s * (2 - squash), s * squash, 1);
            guardSR.transform.localPosition = feet;
            guardShadowSR.transform.localPosition = feet + new Vector3(0, .02f, 0);
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
        static Vector3 FeetAt(float x, float y) => new Vector3(x + .5f, -(y + 1) + .12f, 0);

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
            UpdatePlayerVisual();
            RefreshQuestProps();
            mobs.Grace = 2f;                   // 전투 직후 바로 또 부딪히지 않게
            bgm.Play(map.Kind == MapKind.Cave ? "cave" : "field");
            AutoSave();
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
            if (moveT >= 0)
            {
                moveT += Time.deltaTime / StepTime;
                walkT += Time.deltaTime;
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
                if (c == 'F') StartCoroutine(FountainRoutine());
                else if (c == 'S') StartCoroutine(SignRoutine());
                else if (c == 'H') StartCoroutine(ChestRoutine(nx, ny));
                else if (c == 'D') StartCoroutine(DoorRoutine());
                else if (c == 'M') StartCoroutine(ShopRoutine());
                return;
            }
            moveFrom = new Vector2Int(P.X, P.Y);
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

            // 카메라: 맵 밖이 보이지 않게 가둔다
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            float cx = Mathf.Clamp(rx + .5f, halfW, WorldMap.W - halfW);
            float cy = Mathf.Clamp(-(ry + .5f), -WorldMap.H + halfH, -halfH);
            cam.transform.position = new Vector3(cx, cy, -10);
        }

        Vector2 playerTilePos;

        void OnArrive()
        {
            sfx.Play("step");
            char here = map.At(P.X, P.Y);
            if (here == 'G' && !P.BossDown && map.Kind == MapKind.Field) { StartCoroutine(BossGateRoutine()); return; }
            if (here == 'B' && !P.Boss2Down && map.Kind == MapKind.Cave) { StartCoroutine(Boss2Routine()); return; }
            var mob = mobs.At(P.X, P.Y);
            if (mob != null && mobs.Grace <= 0) StartMobBattle(mob);
        }

        // ------------------------------------------------------------------ 보이는 몬스터
        EnemyDef MobDef(FieldMonster m) =>
            mobs.Map.Kind == MapKind.Cave ? GameData.Cave[m.EnemyIndex % GameData.Cave.Length] : GameData.Normal[m.EnemyIndex % GameData.Normal.Length];

        void StartMobBattle(FieldMonster m)
        {
            if (busy) return;
            var def = MobDef(m);
            mobs.Remove(m);
            SyncMobViews();
            moveT = -1;
            StartCoroutine(BattleRoutine(def));
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
                    float s = MobHeight / Mathf.Max(.01f, body.sprite.bounds.size.y);
                    body.transform.localScale = Vector3.one * s;
                    var sh = new GameObject("MobShadow").AddComponent<SpriteRenderer>();
                    sh.transform.SetParent(fieldRoot, false);
                    sh.sprite = shadowSR.sprite;
                    sh.color = new Color(0, 0, 0, .4f);
                    sh.transform.localScale = new Vector3(.8f, .26f, 1);
                    sh.sortingOrder = 1;
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
            yield return Talk(P.Chapter >= 2 ? "sign_ch2" : "sign");
            HideMessage();
            busy = false;
        }

        IEnumerator BossGateRoutine()
        {
            busy = true;
            yield return Talk("boss_gate");
            yield return BattleRoutine(GameData.King);
        }

        IEnumerator Boss2Routine()
        {
            busy = true;
            yield return Talk("boss2_meet");
            yield return BattleRoutine(GameData.General);
        }
    }
}
