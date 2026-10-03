using GirlAdventure.Sim;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>Resources/Data/*.json 을 읽어 Sim.GameData 에 채운다. 에디터 테스트에서도 쓴다.</summary>
    public static class DataLoader
    {
        public static void Load(bool force = false)
        {
            if (GameData.Loaded && !force) return;
            Party.Set(Read<PartyTable>("party"));      // 동료 장비 부위를 알아야 items.json 장비를 고를 수 있어 GameData 보다 먼저
            GameData.Set(
                Read<EnemyTable>("enemies"),
                Read<SkillTable>("skills"),
                Read<DialogueTable>("dialogue"),
                Read<ItemTable>("items"),
                Read<EncounterTable>("encounters"));
            Npcs.Set(Read<NpcTable>("npcs"));
        }

        static T Read<T>(string name)
        {
            var ta = Resources.Load<TextAsset>("Data/" + name);
            if (ta == null) throw new System.IO.FileNotFoundException("Resources/Data/" + name + ".json 없음");
            return JsonUtility.FromJson<T>(ta.text);
        }
    }
}
