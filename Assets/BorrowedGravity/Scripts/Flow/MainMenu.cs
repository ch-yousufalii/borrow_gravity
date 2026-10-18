using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowGravity
{
    /// <summary>
    /// Title screen in the sci-fi menu style: a "NEW GAME" tag on a divider, selector rows with arrow boxes
    /// (stage, mode, opening cutscene) and a bracketed START button. Keyboard (both players' keys) or mouse.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public HudIcons icons;
        public Transform orbitTarget;
        public float orbitSpeed = 4f;
        public float orbitRadius = 34f;
        public float orbitHeight = 10f;

        enum Row { Stage, Mode, Cutscene, Start, Controls, Quit }
        static readonly Row[] Rows = { Row.Stage, Row.Mode, Row.Cutscene, Row.Start, Row.Controls, Row.Quit };
        static readonly string[] StageNames = { "Shattered Orbit", "Upside-Down Station", "Collapsing Return" };
        static readonly string[] ModeNames = { "Same screen · 2 players", "Online · Host a room", "Online · Join a room" };
        static readonly string[] Keypad = { "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "0", "<" };

        int row = (int)Row.Start;
        int stage;
        int mode;
        bool joinEntry;
        string joinCode = "";
        bool cutscene = true;
        bool showControls;
        float angle, introFade = 1f;

        bool autoStart;

        void Start()
        {
            Time.timeScale = 1f;
            if (NetSession.Online) mode = NetSession.IsHost ? 1 : 2;
            else DevCommandLine();
            Sfx.SetLoop(LoopId.Ambience, true, 0.35f);
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam != null && orbitTarget != null)
            {
                angle += orbitSpeed * Time.deltaTime;
                float a = angle * Mathf.Deg2Rad;
                cam.transform.position = orbitTarget.position + new Vector3(Mathf.Sin(a) * orbitRadius, orbitHeight, -Mathf.Cos(a) * orbitRadius);
                cam.transform.LookAt(orbitTarget.position + Vector3.up * 2f);
            }
            introFade = Mathf.MoveTowards(introFade, 0f, Time.deltaTime);

            if (RoomPanelOpen) { RoomKeys(); return; }
            if (showControls)
            {
                if (Confirm() || PlayerKeys.Pressed(Key.Escape)) { showControls = false; Sfx.Play(SfxId.Plate, 0.5f); }
                return;
            }
            if (PlayerKeys.Pressed(Key.W) || PlayerKeys.Pressed(Key.UpArrow)) Move(-1);
            if (PlayerKeys.Pressed(Key.S) || PlayerKeys.Pressed(Key.DownArrow)) Move(1);
            if (PlayerKeys.Pressed(Key.A) || PlayerKeys.Pressed(Key.LeftArrow)) Change(-1);
            if (PlayerKeys.Pressed(Key.D) || PlayerKeys.Pressed(Key.RightArrow)) Change(1);
            if (Confirm()) Activate(Rows[row]);
        }

        static bool Confirm() => PlayerKeys.Pressed(Key.Enter) || PlayerKeys.Pressed(Key.Space) || PlayerKeys.Pressed(Key.NumpadEnter);

        void Move(int d)
        {
            row = (row + d + Rows.Length) % Rows.Length;
            Sfx.Play(SfxId.Tick, 0.5f);
        }

        void Change(int d)
        {
            switch (Rows[row])
            {
                case Row.Stage:
                    stage = (stage + d + GameFlow.Unlocked) % GameFlow.Unlocked;
                    break;
                case Row.Cutscene:
                    cutscene = !cutscene;
                    break;
                case Row.Mode:
                    mode = (mode + d + ModeNames.Length) % ModeNames.Length;
                    break;
                default:
                    return;
            }
            Sfx.Play(SfxId.Tick, 0.6f, 1.2f);
        }

        void Activate(Row r)
        {
            Sfx.Play(SfxId.Plate, 0.6f);
            switch (r)
            {
                case Row.Start:
                    if (mode == 0) { NetSession.Stop(); StartGame(); }
                    else if (mode == 1) NetSession.StartHost();
                    else { joinEntry = true; NetSession.LastError = ""; }
                    break;
                case Row.Controls:
                    showControls = true;
                    break;
                case Row.Quit:
                    Application.Quit();
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#endif
                    break;
                default:
                    Change(1);
                    break;
            }
        }

        void OnGUI()
        {
            UIKit.Begin();
            float w = UIKit.Width, h = UIKit.Height;
            UIKit.Shade(0.2f);

            // title on the left, menu column on the right — the world shows through, like the reference
            float cx = w * 0.30f;
            UIKit.Text(new Rect(cx - 330, 120, 660, 80), "BORROWED <color=#5ce1ff>GRAVITY</color>", UIKit.Style(UIKit.Face.Display, 64, UIKit.Ink, TextAnchor.MiddleCenter));
            UIKit.Text(new Rect(cx - 330, 196, 660, 24), UIKit.Caps("A two-player co-op gravity adventure"), UIKit.Style(UIKit.Face.Display, 13, UIKit.Dim, TextAnchor.MiddleCenter));

            float mx = w * 0.70f, colW = 440f, x0 = mx - colW * 0.5f;
            float y = 150f;
            GUI.enabled = !RoomPanelOpen; // the room panel sits on top: don't let taps fall through to the menu
            UIKit.Divider(x0, x0 + colW, y, 0.5f);
            UIKit.TagPill(new Vector2(mx - colW * 0.18f, y), "New Game", UIKit.Glow);

            y += 34f;
            int d = UIKit.Selector(new Rect(x0, y, colW, 62), "Stage", $"{stage + 1} · {StageNames[stage]}", Rows[row] == Row.Stage);
            if (d != 0) { row = (int)Row.Stage; Change(d); }
            y += 78f;
            d = UIKit.Selector(new Rect(x0, y, colW, 62), "Mode", ModeNames[mode], Rows[row] == Row.Mode);
            if (d != 0) { row = (int)Row.Mode; Change(d); }
            y += 78f;
            d = UIKit.Selector(new Rect(x0, y, colW, 62), "Opening cutscene", stage == 0 && cutscene ? "On" : "Off", Rows[row] == Row.Cutscene);
            if (d != 0) { row = (int)Row.Cutscene; Change(d); }
            y += 96f;
            string start = mode == 1 ? "Create room" : mode == 2 ? "Join room" : GameFlow.Unlocked > 1 && stage > 0 ? "Continue" : "Start";
            if (UIKit.Button(new Rect(x0 + 40, y, colW - 80, 56), start, Rows[row] == Row.Start, 22)) { row = (int)Row.Start; Activate(Row.Start); }
            y += 82f;
            float half = (colW - 100) * 0.5f;
            if (UIKit.Button(new Rect(x0 + 40, y, half, 42), "Controls", Rows[row] == Row.Controls, 15)) { row = (int)Row.Controls; Activate(Row.Controls); }
            if (UIKit.Button(new Rect(x0 + 60 + half, y, half, 42), "Quit", Rows[row] == Row.Quit, 15)) { row = (int)Row.Quit; Activate(Row.Quit); }
            UIKit.Divider(x0, x0 + colW, y + 70f, 0.3f);

            UIKit.Text(new Rect(0, h - 40, w, 22), "W / S  choose    ·    A / D  change    ·    Space / Enter  confirm", UIKit.Style(UIKit.Face.Body, 14, UIKit.Dim, TextAnchor.MiddleCenter));
            if (!string.IsNullOrEmpty(NetSession.LastError) && !RoomPanelOpen)
                UIKit.Text(new Rect(0, h - 74, w, 24), NetSession.LastError, UIKit.Style(UIKit.Face.Heavy, 16, UIKit.Danger, TextAnchor.MiddleCenter));
            GUI.enabled = true;
            if (RoomPanelOpen) DrawRoom(w, h);
            if (showControls) DrawControls(w);
            if (introFade > 0f) UIKit.Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, introFade));
        }

        /// <summary>Testing two copies on one machine: -bghost [-bgstage N] [-bgnocut] starts a room and the game as soon
        /// as someone joins; -bgjoin &lt;code or IP&gt; joins one.</summary>
        void DevCommandLine()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : "";
                switch (args[i])
                {
                    case "-bgstage": if (int.TryParse(next, out int st)) stage = Mathf.Clamp(st - 1, 0, StageNames.Length - 1); break;
                    case "-bgnocut": cutscene = false; break;
                    case "-bghost": mode = 1; autoStart = true; break;
                    case "-bgjoin": mode = 2; joinCode = next; break;
                }
            }
            if (mode == 1 && autoStart) NetSession.StartHost();
            if (mode == 2 && joinCode.Length > 0) Connect();
        }

        void StartGame()
        {
            if (stage == 0 && cutscene) GameFlow.StartNewGame();
            else GameFlow.LoadStage(stage + 1);
        }

        // ---------- online room ----------

        bool RoomPanelOpen => joinEntry || NetSession.Online;

        void LeaveRoom()
        {
            NetSession.Stop();
            joinEntry = false;
            Sfx.Play(SfxId.Plate, 0.5f);
        }

        void Connect()
        {
            if (NetSession.StartClient(joinCode)) joinEntry = false;
            Sfx.Play(SfxId.Plate, 0.6f);
        }

        void TypeKey(string k)
        {
            if (k == "<") { if (joinCode.Length > 0) joinCode = joinCode.Substring(0, joinCode.Length - 1); }
            else if (joinCode.Length < 15) joinCode += k;
            Sfx.Play(SfxId.Tick, 0.5f);
        }

        void RoomKeys()
        {
            if (PlayerKeys.Pressed(Key.Escape)) { LeaveRoom(); return; }
            if (joinEntry && !NetSession.Online)
            {
                Key[] digits = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
                Key[] pad = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9 };
                for (int i = 0; i < 10; i++) if (PlayerKeys.Pressed(digits[i]) || PlayerKeys.Pressed(pad[i])) TypeKey(i.ToString());
                if (PlayerKeys.Pressed(Key.Period) || PlayerKeys.Pressed(Key.NumpadPeriod)) TypeKey(".");
                if (PlayerKeys.Pressed(Key.Backspace)) TypeKey("<");
                if (PlayerKeys.Pressed(Key.Enter) || PlayerKeys.Pressed(Key.NumpadEnter)) Connect();
                return;
            }
            if (NetSession.IsHost && NetSession.Connected && (Confirm() || autoStart)) { autoStart = false; StartGame(); }
        }

        void DrawRoom(float w, float h)
        {
            UIKit.Shade(0.72f);
            var r = new Rect(w * 0.5f - 300, 120, 600, 620);
            UIKit.Panel(r, true);
            float cx = r.center.x;

            if (joinEntry && !NetSession.Online)
            {
                UIKit.TagPill(new Vector2(cx, r.y), "Join a room", UIKit.Glow);
                UIKit.Text(new Rect(r.x, r.y + 34, r.width, 24), "Type the room code shown on your friend's phone", UIKit.Style(UIKit.Face.Body, 16, UIKit.Dim, TextAnchor.MiddleCenter));
                var field = new Rect(cx - 170, r.y + 70, 340, 64);
                UIKit.Fill(field, new Color(UIKit.Void.r, UIKit.Void.g, UIKit.Void.b, 0.7f));
                UIKit.Brackets(field, 14f, 2f, UIKit.Glow);
                string shown = joinCode.Length > 0 ? joinCode : "····";
                UIKit.Text(field, shown, UIKit.Style(UIKit.Face.Display, 34, joinCode.Length > 0 ? UIKit.Ink : UIKit.Dim, TextAnchor.MiddleCenter));
                for (int i = 0; i < Keypad.Length; i++)
                {
                    var k = new Rect(cx - 162 + (i % 3) * 112, r.y + 152 + (i / 3) * 66, 100, 56);
                    if (UIKit.Button(k, Keypad[i] == "<" ? "Del" : Keypad[i], false, 22)) TypeKey(Keypad[i]);
                }
                if (!string.IsNullOrEmpty(NetSession.LastError))
                    UIKit.Text(new Rect(r.x, r.y + 418, r.width, 22), NetSession.LastError, UIKit.Style(UIKit.Face.Heavy, 15, UIKit.Danger, TextAnchor.MiddleCenter));
                UIKit.Text(new Rect(r.x + 30, r.y + 444, r.width - 60, 40), "Both phones on the same Wi-Fi (or one phone's hotspot). You can also type the host's IP address.", UIKit.Style(UIKit.Face.Body, 13, UIKit.Dim, TextAnchor.MiddleCenter, true));
                if (UIKit.Button(new Rect(cx - 250, r.yMax - 92, 230, 56), "Back", false, 18)) { joinEntry = false; Sfx.Play(SfxId.Plate, 0.5f); }
                if (UIKit.Button(new Rect(cx + 20, r.yMax - 92, 230, 56), "Connect", true, 18)) Connect();
                return;
            }

            if (NetSession.IsHost)
            {
                UIKit.TagPill(new Vector2(cx, r.y), "Your room", UIKit.Sun);
                UIKit.Text(new Rect(r.x, r.y + 40, r.width, 22), UIKit.Caps("Room code"), UIKit.Style(UIKit.Face.Display, 14, UIKit.Dim, TextAnchor.MiddleCenter));
                UIKit.Text(new Rect(r.x, r.y + 66, r.width, 110), NetSession.RoomCode, UIKit.Style(UIKit.Face.Display, 96, UIKit.Sun, TextAnchor.MiddleCenter));
                UIKit.Text(new Rect(r.x, r.y + 180, r.width, 22), string.IsNullOrEmpty(NetSession.LocalAddress) ? "" : "or IP  " + NetSession.LocalAddress, UIKit.Style(UIKit.Face.Body, 16, UIKit.Dim, TextAnchor.MiddleCenter));
                UIKit.Divider(r.x + 40, r.xMax - 40, r.y + 222, 0.4f);
                bool joined = NetSession.Connected;
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
                UIKit.Text(new Rect(r.x, r.y + 240, r.width, 30), joined ? "Player B has joined!" : "Waiting for Player B to join…",
                    UIKit.Style(UIKit.Face.Heavy, 22, joined ? UIKit.Good : new Color(1, 1, 1, pulse), TextAnchor.MiddleCenter));
                UIKit.Text(new Rect(r.x + 30, r.y + 282, r.width - 60, 60), "You play A (Gravity Controller). On the other phone: Mode → Online · Join a room, then type this code.",
                    UIKit.Style(UIKit.Face.Body, 15, UIKit.Ink, TextAnchor.MiddleCenter, true));
                UIKit.Text(new Rect(r.x, r.y + 352, r.width, 22), $"Stage {stage + 1} · {StageNames[stage]}" + (stage == 0 && cutscene ? "  ·  with opening cutscene" : ""),
                    UIKit.Style(UIKit.Face.Body, 15, UIKit.Dim, TextAnchor.MiddleCenter));
                if (UIKit.Button(new Rect(cx - 250, r.yMax - 92, 230, 56), "Close room", false, 18)) LeaveRoom();
                GUI.enabled = joined;
                if (UIKit.Button(new Rect(cx + 20, r.yMax - 92, 230, 56), "Start", joined, 18) && joined) StartGame();
                GUI.enabled = true;
                return;
            }

            // joining phone
            UIKit.TagPill(new Vector2(cx, r.y), "Online", UIKit.Glow);
            bool connected = NetSession.Connected;
            float p2 = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 4f);
            UIKit.Text(new Rect(r.x, r.y + 120, r.width, 40), connected ? "Connected to room " + NetSession.RoomCode : "Looking for room " + (NetSession.RoomCode.Length > 0 ? NetSession.RoomCode : joinCode) + "…",
                UIKit.Style(UIKit.Face.Heavy, 26, connected ? UIKit.Good : new Color(1, 1, 1, p2), TextAnchor.MiddleCenter));
            UIKit.Text(new Rect(r.x + 30, r.y + 180, r.width - 60, 80), connected ? "You play B (Gravity Nullifier). The host starts the game." : "Make sure both phones are on the same Wi-Fi or hotspot, and the room is open on the other phone.",
                UIKit.Style(UIKit.Face.Body, 17, UIKit.Ink, TextAnchor.MiddleCenter, true));
            if (UIKit.Button(new Rect(cx - 115, r.yMax - 92, 230, 56), "Leave", false, 18)) LeaveRoom();
        }

        void DrawControls(float w)
        {
            UIKit.Shade(0.7f);
            var r = new Rect(w * 0.5f - 470, 200, 940, 400);
            UIKit.Panel(r);
            UIKit.TagPill(new Vector2(r.center.x, r.y), "Controls", UIKit.Glow);
            UIKit.PowerRing(new Rect(r.x + 30, r.y + 40, 96, 96), icons?.Ring_Track, icons?.Ring_Cooldown, icons?.Power_A_GravityController, 1f, UIKit.Sun);
            UIKit.Text(new Rect(r.x + 146, r.y + 40, 760, 20), UIKit.Caps("Player A  ·  Gravity Controller"), UIKit.Style(UIKit.Face.Display, 14, UIKit.Sun));
            UIKit.Text(new Rect(r.x + 146, r.y + 64, 770, 110),
                "WASD move · Space jump · E switch the gravity of the nearest marked zone (down / zero-g / up / onto walls). " +
                "Changing a zone affects everything inside it — sometimes you two as well.", UIKit.Style(UIKit.Face.Body, 18, UIKit.Ink, TextAnchor.UpperLeft, true));
            UIKit.PowerRing(new Rect(r.x + 30, r.y + 210, 96, 96), icons?.Ring_Track, icons?.Ring_Cooldown, icons?.Power_B_GravityNullifier, 1f, UIKit.Glow);
            UIKit.Text(new Rect(r.x + 146, r.y + 210, 760, 20), UIKit.Caps("Player B  ·  Gravity Nullifier"), UIKit.Style(UIKit.Face.Display, 14, UIKit.Glow));
            UIKit.Text(new Rect(r.x + 146, r.y + 234, 770, 130),
                "Arrows move · Right Ctrl / Enter jump · Right Shift tap: freeze the object in front of you for a few seconds. " +
                "Hold Right Shift: steer it sideways with the arrows. Tap again to drop it.\nHold R to reset to the last checkpoint · Esc pauses.",
                UIKit.Style(UIKit.Face.Body, 18, UIKit.Ink, TextAnchor.UpperLeft, true));
            if (UIKit.Button(new Rect(r.center.x - 90, r.yMax - 56, 180, 40), "Back", true, 15)) showControls = false;
        }
    }
}
