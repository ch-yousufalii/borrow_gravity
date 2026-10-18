using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// In-game HUD, built only from the art kit's HUD design (<see cref="HudIcons"/> + <see cref="UIKit"/>):
    /// power rings for A (sun) and B (glow), objective banner, gravity indicator, danger banner with countdown ring,
    /// checkpoint banner, engine meter + tilt gauge, player tags / off-screen arrows, and the story / pause /
    /// stage-complete screens. Other scripts talk to it through the static methods.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        static GameUI instance;

        public HudIcons icons;
        public PlayerController playerA, playerB;
        public GravityPower gravityPower;
        public NullifyPower nullifyPower;
        public string controlsA = "WASD / arrows move · mouse or drag to look · Space jump · E switch zone gravity";
        public string controlsB = "WASD / arrows move · mouse or drag to look · Space jump · E tap: nullify, hold: steer";

        class Timed { public string text; public float until; public HudHazard hazard; public float countdownEnd; public float start; }
        Timed warning, checkpoint, message, hint;
        readonly Dictionary<PlayerController, Timed> toasts = new Dictionary<PlayerController, Timed>();
        string countdownLabel;
        Func<float> countdownValue;
        HudObjective objective = HudObjective.ReachStation;
        string objectiveText = "";
        string sectionTitle = "";
        float sectionUntil;
        bool showHelp = false;

        void Awake() => instance = this;

        // ---------- static API ----------

        /// <summary>Seconds left on the running countdown, or -1 (streamed to the joining phone).</summary>
        public static float CountdownLeft => instance != null && instance.countdownValue != null ? Mathf.Max(0f, instance.countdownValue()) : -1f;

        public static void ShowWarning(HudHazard hazard, string text, float countdown, float duration)
        {
            NetSession.UiWarning(hazard, text, countdown, duration);
            if (instance == null) return;
            instance.warning = new Timed { text = text, hazard = hazard, start = Time.time, until = Time.time + Mathf.Max(duration, countdown), countdownEnd = countdown > 0f ? Time.time + countdown : 0f };
        }

        public static void ShowCheckpoint(string label)
        {
            NetSession.UiText(1, label);
            if (instance != null) instance.checkpoint = new Timed { text = label, start = Time.time, until = Time.time + 2.6f };
        }

        public static void ShowMessage(string text, float duration)
        {
            NetSession.UiText(2, text, duration);
            if (instance != null) instance.message = new Timed { text = text, start = Time.time, until = Time.time + duration };
        }

        public static void ShowHint(string text, float duration)
        {
            NetSession.UiText(3, text, duration);
            if (instance != null) instance.hint = new Timed { text = text, start = Time.time, until = Time.time + duration };
        }

        public static void Toast(PlayerController p, string text)
        {
            if (p != null) NetSession.UiToast(p.playerIndex, text);
            if (instance != null && p != null) instance.toasts[p] = new Timed { text = text, until = Time.time + 1.8f };
        }

        public static void SetObjective(HudObjective o, string text)
        {
            NetSession.UiObjective(o, text);
            if (instance == null) return;
            instance.objective = o;
            instance.objectiveText = text;
        }

        public static void SetSection(string title)
        {
            NetSession.UiText(6, title);
            if (instance == null) return;
            instance.sectionTitle = title;
            instance.sectionUntil = Time.time + 5f;
        }

        public static void StartCountdown(string label, HudHazard hazard, Func<float> secondsLeft)
        {
            NetSession.UiCountdown(label, hazard, true);
            if (instance == null) return;
            instance.countdownLabel = label;
            instance.countdownValue = secondsLeft;
        }

        public static void StopCountdown()
        {
            NetSession.UiCountdown("", HudHazard.General, false);
            if (instance != null) instance.countdownValue = null;
        }

        void Update()
        {
            if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.H)) showHelp = !showHelp;
        }

        // ---------- drawing ----------

        static float Fade(Timed t, float inTime = 0.2f, float outTime = 0.35f)
        {
            if (t == null) return 0f;
            float a = Mathf.Clamp01((Time.time - t.start) / inTime);
            float b = Mathf.Clamp01((t.until - Time.time) / outTime);
            return Mathf.Min(a, b);
        }

        void OnGUI()
        {
            UIKit.Begin();
            float w = UIKit.Width, h = UIKit.Height;
            var sm = StageManager.Instance;
            if (sm != null && sm.State == StageState.Intro) { DrawIntro(w, h, sm); return; }

            // split-screen divider (online, each phone shows only its own player full-screen)
            if (!NetSession.Online)
            {
                UIKit.Fill(new Rect(w * 0.5f - 1.5f, 0, 3f, h), new Color(UIKit.Void.r, UIKit.Void.g, UIKit.Void.b, 0.9f));
                UIKit.Fill(new Rect(w * 0.5f - 0.5f, 0, 1f, h), new Color(UIKit.Ink.r, UIKit.Ink.g, UIKit.Ink.b, 0.25f));
            }
            DrawWorldMarkers();
            if (!NetSession.IsClient) DrawPowerA(new Vector2(18, 16));
            if (!NetSession.IsHost) DrawPowerB(new Vector2(w - 18, 16));
            DrawPauseButton(w, sm);
            DrawObjective(w);
            DrawAutoZoneWarning(w);
            DrawCountdown(w);
            DrawWarning(w);
            DrawCheckpoint(w);
            DrawEngine(w, h);
            DrawParts(w);
            DrawMessages(w, h);
            DrawHelp(w, h, sm);
            if (sm != null && sm.State == StageState.Paused) DrawPause(w, h, sm);
            if (sm != null && sm.State == StageState.Complete) DrawComplete(w, h, sm);
        }

        Texture2D GravityIcon(GravityMode m)
        {
            if (icons == null) return null;
            switch (m)
            {
                case GravityMode.Up: return icons.Gravity_Up;
                case GravityMode.Zero: return icons.Gravity_Zero;
                case GravityMode.Left: return icons.Gravity_Left;
                case GravityMode.Right: return icons.Gravity_Right;
                case GravityMode.Forward:
                case GravityMode.Back: return icons.Gravity_Off;
                default: return icons.Gravity_Down;
            }
        }

        void DrawPowerA(Vector2 at)
        {
            var panel = new Rect(at.x, at.y, 344, 124);
            UIKit.Panel(panel, true);
            float cd = gravityPower != null ? gravityPower.CooldownFraction : 0f;
            UIKit.PowerRing(new Rect(panel.x + 8, panel.y + 6, 112, 112), icons?.Ring_Track, icons?.Ring_Cooldown, icons?.Power_A_GravityController, 1f - cd, UIKit.Sun);
            float x = panel.x + 128;
            UIKit.Text(new Rect(x, panel.y + 12, 210, 20), UIKit.Caps("Player A"), UIKit.Style(UIKit.Face.Display, 12, UIKit.Sun));
            UIKit.Text(new Rect(x, panel.y + 28, 210, 24), "Gravity Controller", UIKit.Style(UIKit.Face.Heavy, 19, UIKit.Ink));
            var zone = gravityPower != null ? gravityPower.Target : null;
            if (zone != null)
            {
                UIKit.Tex(new Rect(x - 2, panel.y + 56, 42, 42), GravityIcon(zone.Mode), GravityZone.ColorFor(zone.Mode));
                UIKit.Text(new Rect(x + 46, panel.y + 58, 170, 20), zone.zoneName, UIKit.Style(UIKit.Face.Body, 14, UIKit.Dim));
                UIKit.Text(new Rect(x + 46, panel.y + 76, 170, 22), GravityZone.LabelFor(zone.Mode), UIKit.Style(UIKit.Face.Display, 17, GravityZone.ColorFor(zone.Mode)));
                UIKit.Text(new Rect(x, panel.y + 100, 210, 18), "E  ·  switch gravity", UIKit.Style(UIKit.Face.Body, 12, UIKit.Dim));
            }
            else
            {
                UIKit.Tex(new Rect(x - 2, panel.y + 56, 42, 42), icons?.Gravity_Off, new Color(1, 1, 1, 0.35f));
                UIKit.Text(new Rect(x + 46, panel.y + 66, 170, 36), "No gravity zone\nin range", UIKit.Style(UIKit.Face.Body, 13, UIKit.Dim));
            }
        }

        void DrawPowerB(Vector2 topRight)
        {
            var panel = new Rect(topRight.x - 344, topRight.y, 344, 124);
            UIKit.Panel(panel, true);
            var np = nullifyPower;
            var active = np != null ? np.Active : null;
            // on the joining phone the power runs on the host: use the streamed values
            bool remote = NetSession.IsClient;
            bool isActive = remote ? NetHud.bActive.Length > 0 : active != null;
            bool cooling = remote ? NetHud.bCoolingDown : np != null && np.CooldownRemaining > 0f;
            float fill = remote ? (isActive ? NetHud.bActiveFraction : 1f - NetHud.bCooldown)
                                : active != null ? active.RemainingFraction : 1f - (np != null ? np.CooldownFraction : 0f);
            Color tint = isActive ? UIKit.Glow : (cooling ? UIKit.Dim : UIKit.Glow);
            var ringRect = new Rect(panel.xMax - 120, panel.y + 6, 112, 112);
            UIKit.PowerRing(ringRect, icons?.Ring_Track, isActive ? icons?.Ring_Active : icons?.Ring_Cooldown, icons?.Power_B_GravityNullifier, fill, tint);
            float x = panel.x + 16;
            UIKit.Text(new Rect(x, panel.y + 12, 200, 20), UIKit.Caps("Player B"), UIKit.Style(UIKit.Face.Display, 12, UIKit.Glow));
            UIKit.Text(new Rect(x, panel.y + 28, 200, 24), "Gravity Nullifier", UIKit.Style(UIKit.Face.Heavy, 19, UIKit.Ink));
            string l1, l2;
            if (remote)
            {
                if (isActive) { l1 = $"<color=#5ce1ff>{NetHud.bActive}</color>"; l2 = "Hold POWER: steer · tap: drop"; }
                else if (cooling) { l1 = "Recharging"; l2 = ""; }
                else { l1 = NetHud.bTarget.Length > 0 ? $"Target  <color=#ffcf9a>{NetHud.bTarget}</color>" : "No target in front"; l2 = "POWER  ·  nullify"; }
            }
            else if (active != null)
            {
                l1 = $"<color=#5ce1ff>{active.displayName}</color>  {active.Remaining:0.0}s";
                l2 = np.Steering ? "Steering — arrows move it" : "Hold R-Shift: steer · tap: drop";
            }
            else if (np != null && np.CooldownRemaining > 0f)
            {
                l1 = $"Recharging  {np.CooldownRemaining:0.0}s";
                l2 = "";
            }
            else
            {
                l1 = np != null && np.Target != null ? $"Target  <color=#ffcf9a>{np.Target.displayName}</color>" : "No target in front";
                l2 = "R-Shift  ·  nullify";
            }
            UIKit.Text(new Rect(x, panel.y + 62, 205, 22), l1, UIKit.Style(UIKit.Face.Heavy, 15, UIKit.Ink));
            UIKit.Text(new Rect(x, panel.y + 100, 205, 18), l2, UIKit.Style(UIKit.Face.Body, 12, UIKit.Dim));
        }

        void DrawObjective(float w)
        {
            var r = new Rect(w * 0.5f - 240, 12, 480, 90);
            UIKit.Tex(r, icons?.Banner_Objective);
            UIKit.Tex(new Rect(r.x + 30, r.y + 18, 54, 54), icons != null ? icons.Objective(objective) : null);
            var sm = StageManager.Instance;
            UIKit.Text(new Rect(r.x + 96, r.y + 18, 360, 18), sm != null ? UIKit.Caps(StageShortTitle(sm.stageTitle)) : "", UIKit.Style(UIKit.Face.Display, 11, UIKit.Sun));
            UIKit.Text(new Rect(r.x + 96, r.y + 36, 360, 32), objectiveText.ToUpperInvariant(), UIKit.Style(UIKit.Face.Display, 24, UIKit.Ink));
            if (!string.IsNullOrEmpty(TimeOfDay.CurrentLabel))
                UIKit.Text(new Rect(r.xMax - 150, r.y + 64, 120, 14), UIKit.Caps(TimeOfDay.CurrentLabel), UIKit.Style(UIKit.Face.Display, 9, UIKit.Sun, TextAnchor.MiddleRight));
            if (Time.time < sectionUntil && !string.IsNullOrEmpty(sectionTitle))
            {
                float a = Mathf.Clamp01(sectionUntil - Time.time);
                if (EngineCarrier.Instance != null) // the stability meter sits under the banner: tuck the title inside it
                    UIKit.Text(new Rect(r.x + 96, r.y + 64, 220, 14), sectionTitle.ToUpperInvariant(), UIKit.Style(UIKit.Face.Display, 9, new Color(1, 1, 1, a * 0.85f)));
                else
                    UIKit.Text(new Rect(w * 0.5f - 300, r.yMax + 2, 600, 24), sectionTitle, UIKit.Style(UIKit.Face.Heavy, 17, new Color(1, 1, 1, a), TextAnchor.MiddleCenter));
            }
        }

        static string StageShortTitle(string t)
        {
            int i = t.IndexOf('—');
            return i > 0 ? t.Substring(0, i).Trim() + "  ·  " + t.Substring(i + 1).Trim() : t;
        }

        void DrawAutoZoneWarning(float w)
        {
            foreach (var p in Players.All)
            {
                if (p == null) continue;
                var z = GravityZone.FindZone(p.transform.position, true);
                if (z == null || !z.autoCycle || !z.AutoWarningActive) continue;
                var r = new Rect(w * 0.5f - 190, 132, 380, 64);
                UIKit.Panel(r);
                UIKit.Tex(new Rect(r.x + 10, r.y + 8, 48, 48), icons?.Warn_GravityShift);
                UIKit.Text(new Rect(r.x + 68, r.y + 10, 300, 24), $"GRAVITY SHIFT IN {Mathf.CeilToInt(z.AutoTimeLeft)}", UIKit.Style(UIKit.Face.Display, 18, UIKit.Sun));
                UIKit.Text(new Rect(r.x + 68, r.y + 34, 300, 20), "Get over a safe grip!", UIKit.Style(UIKit.Face.Body, 13, UIKit.Dim));
                return;
            }
        }

        void DrawCountdown(float w)
        {
            if (countdownValue == null) return;
            float left = Mathf.Max(0f, countdownValue());
            var c = new Vector2(w * 0.5f, EngineCarrier.Instance != null ? 300 : 300);
            bool urgent = left < 10f;
            float pulse = urgent ? 1f + 0.06f * Mathf.Sin(Time.time * 12f) : 1f;
            float s = 116f * pulse;
            UIKit.Tex(new Rect(c.x - s * 0.5f, c.y - s * 0.5f, s, s), icons?.Countdown_Ring, urgent ? UIKit.Danger : Color.white);
            UIKit.Text(new Rect(c.x - 60, c.y - 22, 120, 44), Mathf.CeilToInt(left).ToString(), UIKit.Style(UIKit.Face.Display, 36, urgent ? UIKit.Danger : UIKit.Ink, TextAnchor.MiddleCenter));
            UIKit.Text(new Rect(c.x - 220, c.y + 62, 440, 24), UIKit.Caps(countdownLabel), UIKit.Style(UIKit.Face.Display, 14, UIKit.Sun, TextAnchor.MiddleCenter));
        }

        void DrawWarning(float w)
        {
            float a = Fade(warning);
            if (a <= 0f) return;
            var r = new Rect(w * 0.5f - 320 + (1 - a) * 40f, EngineCarrier.Instance != null ? 212 : 216, 640, 120);
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            UIKit.Tex(r, icons?.Banner_Danger);
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
            UIKit.Tex(new Rect(r.x + 22, r.y + 18, 84, 84), icons?.Danger_Pulse, new Color(1, 1, 1, pulse * a));
            UIKit.Tex(new Rect(r.x + 32, r.y + 28, 64, 64), icons != null ? icons.Warning(warning.hazard) : null, new Color(1, 1, 1, a));
            float right = warning.countdownEnd > Time.time ? 100f : 24f;
            UIKit.Text(new Rect(r.x + 120, r.y + 16, r.width - 120 - right, 88), warning.text, UIKit.Style(UIKit.Face.Heavy, 18, UIKit.Ink, TextAnchor.MiddleLeft, true));
            if (warning.countdownEnd > Time.time)
            {
                var c = new Vector2(r.xMax - 58, r.center.y);
                UIKit.Tex(new Rect(c.x - 38, c.y - 38, 76, 76), icons?.Countdown_Ring, new Color(1, 1, 1, a));
                UIKit.Text(new Rect(c.x - 30, c.y - 17, 60, 34), Mathf.CeilToInt(warning.countdownEnd - Time.time).ToString(), UIKit.Style(UIKit.Face.Display, 26, UIKit.Ink, TextAnchor.MiddleCenter));
            }
            GUI.color = old;
        }

        void DrawCheckpoint(float w)
        {
            float a = Fade(checkpoint);
            if (a <= 0f) return;
            var r = new Rect(w * 0.5f - 250, (EngineCarrier.Instance != null ? 336 : 344) - (1 - a) * 20f, 500, 94);
            var old = GUI.color;
            GUI.color = new Color(1, 1, 1, a);
            UIKit.Tex(r, icons?.Banner_Checkpoint);
            UIKit.Tex(new Rect(r.x + 26, r.y + 14, 66, 66), icons?.Checkpoint_Badge);
            UIKit.Text(new Rect(r.x + 104, r.y + 20, 380, 24), UIKit.Caps("Checkpoint"), UIKit.Style(UIKit.Face.Display, 15, UIKit.Good));
            UIKit.Text(new Rect(r.x + 104, r.y + 44, 380, 28), checkpoint.text, UIKit.Style(UIKit.Face.Heavy, 20, UIKit.Ink));
            GUI.color = old;
        }

        void DrawEngine(float w, float h)
        {
            var e = EngineCarrier.Instance;
            if (e == null) return;
            bool remote = NetSession.IsClient;
            float stab = remote ? NetHud.engineStability01 : e.Stability01;
            float tilt = remote ? NetHud.engineTilt : e.Tilt, signed = remote ? NetHud.engineSignedTilt : e.SignedTilt;
            float limit = remote ? NetHud.engineTiltLimit : e.tiltLimit;
            var meter = new Rect(w * 0.5f - 230, 128, 340, 64);
            Color fill = Color.Lerp(UIKit.Danger, UIKit.Good, stab);
            UIKit.Meter(meter, icons?.Meter_Engine_Frame, icons?.Meter_Fill, stab, fill);
            UIKit.Text(new Rect(meter.x, meter.y - 22, meter.width, 20), UIKit.Caps($"Engine stability  {Mathf.RoundToInt(stab * 100)}%"), UIKit.Style(UIKit.Face.Display, 12, UIKit.Sun, TextAnchor.MiddleCenter));
            bool tilting = tilt > limit;
            var gauge = new Rect(meter.xMax + 6, meter.y - 8, 120, 70);
            UIKit.Gauge(gauge, icons?.Tilt_Gauge, icons?.Tilt_Needle, -signed * 2.2f, tilting ? UIKit.Danger : Color.white);
            if (tilting)
                UIKit.Text(new Rect(meter.x, meter.yMax + 2, meter.width + 160, 22), "TILTING — LEVEL THE ENGINE!", UIKit.Style(UIKit.Face.Display, 15, UIKit.Danger, TextAnchor.MiddleCenter));
        }

        /// <summary>Stage 3: who has their engine half (waiting / in hand / installed), under this screen's power panel.</summary>
        void DrawParts(float w)
        {
            int mask = NetSession.IsClient ? NetHud.partsMask : PartsMask();
            if (mask == 0) return;
            bool right = NetSession.IsClient;
            var r = new Rect(right ? w - 18 - 344 : 18, 148, 344, 40);
            UIKit.Panel(r);
            UIKit.Text(new Rect(r.x + 14, r.y, 90, r.height), UIKit.Caps("Engine"), UIKit.Style(UIKit.Face.Display, 11, UIKit.Dim, TextAnchor.MiddleLeft));
            for (int p = 0; p < 2; p++)
            {
                int state = (mask >> (p * 2)) & 3; // 1 waiting, 2 held, 3 installed
                Color c = p == 0 ? UIKit.Sun : UIKit.Glow;
                string label = state == 3 ? "Installed" : state == 2 ? "In hand" : "Pick it up";
                var cell = new Rect(r.x + 96 + p * 122, r.y + 8, 114, 24);
                UIKit.Dot(new Vector2(cell.x + 8, cell.center.y), 12f, state == 1 ? new Color(c.r, c.g, c.b, 0.35f) : state == 3 ? UIKit.Good : c);
                UIKit.Text(new Rect(cell.x + 18, cell.y, cell.width - 18, cell.height), (p == 0 ? "A  " : "B  ") + label,
                    UIKit.Style(UIKit.Face.Heavy, 12, state == 1 ? UIKit.Dim : UIKit.Ink, TextAnchor.MiddleLeft));
            }
        }

        /// <summary>Two bits per half (A low, B high): 0 none, 1 waiting, 2 held, 3 installed.</summary>
        public static int PartsMask()
        {
            int mask = 0;
            foreach (var part in EnginePart.All)
            {
                int s = part.State == EnginePart.PartState.Installed ? 3 : part.State == EnginePart.PartState.Held ? 2 : 1;
                mask |= s << (Mathf.Clamp(part.owner, 0, 1) * 2);
            }
            return mask;
        }

        void DrawMessages(float w, float h)
        {
            float a = Fade(message);
            if (a > 0f)
                UIKit.Text(new Rect(w * 0.5f - 420, h * 0.60f, 840, 56), message.text, UIKit.Style(UIKit.Face.Heavy, 22, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, true));
            float b = Fade(hint, 0.3f, 0.5f);
            if (b > 0f)
            {
                // under the title banner: in third person the bottom-middle of the screen is the player's back
                float y = 132f;
                // transparent instruction panel: white hairline, corner brackets, no fill (final design)
                var r = new Rect(w * 0.5f - 400, y, 800, 74);
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, b);
                MenuStyle.Box(r, MenuStyle.W50, 1f);
                MenuStyle.Brackets(r);
                MenuStyle.Text(new Rect(r.x + 18, r.y + 6, 120, 12), "INSTRUCTIONS", UIKit.Face.Heavy, 9, MenuStyle.W50, 0.3f, TextAnchor.UpperLeft);
                UIKit.Text(new Rect(r.x + 18, r.y + 18, r.width - 36, r.height - 22), hint.text, UIKit.Style(UIKit.Face.Body, 15, UIKit.Ink, TextAnchor.MiddleLeft, true));
                GUI.color = old;
            }
        }

        /// <summary>Touch screens have no Esc key: a small pause button next to the objective banner.</summary>
        void DrawPauseButton(float w, StageManager sm)
        {
            if (sm == null || sm.State != StageState.Playing) return;
            var r = new Rect(w * 0.5f + 252, 30, 52, 52);
            if (UIKit.Button(r, "II", false, 16)) sm.TogglePause();
        }

        void DrawHelp(float w, float h, StageManager sm)
        {
            if (sm != null && sm.ResetHold > 0f)
            {
                var rr = new Rect(w * 0.5f - 140, h * 0.5f + 40, 280, 34);
                UIKit.Panel(rr);
                UIKit.Fill(new Rect(rr.x + 6, rr.y + 6, (rr.width - 12) * sm.ResetHold / sm.holdToResetSeconds, rr.height - 12), UIKit.Sun);
                UIKit.Text(rr, UIKit.Caps("Reset to checkpoint"), UIKit.Style(UIKit.Face.Display, 12, UIKit.Ink, TextAnchor.MiddleCenter));
            }
            if (!showHelp)
            {
                UIKit.Text(new Rect(16, h - 30, 400, 22), "H  ·  show controls", UIKit.Style(UIKit.Face.Body, 13, UIKit.Dim));
                return;
            }
            var r = new Rect(14, h - 96, w - 28, 82);
            UIKit.Panel(r, true);
            UIKit.Text(new Rect(r.x + 16, r.y + 10, 90, 20), UIKit.Caps("A"), UIKit.Style(UIKit.Face.Display, 13, UIKit.Sun));
            UIKit.Text(new Rect(r.x + 44, r.y + 9, r.width - 60, 20), controlsA, UIKit.Style(UIKit.Face.Body, 15, UIKit.Ink));
            UIKit.Text(new Rect(r.x + 16, r.y + 32, 90, 20), UIKit.Caps("B"), UIKit.Style(UIKit.Face.Display, 13, UIKit.Glow));
            UIKit.Text(new Rect(r.x + 44, r.y + 31, r.width - 60, 20), controlsB, UIKit.Style(UIKit.Face.Body, 15, UIKit.Ink));
            UIKit.Text(new Rect(r.x + 16, r.y + 55, r.width - 32, 20), "Hold R · reset to checkpoint      Esc · pause      H · hide controls", UIKit.Style(UIKit.Face.Body, 13, UIKit.Dim));
        }

        static Camera CamFor(PlayerController p) => p != null && p.ViewCamera != null ? p.ViewCamera : Camera.main;

        void DrawWorldMarkers()
        {
            float s = UIKit.Scale, w = UIKit.Width, h = UIKit.Height;
            foreach (var p in new[] { playerA, playerB })
            {
                if (p == null) continue;
                var cam = CamFor(p);
                if (cam == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(p.transform.position + p.Up * 1.7f);
                var gp = new Vector2(sp.x / s, (Screen.height - sp.y) / s);
                bool onScreen = sp.z > 0 && gp.x > 0 && gp.x < w && gp.y > 0 && gp.y < h;
                if (onScreen)
                {
                    var tag = icons != null ? (p.playerIndex == 0 ? icons.Tag_Player_A : icons.Tag_Player_B) : null;
                    if (tag != null) UIKit.Tex(new Rect(gp.x - 19, gp.y - 42, 38, 40), tag);
                    else UIKit.Text(new Rect(gp.x - 20, gp.y - 32, 40, 26), p.displayName, UIKit.Style(UIKit.Face.Display, 18, p.color, TextAnchor.MiddleCenter));
                    if (toasts.TryGetValue(p, out var t) && Time.time < t.until)
                    {
                        var tr = new Rect(gp.x - 150, gp.y - 82, 300, 30);
                        UIKit.Panel(tr, true);
                        UIKit.Text(tr, t.text, UIKit.Style(UIKit.Face.Heavy, 14, UIKit.Ink, TextAnchor.MiddleCenter));
                    }
                }
                else if (icons != null && icons.Offscreen_Arrow != null)
                {
                    var cpos = new Vector2(Mathf.Clamp(gp.x, 44, w - 44), Mathf.Clamp(gp.y, 150, h - 130));
                    if (sp.z < 0) cpos.y = h - 130;
                    float ang = Mathf.Atan2(gp.y - h * 0.5f, gp.x - w * 0.5f) * Mathf.Rad2Deg + 90f;
                    var m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(ang, cpos * s);
                    UIKit.Tex(new Rect(cpos.x - 24, cpos.y - 24, 48, 48), icons.Offscreen_Arrow, p.playerIndex == 0 ? UIKit.Sun : UIKit.Glow);
                    GUI.matrix = m;
                }
            }
            var np = nullifyPower;
            if (np != null && np.Active != null && CamFor(playerB) != null)
            {
                Vector3 sp = CamFor(playerB).WorldToScreenPoint(np.Active.WorldBounds.center);
                if (sp.z > 0)
                {
                    var c = new Vector2(sp.x / s, (Screen.height - sp.y) / s);
                    UIKit.PowerRing(new Rect(c.x - 34, c.y - 34, 68, 68), icons?.Ring_Track, icons?.Ring_Active, null, np.Active.RemainingFraction, UIKit.Glow);
                    UIKit.Text(new Rect(c.x - 30, c.y - 12, 60, 24), np.Active.Remaining.ToString("0.0"), UIKit.Style(UIKit.Face.Display, 15, UIKit.Ink, TextAnchor.MiddleCenter));
                }
            }
            var gpw = gravityPower;
            if (gpw != null && gpw.Target != null && CamFor(playerA) != null)
            {
                Vector3 sp = CamFor(playerA).WorldToScreenPoint(gpw.Target.transform.position);
                if (sp.z > 0)
                {
                    var c = new Vector2(sp.x / s, (Screen.height - sp.y) / s);
                    UIKit.Tex(new Rect(c.x - 22, c.y - 22, 44, 44), GravityIcon(gpw.Target.Mode), new Color(1, 1, 1, 0.8f));
                }
            }
        }

        static readonly string[] StageHints =
        {
            "Learn to bend gravity together.", "Flip the station, find the engine.", "Carry the engine home — fast."
        };

        /// <summary>
        /// The stage instructions card (final design): a transparent double-hairline box with the stage, its hint,
        /// difficulty and keys on the left, mode / objective / players on the right, the story below, a waving
        /// astronaut in the corner and "Ready when you are" underneath. Tap or Space to start.
        /// </summary>
        void DrawIntro(float w, float h, StageManager sm)
        {
            float bw = Mathf.Min(w * 0.62f, 1180f), bh = h * 0.66f;
            var box = new Rect((w - bw) * 0.5f, h * 0.05f, bw, bh);
            MenuStyle.Box(box, MenuStyle.W50, 1f);
            MenuStyle.Box(new Rect(box.x + 5f, box.y + 5f, box.width - 10f, box.height - 10f), MenuStyle.W25, 1f);
            // the small arc ornament on the bottom edge
            GUI.BeginGroup(new Rect(box.x + bw * 0.195f, box.yMax - 26f, 58f, 22f));
            MenuStyle.Box(new Rect(0f, 0f, 116f, 116f), MenuStyle.W50, 1f, 58f);
            GUI.EndGroup();

            int n = Mathf.Clamp(sm.stageNumber, 1, 3);
            MenuStyle.ShadowAlpha = 0.8f; // transparent card over the live game: stronger shadow for legibility
            string sub = sm.stageTitle.Contains("—") ? sm.stageTitle.Substring(sm.stageTitle.IndexOf('—') + 1).Trim() : sm.stageTitle;
            float lx = box.x + bw * 0.06f, lw = bw * 0.5f, y = box.y + 34f;
            MenuStyle.Text(new Rect(lx, y, lw, 12f), "STAGE", UIKit.Face.Heavy, 10, MenuStyle.W50, 0.3f, TextAnchor.UpperLeft);
            y += 18f;
            MenuStyle.Text(new Rect(lx, y, lw, 44f), (n + " · " + sub).ToUpperInvariant(), UIKit.Face.Display, 30, 1f, 0.02f, TextAnchor.UpperLeft);
            y += 50f;
            MenuStyle.Text(new Rect(lx, y, lw, 18f), StageHints[n - 1], UIKit.Face.Body, 14, MenuStyle.W80, 0f, TextAnchor.UpperLeft);
            y += 30f;
            MenuStyle.Text(new Rect(lx, y, 100f, 12f), "DIFFICULTY", UIKit.Face.Heavy, 10, MenuStyle.W50, 0.3f, TextAnchor.UpperLeft);
            float px = lx + MenuStyle.Measure("DIFFICULTY", UIKit.Face.Heavy, 10, 0.3f) + 12f;
            for (int p = 1; p <= 3; p++)
            {
                var pip = new Rect(px + (p - 1) * 27f, y + 5f, 22f, 3f);
                if (p <= n) UIKit.Fill(pip, MenuStyle.White(1f));
                else MenuStyle.Box(pip, MenuStyle.W50, 1f);
            }
            y += 30f;
            float kx = lx;
            foreach (var (keys, label) in new[] { (new[] { "W", "A", "S", "D" }, "move"), (new[] { "Space" }, "jump"), (new[] { "E" }, "power"), (new[] { "Mouse" }, "look") })
            {
                foreach (var k in keys) kx += MenuStyle.Key(kx, y + 10f, k) + 4f;
                float tw = MenuStyle.Measure(label, UIKit.Face.Body, 12, 0f);
                MenuStyle.Text(new Rect(kx, y + 1f, tw + 4f, 18f), label, UIKit.Face.Body, 12, MenuStyle.W50, 0f, TextAnchor.MiddleLeft);
                kx += tw + 18f;
            }
            y += 40f;
            // the story, in the open space under the keys
            UIKit.Text(new Rect(lx, y, box.xMax - lx - 40f, box.yMax - y - 30f), sm.introText, UIKit.Style(UIKit.Face.Body, 15, MenuStyle.White(0.9f), TextAnchor.UpperLeft, true));

            // right column: label / value rows with dashed rules
            float rx = box.x + bw * 0.62f, rw = bw * 0.33f, ry = box.y + 34f;
            string modeText = NetSession.Online ? (NetSession.IsHost ? "Online · Host" : "Online · Joined") : "Local · 2 Players";
            foreach (var (label, value) in new[] { ("MODE", modeText), ("OBJECTIVE", sm.objectiveText), ("PLAYERS", "2 · Co-op") })
            {
                MenuStyle.Text(new Rect(rx, ry, rw, 18f), label, UIKit.Face.Heavy, 10, MenuStyle.W50, 0.3f, TextAnchor.MiddleLeft);
                MenuStyle.Text(new Rect(rx, ry, rw, 18f), value, UIKit.Face.Body, 13, MenuStyle.W80, 0f, TextAnchor.MiddleRight);
                MenuStyle.DashedLine(rx, rx + rw, ry + 26f, MenuStyle.W25);
                ry += 35f;
            }

            MenuStyle.Text(new Rect(0, box.yMax + 46f, w, 22f), "Ready when you are. Stay close to your partner, and don't let go.", UIKit.Face.Body, 17, 1f, 0f, TextAnchor.UpperCenter);
            float pulse = 0.45f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            MenuStyle.Text(new Rect(0, box.yMax + 82f, w, 14f), "TAP OR PRESS SPACE TO START", UIKit.Face.Heavy, 11, pulse, 0.3f, TextAnchor.UpperCenter);

            MenuStyle.Astronaut(new Vector2(150f, h - 18f), 1f);
            MenuStyle.ShadowAlpha = 0.35f;
        }

        void DrawPause(float w, float h, StageManager sm)
        {
            UIKit.Shade(0.72f);
            UIKit.Text(new Rect(0, h * 0.22f, w, 60), UIKit.Caps("Paused", 2), UIKit.Style(UIKit.Face.Display, 44, UIKit.Ink, TextAnchor.MiddleCenter));
            for (int i = 0; i < StageManager.PauseItems.Length; i++)
            {
                var r = new Rect(w * 0.5f - 200, h * 0.38f + i * 62, 400, 50);
                if (UIKit.Button(r, StageManager.PauseItems[i], sm.PauseSelection == i)) { sm.PauseSelection = i; sm.ChoosePauseItem(i); }
            }
            UIKit.Text(new Rect(0, h * 0.38f + StageManager.PauseItems.Length * 62 + 12, w, 22), "W / S or arrows  ·  Space / Enter to confirm  ·  Esc to resume", UIKit.Style(UIKit.Face.Body, 14, UIKit.Dim, TextAnchor.MiddleCenter));
        }

        void DrawComplete(float w, float h, StageManager sm)
        {
            UIKit.Shade(sm.isFinalStage ? 0.55f : 0.78f);
            UIKit.Tex(new Rect(w * 0.5f - 62, 100, 124, 124), icons?.Checkpoint_Badge);
            UIKit.Text(new Rect(0, 236, w, 60), sm.completeTitle, UIKit.Style(UIKit.Face.Display, 46, UIKit.Good, TextAnchor.MiddleCenter));
            var body = new Rect(w * 0.5f - 420, 316, 840, 170);
            UIKit.Panel(body, true);
            UIKit.Text(new Rect(body.x + 30, body.y + 22, body.width - 60, body.height - 40), sm.completeText, UIKit.Style(UIKit.Face.Body, 19, UIKit.Ink, TextAnchor.UpperCenter, true));
            int m = Mathf.FloorToInt(sm.StageTime / 60f), sec = Mathf.FloorToInt(sm.StageTime % 60f);
            UIKit.Text(new Rect(0, body.yMax + 20, w, 24), $"Time {m}:{sec:00}    ·    Respawns {sm.Deaths}", UIKit.Style(UIKit.Face.Heavy, 17, UIKit.Dim, TextAnchor.MiddleCenter));
            string next = sm.isFinalStage || !GameFlow.HasNext(sm.stageNumber) ? "Return to menu" : "Continue to Stage " + (sm.stageNumber + 1);
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            UIKit.Text(new Rect(0, h - 140, w, 34), UIKit.Caps("Tap or press Space · " + next), UIKit.Style(UIKit.Face.Display, 18, new Color(1, 1, 1, pulse), TextAnchor.MiddleCenter));
        }
    }
}
