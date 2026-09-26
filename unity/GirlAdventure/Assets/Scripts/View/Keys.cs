using System.Collections.Generic;
using UnityEngine;

namespace GirlAdventure.View
{
    /// <summary>
    /// 입력 창구. 실제 키보드 + 자동 플레이테스트가 주입하는 가상 입력을 합쳐서 본다.
    /// GameRoot.Update 첫머리에서 BeginFrame() 을 불러 "이번 프레임에 눌림" 을 확정한다.
    /// </summary>
    public static class Keys
    {
        static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> pending = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> downNow = new HashSet<KeyCode>();

        public static void BeginFrame()
        {
            downNow.Clear();
            foreach (var k in pending) downNow.Add(k);
            pending.Clear();
        }

        public static bool Held(KeyCode k) => Input.GetKey(k) || held.Contains(k);
        public static bool Down(KeyCode k) => Input.GetKeyDown(k) || downNow.Contains(k);
        public static bool AnyDown => Input.anyKeyDown || downNow.Count > 0;

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
        }
    }
}
