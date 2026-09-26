using System.Collections;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>필드: 타일맵(런타임 텍스처), 격자 이동, 인카운트, 샘물/표지판/성문 이벤트.</summary>
    public partial class GameRoot
    {
        const float StepTime = .17f, FieldCharHeight = 1.75f;

        Transform fieldRoot;
        SpriteRenderer playerSR, shadowSR;
        Sprite[] sideFrames;
        Sprite frontSprite, backSprite;
        string facing = "down";
        Vector2Int moveFrom;
        float moveT = -1, walkT;
        int stepsSinceBattle;

        void BuildField()
        {
            fieldRoot = new GameObject("Field").transform;
            var mapSR = new GameObject("Map").AddComponent<SpriteRenderer>();
            mapSR.transform.SetParent(fieldRoot, false);
            mapSR.sprite = Art.FromTexture(Painter.Field(map), new Vector2(0, 1), Painter.Tile);   // 1 타일 = 1 유닛
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
            frontSprite = Art.Get("walk_front", FieldCharHeight);
            backSprite = Art.Get("walk_back", FieldCharHeight);
            playerSR.sprite = frontSprite;
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
            RefreshHud();
            moveT = -1;
            UpdatePlayerVisual();
            bgm.Play("field");
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
            if (moveT < 0 && !busy)
            {
                var d = HeldDir();
                if (d != null) TryMove(d);
            }
            UpdatePlayerVisual();
        }

        void TryMove(string dir)
        {
            facing = dir;
            int dx = dir == "left" ? -1 : dir == "right" ? 1 : 0, dy = dir == "up" ? -1 : dir == "down" ? 1 : 0;
            int nx = P.X + dx, ny = P.Y + dy;
            if (map.Blocked(nx, ny))
            {
                if (Keys.AnyDown) sfx.Play("bump");
                char c = map.At(nx, ny);
                if (c == 'F') StartCoroutine(FountainRoutine());
                else if (c == 'S') StartCoroutine(SignRoutine());
                return;
            }
            moveFrom = new Vector2Int(P.X, P.Y);
            P.X = nx; P.Y = ny;
            moveT = 0;
        }

        void UpdatePlayerVisual()
        {
            float k = moveT >= 0 ? moveT : 1;
            float rx = moveT >= 0 ? Mathf.Lerp(moveFrom.x, P.X, k) : P.X;
            float ry = moveT >= 0 ? Mathf.Lerp(moveFrom.y, P.Y, k) : P.Y;
            bool moving = moveT >= 0 || (!busy && HeldDir() != null);
            var feet = FeetAt(rx, ry);
            float bob = 0;
            if (facing == "left" || facing == "right")
            {
                playerSR.sprite = sideFrames[moving ? (int)(walkT / .11f) % 4 : 0];
                playerSR.flipX = facing == "left";          // 원본 프레임은 오른쪽을 본다
                bob = moving ? Mathf.Abs(Mathf.Sin(walkT * 12)) * .08f : Mathf.Sin(Time.time * 2.5f) * .02f;
            }
            else
            {
                playerSR.sprite = facing == "up" ? backSprite : frontSprite;
                playerSR.flipX = false;
                bob = moving ? Mathf.Abs(Mathf.Sin(walkT * 12)) * .1f : Mathf.Sin(Time.time * 2.5f) * .02f;
            }
            playerSR.transform.localPosition = feet + new Vector3(0, bob, 0);
            shadowSR.transform.localPosition = feet + new Vector3(0, .02f, 0);

            // 카메라: 맵 밖이 보이지 않게 가둔다
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            float cx = Mathf.Clamp(rx + .5f, halfW, WorldMap.W - halfW);
            float cy = Mathf.Clamp(-(ry + .5f), -WorldMap.H + halfH, -halfH);
            cam.transform.position = new Vector3(cx, cy, -10);
        }

        void OnArrive()
        {
            sfx.Play("step");
            stepsSinceBattle++;
            if (map.At(P.X, P.Y) == 'G' && !P.BossDown)
            {
                StartCoroutine(BossGateRoutine());
                return;
            }
            if (stepsSinceBattle > 4 && rng.NextDouble() < map.EncounterRate(P.X, P.Y))
            {
                stepsSinceBattle = 0;
                StartCoroutine(BattleRoutine(GameData.Normal[WorldMap.PickEnemyIndex(P.X, P.Y, rng)]));
            }
        }

        IEnumerator FountainRoutine()
        {
            if (busy) yield break;
            busy = true;
            P.RestoreAll();
            RefreshHud();
            sfx.Play("heal");
            yield return Talk("fountain");
            HideMessage();
            busy = false;
        }

        IEnumerator SignRoutine()
        {
            if (busy) yield break;
            busy = true;
            yield return Talk("sign");
            HideMessage();
            busy = false;
        }

        IEnumerator BossGateRoutine()
        {
            busy = true;
            yield return Talk("boss_gate");
            yield return BattleRoutine(GameData.King);
        }
    }
}
