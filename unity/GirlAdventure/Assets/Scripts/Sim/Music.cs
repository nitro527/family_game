using System;
using System.Collections.Generic;

namespace GirlAdventure.Sim
{
    /// <summary>
    /// 배경음악 한 곡. 악보는 문자열로 적는다 (UnityEngine 의존 없음).
    /// 선율/베이스: "C5.2 E5 R.2 F#4.3" — 음이름+옥타브, 점 뒤 숫자는 8분음표 개수(생략 시 1), R 은 쉼표.
    /// 드럼: 한 글자 = 8분음표 하나. k 킥, s 스네어, h 하이햇, . 쉼.
    /// </summary>
    public class Song
    {
        public string Name;
        public int Bpm;
        public bool SoftLead;       // true 면 선율을 삼각파로 (부드러운 곡)
        public string Lead, Bass, Drums;
    }

    /// <summary>칩튠 BGM 악보 모음 + 합성기. 모든 곡은 8마디(4/4)로 끝과 처음이 이어지게 반복된다.</summary>
    public static class Music
    {
        public static readonly Song[] Songs =
        {
            new Song
            {
                Name = "title", Bpm = 100,
                Lead = "D5 D5 D5 D5 G5.4 | A5.2 B5.2 C6.2 A5.2 | B5.3 G5 D5.2 G5.2 | A5.6 R.2 |" +
                       "D5 D5 D5 D5 G5.4 | A5.2 B5.2 C6.2 E6.2 | D6.2 B5.2 A5.2 F#5.2 | G5.6 R.2",
                Bass = "G2.2 D3.2 G2.2 D3.2 | D3.2 A2.2 D3.2 F#3.2 | G2.2 D3.2 E3.2 B2.2 | D3.2 A2.2 D3.2 A2.2 |" +
                       "G2.2 D3.2 G2.2 D3.2 | C3.2 G3.2 A2.2 E3.2 | D3.2 A2.2 D3.2 F#3.2 | G2.2 D3.2 G2.4",
                Drums = "k...s...k...s...k...s...k.k.s.s.k...s...k...s...k...s.s.k.k.s...",
            },
            new Song
            {
                Name = "field", Bpm = 112,
                Lead = "C5.2 E5.2 G5.3 E5 | F5.2 A5.2 G5.4 | E5.2 C5.2 D5.2 E5.2 | D5.6 R.2 |" +
                       "C5.2 E5.2 G5.3 C6 | A5.2 F5.2 G5.4 | E5.2 D5.2 C5.2 D5.2 | C5.6 R.2",
                Bass = "C3.2 G3.2 C3.2 G3.2 | F2.2 C3.2 G2.2 D3.2 | C3.2 G3.2 A2.2 E3.2 | G2.2 D3.2 G2.2 B2.2 |" +
                       "C3.2 G3.2 C3.2 G3.2 | F2.2 C3.2 G2.2 D3.2 | C3.2 G3.2 G2.2 B2.2 | C3.2 G2.2 C3.4",
                Drums = "k.h.s.h.k.h.s.h.k.h.s.h.k.h.shhh" + "k.h.s.h.k.h.s.h.k.h.s.h.k.hks.s.",
            },
            new Song
            {
                Name = "battle", Bpm = 150,
                Lead = "A4 A4 C5 A4 E5.2 D5.2 | C5 B4 A4 B4 C5.2 E5.2 | F5 F5 E5 D5 E5.2 C5.2 | B4 A4 G#4 B4 E5.4 |" +
                       "A5 A5 G5 F5 E5.2 D5.2 | C5 D5 E5 F5 G5.2 E5.2 | F5.2 E5.2 D5.2 B4.2 | A4.4 G#4.2 B4.2",
                Bass = "A2 A3 A2 A3 A2 A3 A2 A3 | A2 A3 A2 A3 A2 A3 A2 A3 | F2 F3 F2 F3 F2 F3 F2 F3 | E2 E3 E2 E3 E2 E3 E2 E3 |" +
                       "F2 F3 F2 F3 F2 F3 F2 F3 | C3 C4 C3 C4 C3 C4 C3 C4 | D3 D4 D3 D4 E2 E3 E2 E3 | E2 E3 E2 E3 E2 E3 E2 E3",
                Drums = "khskkhshkhskkhshkhskkhshkhskkhsskhskkhshkhskkhshkhskkhshkhsksssk",
            },
            new Song
            {
                Name = "boss", Bpm = 138,
                Lead = "D5.3 D5 F5.2 D5.2 | C#5.3 C#5 E5.2 A4.2 | D5 E5 F5 G5 A5.2 G5.2 | F5.2 E5.2 C#5.4 |" +
                       "A5.3 A5 G5.2 F5.2 | G5.3 G5 F5.2 E5.2 | F5.2 D5.2 E5.2 C#5.2 | D5.6 R.2",
                Bass = "D2 D2 D3 D2 D2 D2 D3 D2 | A2 A2 A3 A2 A2 A2 A3 A2 | D2 D2 D3 D2 D2 D2 D3 D2 | A2 A2 A3 A2 A2 A2 A3 A2 |" +
                       "F2 F2 F3 F2 F2 F2 F3 F2 | C3 C3 C4 C3 C3 C3 C4 C3 | A#2 A#2 A#3 A#2 A2 A2 A3 A2 | D2 D2 D3 D2 D2 D2 D3 D2",
                Drums = "kkshkkshkkshkkshkkshkkshkkshksssk.shkkshkkshkkshkkshkkshkkshssss",
            },
            new Song
            {
                Name = "cave", Bpm = 104,
                Lead = "E5.2 G5.2 F#5.2 E5.2 | D#5.4 B4.4 | E5.2 G5.2 A5.2 B5.2 | A5.6 R.2 |" +
                       "G5.2 F#5.2 E5.2 D5.2 | C5.4 E5.4 | D#5.2 F#5.2 A5.2 F#5.2 | E5.6 R.2",
                Bass = "E2 B2 E3 B2 E2 B2 E3 B2 | B1 F#2 B2 F#2 B1 F#2 B2 F#2 | E2 B2 E3 B2 E2 B2 E3 B2 | A1 E2 A2 E2 A1 E2 A2 E2 |" +
                       "C2 G2 C3 G2 C2 G2 C3 G2 | A1 E2 A2 E2 A1 E2 A2 E2 | B1 F#2 B2 F#2 B1 F#2 B2 F#2 | E2 B2 E3 B2 E2.4",
                Drums = "k..h.k.hk..h.k.hk..h.k.hk..h.k.hk..h.k.hk..h.k.hk..h.k.hk..hsk.s",
            },
            new Song
            {
                Name = "ending", Bpm = 96, SoftLead = true,
                Lead = "C5.2 F5.2 A5.4 | G5.2 F5.2 E5.2 F5.2 | G5.3 A5 A#5.2 G5.2 | A5.6 R.2 |" +
                       "F5.2 A5.2 C6.4 | A#5.2 A5.2 G5.2 F5.2 | E5.2 G5.2 C5.2 E5.2 | F5.6 R.2",
                Bass = "F2 C3 F3 A3 F2 C3 F3 A3 | C3 G3 C4 G3 C3 G3 C4 G3 | A#2 F3 A#3 F3 C3 G3 C4 G3 | F2 C3 F3 A3 F2 C3 F3 A3 |" +
                       "F2 C3 F3 A3 F2 C3 F3 A3 | A#2 F3 A#3 F3 A#2 F3 A#3 F3 | C3 G3 C4 G3 C3 G3 C4 G3 | F2 C3 F3 C3 F2.4",
                Drums = "",
            },
        };

        public static Song Get(string name)
        {
            foreach (var s in Songs) if (s.Name == name) return s;
            return null;
        }

        /// <summary>악보 한 줄을 (주파수 Hz, 8분음표 길이) 목록으로. 쉼표는 주파수 0.</summary>
        public static List<(double freq, int len)> Parse(string track)
        {
            var list = new List<(double, int)>();
            if (string.IsNullOrEmpty(track)) return list;
            foreach (var tok in track.Split(new[] { ' ', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int dot = tok.IndexOf('.');
                string note = dot < 0 ? tok : tok.Substring(0, dot);
                int len = dot < 0 ? 1 : int.Parse(tok.Substring(dot + 1));
                list.Add((note == "R" ? 0 : Freq(note), len));
            }
            return list;
        }

        /// <summary>트랙 길이 (8분음표 개수).</summary>
        public static int Steps(string track)
        {
            int n = 0;
            foreach (var (_, len) in Parse(track)) n += len;
            return n;
        }

        static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        /// <summary>"A4" → 440Hz. 모르는 음이름이면 예외.</summary>
        public static double Freq(string note)
        {
            string name = note.Substring(0, note.Length - 1);
            int oct = note[note.Length - 1] - '0';
            int idx = Array.IndexOf(Names, name);
            if (idx < 0 || oct < 0 || oct > 8) throw new FormatException("알 수 없는 음: " + note);
            int midi = (oct + 1) * 12 + idx;
            return 440.0 * Math.Pow(2, (midi - 69) / 12.0);
        }

        /// <summary>곡 한 바퀴를 모노 샘플(-1~1)로 합성한다. 같은 곡은 항상 같은 결과.</summary>
        public static float[] Render(Song s, int rate)
        {
            int stepLen = (int)(rate * 30.0 / s.Bpm);            // 8분음표 하나의 샘플 수
            int total = Steps(s.Lead) * stepLen;
            var buf = new float[total];
            Voice(buf, Parse(s.Lead), stepLen, rate, s.SoftLead ? 2 : 0, s.SoftLead ? .20f : .10f);
            Voice(buf, Parse(s.Bass), stepLen, rate, 1, .20f);
            Drums(buf, s.Drums, stepLen, rate);
            for (int i = 0; i < total; i++) buf[i] = Math.Max(-1f, Math.Min(1f, buf[i]));
            return buf;
        }

        /// <summary>wave: 0 사각파(25% 듀티), 1 삼각파(베이스), 2 삼각파+비브라토(부드러운 선율).</summary>
        static void Voice(float[] buf, List<(double freq, int len)> notes, int stepLen, int rate, int wave, float vol)
        {
            int pos = 0;
            foreach (var (f, len) in notes)
            {
                int n = len * stepLen;
                if (f > 0)
                {
                    int gate = (int)(n * .88f);                       // 음 사이에 살짝 틈 → 또박또박
                    double ph = 0;
                    for (int i = 0; i < n && pos + i < buf.Length; i++)
                    {
                        double t = i / (double)rate;
                        double ff = wave == 2 ? f * (1 + .004 * Math.Sin(t * 2 * Math.PI * 5.5)) : f;
                        ph = (ph + ff / rate) % 1.0;
                        float s = wave == 0 ? (ph < .25 ? 1f : -1f) : (float)(1 - 4 * Math.Abs(ph - .5));
                        float env = i < rate * .004 ? i / (rate * .004f)                       // 어택
                                  : i < gate ? 1f - .25f * i / gate                            // 살짝 감쇠
                                  : Math.Max(0, .75f * (1 - (i - gate) / (float)(n - gate)));  // 릴리스
                        buf[pos + i] += s * env * vol;
                    }
                }
                pos += n;
            }
        }

        static void Drums(float[] buf, string pattern, int stepLen, int rate)
        {
            if (string.IsNullOrEmpty(pattern)) return;
            var rng = new Random(7);
            for (int k = 0; k < pattern.Length; k++)
            {
                char c = pattern[k];
                if (c == '.') continue;
                int start = k * stepLen;
                int n = Math.Min(stepLen, (int)(rate * (c == 'k' ? .12 : c == 's' ? .10 : .03)));
                double ph = 0;
                for (int i = 0; i < n && start + i < buf.Length; i++)
                {
                    float decay = 1 - i / (float)n;
                    float s;
                    if (c == 'k')
                    {
                        ph += (150 - 110 * i / (double)n) / rate;     // 음정이 뚝 떨어지는 킥
                        s = (ph % 1 < .5 ? 1f : -1f) * .22f * decay * decay;
                    }
                    else s = (float)(rng.NextDouble() * 2 - 1) * (c == 's' ? .10f : .05f) * decay * decay;
                    buf[start + i] += s;
                }
            }
        }
    }
}
