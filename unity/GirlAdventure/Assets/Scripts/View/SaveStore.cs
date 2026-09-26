using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>세이브 1칸을 PlayerPrefs 에 JSON 으로 보관한다.</summary>
    public static class SaveStore
    {
        const string Key = "save1";

        public static bool Exists => Peek() != null;

        /// <summary>저장된 내용 (없거나 깨졌으면 null).</summary>
        public static SaveData Peek()
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var s = JsonUtility.FromJson<SaveData>(json);
                return s != null && s.Version >= 1 && s.Lv >= 1 ? s : null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("세이브 읽기 실패: " + e.Message);
                return null;
            }
        }

        public static void Save(PlayerState p)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(p.ToSave(System.DateTime.Now.ToString("MM-dd HH:mm"))));
            PlayerPrefs.Save();
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
