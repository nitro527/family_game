using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 상태 오브젝트 (props.json Objects, 2026-10-10 소품 데이터화 5단계): 맵이 바뀌면 겹마다 스프라이트를 만들고,
    /// 상태가 바뀌면(RefreshQuestProps) 조건대로 보이고 숨고 색을 입히고, 매 프레임 움직임(Anim)을 준다.
    /// 예: 동굴 기지 철창(닫힌 철창 → 갇힌 지훈 → 앞 창살, 합류하면 열린 철창), 도서관 마법진의 엄마.
    /// </summary>
    public partial class GameRoot
    {
        readonly List<(ObjectDef o, LayerDef l, SpriteRenderer sr, Vector3 at)> objViews = new List<(ObjectDef, LayerDef, SpriteRenderer, Vector3)>();
        static readonly Dictionary<string, Sprite> objPropSprites = new Dictionary<string, Sprite>();

        static Sprite ObjPropSprite(string id, float pivotX)
        {
            string key = id + "|" + pivotX;
            if (objPropSprites.TryGetValue(key, out var s)) return s;
            var t = Resources.Load<Texture2D>("Tiles/Props/" + id);
            s = t == null ? null : Art.FromTexture(t, new Vector2(pivotX, 0), Painter.Tile);
            objPropSprites[key] = s;
            return s;
        }

        void RebuildObjects()
        {
            foreach (var v in objViews) if (v.sr) Destroy(v.sr.gameObject);
            objViews.Clear();
            if (map == null) return;
            foreach (var o in PropCatalog.ObjectsOn(map.Kind))
                foreach (var l in o.Layers ?? new LayerDef[0])
                {
                    var sr = new GameObject("Obj_" + o.Id).AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(fieldRoot, false);
                    float pv = l.PivotX > 0 ? l.PivotX : .5f;
                    sr.sprite = !string.IsNullOrEmpty(l.Art) ? Art.Get(l.Art, l.Height > 0 ? l.Height : FieldCharHeight) : ObjPropSprite(l.Prop, pv);
                    sr.sortingOrder = 2;
                    var at = FeetAt(o.X, o.Y) + new Vector3(l.Dx, l.Dy, l.Z);
                    sr.transform.localPosition = at;
                    objViews.Add((o, l, sr, at));
                }
            RefreshObjects();
        }

        void RefreshObjects()
        {
            foreach (var (o, l, sr, _) in objViews)
            {
                if (!sr) continue;
                sr.enabled = sr.sprite != null && PropCatalog.Cond(l.ShowIf, P);
                sr.color = !string.IsNullOrEmpty(l.Tint) && PropCatalog.Cond(l.TintIf, P) && ColorUtility.TryParseHtmlString(l.Tint, out var c) ? c : Color.white;
            }
        }

        /// <summary>겹 움직임: sob = 훌쩍훌쩍 어깨 들썩, cheer = 통통 뛰며 흔들기, bob = 둥실, breathe = 보스가 숨 쉬듯 천천히 들썩·기우뚱 (던전 보스).</summary>
        void TickObjects()
        {
            float t = Time.time;
            foreach (var (o, l, sr, at) in objViews)
            {
                if (!sr || !sr.enabled || string.IsNullOrEmpty(l.Anim)) continue;
                string a = !string.IsNullOrEmpty(l.Anim2) && PropCatalog.Cond(l.AnimIf, P) ? l.Anim2 : l.Anim;
                float y = a == "sob" ? Mathf.Abs(Mathf.Sin(t * 7)) * .025f : a == "cheer" ? Mathf.Abs(Mathf.Sin(t * 6)) * .08f : a == "bob" ? Mathf.Sin(t * 2) * .05f : a == "breathe" ? Mathf.Sin(t * 1.7f) * .05f : 0;
                sr.transform.localPosition = at + new Vector3(0, y, 0);
                sr.transform.localRotation = Quaternion.Euler(0, 0, a == "cheer" ? Mathf.Sin(t * 6) * 6 : a == "breathe" ? Mathf.Sin(t * .85f) * 2.5f : 0);
            }
        }
    }
}
