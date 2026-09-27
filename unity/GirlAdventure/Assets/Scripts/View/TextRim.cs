using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// 글자 둘레에 얇은 테두리. Unity 기본 Outline 은 대각선 4방향으로만 글자를 한 벌씩 더 찍어서
    /// 큰 화면(960×540 기준을 2배로 늘림)에서는 검은 글자가 흰 글자와 어긋나 겹쳐 보였다 (눈 아픔).
    /// 여기서는 8방향으로 가깝게 둘러 매끈한 테두리 한 줄처럼 보이게 한다.
    /// </summary>
    public class TextRim : Shadow
    {
        static readonly Vector2[] Dirs =
        {
            new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
            new Vector2(.7f, .7f), new Vector2(-.7f, .7f), new Vector2(.7f, -.7f), new Vector2(-.7f, -.7f),
        };
        readonly List<UIVertex> verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            verts.Clear();
            vh.GetUIVertexStream(verts);
            float d = effectDistance.x;
            int start = 0, end = verts.Count;
            foreach (var dir in Dirs)
            {
                // 원본 범위를 테두리로 바꾸고 원본 사본을 뒤에 붙임 → 글자가 항상 맨 위
                ApplyShadowZeroAlloc(verts, effectColor, start, end, dir.x * d, dir.y * d);
                start = end;
                end = verts.Count;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }

        public static TextRim Add(Graphic g, Color color, float distance)
        {
            var r = g.gameObject.AddComponent<TextRim>();
            r.effectColor = color;
            r.effectDistance = new Vector2(distance, distance);
            r.useGraphicAlpha = true;
            return r;
        }
    }
}
