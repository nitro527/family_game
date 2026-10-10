using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 맵마다 떠다니는 것 한 줄 (2026-10-10 코드 정리 C-2): 얼마나 자주(Every), 어디서(화면 기준 범위 — a·반너비 + b),
    /// 어디로(속도 범위), 얼마나 오래·크게, 무슨 그림·색(둘 중 하나, ChanceA 확률로 첫째), 빙글, 그리는 순서.
    /// Room 이면 화면이 아니라 맵 좌표 그대로 (집 안 햇살 먼지).
    /// </summary>
    public class AmbientDef
    {
        public float Every = .45f;
        public float X0a = -1, X0b, X1a = 1, X1b, Y0a = -1, Y0b, Y1a = 1, Y1b;
        public bool Room;
        public float VxMin, VxMax, VyMin, VyMax;
        public float LifeMin = 3, LifeMax = 3, ScaleMin = .1f, ScaleMax = .1f, SpinMin, SpinMax;
        public string Sprite = "puff";                 // puff · petal · spark
        public Color ColorA = Color.white, ColorB = Color.white;
        public float ChanceA = .5f;
        public int Order = 7;
        public int Butterflies;                         // 이 맵에 맴도는 나비 수
    }

    /// <summary>
    /// 필드 분위기 (2026-10-10 코드 정리 C-2 — GameRoot 안에 맵마다 if 로 있던 것을 표 + 부품으로):
    /// 맵 종류마다 떠다니는 것(꽃잎·빛 먼지·눈·불티·낙엽·모래바람·마법 먼지)과 나비. 그 맵 챕터의 보스를 이기면 Calm 표를 쓴다
    /// (눈보라 → 사뿐사뿐, 시든 잎 → 초록 잎, 보랏빛 먼지 → 금빛 반짝이).
    /// </summary>
    public class FieldAmbience
    {
        static readonly AmbientDef Petals = new AmbientDef
        {
            Every = .45f, X0a = -1, X0b = -.5f, X1a = .5f, X1b = -.5f, Y0a = 1, Y0b = .5f, Y1a = 1, Y1b = .5f,
            VxMin = .6f, VxMax = 1.1f, VyMin = -.9f, VyMax = -.5f, LifeMin = 9, LifeMax = 9, ScaleMin = .12f, ScaleMax = .18f, SpinMin = -160, SpinMax = 160,
            Sprite = "petal", ColorA = new Color(1f, .8f, .9f, .9f), ColorB = new Color(1f, 1f, 1f, .85f), Order = 4, Butterflies = 7,
        };
        static readonly AmbientDef CaveMotes = new AmbientDef
        {
            Every = .12f, VxMin = -.15f, VxMax = .15f, VyMin = .05f, VyMax = .3f, LifeMin = 2.5f, LifeMax = 4.5f, ScaleMin = .08f, ScaleMax = .16f,
            ColorA = new Color(.5f, .95f, 1f, .9f), ColorB = new Color(1f, .8f, 1f, .9f), ChanceA = .6f,
        };
        static readonly AmbientDef Blizzard = new AmbientDef
        {
            Every = .035f, X0a = -1, X0b = -4, Y0a = 1, Y0b = .3f, Y1a = 1, Y1b = .3f,
            VxMin = 1.2f, VxMax = 2.2f, VyMin = -1.6f, VyMax = -1f, LifeMin = 9, LifeMax = 12, ScaleMin = .05f, ScaleMax = .1f,
            ColorA = new Color(1f, 1f, 1f, .92f), ColorB = new Color(1f, 1f, 1f, .92f),
        };
        static readonly AmbientDef Snowfall = new AmbientDef
        {
            Every = .09f, X0a = -1, X0b = -1, Y0a = 1, Y0b = .3f, Y1a = 1, Y1b = .3f,
            VxMin = .05f, VxMax = .3f, VyMin = -.9f, VyMax = -.5f, LifeMin = 9, LifeMax = 12, ScaleMin = .05f, ScaleMax = .1f,
            ColorA = new Color(1f, 1f, 1f, .92f), ColorB = new Color(1f, 1f, 1f, .92f),
        };
        static readonly AmbientDef HomeDust = new AmbientDef
        {
            Every = .5f, Room = true, X0b = 13.5f, X1b = 21.5f, Y0b = -14f, Y1b = -8.5f, X0a = 0, X1a = 0, Y0a = 0, Y1a = 0,
            VxMin = -.08f, VxMax = .08f, VyMin = .02f, VyMax = .12f, LifeMin = 2.5f, LifeMax = 4, ScaleMin = .04f, ScaleMax = .07f,
            ColorA = new Color(1f, .95f, .75f, .7f), ColorB = new Color(1f, .95f, .75f, .7f),
        };
        static readonly AmbientDef Embers = new AmbientDef
        {
            Every = .08f, Y0a = -1, Y0b = -.2f, Y1a = -1, Y1b = -.2f,
            VxMin = -.3f, VxMax = .3f, VyMin = 1.2f, VyMax = 2.4f, LifeMin = 3, LifeMax = 5, ScaleMin = .05f, ScaleMax = .1f,
            ColorA = new Color(1f, .55f, .15f, .9f), ColorB = new Color(1f, .85f, .3f, .9f), ChanceA = .6f,
        };
        static readonly AmbientDef DeadLeaves = new AmbientDef
        {
            Every = .3f, Y0a = 1, Y0b = .5f, Y1a = 1, Y1b = .5f,
            VxMin = -.4f, VxMax = .6f, VyMin = -1f, VyMax = -.6f, LifeMin = 9, LifeMax = 9, ScaleMin = .16f, ScaleMax = .24f, SpinMin = -200, SpinMax = 200,
            Sprite = "petal", ColorA = new Color(.75f, .55f, .25f, .95f), ColorB = new Color(.95f, .8f, .3f, .95f),
        };
        static readonly AmbientDef GreenLeaves = new AmbientDef
        {
            Every = .3f, Y0a = 1, Y0b = .5f, Y1a = 1, Y1b = .5f,
            VxMin = -.4f, VxMax = .6f, VyMin = -1f, VyMax = -.6f, LifeMin = 9, LifeMax = 9, ScaleMin = .16f, ScaleMax = .24f, SpinMin = -200, SpinMax = 200,
            Sprite = "petal", ColorA = new Color(.45f, .8f, .35f, .95f), ColorB = new Color(.65f, .9f, .4f, .95f), Butterflies = 7,
        };
        static readonly AmbientDef MagicDust = new AmbientDef
        {
            Every = .15f, VxMin = -.12f, VxMax = .12f, VyMin = .1f, VyMax = .35f, LifeMin = 2.5f, LifeMax = 4, ScaleMin = .08f, ScaleMax = .14f,
            ColorA = new Color(.7f, .4f, 1f, .85f), ColorB = new Color(1f, .5f, .9f, .8f),
        };
        static readonly AmbientDef GoldGlitter = new AmbientDef
        {
            Every = .15f, VxMin = -.12f, VxMax = .12f, VyMin = .1f, VyMax = .35f, LifeMin = 2.5f, LifeMax = 4, ScaleMin = .1f, ScaleMax = .16f, SpinMin = -90, SpinMax = 90,
            Sprite = "spark", ColorA = new Color(1f, .9f, .5f, .9f), ColorB = new Color(1f, .9f, .5f, .9f),
        };
        static readonly AmbientDef SandWind = new AmbientDef
        {
            Every = .1f, X0a = -1, X0b = -.3f, X1a = -1, X1b = -.3f,
            VxMin = 2.2f, VxMax = 3.6f, VyMin = -.2f, VyMax = .15f, LifeMin = 4, LifeMax = 6, ScaleMin = .05f, ScaleMax = .11f,
            ColorA = new Color(1f, .9f, .68f, .55f), ColorB = new Color(1f, .9f, .68f, .55f),
        };

        /// <summary>맵 종류 → (보스 전, 보스를 이긴 뒤). 없는 맵은 초원 꽃잎.</summary>
        static readonly Dictionary<MapKind, (AmbientDef before, AmbientDef after)> table = new Dictionary<MapKind, (AmbientDef, AmbientDef)>
        {
            { MapKind.Cave, (CaveMotes, CaveMotes) }, { MapKind.Underworld, (CaveMotes, CaveMotes) },
            { MapKind.Snow, (Blizzard, Snowfall) }, { MapKind.SnowTown, (Blizzard, Snowfall) },
            { MapKind.Home, (HomeDust, HomeDust) },
            { MapKind.Lava, (Embers, Embers) }, { MapKind.LavaTown, (Embers, Embers) },
            { MapKind.Forest, (DeadLeaves, GreenLeaves) },
            { MapKind.Library, (MagicDust, GoldGlitter) },
            { MapKind.Desert, (SandWind, SandWind) },
            { MapKind.Oasis, (WithButterflies(SandWind, 3), WithButterflies(SandWind, 3)) },
        };

        static AmbientDef WithButterflies(AmbientDef d, int n)
        {
            var c = (AmbientDef)d.MemberwiseCloneDef();
            c.Butterflies = n;
            return c;
        }

        /// <summary>이 맵에서 지금 쓰는 표 (cleared = 그 맵 챕터의 보스를 이겼나).</summary>
        public static AmbientDef For(MapKind k, bool cleared) => table.TryGetValue(k, out var d) ? (cleared ? d.after : d.before) : Petals;

        readonly ParticleLayer layer;
        readonly Dictionary<string, Sprite> sprites;
        float timer;

        public FieldAmbience(ParticleLayer layer, Dictionary<string, Sprite> sprites)
        {
            this.layer = layer;
            this.sprites = sprites;
        }

        /// <summary>시간이 되면 하나 띄운다 (화면 기준이면 카메라 위치·크기로).</summary>
        public void Tick(float dt, AmbientDef d, Camera cam)
        {
            timer -= dt;
            if (timer > 0) return;
            timer = d.Every;
            var c = cam.transform.position;
            float hh = cam.orthographicSize, hw = hh * cam.aspect;
            Vector3 p = d.Room
                ? new Vector3(Random.Range(d.X0b, d.X1b), Random.Range(d.Y0b, d.Y1b), 0)
                : new Vector3(c.x + Random.Range(d.X0a * hw + d.X0b, d.X1a * hw + d.X1b), c.y + Random.Range(d.Y0a * hh + d.Y0b, d.Y1a * hh + d.Y1b), 0);
            var col = Random.value < d.ChanceA ? d.ColorA : d.ColorB;
            float spin = d.SpinMin != 0 || d.SpinMax != 0 ? Random.Range(d.SpinMin, d.SpinMax) : 0;
            layer.Spawn(sprites[d.Sprite], p, new Vector3(Random.Range(d.VxMin, d.VxMax), Random.Range(d.VyMin, d.VyMax)),
                        Random.Range(d.LifeMin, d.LifeMax), Random.Range(d.ScaleMin, d.ScaleMax), col, spin, d.Order, ParticleFade.InOut);
        }
    }

    static class AmbientDefExt
    {
        public static AmbientDef MemberwiseCloneDef(this AmbientDef d) => new AmbientDef
        {
            Every = d.Every, X0a = d.X0a, X0b = d.X0b, X1a = d.X1a, X1b = d.X1b, Y0a = d.Y0a, Y0b = d.Y0b, Y1a = d.Y1a, Y1b = d.Y1b, Room = d.Room,
            VxMin = d.VxMin, VxMax = d.VxMax, VyMin = d.VyMin, VyMax = d.VyMax, LifeMin = d.LifeMin, LifeMax = d.LifeMax,
            ScaleMin = d.ScaleMin, ScaleMax = d.ScaleMax, SpinMin = d.SpinMin, SpinMax = d.SpinMax, Sprite = d.Sprite,
            ColorA = d.ColorA, ColorB = d.ColorB, ChanceA = d.ChanceA, Order = d.Order, Butterflies = d.Butterflies,
        };
    }
}
