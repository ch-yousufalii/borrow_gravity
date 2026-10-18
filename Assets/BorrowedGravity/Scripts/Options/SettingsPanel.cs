using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowGravity
{
    /// <summary>
    /// The control settings screen (main menu and pause menu), in the transparent menu style: button size, joystick
    /// size, joystick sensitivity and camera sensitivity, with a live preview of the on-screen controls.
    /// Keyboard: W/S choose, A/D change, Esc / Enter on Back closes. Mouse/touch: arrows, Reset, Back.
    /// </summary>
    public static class SettingsPanel
    {
        const int Rows = 5;
        static int selected;
        static readonly string[] Labels = { "MOVEMENT", "BUTTON SIZE", "JOYSTICK SIZE", "JOYSTICK SENSITIVITY", "CAMERA SENSITIVITY" };
        static readonly string[] Hints =
        {
            "Floating joystick on the left half, or four arrow buttons.",
            "Size of the JUMP and POWER buttons.",
            "Size of the movement joystick.",
            "Higher = less drag needed for full speed.",
            "How fast finger drags and the mouse turn the camera.",
        };

        static float Get(int i) => i switch
        {
            1 => ControlSettings.ButtonSize,
            2 => ControlSettings.StickSize,
            3 => ControlSettings.StickSensitivity,
            _ => ControlSettings.LookSensitivity
        };

        static void Change(int i, int d)
        {
            switch (i)
            {
                case 0: ControlSettings.Movement = ControlSettings.Movement == MoveStyle.Joystick ? MoveStyle.Arrows : MoveStyle.Joystick; break;
                case 1: ControlSettings.ButtonSize += d * ControlSettings.SizeStep; break;
                case 2: ControlSettings.StickSize += d * ControlSettings.SizeStep; break;
                case 3: ControlSettings.StickSensitivity += d * ControlSettings.SensStep; break;
                default: ControlSettings.LookSensitivity += d * ControlSettings.SensStep; break;
            }
            Sfx.Play(SfxId.Tick, 0.6f, 1.2f);
        }

        static string Value(int i) => i == 0 ? (ControlSettings.Movement == MoveStyle.Joystick ? "JOYSTICK" : "ARROWS")
                                    : i <= 2 ? Mathf.RoundToInt(Get(i) * 100f) + "%" : Get(i).ToString("0.0") + "×";

        /// <summary>Keyboard handling; returns true when the panel should close.</summary>
        public static bool HandleKeys()
        {
            if (PlayerKeys.Pressed(Key.Escape) || PlayerKeys.Pressed(Key.Enter) || PlayerKeys.Pressed(Key.NumpadEnter)) return true;
            if (PlayerKeys.Pressed(Key.W) || PlayerKeys.Pressed(Key.UpArrow)) { selected = (selected + Rows - 1) % Rows; Sfx.Play(SfxId.Tick, 0.5f); }
            if (PlayerKeys.Pressed(Key.S) || PlayerKeys.Pressed(Key.DownArrow)) { selected = (selected + 1) % Rows; Sfx.Play(SfxId.Tick, 0.5f); }
            if (PlayerKeys.Pressed(Key.A) || PlayerKeys.Pressed(Key.LeftArrow)) Change(selected, -1);
            if (PlayerKeys.Pressed(Key.D) || PlayerKeys.Pressed(Key.RightArrow)) Change(selected, 1);
            return false;
        }

        /// <summary>Draws the panel (call inside OnGUI after UIKit.Begin). Returns true when Back was clicked.</summary>
        public static bool Draw(float w, float h)
        {
            var e = Event.current;
            float mw = Mathf.Min(w * 0.92f, 660f), mh = 620f;
            var r = new Rect((w - mw) * 0.5f, Mathf.Max(10f, (h - mh) * 0.5f), mw, mh);
            MenuStyle.Box(r, 1f, 1f);
            float x = r.x + 26f, inner = r.width - 52f, y = r.y + 26f;
            MenuStyle.Text(new Rect(x, y, inner, 22f), "SETTINGS", UIKit.Face.Display, 18, 1f, 0.3f, TextAnchor.UpperLeft);
            MenuStyle.Text(new Rect(x, y + 26f, inner, 16f), "On-screen controls · saved on this device", UIKit.Face.Body, 12, MenuStyle.W50, 0f, TextAnchor.UpperLeft);
            y += 26f + 16f + 16f;

            for (int i = 0; i < Rows; i++)
            {
                var row = new Rect(x, y, inner, 60f);
                bool hover = row.Contains(e.mousePosition);
                if (hover && e.type == EventType.MouseMove) selected = i;
                if (hover && e.type == EventType.MouseDown) selected = i;
                bool active = selected == i;
                MenuStyle.Box(row, active ? MenuStyle.W25 : hover ? MenuStyle.W12 : 0f, 1f);
                if (active) MenuStyle.Brackets(row);
                var left = new Rect(row.x + 12f, row.center.y - 16f, 32f, 32f);
                var right = new Rect(row.xMax - 44f, row.center.y - 16f, 32f, 32f);
                foreach (var (box, d) in new[] { (left, -1), (right, 1) })
                {
                    bool over = box.Contains(e.mousePosition);
                    if (over) UIKit.Fill(box, MenuStyle.White(MenuStyle.W12));
                    MenuStyle.Box(box, MenuStyle.W50, 1f);
                    MenuStyle.Text(box, d < 0 ? "‹" : "›", UIKit.Face.Heavy, 18, 1f, 0f, TextAnchor.MiddleCenter, -1f);
                    if (over && e.type == EventType.MouseDown) { e.Use(); selected = i; Change(i, d); }
                }
                var mid = new Rect(left.xMax + 10f, row.y + 9f, right.x - left.xMax - 20f, row.height - 18f);
                MenuStyle.Text(new Rect(mid.x, mid.y, mid.width, 12f), Labels[i], UIKit.Face.Heavy, 10, MenuStyle.W50, 0.3f, TextAnchor.UpperCenter);
                MenuStyle.Text(new Rect(mid.x, mid.y + 13f, mid.width, 22f), Value(i), UIKit.Face.Heavy, 17, 1f, 0.08f, TextAnchor.UpperCenter);
                MenuStyle.Text(new Rect(mid.x, mid.y + 33f, mid.width, 14f), Hints[i], UIKit.Face.Body, 11, MenuStyle.W50, 0f, TextAnchor.UpperCenter);
                y += 60f + 5f;
            }

            // live preview of the on-screen controls at the chosen sizes
            y += 6f;
            MenuStyle.Text(new Rect(x, y, inner, 12f), "PREVIEW", UIKit.Face.Heavy, 10, MenuStyle.W50, 0.3f, TextAnchor.UpperLeft);
            float py = y + 70f;
            float sr = 44f * ControlSettings.StickSize;
            var stickC = new Vector2(x + 30f + sr, py);
            if (ControlSettings.Movement == MoveStyle.Arrows)
            {
                float ar = sr * 0.42f;
                var dirs = new[] { new Vector2(0f, -1f), new Vector2(0f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f) };
                for (int i = 0; i < 4; i++)
                {
                    var c = stickC + dirs[i] * sr * 0.8f;
                    var rr = new Rect(c.x - ar, c.y - ar, ar * 2f, ar * 2f);
                    MenuStyle.Box(rr, MenuStyle.W50, 1f, ar);
                    VirtualControls.Chevron(c, i, ar * 0.55f, MenuStyle.W80);
                }
                MenuStyle.Text(new Rect(stickC.x + sr + 12f, py - 9f, 220f, 18f), "arrow buttons", UIKit.Face.Body, 11, MenuStyle.W50, 0f, TextAnchor.MiddleLeft);
            }
            else
            {
                MenuStyle.Box(new Rect(stickC.x - sr, stickC.y - sr, sr * 2f, sr * 2f), MenuStyle.W50, 1f, sr);
                // inner ring: how far you drag for full speed
                float full = sr * Mathf.Clamp01(ControlSettings.DeadZone + (1f - ControlSettings.DeadZone) / ControlSettings.StickSensitivity);
                MenuStyle.Box(new Rect(stickC.x - full, stickC.y - full, full * 2f, full * 2f), MenuStyle.W25, 1f, full);
                float knob = 16f * ControlSettings.StickSize;
                MenuStyle.Box(new Rect(stickC.x - knob, stickC.y - knob, knob * 2f, knob * 2f), 0.85f, 0f, knob);
                MenuStyle.Text(new Rect(stickC.x + sr + 12f, py - 9f, 220f, 18f), "full speed at the inner ring", UIKit.Face.Body, 11, MenuStyle.W50, 0f, TextAnchor.MiddleLeft);
            }
            float br = 30f * ControlSettings.ButtonSize;
            var jumpC = new Vector2(r.xMax - 26f - br, py + 8f);
            var powC = new Vector2(jumpC.x - br * 2f - 14f, py - 18f);
            foreach (var (c, label) in new[] { (jumpC, "JUMP"), (powC, "POWER") })
            {
                MenuStyle.Box(new Rect(c.x - br, c.y - br, br * 2f, br * 2f), MenuStyle.W50, 1f, br);
                MenuStyle.Text(new Rect(c.x - br, c.y - br, br * 2f, br * 2f), label, UIKit.Face.Heavy, Mathf.RoundToInt(10 * Mathf.Clamp(ControlSettings.ButtonSize, 0.8f, 1.3f)), 1f, 0.15f);
            }

            var back = new Rect(r.xMax - 26f - 96f, r.yMax - 26f - 34f, 96f, 34f);
            var reset = new Rect(back.x - 10f - 96f, back.y, 96f, 34f);
            if (MenuStyle.Button(reset, "Reset")) { ControlSettings.ResetToDefaults(); Sfx.Play(SfxId.Plate, 0.5f); }
            return MenuStyle.Button(back, "Back", true);
        }
    }
}
