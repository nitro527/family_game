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
            MapKinds.Set(Read<MapKindTable>("mapkinds"));     // 맵 종류 표 (맵을 만들기 전에)
            Party.Set(Read<PartyTable>("party"));      // 동료 장비 부위를 알아야 items.json 장비를 고를 수 있어 GameData 보다 먼저
            GameData.Set(
                Read<EnemyTable>("enemies"),
                Read<SkillTable>("skills"),
                Read<DialogueTable>("dialogue"),
                Read<ItemTable>("items"),
                Read<EncounterTable>("encounters"));
            Npcs.Set(Read<NpcTable>("npcs"));
            Quests.Set(Read<QuestTable>("quests"));
            Chapters.Set(Read<ChapterTable>("chapters"));      // 챕터 표 (적 이름으로 보스를 찾으니 GameData 다음)
            PropCatalog.Set(Read<PropTable>("props"));     // 맵 소품 목록·맵별 테마 (MapPainter 가 읽는다)
            var stories = Read<StoryTable>("stories");     // 이야기 장면·부탁 수첩·가족 앨범 (대사는 대사 표에 합친다)
            Stories.Set(stories);
            GameData.AddTalk(stories.Talks);
        }

        static T Read<T>(string name)
        {
            var ta = Resources.Load<TextAsset>("Data/" + name);
            if (ta == null) throw new System.IO.FileNotFoundException("Resources/Data/" + name + ".json 없음");
            return JsonUtility.FromJson<T>(ta.text);
        }
    }
}
