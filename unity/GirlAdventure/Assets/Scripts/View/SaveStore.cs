using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 세이브 3칸을 PlayerPrefs 에 JSON 으로 보관한다 (키 save1~save3, 예전 1칸 세이브 = 1번 칸).
    /// Slot = 지금 플레이 중인 칸 (자동 저장이 여기에 쓴다), lastSlot = 마지막으로 저장한 칸.
    /// 안전장치 (PLAN_NEXT 77): 저장할 때마다 바로 전 내용을 "saveN.bak" 에 남기고, 본 세이브가 깨졌으면 .bak 에서 읽는다.
    /// 게임이 업데이트되어 세이브 버전이 오른 뒤 처음 저장할 때는 옛 버전 세이브를 "saveN.vK" 로 한 벌 따로 보관한다.
    /// </summary>
    public static class SaveStore
    {
        public const int Slots = 3;
        const string LastKey = "lastSlot";

        static int slot;
        public static int Slot { get => slot; set => slot = Mathf.Clamp(value, 0, Slots - 1); }

        static string Key(int s) => "save" + (s + 1);
        static string BakKey(int s) => Key(s) + ".bak";
        static string VersionKey(int s, int v) => Key(s) + ".v" + v;

        public static bool AnyExists => Count > 0;

        public static int Count
        {
            get
            {
                int n = 0;
                for (int s = 0; s < Slots; s++) if (Peek(s) != null) n++;
                return n;
            }
        }

        /// <summary>마지막으로 저장한 칸 (그 칸이 비었으면 첫 번째로 찬 칸, 다 비었으면 0).</summary>
        public static int LastSlot
        {
            get
            {
                int s = Mathf.Clamp(PlayerPrefs.GetInt(LastKey, 0), 0, Slots - 1);
                if (Peek(s) != null) return s;
                for (int i = 0; i < Slots; i++) if (Peek(i) != null) return i;
                return 0;
            }
        }

        /// <summary>마지막 칸의 내용 (없으면 null).</summary>
        public static SaveData Peek() => Peek(LastSlot);

        /// <summary>저장된 내용 (없으면 null). 본 세이브가 깨졌으면 바로 전 저장본(.bak)을 읽는다.</summary>
        public static SaveData Peek(int s)
        {
            string json = PlayerPrefs.GetString(Key(s), "");
            if (string.IsNullOrEmpty(json)) return null;
            var d = Parse(json);
            if (d != null) return d;
            Debug.LogWarning($"세이브 {s + 1}번이 깨져서 바로 전 저장본을 읽어요");
            return Parse(PlayerPrefs.GetString(BakKey(s), ""));
        }

        /// <summary>JSON → 세이브 (비었거나 깨졌거나 말이 안 되면 null).</summary>
        public static SaveData Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var d = JsonUtility.FromJson<SaveData>(json);
                return d != null && d.Version >= 1 && d.Lv >= 1 ? d : null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("세이브 읽기 실패: " + e.Message);
                return null;
            }
        }

        /// <summary>업데이트 전 옛 버전 세이브 (없으면 null) — 문제가 생기면 사람이 꺼내 볼 수 있게.</summary>
        public static SaveData PeekVersion(int s, int v) => Parse(PlayerPrefs.GetString(VersionKey(s, v), ""));
        public static SaveData PeekBackup(int s) => Parse(PlayerPrefs.GetString(BakKey(s), ""));

        /// <summary>선택창에 보일 칸 이름: "1. 서진 Lv 5 · 챕터 2 · 09-26 12:30" / "2. (비어 있음)".</summary>
        public static string Label(int s)
        {
            var d = Peek(s);
            if (d == null) return $"{s + 1}. <color=#9AA3C7>(비어 있음)</color>";
            return $"{s + 1}. {d.Name} Lv {d.Lv} <size=15><color=#9AA3C7>챕터 {Mathf.Max(1, d.Chapter)} · {d.SavedAt}</color></size>";
        }

        public static void Save(PlayerState p)
        {
            string old = PlayerPrefs.GetString(Key(Slot), "");
            var od = Parse(old);
            if (od != null)
            {
                PlayerPrefs.SetString(BakKey(Slot), old);                               // 바로 전 저장본
                if (od.Version < SaveData.CurrentVersion && !PlayerPrefs.HasKey(VersionKey(Slot, od.Version)))
                    PlayerPrefs.SetString(VersionKey(Slot, od.Version), old);           // 업데이트 전 옛 버전 (한 번만)
            }
            PlayerPrefs.SetString(Key(Slot), JsonUtility.ToJson(p.ToSave(System.DateTime.Now.ToString("MM-dd HH:mm"))));
            PlayerPrefs.SetInt(LastKey, Slot);
            PlayerPrefs.Save();
        }

        public static void Delete(int s)
        {
            DeleteAll(s);
            PlayerPrefs.Save();
        }

        /// <summary>그 칸의 세이브·바로 전 저장본·옛 버전 보관본을 모두 지운다 (새로 시작할 때 옛 모험이 되살아나지 않게).</summary>
        static void DeleteAll(int s)
        {
            PlayerPrefs.DeleteKey(Key(s));
            PlayerPrefs.DeleteKey(BakKey(s));
            for (int v = 1; v <= SaveData.CurrentVersion; v++) PlayerPrefs.DeleteKey(VersionKey(s, v));
        }

        /// <summary>모든 칸을 지운다 (플레이테스트용).</summary>
        public static void Delete()
        {
            for (int s = 0; s < Slots; s++) DeleteAll(s);
            PlayerPrefs.DeleteKey(LastKey);
            PlayerPrefs.Save();
        }
    }
}
