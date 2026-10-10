using System.Collections.Generic;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>입자가 사라지는 모양: Linear = 처음부터 서서히, Tail = 끝 40% 에서, InOut = 서서히 나타났다 사라지며 살랑 (꽃잎·빛 먼지).</summary>
    public enum ParticleFade { Linear, Tail, InOut }

    /// <summary>
    /// 날아가다 사라지는 작은 그림들 (2026-10-10 코드 정리 C — 필드와 전투가 따로 갖고 있던 입자 목록을 하나로).
    /// Spawn 으로 띄우고, 매 프레임 Tick, 화면을 떠날 때 Clear. 위치는 부모 기준(로컬) 또는 월드.
    /// </summary>
    public class ParticleLayer
    {
        class Particle
        {
            public SpriteRenderer sr;
            public Vector3 vel;
            public float age, life, spin, alpha = -1;
            public ParticleFade fade;
        }

        readonly Transform parent;
        readonly ParticleFade defaultFade;
        readonly int defaultOrder;
        readonly bool worldSpace;
        readonly string label;
        readonly List<Particle> list = new List<Particle>();

        public ParticleLayer(Transform parent, string label, ParticleFade fade, int order, bool worldSpace = false)
        {
            this.parent = parent;
            this.label = label;
            defaultFade = fade;
            defaultOrder = order;
            this.worldSpace = worldSpace;
        }

        public int Count => list.Count;

        public SpriteRenderer Spawn(Sprite s, Vector3 pos, Vector3 vel, float life, float scale, Color color, float spin = 0, int? order = null, ParticleFade? fade = null)
        {
            var sr = new GameObject(label).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            if (worldSpace) sr.transform.position = pos; else sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * scale;
            sr.sprite = s;
            sr.color = color;
            sr.sortingOrder = order ?? defaultOrder;
            list.Add(new Particle { sr = sr, vel = vel, life = life, spin = spin, fade = fade ?? defaultFade });
            return sr;
        }

        public void Tick(float dt)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var f = list[i];
                f.age += dt;
                if (f.age >= f.life || !f.sr)
                {
                    if (f.sr) Object.Destroy(f.sr.gameObject);
                    list.RemoveAt(i);
                    continue;
                }
                var tr = f.sr.transform;
                if (worldSpace) tr.position += f.vel * dt; else tr.localPosition += f.vel * dt;
                tr.Rotate(0, 0, f.spin * dt);
                var col = f.sr.color;
                switch (f.fade)
                {
                    case ParticleFade.InOut:
                        if (f.alpha < 0) f.alpha = col.a;
                        col.a = f.alpha * Mathf.Sin(Mathf.PI * f.age / f.life);
                        tr.localPosition += new Vector3(Mathf.Sin(f.age * 2 + i) * .15f * dt, 0, 0);   // 살랑살랑
                        break;
                    case ParticleFade.Tail:
                        col.a = Mathf.Clamp01((f.life - f.age) / (f.life * .4f));
                        break;
                    default:
                        col.a = 1 - f.age / f.life;
                        break;
                }
                f.sr.color = col;
            }
        }

        public void Clear()
        {
            foreach (var f in list) if (f.sr) Object.Destroy(f.sr.gameObject);
            list.Clear();
        }
    }
}
