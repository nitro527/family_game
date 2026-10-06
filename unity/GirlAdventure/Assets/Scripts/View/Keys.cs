using System.Collections.Generic;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 입력 창구. 실제 키보드 + 게임패드 + 화면 터치 버튼 + 자동 플레이테스트의 가상 입력을 합쳐서 본다.
    /// 게임패드·터치는 "키보드 키를 누른 것처럼" 바꿔 넣는다 (방향 → 화살표, A 버튼 → Enter/Z).
    /// GameRoot.Update 첫머리에서 BeginFrame() 을 불러 "이번 프레임에 눌림" 을 확정한다.
    /// </summary>
    public static class Keys
    {
        static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();       // 플레이테스트 가상 입력
        static readonly HashSet<KeyCode> pending = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> downNow = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> touchHeld = new HashSet<KeyCode>();  // 화면 터치 버튼
        static readonly HashSet<KeyCode> devNow = new HashSet<KeyCode>();     // 패드 + 터치 (이번 프레임)
        static readonly HashSet<KeyCode> devPrev = new HashSet<KeyCode>();

        const float Dead = .5f;

        public static void BeginFrame()
        {
            downNow.Clear();
            foreach (var k in pending) downNow.Add(k);
            pending.Clear();

            devPrev.Clear();
            foreach (var k in devNow) devPrev.Add(k);
            devNow.Clear();
            ReadPad(devNow);
            foreach (var k in touchHeld) devNow.Add(k);
            foreach (var k in devNow) if (!devPrev.Contains(k)) downNow.Add(k);   // 새로 눌린 것만 "Down"
        }

        /// <summary>
        /// 게임패드: 왼쪽 스틱(기본 Horizontal/Vertical 축) 또는 십자키(PadDpadX/Y = Xbox 패드 6·7번째 축, 위가 +),
        /// A(0)/Start(7) 버튼 = 확인, Y(3)/Back(6) = 메뉴(Esc). 스틱을 기울이고 있으면 스틱이 먼저.
        /// </summary>
        static void ReadPad(HashSet<KeyCode> into)
        {
#if ENABLE_INPUT_SYSTEM
            if (ReadNewPad(into)) return;                // 새 입력 시스템이 패드를 알면 그쪽 (닌텐도 프로 컨트롤러 버튼 배치를 안다)
#endif
            float h = 0, v = 0;
            try { h = Input.GetAxisRaw("Horizontal"); v = Input.GetAxisRaw("Vertical"); }
            catch (System.ArgumentException) { }
            if (Mathf.Abs(h) <= Dead && Mathf.Abs(v) <= Dead)
            {
                try { h = Input.GetAxisRaw("PadDpadX"); v = Input.GetAxisRaw("PadDpadY"); }
                catch (System.ArgumentException) { }     // 축 설정이 없는 옛 프로젝트 설정이면 스틱만
            }
            // 한 번에 한 방향만 (대각선이면 더 크게 기운 쪽)
            if (Mathf.Abs(h) >= Mathf.Abs(v))
            {
                if (h > Dead) into.Add(KeyCode.RightArrow);
                else if (h < -Dead) into.Add(KeyCode.LeftArrow);
            }
            else
            {
                if (v > Dead) into.Add(KeyCode.UpArrow);
                else if (v < -Dead) into.Add(KeyCode.DownArrow);
            }
            if (Input.GetKey(KeyCode.JoystickButton0) || Input.GetKey(KeyCode.JoystickButton7))
            {
                into.Add(KeyCode.Return);
                into.Add(KeyCode.Z);
            }
            if (Input.GetKey(KeyCode.JoystickButton3) || Input.GetKey(KeyCode.JoystickButton6)) into.Add(KeyCode.Escape);
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>
        /// 게임패드 (새 입력 시스템, 사용자 2026-10-06 "닌텐도 패드로 되면 좋겠어"): 십자키·왼쪽 스틱 = 방향키.
        /// 닌텐도(프로 컨트롤러): A(오른쪽) = 확인(Enter·Z), B(아래) = 취소(X), X(위)·+ = 메뉴(Esc).
        /// 다른 패드(Xbox 등): 아래 = 확인, 오른쪽 = 취소, 위·Start = 메뉴. 패드가 없으면 false (예전 방식으로).
        /// </summary>
        static bool ReadNewPad(HashSet<KeyCode> into)
        {
            var g = UnityEngine.InputSystem.Gamepad.current;
            if (g == null) return false;
            Vector2 d = g.dpad.ReadValue(), s = g.leftStick.ReadValue();
            Vector2 v = d.sqrMagnitude > .25f ? d : s;
            if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) { if (v.x > Dead) into.Add(KeyCode.RightArrow); else if (v.x < -Dead) into.Add(KeyCode.LeftArrow); }
            else { if (v.y > Dead) into.Add(KeyCode.UpArrow); else if (v.y < -Dead) into.Add(KeyCode.DownArrow); }
            bool nintendo = IsNintendo(g);
            var confirm = nintendo ? g.buttonEast : g.buttonSouth;
            var cancel = nintendo ? g.buttonSouth : g.buttonEast;
            if (confirm.isPressed) { into.Add(KeyCode.Return); into.Add(KeyCode.Z); }
            if (cancel.isPressed) into.Add(KeyCode.X);
            if (g.buttonNorth.isPressed || g.startButton.isPressed) into.Add(KeyCode.Escape);
            return true;
        }

        /// <summary>닌텐도 패드인가 (프로 컨트롤러·닌텐도 제조사) — A/B 위치가 Xbox 와 반대.</summary>
        public static bool IsNintendo(UnityEngine.InputSystem.Gamepad g)
        {
            if (g == null) return false;
            string n = (g.GetType().Name + " " + g.displayName + " " + g.description.manufacturer + " " + g.description.product).ToLowerInvariant();
            return n.Contains("switch") || n.Contains("nintendo") || n.Contains("pro controller") || n.Contains("joy-con");
        }

        /// <summary>지금 연결된 패드 이름 (설정 화면 안내용, 없으면 "").</summary>
        public static string PadName => UnityEngine.InputSystem.Gamepad.current?.displayName ?? "";
#else
        public static string PadName => "";
#endif

        public static bool Held(KeyCode k) => Input.GetKey(k) || held.Contains(k) || devNow.Contains(k);
        public static bool Down(KeyCode k) => Input.GetKeyDown(k) || downNow.Contains(k);
        public static bool AnyDown => Input.anyKeyDown || downNow.Count > 0;

        // ---- 화면 터치 버튼 (TouchPad 가 부른다)
        public static void Touch(KeyCode k, bool on)
        {
            if (on) touchHeld.Add(k);
            else touchHeld.Remove(k);
        }

        // ---- 가상 입력 (플레이테스트용)
        public static void Press(KeyCode k) => pending.Add(k);

        public static void Hold(KeyCode k, bool on)
        {
            if (on) { if (held.Add(k)) pending.Add(k); }
            else held.Remove(k);
        }

        public static void ReleaseAll()
        {
            held.Clear();
            pending.Clear();
            touchHeld.Clear();
        }
    }
}
