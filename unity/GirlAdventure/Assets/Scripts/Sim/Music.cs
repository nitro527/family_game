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
        public bool NoEcho;         // true 면 선율 메아리 채널을 끈다
        public string Lead, Bass, Drums;
    }

    /// <summary>
    /// 칩튠 BGM 악보 모음 + 합성기. 곡은 8마디 또는 16마디(A-B, 4/4)로 끝과 처음이 이어지게 반복된다.
    /// 선율은 16분음표 뒤에 작고 가는 소리로 한 번 더 울려(메아리 채널) 패미컴 곡처럼 풍성하게 들린다.
    /// </summary>
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
                // A (8마디, 다장조) → B (8마디, 가단조로 살짝 흐렸다가 솔로 끝나 A 로 돌아간다)
                Lead = "C5.2 E5.2 G5.3 E5 | F5.2 A5.2 G5.4 | E5.2 C5.2 D5.2 E5.2 | D5.6 R.2 |" +
                       "C5.2 E5.2 G5.3 C6 | A5.2 F5.2 G5.4 | E5.2 D5.2 C5.2 D5.2 | C5.6 R.2 |" +
                       "A5.2 G5.2 E5.2 C5.2 | F5.2 E5.2 D5.4 | G5.2 E5.2 C6.3 B5 | A5.6 R.2 |" +
                       "A5.2 G5.2 E5.2 G5.2 | F5.2 A5.2 C6.4 | B5.2 A5.2 G5.2 F5.2 | G5.6 R.2",
                Bass = "C3.2 G3.2 C3.2 G3.2 | F2.2 C3.2 G2.2 D3.2 | C3.2 G3.2 A2.2 E3.2 | G2.2 D3.2 G2.2 B2.2 |" +
                       "C3.2 G3.2 C3.2 G3.2 | F2.2 C3.2 G2.2 D3.2 | C3.2 G3.2 G2.2 B2.2 | C3.2 G2.2 C3.4 |" +
                       "A2.2 E3.2 A2.2 E3.2 | F2.2 C3.2 G2.2 D3.2 | C3.2 G3.2 E3.2 G3.2 | F2.2 C3.2 F2.2 A2.2 |" +
                       "A2.2 E3.2 A2.2 C3.2 | F2.2 C3.2 F2.2 A2.2 | G2.2 D3.2 G2.2 B2.2 | G2.2 D3.2 G2.4",
                Drums = "k.h.s.h.k.h.s.h.k.h.s.h.k.h.shhh" + "k.h.s.h.k.h.s.h.k.h.s.h.k.hks.s." +
                        "k.hhs.h.k.hhs.h.k.hhs.h.k.h.shhh" + "k.hhs.h.k.hhs.h.k.hhs.h.kks.ssss",
            },
            new Song
            {
                Name = "battle", Bpm = 150,
                // A (가단조로 몰아친다) → B (다장조로 밝게 반격하다가 E 로 긴장을 올려 A 로)
                Lead = "A4 A4 C5 A4 E5.2 D5.2 | C5 B4 A4 B4 C5.2 E5.2 | F5 F5 E5 D5 E5.2 C5.2 | B4 A4 G#4 B4 E5.4 |" +
                       "A5 A5 G5 F5 E5.2 D5.2 | C5 D5 E5 F5 G5.2 E5.2 | F5.2 E5.2 D5.2 B4.2 | A4.4 G#4.2 B4.2 |" +
                       "C5 E5 G5 E5 C6.2 B5.2 | A5 G5 F5 E5 D5.2 G5.2 | E5 F5 G5 A5 G5.2 E5.2 | D5.2 E5.2 F5.4 |" +
                       "F5 E5 D5 C5 D5.2 F5.2 | E5 D5 C5 B4 C5.2 E5.2 | D5.2 C5.2 B4.2 G#4.2 | A4.2 B4.2 C5.2 E5.2",
                Bass = "A2 A3 A2 A3 A2 A3 A2 A3 | A2 A3 A2 A3 A2 A3 A2 A3 | F2 F3 F2 F3 F2 F3 F2 F3 | E2 E3 E2 E3 E2 E3 E2 E3 |" +
                       "F2 F3 F2 F3 F2 F3 F2 F3 | C3 C4 C3 C4 C3 C4 C3 C4 | D3 D4 D3 D4 E2 E3 E2 E3 | E2 E3 E2 E3 E2 E3 E2 E3 |" +
                       "C3 C4 C3 C4 C3 C4 C3 C4 | F2 F3 F2 F3 F2 F3 F2 F3 | C3 C4 C3 C4 C3 C4 C3 C4 | G2 G3 G2 G3 G2 G3 G2 G3 |" +
                       "D3 D4 D3 D4 D3 D4 D3 D4 | A2 A3 A2 A3 A2 A3 A2 A3 | E2 E3 E2 E3 E2 E3 E2 E3 | E2 E3 E2 E3 G#2 G#3 B2 B3",
                Drums = "khskkhshkhskkhshkhskkhshkhskkhsskhskkhshkhskkhshkhskkhshkhsksssk" +
                        "k.hsk.hsk.hsk.hsk.hsk.hsk.hsk.hsk.hsk.hsk.hsk.hsk.hsk.hskkssssss",
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
                // A (마단조, 조심조심) → B (사장조 쪽으로 빛이 살짝 들었다가 B 로 멈칫, A 로)
                Lead = "E5.2 G5.2 F#5.2 E5.2 | D#5.4 B4.4 | E5.2 G5.2 A5.2 B5.2 | A5.6 R.2 |" +
                       "G5.2 F#5.2 E5.2 D5.2 | C5.4 E5.4 | D#5.2 F#5.2 A5.2 F#5.2 | E5.6 R.2 |" +
                       "B5.2 A5.2 G5.2 F#5.2 | E5.4 G5.4 | A5.2 G5.2 F#5.2 E5.2 | F#5.6 R.2 |" +
                       "C6.2 B5.2 A5.2 G5.2 | F#5.4 A5.4 | G5.2 F#5.2 E5.2 D#5.2 | B4.6 R.2",
                Bass = "E2 B2 E3 B2 E2 B2 E3 B2 | B1 F#2 B2 F#2 B1 F#2 B2 F#2 | E2 B2 E3 B2 E2 B2 E3 B2 | A1 E2 A2 E2 A1 E2 A2 E2 |" +
                       "C2 G2 C3 G2 C2 G2 C3 G2 | A1 E2 A2 E2 A1 E2 A2 E2 | B1 F#2 B2 F#2 B1 F#2 B2 F#2 | E2 B2 E3 B2 E2.4 |" +
                       "G2 D3 G3 D3 G2 D3 G3 D3 | C2 G2 C3 G2 C2 G2 C3 G2 | A1 E2 A2 E2 A1 E2 A2 E2 | B1 F#2 B2 F#2 B1 F#2 B2 F#2 |" +
                       "A1 E2 A2 E2 A1 E2 A2 E2 | D2 A2 D3 A2 D2 A2 D3 A2 | C2 G2 C3 G2 B1 F#2 B2 F#2 | B1 F#2 B2 F#2 B1.4",
                Drums = "k..h.k.hk..h.k.hk..h.k.hk..h.k.hk..h.k.hk..h.k.hk..h.k.hk..hsk.s" +
                        "k..h.k.hk..h.k.hk..h.k.hk..h.k.hk..h.k.hk..h.k.hk..h.k.hk.shsk.s",
            },
            new Song
            {
                Name = "desert", Bpm = 108,
                // 사막 (챕터 3): 레 프리지안 도미넌트로 신비롭게, 낙타 걸음 같은 쿵-짝 리듬.
                // A (뜨거운 모래) → B (솔단조, 오아시스 그늘에서 한숨 돌렸다가 파♯·미♭ 으로 다시 사막으로)
                Lead = "D5.2 D#5 F#5 G5.2 A5.2 | A#5 A5 G5 F#5 G5.4 | F#5.2 G5 F#5 D#5.2 F#5.2 | D5.6 R.2 |" +
                       "A5.2 A#5 A5 G5.2 F#5.2 | G5 A5 A#5 C6 A5.4 | G5 F#5 D#5 F#5 G5.2 F#5.2 | D5.6 R.2 |" +
                       "G5.2 A#5.2 D6.3 C6 | A#5.2 A5.2 G5.4 | F5.2 G5.2 A5.2 C6.2 | A5.6 R.2 |" +
                       "G5.2 A#5.2 D6.3 D#6 | D6.2 C6.2 A#5.4 | A5 A#5 A5 G5 F#5.2 D#5.2 | D5.6 R.2",
                Bass = "D2.2 A2.2 D3.2 A2.2 | D2.2 A2.2 D3.2 A2.2 | D#2.2 A#2.2 D#3.2 A#2.2 | D2.2 A2.2 D3.2 A2.2 |" +
                       "D2.2 A2.2 D3.2 A2.2 | C2.2 G2.2 C3.2 G2.2 | D#2.2 A#2.2 D2.2 A2.2 | D2.2 A2.2 D2.4 |" +
                       "G2.2 D3.2 G3.2 D3.2 | G2.2 D3.2 G3.2 D3.2 | F2.2 C3.2 F3.2 C3.2 | F2.2 C3.2 F3.2 C3.2 |" +
                       "G2.2 D3.2 G3.2 D3.2 | D#2.2 A#2.2 D#3.2 A#2.2 | D2.2 A2.2 D3.2 A2.2 | D2.2 A2.2 D2.4",
                Drums = "k..hk.h.k..hk.h.k..hk.h.k.hhs.h." + "k..hk.h.k..hk.h.k..hk.h.k.hhs.s." +
                        "k..hs.h.k..hs.h.k..hs.h.k..hs.hh" + "k..hs.h.k..hs.h.k.hhs.h.k.s.s.s.",
            },
            new Song
            {
                Name = "lava", Bpm = 120,
                // 용암 땅 (챕터 4): A 는 라단조로 무겁게 쿵쿵 (아빠 발걸음), B 는 바단조→미 로 화산이 끓어오르듯 올라갔다 A 로
                Lead = "A4.2 C5.2 E5.2 A5.2 | G#5.3 F5 E5.4 | D5.2 F5.2 A5.2 D6.2 | C6.3 B5 A5.4 |" +
                       "A4.2 C5.2 E5.2 A5.2 | B5.3 C6 D6.4 | C6.2 B5.2 A5.2 G#5.2 | A5.6 R.2 |" +
                       "F5.2 G#5.2 C6.4 | A#5.2 G#5.2 G5.4 | F5.2 G5.2 G#5.2 A#5.2 | C6.6 R.2 |" +
                       "C6.2 D6.2 E6.4 | D6.2 C6.2 B5.4 | A5.2 B5.2 C6.2 D6.2 | E6.6 R.2",
                Bass = "A1 A2 A1 A2 A1 A2 A1 A2 | E1 E2 E1 E2 E1 E2 E1 E2 | D2 D3 D2 D3 D2 D3 D2 D3 | E1 E2 E1 E2 E1 E2 E1 E2 |" +
                       "A1 A2 A1 A2 A1 A2 A1 A2 | G1 G2 G1 G2 G1 G2 G1 G2 | E1 E2 E1 E2 E1 E2 E1 E2 | A1 A2 A1 A2 A1.4 |" +
                       "F1 F2 F1 F2 F1 F2 F1 F2 | C#2 C#3 C#2 C#3 C2 C3 C2 C3 | F1 F2 F1 F2 G1 G2 G1 G2 | C2 C3 C2 C3 C2 C3 C2 C3 |" +
                       "A1 A2 A1 A2 A1 A2 A1 A2 | E1 E2 E1 E2 E1 E2 E1 E2 | F1 F2 F1 F2 D2 D3 D2 D3 | E1 E2 E1 E2 E1 E2 E1 E2",
                Drums = "k.h.k.h.k.h.s.h." + "k.h.k.h.k.hks.h." + "k.h.k.h.k.h.s.h." + "k.h.k.h.k.hks.s." +
                        "kkh.s.h.kkh.s.h." + "kkh.s.h.kkh.s.hh" + "kkh.s.h.kkh.s.h." + "kkh.s.h.k.s.s.s.",
            },
            new Song
            {
                Name = "snowtown", Bpm = 84, SoftLead = true,
                // 눈꽃 마을 (Q-25): 바장조 오르골처럼 포근하게, B 는 라단조로 살짝 쓸쓸했다가 돌아온다
                Lead = "F5.2 A5.2 C6.2 A5.2 | G5.2 A#5.2 A5.4 | F5.2 A5.2 C6.2 D6.2 | C6.6 R.2 |" +
                       "D6.2 C6.2 A#5.2 A5.2 | G5.2 A5.2 A#5.4 | A5.2 G5.2 F5.2 E5.2 | F5.6 R.2 |" +
                       "D5.2 F5.2 A5.4 | G5.2 F5.2 E5.4 | F5.2 G5.2 A5.2 A#5.2 | C6.6 R.2 |" +
                       "A#5.2 A5.2 G5.4 | A5.2 G5.2 F5.4 | G5.2 A5.2 G5.2 E5.2 | F5.6 R.2",
                Bass = "F2.4 C2.4 | A#1.4 F2.4 | F2.4 A#1.4 | C2.4 C2.4 |" +
                       "A#1.4 F2.4 | C2.4 C2.4 | C2.4 C2.4 | F2.4 F2.4 |" +
                       "D2.4 A1.4 | A#1.4 C2.4 | F2.4 A#1.4 | C2.4 C2.4 |" +
                       "A#1.4 F2.4 | D2.4 A#1.4 | C2.4 C2.4 | F2.4 F2.4",
                Drums = "k...h...k...h...k...h...k...h...k...h...k...h...k...h...k...h...k...h...k...h...k...h...k...h...k...h...k...h...k...h...k...h...",
            },
            new Song
            {
                Name = "library", Bpm = 88, SoftLead = true,
                // 도서관 (챕터 6 ⭐): 마단조 오르골처럼 신비롭게 (악마가 숨어 있는 책장 사이), B 는 사장조로 잠깐 밝아졌다가 돌아온다
                Lead = "E5.2 G5.2 B5.2 G5.2 | A5.2 C6.2 B5.4 | G5.2 B5.2 E6.2 D#6.2 | E6.4 B5.4 |" +
                       "C6.2 B5.2 A5.2 G5.2 | F#5.2 G5.2 A5.4 | B5.2 A5.2 G5.2 F#5.2 | E5.6 R.2 |" +
                       "G5.2 B5.2 D6.4 | C6.2 B5.2 A5.4 | B5.2 C6.2 D6.2 E6.2 | D6.6 R.2 |" +
                       "C6.2 B5.2 A5.4 | B5.2 A5.2 G5.4 | F#5.2 G5.2 A5.2 D#5.2 | E5.6 R.2",
                Bass = "E2.4 B1.4 | A1.4 E2.4 | E2.4 G2.4 | B1.4 B1.4 |" +
                       "A1.4 E2.4 | D2.4 A1.4 | B1.4 B1.4 | E2.4 E2.4 |" +
                       "G1.4 D2.4 | A1.4 E2.4 | G1.4 C2.4 | D2.4 D2.4 |" +
                       "A1.4 E2.4 | E2.4 G1.4 | B1.4 B1.4 | E2.4 E2.4",
                Drums = "k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.k...h.h.",
            },
            new Song
            {
                Name = "forest", Bpm = 104, SoftLead = true,
                // 숲 (챕터 7 ✏️): A 는 사장조로 새소리처럼 통통 튀고, B 는 마단조로 깊은 숲 속에 살짝 들어갔다가 햇살로 돌아온다
                Lead = "G5.2 B5.2 D6.2 B5.2 | C6.2 A5.2 B5.4 | G5.2 A5.2 B5.2 D6.2 | E6.4 D6.4 |" +
                       "C6.2 B5.2 A5.2 G5.2 | A5.2 B5.2 C6.4 | B5.2 A5.2 G5.2 F#5.2 | G5.6 R.2 |" +
                       "E5.2 G5.2 B5.4 | A5.2 G5.2 F#5.4 | G5.2 A5.2 B5.2 C6.2 | B5.6 R.2 |" +
                       "C6.2 D6.2 E6.4 | D6.2 B5.2 G5.4 | A5.2 B5.2 A5.2 F#5.2 | G5.6 R.2",
                Bass = "G1.4 D2.4 | C2.4 G1.4 | G1.4 B1.4 | C2.4 D2.4 |" +
                       "A1.4 E2.4 | D2.4 A1.4 | D2.4 D2.4 | G1.4 G1.4 |" +
                       "E2.4 B1.4 | A1.4 D2.4 | E2.4 A1.4 | B1.4 B1.4 |" +
                       "C2.4 G1.4 | G1.4 E2.4 | D2.4 D2.4 | G1.4 G1.4",
                Drums = "k.h.s.h.k.hks.h.k.h.s.h.k.hks.hhk.h.s.h.k.hks.h.k.h.s.h.k.hks.hhk.h.s.h.k.hks.h.k.h.s.h.k.hks.hhk.h.s.h.k.hks.h.k.h.s.h.k.hks.hh",
            },
            new Song
            {
                Name = "snow", Bpm = 100, SoftLead = true,
                // 눈 덮인 땅 (챕터 5): A 는 라장조로 사뿐사뿐 눈 밟는 소리 (썰매 방울 같은 하이햇), B 는 나단조로 살짝 차갑게 갔다가 돌아온다
                Lead = "D5.2 F#5.2 A5.2 F#5.2 | G5.2 B5.2 A5.4 | F#5.2 A5.2 D6.2 C#6.2 | B5.4 A5.4 |" +
                       "G5.2 F#5.2 E5.2 D5.2 | E5.2 F#5.2 G5.4 | A5.2 G5.2 F#5.2 E5.2 | D5.6 R.2 |" +
                       "B4.2 D5.2 F#5.4 | E5.2 D5.2 C#5.4 | D5.2 E5.2 F#5.2 G5.2 | A5.6 R.2 |" +
                       "B5.2 A5.2 G5.4 | F#5.2 E5.2 D5.4 | E5.2 F#5.2 G5.2 E5.2 | D5.6 R.2",
                Bass = "D2.4 A1.4 | G1.4 D2.4 | D2.4 F#2.4 | G1.4 A1.4 |" +
                       "G1.4 D2.4 | A1.4 E2.4 | A1.4 A1.4 | D2.4 D2.4 |" +
                       "B1.4 F#2.4 | A1.4 E2.4 | B1.4 E2.4 | F#1.4 A1.4 |" +
                       "G1.4 D2.4 | D2.4 B1.4 | A1.4 A1.4 | D2.4 D2.4",
                Drums = "k.hhs.hhk.hhs.hhk.hhs.hhk.hhs.s.k.hhs.hhk.hhs.hhk.hhs.hhk.hhs.s.k.hhs.hhk.hhs.hhk.hhs.hhk.hhs.s.k.hhs.hhk.hhs.hhk.hhs.hhk.hhs.s.",
            },
            new Song
            {
                Name = "castle", Bpm = 100,
                // 킹 젤리의 성 (챕터 1 던전, 2026-10-11): 라단조 행진 — 웅장하지만 으스스하게. A (성 안 복도) → B (옥좌가 가까워지며 높아졌다가 다시 라로)
                Lead = "D5.3 D5 F5.2 A5.2 | G5.2 F5.2 E5.4 | F5.3 E5 D5.2 C#5.2 | D5.6 R.2 |" +
                       "A5.3 A5 A#5.2 A5.2 | G5.2 F5.2 G5.4 | E5.2 F5.2 G5.2 E5.2 | A5.6 R.2 |" +
                       "D6.3 C6 A#5.2 A5.2 | G5.3 A5 F5.4 | E5.2 G5.2 A#5.2 G5.2 | A5.6 R.2 |" +
                       "F5.3 G5 A5.2 F5.2 | E5.2 D5.2 C#5.4 | D5.2 E5.2 F5.2 E5.2 | D5.6 R.2",
                Bass = "D2.2 A2.2 D3.2 A2.2 | C2.2 G2.2 C3.2 G2.2 | A#1.2 F2.2 A1.2 E2.2 | D2.2 A2.2 D2.4 |" +
                       "F2.2 C3.2 F2.2 C3.2 | A#1.2 F2.2 C2.2 G2.2 | C2.2 G2.2 C3.2 G2.2 | A1.2 E2.2 A2.2 C#3.2 |" +
                       "A#1.2 F2.2 A#2.2 F2.2 | C2.2 G2.2 D2.2 A2.2 | C2.2 G2.2 C3.2 G2.2 | A1.2 E2.2 A2.2 E2.2 |" +
                       "D2.2 A2.2 D3.2 A2.2 | A1.2 E2.2 A2.2 C#3.2 | A#1.2 F2.2 A1.2 E2.2 | D2.2 A2.2 D2.4",
                Drums = "k.s.k.s.k.s.k.s.k.s.k.s.k.s.kks." + "k.s.k.s.k.s.k.s.k.s.k.s.k.sskss." +
                        "k.s.k.s.k.s.k.s.k.s.k.s.k.s.kks." + "k.s.k.s.k.s.k.s.k.s.kks.k.sskss.",
            },
            new Song
            {
                Name = "dungeon", Bpm = 96, SoftLead = true,
                // 챕터 던전 (2026-10-11): 가단조, 조심조심 걷는 긴장 — A (어두운 복도) → B (깊은 곳에서 높아졌다가 솔♯ 으로 멈칫, 다시 라)
                Lead = "A4.2 C5.2 E5.2 D#5.2 | E5.4 R.2 B4.2 | A4.2 C5.2 F5.2 E5.2 | D5.6 R.2 |" +
                       "F5.2 E5.2 D5.2 C5.2 | B4.4 G#4.4 | A4.2 B4.2 C5.2 D5.2 | E5.6 R.2 |" +
                       "C6.2 B5.2 A5.2 G#5.2 | A5.4 E5.4 | F5.2 G5.2 A5.2 F5.2 | E5.6 R.2 |" +
                       "D5.2 F5.2 E5.2 C5.2 | B4.4 D5.4 | C5.2 B4.2 A4.2 G#4.2 | A4.6 R.2",
                Bass = "A2.2 E3.2 A2.2 E3.2 | G#2.2 E3.2 G#2.2 E3.2 | F2.2 C3.2 F2.2 C3.2 | D2.2 A2.2 D2.2 A2.2 |" +
                       "D2.2 A2.2 D2.2 A2.2 | E2.2 B2.2 E2.2 G#2.2 | A2.2 E3.2 A2.2 E3.2 | E2.2 B2.2 E2.4 |" +
                       "A2.2 E3.2 A2.2 E3.2 | C3.2 G3.2 C3.2 G3.2 | F2.2 C3.2 F2.2 C3.2 | E2.2 B2.2 E2.2 B2.2 |" +
                       "D2.2 A2.2 D2.2 A2.2 | G#2.2 E3.2 G#2.2 E3.2 | A2.2 E3.2 E2.2 G#2.2 | A2.2 E3.2 A2.4",
                Drums = "k..h..k.k..h.sk.k..h..k.k..h.sk." + "k..h..k.k..h.sk.k..h..k.k.s.k.ss" +
                        "k..h..k.k..h.sk.k..h..k.k..h.sk." + "k..h..k.k..h.sk.k..h..k.k.s.k.ss",
            },
            new Song
            {
                Name = "underworld", Bpm = 84, SoftLead = true,
                // 지하세계 (숨겨진 구멍 아래): A 는 온음계(도 레 미 파♯ 솔♯ 라♯)로 둥둥 떠다니고, B 는 라단조로 살짝 무섭게 내려갔다가 돌아온다
                Lead = "C5.2 D5.2 E5.2 F#5.2 | G#5.4 E5.4 | A#5.2 G#5.2 F#5.2 E5.2 | D5.6 R.2 |" +
                       "E5.2 F#5.2 G#5.2 A#5.2 | C6.4 A#5.4 | G#5.2 E5.2 D5.2 E5.2 | C5.6 R.2 |" +
                       "A4.2 C5.2 E5.4 | D5.2 C5.2 B4.4 | A4.2 E5.2 F5.2 E5.2 | D#5.6 R.2 |" +
                       "C5.2 E5.2 A5.4 | G#5.2 F#5.2 E5.4 | D5.2 E5.2 F#5.2 G#5.2 | A#5.6 R.2",
                Bass = "C2.4 F#2.4 | E2.4 A#1.4 | G#1.4 D2.4 | C2.4 C2.4 |" +
                       "E2.4 A#1.4 | G#1.4 D2.4 | F#2.4 C2.4 | C2.4 F#1.4 |" +
                       "A1.4 E2.4 | F1.4 C2.4 | A1.4 F1.4 | B1.4 B1.4 |" +
                       "A1.4 E2.4 | E2.4 G#1.4 | D2.4 F#2.4 | G#1.4 A#1.4",
                Drums = "k.......k...h...k.......k...h..." + "k.......k...h...k.......k.h.h.h." +
                        "k.......k...h...k.......k...h..." + "k.......k...h...k...k...k.h.s.h.",
            },
            new Song
            {
                Name = "village", Bpm = 92, SoftLead = true,
                // 별빛 마을: 삼각파로 느긋하게. A (사장조) → B (마단조로 살짝 저녁빛, 라 7화음으로 A 로 돌아간다)
                Lead = "G5.2 B5.2 D6.3 B5 | C6.2 A5.2 F#5.4 | G5.2 E5.2 D5.2 G5.2 | A5.6 R.2 |" +
                       "G5.2 B5.2 D6.3 E6 | D6.2 B5.2 C6.4 | B5.2 A5.2 F#5.2 A5.2 | G5.6 R.2 |" +
                       "E5.2 G5.2 B5.4 | A5.2 G5.2 F#5.4 | D5.2 F#5.2 A5.3 G5 | F#5.6 R.2 |" +
                       "E5.2 G5.2 C6.4 | B5.2 A5.2 G5.4 | A5.2 B5.2 C6.2 A5.2 | D5.6 R.2",
                Bass = "G2.2 D3.2 B2.2 D3.2 | C3.2 G3.2 D3.2 A3.2 | E3.2 B3.2 G2.2 D3.2 | D3.2 A3.2 D3.2 F#3.2 |" +
                       "G2.2 D3.2 B2.2 D3.2 | G2.2 D3.2 C3.2 G3.2 | D3.2 A3.2 D3.2 F#3.2 | G2.2 D3.2 G2.4 |" +
                       "E2.2 B2.2 E3.2 B2.2 | D3.2 A2.2 D3.2 A2.2 | D3.2 A3.2 F#3.2 A3.2 | B2.2 F#3.2 D3.2 F#3.2 |" +
                       "C3.2 G3.2 E3.2 G3.2 | G2.2 D3.2 B2.2 D3.2 | A2.2 E3.2 C3.2 E3.2 | D3.2 A3.2 D3.2 C3.2",
                Drums = "k..hk.h.k..hk.h.k..hk.h.k..hk.h.k..hk.h.k..hk.h.k..hk.h.k.h.s.h." +
                        "k..hk.h.k..hk.h.k..hk.h.k..hk.h.k..hk.h.k..hk.h.k..hk.h.k.hhs.s.",
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
            if (!s.NoEcho) Voice(buf, Parse(s.Lead), stepLen, rate, s.SoftLead ? 2 : 3, s.SoftLead ? .07f : .035f, stepLen / 2);   // 메아리
            Voice(buf, Parse(s.Bass), stepLen, rate, 1, .20f);
            Drums(buf, s.Drums, stepLen, rate);
            for (int i = 0; i < total; i++) buf[i] = Math.Max(-1f, Math.Min(1f, buf[i]));
            return buf;
        }

        /// <summary>
        /// wave: 0 사각파(25% 듀티), 1 삼각파(베이스), 2 삼각파+비브라토(부드러운 선율), 3 가는 사각파(12.5%, 메아리).
        /// offset 만큼 늦게 시작하고, 곡 끝을 넘친 소리는 처음으로 감아 넣어 반복 이음새가 끊기지 않게 한다.
        /// </summary>
        static void Voice(float[] buf, List<(double freq, int len)> notes, int stepLen, int rate, int wave, float vol, int offset = 0)
        {
            int pos = offset;
            foreach (var (f, len) in notes)
            {
                int n = len * stepLen;
                if (f > 0)
                {
                    int gate = (int)(n * .88f);                       // 음 사이에 살짝 틈 → 또박또박
                    double ph = 0;
                    for (int i = 0; i < n; i++)
                    {
                        double t = i / (double)rate;
                        double ff = wave == 2 ? f * (1 + .004 * Math.Sin(t * 2 * Math.PI * 5.5)) : f;
                        ph = (ph + ff / rate) % 1.0;
                        float s = wave == 0 ? (ph < .25 ? 1f : -1f) : wave == 3 ? (ph < .125 ? 1f : -1f) : (float)(1 - 4 * Math.Abs(ph - .5));
                        float env = i < rate * .004 ? i / (rate * .004f)                       // 어택
                                  : i < gate ? 1f - .25f * i / gate                            // 살짝 감쇠
                                  : Math.Max(0, .75f * (1 - (i - gate) / (float)(n - gate)));  // 릴리스
                        buf[(pos + i) % buf.Length] += s * env * vol;
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
