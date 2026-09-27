using System.Collections.Generic;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>Resources/Girl 의 캐릭터 이미지를 스프라이트로 로드·캐시한다. "Boy/..." 처럼 폴더를 붙이면 그 폴더(동생 지훈)에서.</summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// 이미지 스프라이트. pivot 기본값은 발밑(바닥 중앙).
        /// PPU 는 "이미지 높이 = heightUnits 월드 유닛" 이 되도록 계산.
        /// </summary>
        public static Sprite Get(string name, float heightUnits = 1f, bool bottomPivot = true)
        {
            string key = $"{name}|{heightUnits}|{bottomPivot}";
            if (cache.TryGetValue(key, out var s)) return s;
            string path = name.Contains("/") ? name : "Girl/" + name;
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogError($"[Art] missing Resources/{path}");
                return null;
            }
            tex.filterMode = FilterMode.Bilinear;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                bottomPivot ? new Vector2(.5f, 0) : new Vector2(.5f, .5f), tex.height / heightUnits);
            s.name = name;
            cache[key] = s;
            return s;
        }

        /// <summary>UI Image 용 (가운데 pivot; UI 는 크기를 RectTransform 으로 정하므로 PPU 무관).</summary>
        public static Sprite UI(string name) => Get(name, 1f, false);

        public static Sprite FromTexture(Texture2D tex, Vector2 pivot, float ppu) =>
            Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, ppu);
    }
}
