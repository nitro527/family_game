using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GirlAdventure.View
{
    /// <summary>
    /// 태블릿용 화면 버튼: 왼쪽 아래 십자 버튼, 오른쪽 아래 A 버튼, 그 위 메뉴(상태 창) 버튼.
    /// 누르고 있는 동안 Keys 에 해당 키를 넣는다. 필드에서 대화창이 없을 때만 보인다
    /// (전투 명령창과 대화창은 원래 터치로 누를 수 있다).
    /// </summary>
    public partial class GameRoot
    {
        GameObject touchPad;
        /// <summary>터치 화면이 아니어도 버튼을 보이게 (PC 확인·플레이테스트용).</summary>
        public bool ForceTouchPad;

        void BuildTouchPad()
        {
            touchPad = UIKit.Stretch(ui, "TouchPad").gameObject;
            var t = touchPad.transform;
            const float s = 84, x0 = 22, y0 = 540 - 16 - 3 * s;
            PadButton(t, "Up", "▲", x0 + s, y0, s, KeyCode.UpArrow);
            PadButton(t, "Left", "◀", x0, y0 + s, s, KeyCode.LeftArrow);
            PadButton(t, "Right", "▶", x0 + 2 * s, y0 + s, s, KeyCode.RightArrow);
            PadButton(t, "Down", "▼", x0 + s, y0 + 2 * s, s, KeyCode.DownArrow);
            PadButton(t, "A", "A", 960 - 22 - 110, 540 - 30 - 110, 110, KeyCode.Return, KeyCode.Z);
            PadButton(t, "Menu", "메뉴", 960 - 22 - 84, 540 - 30 - 110 - 92, 84, KeyCode.Escape);
            touchPad.SetActive(false);
        }

        void PadButton(Transform parent, string name, string label, float x, float y, float size, params KeyCode[] keys)
        {
            var img = UIKit.Window(parent, name, x + 4, y + 4, size - 8, size - 8);
            img.color = new Color(1, 1, 1, .45f);
            UIKit.Label(img.transform, "Label", label, 0, 0, size - 8, size - 8, (int)(size * .38f), TextAnchor.MiddleCenter, Color.white, true);
            var trig = img.gameObject.AddComponent<EventTrigger>();
            void On(EventTriggerType type, bool down)
            {
                var e = new EventTrigger.Entry { eventID = type };
                e.callback.AddListener(_ =>
                {
                    foreach (var k in keys) Keys.Touch(k, down);
                    img.color = new Color(1, 1, 1, down ? .8f : .45f);
                });
                trig.triggers.Add(e);
            }
            On(EventTriggerType.PointerDown, true);
            On(EventTriggerType.PointerUp, false);
            On(EventTriggerType.PointerExit, false);
        }

        void TickTouchPad()
        {
            bool show = (Input.touchSupported || ForceTouchPad) && Current == Mode.Field && !msgWin.gameObject.activeSelf && !statusOpen;
            if (touchPad.activeSelf == show) return;
            touchPad.SetActive(show);
            if (!show)
                foreach (var k in new[] { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.Return, KeyCode.Z, KeyCode.Escape })
                    Keys.Touch(k, false);       // 숨길 때 눌린 채로 남지 않게
        }

        /// <summary>플레이테스트용: 이름으로 터치 버튼 찾기 (Up/Down/Left/Right/A).</summary>
        public GameObject TouchButton(string name) => touchPad.transform.Find(name)?.gameObject;
        public bool TouchPadVisible => touchPad.activeSelf;
    }
}
