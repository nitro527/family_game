using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>필드를 돌아다니는 몬스터 한 마리 (심볼 인카운트).</summary>
    public class FieldMonster
    {
        public int Id;
        public int X, Y, FromX, FromY;
        public int EnemyIndex;          // 초원: GameData.Normal, 동굴: GameData.Cave 인덱스
        public float MoveT = 1;         // 0→1 한 칸 이동 진행도 (1 이면 멈춰 있음)
        public float Wait;              // 다음 걸음까지 남은 시간
    }

    /// <summary>
    /// 심볼 인카운트 규칙: 몬스터가 필드에 보이고 천천히 돌아다닌다.
    /// 시간이 지나면 한 마리씩 생기고(최대 Max), 주인공과 같은 칸이 되면 전투.
    /// 안전지대(마을·동굴 입구·길)에는 들어오지 않는다. UnityEngine 의존 없음.
    /// </summary>
    public class FieldMonsters
    {
        public const int Max = 6, StartCount = 3;
        public const float SpawnEvery = 7f, StepTime = .5f, MinSpawnDist = 6;

        public readonly List<FieldMonster> List = new List<FieldMonster>();
        public WorldMap Map { get; private set; }
        public float Grace;             // 전투 직후 잠깐은 부딪혀도 전투가 안 난다
        public bool Paused;             // 플레이테스트용: 생성·이동 멈춤
        /// <summary>주인공이 이 맵에 비해 훨씬 강하면 true: 몬스터가 다가오지 않고 도망간다 (DQ 심볼). 부딪히러 가면 싸울 수는 있다.</summary>
        public bool Timid;

        /// <summary>이 레벨 이상이면 그 맵 몬스터가 겁먹고 도망간다 (초원: 킹 젤리를 깬 뒤 조금 더, 동굴: 장군을 깬 뒤 조금 더).</summary>
        public static int TimidLevel(MapKind kind) => kind == MapKind.Cave ? 11 : kind == MapKind.Desert ? 16 : kind == MapKind.Underworld ? 99 : kind == MapKind.Lava ? 20 : 7;   // 지하세계 젤리는 겁이 없다
        readonly Random r;
        float spawnTimer;
        int nextId;

        public FieldMonsters(Random rng) { r = rng; }

        /// <summary>맵이 바뀌면 싹 비우고 몇 마리를 미리 뿌린다.</summary>
        public void Reset(WorldMap m, int px, int py, int initial = StartCount)
        {
            Map = m;
            List.Clear();
            spawnTimer = SpawnEvery;
            for (int i = 0; i < initial; i++) TrySpawn(px, py);
        }

        /// <summary>몬스터가 살 수 있는 칸: 인카운트 지형이고 안전지대가 아님.</summary>
        public bool Habitat(int x, int y) => Map.EncounterRate(x, y) > 0 && !Map.Safe(x, y)
                                             && !(Map.Kind == MapKind.Cave && (x, y) == WorldMap.GuardSpot);   // 지킴이 자리는 비워 둔다

        public FieldMonster At(int x, int y)
        {
            foreach (var m in List) if (m.X == x && m.Y == y) return m;
            return null;
        }

        public FieldMonster TrySpawn(int px, int py)
        {
            if (List.Count >= Max) return null;
            for (int tries = 0; tries < 60; tries++)
            {
                int x = r.Next(WorldMap.W), y = r.Next(WorldMap.H);
                if (!Habitat(x, y) || At(x, y) != null) continue;
                if (Math.Abs(x - px) + Math.Abs(y - py) < MinSpawnDist) continue;
                var m = new FieldMonster
                {
                    Id = nextId++, X = x, Y = y, FromX = x, FromY = y, Wait = 1 + (float)r.NextDouble() * 2,
                    EnemyIndex = WorldMap.PickIndex(Map.Kind, x, y, r),
                };
                List.Add(m);
                return m;
            }
            return null;
        }

        public FieldMonster Spawn(int x, int y, int enemyIndex)
        {
            var m = new FieldMonster { Id = nextId++, X = x, Y = y, FromX = x, FromY = y, EnemyIndex = enemyIndex, Wait = 99 };
            List.Add(m);
            return m;
        }

        public void Remove(FieldMonster m) => List.Remove(m);

        /// <summary>
        /// 시간을 dt 만큼 흘린다. 주인공(px,py)과 부딪힌 몬스터가 있으면 돌려준다.
        /// 가까우면(4칸) 가끔 주인공 쪽으로 다가온다 (Timid 면 반대로 멀어진다).
        /// </summary>
        public FieldMonster Tick(float dt, int px, int py)
        {
            if (Map == null) return null;
            if (Grace > 0) Grace -= dt;
            if (Paused) return null;
            spawnTimer -= dt;
            if (spawnTimer <= 0)
            {
                spawnTimer = SpawnEvery;
                TrySpawn(px, py);
            }
            foreach (var m in List)
            {
                if (m.MoveT < 1) { m.MoveT = Math.Min(1, m.MoveT + dt / StepTime); continue; }
                m.Wait -= dt;
                if (m.Wait > 0) continue;
                m.Wait = 1.2f + (float)r.NextDouble() * 1.6f;
                int dist = Math.Abs(m.X - px) + Math.Abs(m.Y - py);
                int dx = 0, dy = 0;
                if (dist <= 4 && (Timid || Grace <= 0) && r.NextDouble() < (Timid ? .8 : .45))
                {
                    int sx = Math.Sign(px - m.X), sy = Math.Sign(py - m.Y);
                    if (Timid) { sx = -sx; sy = -sy; if (sx == 0 && sy == 0) sx = 1; }
                    if (Math.Abs(px - m.X) > Math.Abs(py - m.Y)) dx = sx; else dy = sy;
                    if (dx == 0 && dy == 0) dx = sx != 0 ? sx : 1;
                }
                else
                {
                    int d = r.Next(5);                  // 0~3 방향, 4 는 제자리
                    if (d == 4) continue;
                    dx = d == 0 ? 1 : d == 1 ? -1 : 0;
                    dy = d == 2 ? 1 : d == 3 ? -1 : 0;
                }
                int nx = m.X + dx, ny = m.Y + dy;
                bool toPlayer = nx == px && ny == py;
                if (toPlayer && Timid) continue;            // 겁먹은 몬스터는 스스로 부딪히지 않는다
                if (!Habitat(nx, ny) || (!toPlayer && At(nx, ny) != null)) continue;   // 안전지대의 주인공은 못 건드린다
                if (toPlayer && Grace > 0) continue;
                m.FromX = m.X; m.FromY = m.Y;
                m.X = nx; m.Y = ny;
                m.MoveT = 0;
            }
            if (Grace > 0) return null;
            return At(px, py);
        }
    }
}
