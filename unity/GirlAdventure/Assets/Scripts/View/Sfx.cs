using System.Collections.Generic;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>칩튠 효과음을 코드로 합성해 재생한다 (외부 오디오 파일 없음).</summary>
    public class Sfx : MonoBehaviour
    {
        enum Wave { Square, Triangle, Saw }

        AudioSource src;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            Add("step", Wave.Square, .025f, .10f, 180);
            Add("bump", Wave.Square, .05f, .18f, 110);
            Add("select", Wave.Square, .03f, .15f, 880);
            Add("encounter", Wave.Square, .06f, .22f, 523, 659, 784, 1046, 784, 1046);
            Add("fire", Wave.Saw, .05f, .18f, 300, 500, 800, 1100);
            Add("hit", Wave.Square, .07f, .30f, 180, 90);
            Add("hurt", Wave.Saw, .07f, .25f, 140, 100, 70);
            Add("star", Wave.Triangle, .07f, .35f, 1046, 1318, 1568, 2093, 1568, 2093, 2637);
            Add("heal", Wave.Triangle, .09f, .30f, 523, 659, 784, 1046);
            Add("win", Wave.Square, .11f, .22f, 523, 523, 523, 698, 0, 880, 1046);
            Add("lvup", Wave.Triangle, .10f, .35f, 659, 784, 1046, 1318, 1568);
            Add("miss", Wave.Triangle, .09f, .25f, 300, 200);
            Add("run", Wave.Square, .05f, .15f, 500, 400, 300, 200);
            Add("chest", Wave.Square, .07f, .25f, 523, 659, 784, 1046, 1318);
            Add("unlock", Wave.Square, .05f, .22f, 220, 0, 330, 660);
            Add("start", Wave.Triangle, .12f, .30f, 392, 523, 659, 784);
        }

        void Add(string name, Wave wave, float noteDur, float vol, params float[] freqs)
        {
            const int rate = 22050;
            int per = (int)(rate * noteDur), n = per * freqs.Length;
            var data = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                float f = freqs[k];
                if (f <= 0) continue;
                for (int i = 0; i < per; i++)
                {
                    float t = i / (float)rate, ph = t * f % 1f;
                    float s = wave == Wave.Square ? (ph < .5f ? 1 : -1)
                            : wave == Wave.Triangle ? 1 - 4 * Mathf.Abs(ph - .5f)
                            : 2 * ph - 1;
                    float env = Mathf.Pow(1 - i / (float)per, 1.6f);
                    data[k * per + i] = s * env * vol;
                }
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            clips[name] = clip;
        }

        public void Play(string name)
        {
            if (clips.TryGetValue(name, out var c)) src.PlayOneShot(c);
        }
    }
}
