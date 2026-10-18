using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowGravity
{
    /// <summary>
    /// The settings screen (main menu and pause menu), in the transparent menu style, with two tabs:
    /// CONTROLS (movement style, button size, joystick size and sensitivity, camera sensitivity, with a live preview
    /// of the on-screen controls) and SOUND (master, effects and music volume).
    /// Keyboard: Tab / Q / E switch tab, W/S choose, A/D change, Esc / Enter closes. Mouse/touch: tabs, arrows, Reset, Back.
    /// </summary>
    public static class SettingsPanel
    {
        static int selected, tab; // tab 0 = controls, 1 = sound
        static int Rows => tab == 0 ? 5 : 3;
        static readonly string[] ControlLabels = { "MOVEMENT", "BUTTON SIZE", "JOYSTICK SIZE", "JOYSTICK SENSITIVITY", "CAMERA SENSITIVITY" };
        static readonly string[] ControlHints =
        {
            "Floating joystick on the left half, or four arrow buttons.",
            "Size of the JUMP and POWER buttons.",
            "Size of the movement joystick.",
            "Higher = less drag needed for full speed.",
            "How fast finger drags and the mouse turn the camera.",
        };
        static readonly string[] SoundLabels = { "MASTER VOLUME", "SOUND EFFECTS", "MUSIC" };
        static readonly string[] SoundHints = { "Everything at once.", "Jumps, powers, doors, alarms…", "The background music." };
        static string Label(int i) => tab == 0 ? ControlLabels[i] : SoundLabels[i];
        static string Hint(int i) => tab == 0 ? ControlHints[i] : SoundHints[i];

        static float Volume(int i) => i == 0 ? SoundSettings.Master : i == 1 ? SoundSettings.Effects : SoundSettings.Music;

        static void SwitchTab()
        {
            tab = 1 - tab;
            selected = 0;
            Sfx.Play(SfxId.Tick, 0.5f);
        }

        static float Get(int i) => i switch
        {
            1 => ControlSettings.ButtonSize,
            2 => ControlSettings.StickSize,
            3 => ControlSettings.StickSensitivity,
            _ => ControlSettings.LookSensitivity
        };

        static void Change(int i, int d)
        {
            if (tab == 1)
            {
                float v = Volume(i) + d * SoundSettings.Step;
                if (i == 0) SoundSettings.Master = v; else if (i == 1) SoundSettings.Effects = v; else SoundSettings.Music = v;
                Sfx.Play(SfxId.Tick, 0.8f, 1.2f); // at the new level, so you hear it
                return;
            }
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

        static string Value(int i) => tab == 1 ? Mathf.RoundToInt(Volume(i) * 100f) + "%" : i == 0 ? (ControlSettings.Movement == MoveStyle.Joystick ? "JOYSTICK" : "ARROWS")
                                    : i <= 2 ? Mathf.RoundToInt(Get(i) * 100f) + "%" : Get(i).ToString("0.0") + "×";

        /// <summary>Keyboard handling; returns true when the panel should close.</summary>
        public static bool HandleKeys()
        {
            if (PlayerKeys.Pressed(Key.Escape) || PlayerKeys.Pressed(Key.Enter) || PlayerKeys.Pressed(Key.NumpadEnter)) return true;
            if (PlayerKeys.Pressed(Key.Tab) || PlayerKeys.Pressed(Key.Q) || PlayerKeys.Pressed(Key.E)) SwitchTab();
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
            float mw = Mathf.Min(w * 0.92f, 660f), mh = tab == 0 ? 660f : 420f;
            if (selected >= Rows) selected = 0;
            var r = new Rect((w - mw) * 0.5f, Mathf.Max(10f, (h - mh) * 0.5f), mw, mh);
            MenuStyle.Box(r, 1f, 1f);
            float x = r.x + 26f, inner = r.width - 52f, y = r.y + 26f;
            MenuStyle.Text(new Rect(x, y, inner, 22f), "SETTINGS", UIKit.Face.Display, 18, 1f, 0.3f, TextAnchor.UpperLeft);
            MenuStyle.Text(new Rect(x, y + 26f, inner, 16f), "Saved on this device", UIKit.Face.Body, 12, MenuStyle.W50, 0f, TextAnchor.UpperLeft);
            // tabs (top right): CONTROLS | SOUND
            for (int t = 0; t < 2; t++)
            {
                var tr = new Rect(r.xMax - 26f - (2 - t) * 112f + 8f, y, 104f, 30f);
                bool on = tab == t, over = tr.Contains(e.mousePosition);
                if (on) UIKit.Fill(tr, MenuStyle.White(MenuStyle.W12));
                MenuStyle.Box(tr, on ? 1f : over ? MenuStyle.W50 : MenuStyle.W25, 1f, 15f);
                MenuStyle.Text(tr, t == 0 ? "CONTROLS" : "SOUND", UIKit.Face.Heavy, 11, on ? 1f : MenuStyle.W80, 0.2f);
                if (over && e.type == EventType.MouseDown && !on) { e.Use(); SwitchTab(); }
            }
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
                MenuStyle.Text(new Rect(mid.x, mid.y, mid.width, 12f), Label(i), UIKit.Face.Heavy, 10, MenuStyle.W50, 0.3f, TextAnchor.UpperCenter);
                MenuStyle.Text(new Rect(mid.x, mid.y + 13f, mid.width, 22f), Value(i), UIKit.Face.Heavy, 17, 1f, 0.08f, TextAnchor.UpperCenter);
                if (tab == 1)
                {
                    // level bar instead of a hint
                    var bar = new Rect(mid.center.x - 120f, mid.y + 38f, 240f, 4f);
                    UIKit.Fill(bar, MenuStyle.White(MenuStyle.W25));
                    UIKit.Fill(new Rect(bar.x, bar.y, bar.width * Volume(i), bar.height), MenuStyle.White(1f));
                }
                else MenuStyle.Text(new Rect(mid.x, mid.y + 33f, mid.width, 14f), Hint(i), UIKit.Face.Body, 11, MenuStyle.W50, 0f, TextAnchor.UpperCenter);
                y += 60f + 5f;
            }

            var back = new Rect(r.xMax - 26f - 96f, r.yMax - 26f - 34f, 96f, 34f);
            var reset = new Rect(back.x - 10f - 96f, back.y, 96f, 34f);
            if (tab == 1)
            {
                MenuStyle.Text(new Rect(x, back.y, inner - 220f, 34f), "Tab / Q / E: switch tab", UIKit.Face.Body, 11, MenuStyle.W50, 0f, TextAnchor.MiddleLeft);
                if (MenuStyle.Button(reset, "Reset")) { SoundSettings.ResetToDefaults(); Sfx.Play(SfxId.Plate, 0.5f); }
                return MenuStyle.Button(back, "Back", true);
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

            if (MenuStyle.Button(reset, "Reset")) { ControlSettings.ResetToDefaults(); Sfx.Play(SfxId.Plate, 0.5f); }
            return MenuStyle.Button(back, "Back", true);
        }
    }
}
