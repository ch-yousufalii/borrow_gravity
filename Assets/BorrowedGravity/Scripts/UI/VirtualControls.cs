using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowGravity
{
    /// <summary>
    /// On-screen joystick + JUMP + POWER for each player, in that player's half of the split screen.
    /// Works with multi-touch and the mouse; feeds <see cref="VirtualInput"/>. J toggles it.
    /// </summary>
    public class VirtualControls : MonoBehaviour
    {
        public static bool Visible = true;

        public HudIcons icons;
        public float stickRadius = 70f;
        public float buttonRadius = 46f;
        [Tooltip("Camera turn per reference-pixel of finger drag (degrees).")]
        public float dragLookSpeed = 0.28f;
        [Tooltip("Camera turn per screen pixel of mouse movement (degrees).")]
        public float mouseLookSpeed = 0.12f;

        class Stick { public int pointer = int.MinValue; public Vector2 center, knob, input; }
        class Button { public Vector2 center; public bool held; }

        readonly Stick[] sticks = { new Stick(), new Stick() };
        readonly Button[] jumps = { new Button(), new Button() };
        readonly Button[] powers = { new Button(), new Button() };
        readonly List<(int id, Vector2 pos, bool began)> pointers = new List<(int, Vector2, bool)>();
        bool mouseWasDown;
        readonly Dictionary<int, (Vector2 last, int player)> lookPointers = new Dictionary<int, (Vector2, int)>();
        readonly HashSet<int> alive = new HashSet<int>();

        /// <summary>Online each phone shows only its own player's controls, across the whole screen.</summary>
        static bool Shows(int p) => !NetSession.Online || p == NetSession.LocalPlayer;

        // sizes chosen in Settings (ControlSettings)
        float SR => stickRadius * ControlSettings.StickSize;
        float BR => buttonRadius * ControlSettings.ButtonSize;

        void Layout()
        {
            float w = UIKit.Width, h = UIKit.Height, half = NetSession.Online ? w : w * 0.5f;
            float sr = SR, br = BR;
            float y = h - 80f - sr;
            for (int p = 0; p < 2; p++)
            {
                float x0 = NetSession.Online ? 0f : p * half;
                sticks[p].center = new Vector2(x0 + 50f + sr, y);
                jumps[p].center = new Vector2(x0 + half - 44f - br, h - 112f - br * 0.4f);
                powers[p].center = new Vector2(jumps[p].center.x - br * 2f - 18f, jumps[p].center.y - 40f * ControlSettings.ButtonSize);
            }
        }

        void Update()
        {
            if (PlayerKeys.Pressed(Key.J)) Visible = !Visible;
            float s = Screen.height / UIKit.ReferenceHeight;
            if (s <= 0f) return;
            Layout();
            GatherPointers(s);
            Look(s);
            if (!Visible)
            {
                for (int p = 0; p < 2; p++) if (Shows(p)) VirtualInput.Set(p, Vector2.zero, false, false);
                return;
            }

            for (int p = 0; p < 2; p++)
            {
                if (!Shows(p)) continue; // the other player's input arrives over the network
                var st = sticks[p];
                // keep the captured pointer, or capture a new press inside the stick area
                Vector2? pos = null;
                foreach (var ptr in pointers)
                {
                    if (ptr.id == st.pointer) { pos = ptr.pos; break; }
                    if (st.pointer == int.MinValue && ptr.began && Vector2.Distance(ptr.pos, st.center) < SR * 1.6f)
                    {
                        st.pointer = ptr.id;
                        pos = ptr.pos;
                        break;
                    }
                }
                if (pos == null) { st.pointer = int.MinValue; st.knob = Vector2.zero; st.input = Vector2.zero; }
                else
                {
                    Vector2 raw = (pos.Value - st.center) / SR;
                    st.knob = Vector2.ClampMagnitude(raw, 1f);           // where the knob is drawn
                    st.input = ControlSettings.ApplyStick(raw);          // dead zone + sensitivity
                }

                jumps[p].held = Touching(jumps[p].center, st.pointer);
                powers[p].held = Touching(powers[p].center, st.pointer);
                // GUI y points down, the game's "forward" is up on the stick
                VirtualInput.Set(p, new Vector2(st.input.x, -st.input.y), jumps[p].held, powers[p].held);
            }
        }


        // ---------- camera look: drag a finger (or the mouse) anywhere that isn't a control; or just move the mouse ----------

        int PlayerAt(float x) => NetSession.Online ? NetSession.LocalPlayer : (x < UIKit.Width * 0.5f ? 0 : 1);

        bool OnControl(Vector2 pos)
        {
            if (pos.y < 110f) return true; // power panels, objective banner, pause button
            if (!Visible) return false;
            for (int p = 0; p < 2; p++)
            {
                if (!Shows(p)) continue;
                if (Vector2.Distance(pos, sticks[p].center) < SR * 1.6f) return true;
                if (Vector2.Distance(pos, jumps[p].center) < BR * 1.2f) return true;
                if (Vector2.Distance(pos, powers[p].center) < BR * 1.2f) return true;
            }
            return false;
        }

        void Look(float scale)
        {
            var sm = StageManager.Instance;
            bool playing = sm != null && sm.isActiveAndEnabled && sm.State == StageState.Playing;
            alive.Clear();
            foreach (var ptr in pointers)
            {
                alive.Add(ptr.id);
                if (lookPointers.TryGetValue(ptr.id, out var lp))
                {
                    Vector2 d = ptr.pos - lp.last;
                    if (playing) VirtualInput.AddLook(lp.player, new Vector2(d.x, d.y) * dragLookSpeed * ControlSettings.LookSensitivity);
                    lookPointers[ptr.id] = (ptr.pos, lp.player);
                }
                else if (ptr.began && !OnControl(ptr.pos)) lookPointers[ptr.id] = (ptr.pos, PlayerAt(ptr.pos.x));
            }
            if (lookPointers.Count > 0)
            {
                var gone = new List<int>();
                foreach (var id in lookPointers.Keys) if (!alive.Contains(id)) gone.Add(id);
                foreach (var id in gone) lookPointers.Remove(id);
            }

            // laptop: moving the mouse turns the camera of the player whose half it is over (not while a button is held)
            var mouse = Mouse.current;
            if (!playing || Application.isMobilePlatform || mouse == null || pointers.Count > 0) return;
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed) return;
            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude < 0.01f) return;
            Vector2 sp = mouse.position.ReadValue();
            if (sp.x < 0f || sp.y < 0f || sp.x > Screen.width || sp.y > Screen.height) return;
            VirtualInput.AddLook(PlayerAt(sp.x / scale), new Vector2(delta.x, -delta.y) * mouseLookSpeed * ControlSettings.LookSensitivity);
        }

        bool Touching(Vector2 center, int ignorePointer)
        {
            foreach (var ptr in pointers)
                if (ptr.id != ignorePointer && Vector2.Distance(ptr.pos, center) < BR * 1.2f) return true;
            return false;
        }

        void GatherPointers(float s)
        {
            pointers.Clear();
            var ts = Touchscreen.current;
            if (ts != null)
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed) continue;
                    var sp = t.position.ReadValue();
                    pointers.Add((t.touchId.ReadValue(), new Vector2(sp.x / s, (Screen.height - sp.y) / s), t.press.wasPressedThisFrame));
                }
            // some phones also report a finger as a mouse: only use the mouse when no finger is down
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed && pointers.Count == 0)
            {
                var sp = mouse.position.ReadValue();
                pointers.Add((-1, new Vector2(sp.x / s, (Screen.height - sp.y) / s), !mouseWasDown));
            }
            mouseWasDown = mouse != null && mouse.leftButton.isPressed;
        }

        void OnGUI()
        {
            if (!Visible) return;
            var sm = StageManager.Instance;
            if (sm != null && sm.State != StageState.Playing) return;
            UIKit.Begin();
            Layout();
            for (int p = 0; p < 2; p++)
            {
                if (!Shows(p)) continue;
                Color c = p == 0 ? UIKit.Sun : UIKit.Glow;
                var st = sticks[p];
                float d = SR * 2f;
                UIKit.Dot(st.center, d, new Color(UIKit.Void.r, UIKit.Void.g, UIKit.Void.b, 0.35f));
                UIKit.Tex(new Rect(st.center.x - SR, st.center.y - SR, d, d), icons?.Ring_Track, new Color(1, 1, 1, 0.7f));
                var knob = st.center + st.knob * SR;
                UIKit.Dot(knob, 54f * ControlSettings.StickSize, new Color(c.r, c.g, c.b, st.pointer != int.MinValue ? 0.9f : 0.55f));
                UIKit.Dot(knob, 30f * ControlSettings.StickSize, new Color(1, 1, 1, 0.35f));
                UIKit.Text(new Rect(st.center.x - 60, st.center.y + SR + 4, 120, 18), UIKit.Caps(p == 0 ? "Player A" : "Player B"), UIKit.Style(UIKit.Face.Display, 10, c, TextAnchor.MiddleCenter));
                DrawButton(jumps[p], c, "Jump", null);
                DrawButton(powers[p], c, p == 0 ? "Gravity" : "Nullify", p == 0 ? icons?.Power_A_GravityController : icons?.Power_B_GravityNullifier);
            }
        }

        void DrawButton(Button b, Color c, string label, Texture icon)
        {
            float d = BR * 2f;
            UIKit.Dot(b.center, d, new Color(UIKit.Void.r, UIKit.Void.g, UIKit.Void.b, b.held ? 0.7f : 0.4f));
            UIKit.Tex(new Rect(b.center.x - BR, b.center.y - BR, d, d), icons?.Ring_Track, b.held ? c : new Color(1, 1, 1, 0.7f));
            if (icon != null) UIKit.Tex(new Rect(b.center.x - BR * 0.62f, b.center.y - BR * 0.62f, d * 0.62f, d * 0.62f), icon);
            else UIKit.Text(new Rect(b.center.x - 40, b.center.y - 12, 80, 24), UIKit.Caps(label), UIKit.Style(UIKit.Face.Display, 13, b.held ? c : UIKit.Ink, TextAnchor.MiddleCenter));
            if (icon != null) UIKit.Text(new Rect(b.center.x - 50, b.center.y + BR + 2, 100, 16), UIKit.Caps(label), UIKit.Style(UIKit.Face.Display, 10, c, TextAnchor.MiddleCenter));
        }
    }
}
