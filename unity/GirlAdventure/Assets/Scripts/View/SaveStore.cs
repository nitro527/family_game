using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 세이브 3칸을 PlayerPrefs 에 JSON 으로 보관한다 (키 save1~save3, 예전 1칸 세이브 = 1번 칸).
    /// Slot = 지금 플레이 중인 칸 (자동 저장이 여기에 쓴다), lastSlot = 마지막으로 저장한 칸.
    /// </summary>
    public static class SaveStore
    {
        public const int Slots = 3;
        const string LastKey = "lastSlot";

        static int slot;
        public static int Slot { get => slot; set => slot = Mathf.Clamp(value, 0, Slots - 1); }

        static string Key(int s) => "save" + (s + 1);

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

        /// <summary>저장된 내용 (없거나 깨졌으면 null).</summary>
        public static SaveData Peek(int s)
        {
            string json = PlayerPrefs.GetString(Key(s), "");
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

        /// <summary>선택창에 보일 칸 이름: "1. 첫째 Lv 5 · 챕터 2 · 09-26 12:30" / "2. (비어 있음)".</summary>
        public static string Label(int s)
        {
            var d = Peek(s);
            if (d == null) return $"{s + 1}. <color=#9AA3C7>(비어 있음)</color>";
            return $"{s + 1}. {d.Name} Lv {d.Lv} <size=15><color=#9AA3C7>챕터 {Mathf.Max(1, d.Chapter)} · {d.SavedAt}</color></size>";
        }

        public static void Save(PlayerState p)
        {
            PlayerPrefs.SetString(Key(Slot), JsonUtility.ToJson(p.ToSave(System.DateTime.Now.ToString("MM-dd HH:mm"))));
            PlayerPrefs.SetInt(LastKey, Slot);
            PlayerPrefs.Save();
        }

        public static void Delete(int s)
        {
            PlayerPrefs.DeleteKey(Key(s));
            PlayerPrefs.Save();
        }

        /// <summary>모든 칸을 지운다 (플레이테스트용).</summary>
        public static void Delete()
        {
            for (int s = 0; s < Slots; s++) PlayerPrefs.DeleteKey(Key(s));
            PlayerPrefs.DeleteKey(LastKey);
            PlayerPrefs.Save();
        }
    }
}
