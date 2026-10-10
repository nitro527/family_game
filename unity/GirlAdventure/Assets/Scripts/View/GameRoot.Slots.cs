using System.Collections;
using System.Collections.Generic;
using GirlAdventure.Sim;

namespace GirlAdventure.View
{
    /// <summary>타이틀의 세이브 칸 고르기 ("모험 책" 3권).</summary>
    public partial class GameRoot
    {
        int pickedSlot = -1;

        /// <summary>대화창에 질문을 띄우고 칸을 고르게 한다. 결과는 pickedSlot (그만두기 = -1).</summary>
        IEnumerator AskSlot(string talkId, bool onlyUsed)
        {
            ShowMessageWindow(true);
            SetFace("smile");
            msgText.text = GameData.Talk(talkId)[0].Text;
            var slots = new List<int>();
            var labels = new List<string>();
            for (int s = 0; s < SaveStore.Slots; s++)
            {
                if (onlyUsed && SaveStore.Peek(s) == null) continue;
                slots.Add(s);
                labels.Add(SaveStore.Label(s));
            }
            labels.Add("그만두기");
            // 기본 선택: 이어하기는 마지막 칸, 새로 시작은 첫 빈 칸 (없으면 마지막 칸)
            int def = onlyUsed ? SaveStore.LastSlot : FirstEmptySlot();
            choiceIndex = System.Math.Max(0, slots.IndexOf(def));
            yield return Choose(labels.ToArray());
            pickedSlot = choicePicked < slots.Count ? slots[choicePicked] : -1;
        }

        static int FirstEmptySlot()
        {
            for (int s = 0; s < SaveStore.Slots; s++)
                if (SaveStore.Peek(s) == null) return s;
            return SaveStore.LastSlot;
        }

        // ---- 프로브용
        public bool SlotPickerOpen => Current == Mode.Title && busy && choices.Visible;
        public int CurrentSlot => SaveStore.Slot;
        public void DebugSaveTo(int slot) { SaveStore.Slot = slot; AutoSave(); }
    }
}
