using System.Collections.Generic;
using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 배경음악 재생기. Sim.Music 악보를 처음 틀 때 합성해 두고 반복 재생한다.
    /// 음량은 4단계(100/60/30/0%)이며 PlayerPrefs "bgmVol" 에 저장된다.
    /// </summary>
    public class Bgm : MonoBehaviour
    {
        const int Rate = 22050;
        const float Base = .8f;                      // 효과음보다 살짝 작게
        static readonly float[] Levels = { 1f, .6f, .3f, 0f };

        AudioSource src;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        float fade = 1;                              // 곡이 바뀔 때 0 → 1 로 짧게 페이드 인
        int level;

        public string Current { get; private set; } = "";
        public int LevelPercent => Mathf.RoundToInt(Levels[level] * 100);

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            level = Mathf.Clamp(PlayerPrefs.GetInt("bgmVol", 0), 0, Levels.Length - 1);
        }

        /// <summary>곡을 튼다. 이미 같은 곡이면 처음부터 다시 틀지 않는다.</summary>
        public void Play(string name)
        {
            if (name == Current && src.isPlaying) return;
            var song = Music.Get(name);
            if (song == null) { Debug.LogWarning("BGM 없음: " + name); return; }
            if (!clips.TryGetValue(name, out var clip))
            {
                var data = Music.Render(song, Rate);
                clip = AudioClip.Create("bgm_" + name, data.Length, 1, Rate, false);
                clip.SetData(data, 0);
                clips[name] = clip;
            }
            Current = name;
            src.clip = clip;
            fade = 0;
            Apply();
            src.Play();
        }

        public void Stop()
        {
            Current = "";
            src.Stop();
        }

        /// <summary>음량을 다음 단계로 (100 → 60 → 30 → 0 → 100 ...).</summary>
        public int CycleVolume()
        {
            level = (level + 1) % Levels.Length;
            PlayerPrefs.SetInt("bgmVol", level);
            Apply();
            return LevelPercent;
        }

        void Update()
        {
            if (fade >= 1) return;
            fade = Mathf.Min(1, fade + Time.unscaledDeltaTime / .4f);
            Apply();
        }

        void Apply() => src.volume = Base * Levels[level] * fade;
    }
}
