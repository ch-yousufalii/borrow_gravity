using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowGravity
{
    /// <summary>
    /// The settings screen (main menu and pause menu), in the transparent menu style, with just two options:
    /// CONTROLS (on-screen joystick or arrow buttons) and MUSIC (volume). Everything is saved on this device as it changes.
    /// Keyboard: W/S choose, A/D change, Esc / Enter closes. Mouse/touch: the arrows on each row, Save.
    /// </summary>
    public static class SettingsPanel
    {
        const int Rows = 2;
        static int selected;
        static readonly string[] Labels = { "CONTROLS", "MUSIC" };
        static readonly string[] Hints = { "On-screen joystick, or four arrow buttons.", "Background music volume." };

        static void Change(int i, int d)
        {
            if (i == 0) ControlSettings.Movement = ControlSettings.Movement == MoveStyle.Joystick ? MoveStyle.Arrows : MoveStyle.Joystick;
            else SoundSettings.Music += d * SoundSettings.Step;
            Sfx.Play(SfxId.Tick, 0.7f, 1.2f);
        }

        static string Value(int i) => i == 0
            ? (ControlSettings.Movement == MoveStyle.Joystick ? "JOYSTICK" : "BUTTONS")
            : Mathf.RoundToInt(SoundSettings.Music * 100f) + "%";

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

        /// <summary>Draws the panel (call inside OnGUI after UIKit.Begin). Returns true when Save was clicked.</summary>
        public static bool Draw(float w, float h)
        {
            var e = Event.current;
            const float rowH = 62f;
            float mw = Mathf.Min(w * 0.9f, 500f), mh = 22f + 18f + 20f + Rows * (rowH + 6f) + 16f + 36f + 22f;
            var r = new Rect((w - mw) * 0.5f, Mathf.Max(10f, (h - mh) * 0.5f), mw, mh);
            MenuStyle.Box(r, 1f, 1f);
            float x = r.x + 22f, inner = r.width - 44f, y = r.y + 22f;
            MenuStyle.Text(new Rect(x, y, inner, 18f), "SETTINGS", UIKit.Face.Display, 17, 1f, 0.3f, TextAnchor.UpperLeft);
            y += 18f + 20f;

            for (int i = 0; i < Rows; i++)
            {
                var row = new Rect(x, y, inner, rowH);
                bool hover = row.Contains(e.mousePosition);
                if (hover && (e.type == EventType.MouseMove || e.type == EventType.MouseDown)) selected = i;
                bool active = selected == i;
                MenuStyle.Box(row, active ? MenuStyle.W25 : hover ? MenuStyle.W12 : 0f, 1f);
                if (active) MenuStyle.Brackets(row);
                var left = new Rect(row.x + 10f, row.center.y - 16f, 32f, 32f);
                var right = new Rect(row.xMax - 42f, row.center.y - 16f, 32f, 32f);
                foreach (var (box, d) in new[] { (left, -1), (right, 1) })
                {
                    bool over = box.Contains(e.mousePosition);
                    if (over) UIKit.Fill(box, MenuStyle.White(MenuStyle.W12));
                    MenuStyle.Box(box, MenuStyle.W50, 1f);
                    MenuStyle.Text(box, d < 0 ? "‹" : "›", UIKit.Face.Heavy, 16, 1f, 0f, TextAnchor.MiddleCenter, -1f);
                    if (over && e.type == EventType.MouseDown) { e.Use(); selected = i; Change(i, d); }
                }
                var mid = new Rect(left.xMax + 8f, row.y + 7f, right.x - left.xMax - 16f, row.height - 14f);
                MenuStyle.Text(new Rect(mid.x, mid.y, mid.width, 11f), Labels[i], UIKit.Face.Heavy, 10, MenuStyle.W50, 0.3f, TextAnchor.UpperCenter);
                MenuStyle.Text(new Rect(mid.x, mid.y + 13f, mid.width, 20f), Value(i), UIKit.Face.Heavy, 16, 1f, 0.08f, TextAnchor.UpperCenter);
                if (i == 1)
                {
                    var bar = new Rect(mid.center.x - 100f, mid.y + 40f, 200f, 3f);
                    UIKit.Fill(bar, MenuStyle.White(MenuStyle.W25));
                    UIKit.Fill(new Rect(bar.x, bar.y, bar.width * SoundSettings.Music, bar.height), MenuStyle.White(1f));
                }
                else MenuStyle.Text(new Rect(mid.x, mid.y + 36f, mid.width, 14f), Hints[i], UIKit.Face.Body, 11, MenuStyle.W50, 0f, TextAnchor.UpperCenter);
                y += rowH + 6f;
            }

            var save = new Rect(r.xMax - 22f - 96f, r.yMax - 22f - 36f, 96f, 36f);
            return MenuStyle.Button(save, "Save", true);
        }
    }
}
