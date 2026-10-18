using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowGravity
{
    /// <summary>
    /// Two-phone play over the local network (same Wi-Fi or one phone's hotspot) with no server: the host creates a
    /// room with a 4-digit code, the other phone types the code (or the host's IP) and finds it by UDP broadcast.
    /// The host runs the whole game and plays A; the joining phone plays B, sends its controls and mirrors the host's
    /// world (<see cref="NetWorld"/>), HUD and sounds. Offline (the default) nothing here runs.
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public const int Port = 47810;
        const byte Magic = 0xB6;
        const float TimeoutSeconds = 6f;

        public static NetMode Mode { get; private set; } = NetMode.Offline;
        public static NetStatus Status { get; private set; } = NetStatus.Idle;
        public static string RoomCode { get; private set; } = "";
        public static string LocalAddress { get; private set; } = "";
        public static string LastError = "";

        public static bool Online => Mode != NetMode.Offline;
        public static bool IsHost => Mode == NetMode.Host;
        public static bool IsClient => Mode == NetMode.Client;
        public static bool Connected => Status == NetStatus.Connected;
        /// <summary>The player this device controls: A (0) on the host, B (1) on the joining phone.</summary>
        public static int LocalPlayer => IsClient ? 1 : 0;
        /// <summary>True while the client is loading a scene the host told it to.</summary>
        public static bool ApplyingRemote { get; private set; }

        static NetSession runner;
        static UdpClient udp;
        static Thread thread;
        static volatile bool running;
        static readonly ConcurrentQueue<(byte[] data, IPEndPoint from)> inbox = new ConcurrentQueue<(byte[], IPEndPoint)>();
        static IPEndPoint peer;
        static string joinTarget = "";
        static float lastReceive, nextSearch, nextPing, nextInput;

        // remote B input as seen by the host
        static Vector2 rMove;
        static bool rJump, rPower, jumpPulse, powerPulse;
        static byte rJumpCount, rPowerCount;
        static bool gotInput;

        // local B input as sent by the client
        static byte lJumpCount, lPowerCount;

        // test switches for a headless joining copy: -bgmove X (hold the stick at X) and -bgjump S (jump every S seconds)
        static Vector2 devMove;
        static float devJumpEvery, nextDevJump;

        static readonly MemoryStream outStream = new MemoryStream(1400);
        static readonly BinaryWriter writer = new BinaryWriter(outStream, Encoding.UTF8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Mode = NetMode.Offline;
            Status = NetStatus.Idle;
            RoomCode = joinTarget = LastError = "";
            peer = null;
            runner = null;
        }

        // ---------- starting / stopping ----------

        public static bool StartHost()
        {
            Stop();
            try
            {
                udp = new UdpClient(AddressFamily.InterNetwork);
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
                udp.EnableBroadcast = true;
            }
            catch (Exception e)
            {
                LastError = "Could not open the room (" + e.Message + ")";
                CloseSocket();
                return false;
            }
            Mode = NetMode.Host;
            Status = NetStatus.Waiting;
            RoomCode = UnityEngine.Random.Range(1000, 10000).ToString();
            Begin();
            return true;
        }

        /// <summary>Join by room code (found by broadcast) or by the host's IP address.</summary>
        public static bool StartClient(string codeOrAddress)
        {
            Stop();
            joinTarget = (codeOrAddress ?? "").Trim();
            if (joinTarget.Length == 0) { LastError = "Type the room code first"; return false; }
            try
            {
                udp = new UdpClient(AddressFamily.InterNetwork);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                udp.EnableBroadcast = true;
            }
            catch (Exception e)
            {
                LastError = "Network unavailable (" + e.Message + ")";
                CloseSocket();
                return false;
            }
            Mode = NetMode.Client;
            Status = NetStatus.Searching;
            ReadDevSwitches();
            RoomCode = joinTarget.Contains(".") ? "" : joinTarget;
            Begin();
            return true;
        }

        static void Begin()
        {
            LastError = "";
            Application.runInBackground = true; // the host must keep simulating (and answering) when its window isn't focused
            LocalAddress = FindLocalAddress();
            peer = null;
            gotInput = false;
            lastReceive = Time.realtimeSinceStartup;
            nextSearch = 0f;
            NetHud.Clear();
            AcquireMulticastLock();
            running = true;
            thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "BG-Net" };
            thread.Start();
            if (runner == null)
            {
                var go = new GameObject("NetSession");
                DontDestroyOnLoad(go);
                runner = go.AddComponent<NetSession>();
            }
        }

        public static void Stop()
        {
            if (Mode == NetMode.Offline && udp == null) return;
            if (peer != null) { Write(NetMsg.Bye); SendTo(peer); }
            CloseSocket();
            ReleaseMulticastLock();
            Mode = NetMode.Offline;
            Status = NetStatus.Idle;
            peer = null;
            RoomCode = "";
            NetHud.Clear();
        }

        static void CloseSocket()
        {
            running = false;
            try { udp?.Close(); } catch { }
            udp = null;
            thread = null;
            while (inbox.TryDequeue(out _)) { }
        }

        static void ReceiveLoop()
        {
            var u = udp;
            while (running && u != null)
            {
                try
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var data = u.Receive(ref from);
                    if (data.Length >= 2 && data[0] == Magic) inbox.Enqueue((data, from));
                }
                catch (SocketException) { if (!running) return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception) { if (!running) return; }
            }
        }

        void OnDestroy()
        {
            if (runner == this) { Stop(); runner = null; }
        }

        void OnApplicationQuit() => Stop();

        // ---------- per frame ----------

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        static void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            ApplyingRemote = false;
            // single Game scene: GameStages sets the world up when it shows a stage; separate stage scenes: here
            if (Online && NetWorld.Instance == null && UnityEngine.Object.FindFirstObjectByType<StageManager>() != null) NetWorld.Setup(s);
        }

        void Update()
        {
            if (!Online) return;
            float now = Time.realtimeSinceStartup;
            while (inbox.TryDequeue(out var m))
            {
                try { Handle(m.data, m.from); }
                catch (Exception e) { Debug.LogWarning("[Net] bad packet: " + e.Message); }
            }
            if (!Online) return;

            if (IsClient && Status == NetStatus.Searching && now >= nextSearch)
            {
                nextSearch = now + 0.4f;
                if (joinTarget.Contains(".") && IPAddress.TryParse(joinTarget, out var ip))
                {
                    Write(NetMsg.Hello); writer.Write("");
                    SendTo(new IPEndPoint(ip, Port));
                }
                else
                {
                    Write(NetMsg.Query); writer.Write(joinTarget);
                    foreach (var b in BroadcastTargets()) SendTo(b);
                }
            }

            if (Connected)
            {
                if (now >= nextPing) { nextPing = now + 1f; Write(NetMsg.Ping); SendTo(peer); }
                if (now - lastReceive > TimeoutSeconds) Lose("Lost connection to the other phone");
            }

            if (IsHost && gotInput) FeedRemoteInput();
            if (IsClient && Connected && now >= nextInput) { nextInput = now + 1f / 30f; SendInput(false); }
        }

        static void Lose(string why)
        {
            LastError = why;
            Stop();
            Status = NetStatus.Lost;
            if (SceneManager.GetActiveScene().name != GameFlow.MenuScene) GameFlow.LoadMenu();
        }

        // ---------- messages in ----------

        static void Handle(byte[] data, IPEndPoint from)
        {
            using var ms = new MemoryStream(data, 2, data.Length - 2, false);
            using var r = new BinaryReader(ms, Encoding.UTF8);
            var type = (NetMsg)data[1];
            bool fromPeer = peer != null && peer.Equals(from);
            if (fromPeer) lastReceive = Time.realtimeSinceStartup;

            if (IsHost)
            {
                switch (type)
                {
                    case NetMsg.Query:
                        if (r.ReadString() == RoomCode && (peer == null || fromPeer)) { Write(NetMsg.Answer); writer.Write(RoomCode); SendTo(from); }
                        return;
                    case NetMsg.Hello:
                    {
                        string code = r.ReadString();
                        if ((code == RoomCode || code == "") && (peer == null || fromPeer))
                        {
                            peer = from;
                            Status = NetStatus.Connected;
                            lastReceive = Time.realtimeSinceStartup;
                            Write(NetMsg.Welcome); writer.Write(RoomCode);
                            SendTo(peer);
                            Sfx.Play(SfxId.Checkpoint, 0.7f);
                            // already in a stage (the other phone rejoined)? send it there
                            var active = SceneManager.GetActiveScene().name;
                            if (active != GameFlow.MenuScene) SendScene(active, GameFlow.ShowIntro);
                            if (GameStages.Instance != null) SendStage(GameStages.Instance.Current, GameFlow.ShowIntro);
                        }
                        return;
                    }
                }
                if (!fromPeer) return;
                switch (type)
                {
                    case NetMsg.Input: ReadInput(r); break;
                    case NetMsg.Action: DoAction((NetAction)r.ReadByte(), r.ReadByte()); break;
                    case NetMsg.Bye: Lose("The other player left the room"); break;
                }
                return;
            }

            // client
            switch (type)
            {
                case NetMsg.Answer:
                    if (Status == NetStatus.Searching && r.ReadString() == joinTarget)
                    {
                        peer = from;
                        Write(NetMsg.Hello); writer.Write(joinTarget);
                        SendTo(peer);
                    }
                    return;
                case NetMsg.Welcome:
                    if (Status != NetStatus.Connected && (peer == null || peer.Address.Equals(from.Address)))
                    {
                        peer = from;
                        RoomCode = r.ReadString();
                        Status = NetStatus.Connected;
                        lastReceive = Time.realtimeSinceStartup;
                        Sfx.Play(SfxId.Checkpoint, 0.7f);
                    }
                    return;
            }
            if (!fromPeer) return;
            switch (type)
            {
                case NetMsg.Scene:
                {
                    string scene = r.ReadString();
                    GameFlow.ShowIntro = r.ReadBoolean();
                    ApplyingRemote = true;
                    Time.timeScale = 1f;
                    Sfx.StopAllLoops();
                    SceneManager.LoadScene(scene);
                    break;
                }
                case NetMsg.Stage:
                {
                    int stage = r.ReadInt32();
                    GameFlow.ShowIntro = r.ReadBoolean();
                    GameFlow.PendingStage = stage;
                    // already in the Game scene: switch now; otherwise the scene shows it when it finishes loading
                    var gs = GameStages.Instance;
                    if (!ApplyingRemote && gs != null && SceneManager.GetActiveScene().name == GameFlow.GameScene) gs.Show(stage);
                    break;
                }
                case NetMsg.World: NetWorld.Instance?.ReadWorld(r); break;
                case NetMsg.Hud: NetWorld.Instance?.ReadHud(r); break;
                case NetMsg.Spawn: NetWorld.Instance?.ReadSpawn(r); break;
                case NetMsg.Despawn: NetWorld.Instance?.ReadDespawn(r); break;
                case NetMsg.Ui: ReadUi(r); break;
                case NetMsg.Sfx: ReadSfx(r); break;
                case NetMsg.Bye: Lose("The host closed the room"); break;
            }
        }

        // ---------- input relay ----------

        static void ReadDevSwitches()
        {
            devMove = Vector2.zero;
            devJumpEvery = 0f;
            if (Application.isEditor) return;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == "-bgmove" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x)) devMove = new Vector2(x, 0f);
                if (args[i] == "-bgjump" && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float j)) devJumpEvery = j;
            }
        }

        static void SendInput(bool force)
        {
            var b = Players.Get(1);
            var a = Players.Get(0);
            Vector2 move = VirtualInput.Move(1);
            bool jump = VirtualInput.JumpHeld(1), power = VirtualInput.PowerHeld(1);
            foreach (var p in new[] { a, b })
            {
                if (p == null) continue;
                move += p.keys.ReadMove();
                jump |= PlayerKeys.Held(p.keys.jump) || PlayerKeys.Held(p.keys.jumpAlt);
                power |= p.keys.PowerHeld();
            }
            move = Vector2.ClampMagnitude(move + devMove, 1f);
            if (devJumpEvery > 0f && Time.time >= nextDevJump) { nextDevJump = Time.time + devJumpEvery; lJumpCount++; }
            Write(NetMsg.Input);
            writer.Write((sbyte)Mathf.RoundToInt(move.x * 127f));
            writer.Write((sbyte)Mathf.RoundToInt(move.y * 127f));
            writer.Write((byte)((jump ? 1 : 0) | (power ? 2 : 0)));
            writer.Write(lJumpCount);
            writer.Write(lPowerCount);
            SendTo(peer);
        }

        /// <summary>Client: count presses as they happen so even a tap shorter than a packet reaches the host.</summary>
        void LateUpdate()
        {
            if (!IsClient || !Connected) return;
            bool jumpDown = VirtualInput.JumpPressed(1), powerDown = VirtualInput.PowerPressed(1);
            foreach (var p in Players.All)
            {
                if (p == null) continue;
                jumpDown |= p.keys.JumpPressed();
                powerDown |= p.keys.PowerPressed();
            }
            if (jumpDown) lJumpCount++;
            if (powerDown) lPowerCount++;
            if (jumpDown || powerDown) SendInput(true);
        }

        static void ReadInput(BinaryReader r)
        {
            rMove = new Vector2(r.ReadSByte() / 127f, r.ReadSByte() / 127f);
            byte f = r.ReadByte();
            rJump = (f & 1) != 0;
            rPower = (f & 2) != 0;
            byte jc = r.ReadByte(), pc = r.ReadByte();
            if (gotInput && jc != rJumpCount) jumpPulse = true;
            if (gotInput && pc != rPowerCount) powerPulse = true;
            rJumpCount = jc;
            rPowerCount = pc;
            gotInput = true;
        }

        static bool lastJumpOut, lastPowerOut;

        static void FeedRemoteInput()
        {
            // a press counted on the phone becomes one frame of "down" here, even if the button is already up again
            bool jump = rJump || (jumpPulse && !lastJumpOut);
            bool power = rPower || (powerPulse && !lastPowerOut);
            if (jump && !lastJumpOut) jumpPulse = false;
            if (power && !lastPowerOut) powerPulse = false;
            lastJumpOut = jump;
            lastPowerOut = power;
            VirtualInput.Set(1, rMove, jump, power);
        }

        // ---------- actions (client -> host) ----------

        public static void SendAction(NetAction a, int arg = 0)
        {
            if (!IsClient || !Connected) return;
            Write(NetMsg.Action);
            writer.Write((byte)a);
            writer.Write((byte)arg);
            SendTo(peer);
        }

        static void DoAction(NetAction a, byte arg)
        {
            var sm = StageManager.Instance;
            switch (a)
            {
                case NetAction.Confirm: sm?.RemoteConfirm(); break;
                case NetAction.Skip: UnityEngine.Object.FindFirstObjectByType<Cutscene>()?.SkipNow(); break;
                case NetAction.Pause: sm?.TogglePause(); break;
                case NetAction.PauseItem: sm?.ChoosePauseItem(arg); break;
                case NetAction.ResetCheckpoint: sm?.ResetToCheckpoint(); break;
            }
        }

        // ---------- host -> client ----------

        public static void SendScene(string scene, bool showIntro)
        {
            if (!IsHost || !Connected) return;
            Write(NetMsg.Scene);
            writer.Write(scene);
            writer.Write(showIntro);
            SendTo(peer);
        }

        /// <summary>Host: a level was switched inside the Game scene.</summary>
        public static void SendStage(int stage, bool showIntro)
        {
            if (!IsHost || !Connected) return;
            Write(NetMsg.Stage);
            writer.Write(stage);
            writer.Write(showIntro);
            SendTo(peer);
        }

        public static void UiWarning(HudHazard hazard, string text, float countdown, float duration)
        {
            if (!BeginUi(UiEvt.Warning)) return;
            writer.Write((byte)hazard); writer.Write(text ?? ""); writer.Write(countdown); writer.Write(duration);
            SendTo(peer);
        }

        public static void UiText(int kind, string text, float duration = 0f)
        {
            if (!BeginUi((UiEvt)kind)) return;
            writer.Write(text ?? ""); writer.Write(duration);
            SendTo(peer);
        }

        public static void UiToast(int player, string text)
        {
            if (!BeginUi(UiEvt.Toast)) return;
            writer.Write((byte)player); writer.Write(text ?? "");
            SendTo(peer);
        }

        public static void UiObjective(HudObjective o, string text)
        {
            if (!BeginUi(UiEvt.Objective)) return;
            writer.Write((byte)o); writer.Write(text ?? "");
            SendTo(peer);
        }

        public static void UiCountdown(string label, HudHazard hazard, bool start)
        {
            if (!BeginUi(start ? UiEvt.CountdownStart : UiEvt.CountdownStop)) return;
            writer.Write(label ?? ""); writer.Write((byte)hazard);
            SendTo(peer);
        }

        public static void UiShake(float amount, float duration)
        {
            if (!BeginUi(UiEvt.Shake)) return;
            writer.Write(amount); writer.Write(duration);
            SendTo(peer);
        }

        static bool BeginUi(UiEvt e)
        {
            if (!IsHost || !Connected) return false;
            Write(NetMsg.Ui);
            writer.Write((byte)e);
            return true;
        }

        static void ReadUi(BinaryReader r)
        {
            var e = (UiEvt)r.ReadByte();
            switch (e)
            {
                case UiEvt.Warning: GameUI.ShowWarning((HudHazard)r.ReadByte(), r.ReadString(), r.ReadSingle(), r.ReadSingle()); break;
                case UiEvt.Checkpoint: GameUI.ShowCheckpoint(r.ReadString()); break;
                case UiEvt.Message: GameUI.ShowMessage(r.ReadString(), r.ReadSingle()); break;
                case UiEvt.Hint: GameUI.ShowHint(r.ReadString(), r.ReadSingle()); break;
                case UiEvt.Section: GameUI.SetSection(r.ReadString()); break;
                case UiEvt.Toast: { int p = r.ReadByte(); GameUI.Toast(Players.Get(p), r.ReadString()); break; }
                case UiEvt.Objective: { var o = (HudObjective)r.ReadByte(); GameUI.SetObjective(o, r.ReadString()); break; }
                case UiEvt.CountdownStart: GameUI.StartCountdown(r.ReadString(), (HudHazard)r.ReadByte(), () => NetHud.countdownLeft); break;
                case UiEvt.CountdownStop: GameUI.StopCountdown(); break;
                case UiEvt.Shake: CoopCamera.Shake(r.ReadSingle(), r.ReadSingle()); break;
            }
        }

        // sounds are forwarded only in stages; menus and cutscenes play their own on each phone
        static bool ForwardSounds => IsHost && Connected && NetWorld.Instance != null;

        public static void ForwardSfx(SfxId id, float volume, float pitch)
        {
            if (!ForwardSounds) return;
            Write(NetMsg.Sfx);
            writer.Write((byte)SfxEvt.Play); writer.Write((byte)id); writer.Write(volume); writer.Write(pitch);
            SendTo(peer);
        }

        public static void ForwardLoop(LoopId id, bool on, float volume)
        {
            if (!ForwardSounds) return;
            Write(NetMsg.Sfx);
            writer.Write((byte)SfxEvt.Loop); writer.Write((byte)id); writer.Write(on); writer.Write(volume);
            SendTo(peer);
        }

        public static void ForwardStopLoops()
        {
            if (!ForwardSounds) return;
            Write(NetMsg.Sfx);
            writer.Write((byte)SfxEvt.StopLoops);
            SendTo(peer);
        }

        static void ReadSfx(BinaryReader r)
        {
            switch ((SfxEvt)r.ReadByte())
            {
                case SfxEvt.Play: { var id = (SfxId)r.ReadByte(); Sfx.Play(id, r.ReadSingle(), r.ReadSingle()); break; }
                case SfxEvt.Loop: { var id = (LoopId)r.ReadByte(); bool on = r.ReadBoolean(); Sfx.SetLoop(id, on, r.ReadSingle()); break; }
                case SfxEvt.StopLoops: Sfx.StopAllLoops(); break;
            }
        }

        // ---------- raw packets (used by NetWorld too) ----------

        internal static BinaryWriter Write(NetMsg type)
        {
            outStream.SetLength(0);
            writer.Write(Magic);
            writer.Write((byte)type);
            return writer;
        }

        internal static long WrittenBytes => outStream.Length;

        internal static void SendToPeer()
        {
            if (peer != null) SendTo(peer);
        }

        static void SendTo(IPEndPoint to)
        {
            if (udp == null || to == null) return;
            writer.Flush();
            try { udp.Send(outStream.GetBuffer(), (int)outStream.Length, to); }
            catch (Exception) { /* unreachable broadcast address etc. */ }
        }

        // ---------- addresses ----------

        static string FindLocalAddress()
        {
            try
            {
                using var s = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                s.Connect("10.255.255.255", 1); // no packet is sent; just picks the outgoing interface
                if (s.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address)) return ep.Address.ToString();
            }
            catch { }
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                            return ua.Address.ToString();
                }
            }
            catch { }
            return "";
        }

        static List<IPEndPoint> BroadcastTargets()
        {
            var list = new List<IPEndPoint> { new IPEndPoint(IPAddress.Broadcast, Port), new IPEndPoint(IPAddress.Loopback, Port) };
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ua.Address)) continue;
                        byte[] ip = ua.Address.GetAddressBytes();
                        byte[] mask = ua.IPv4Mask != null ? ua.IPv4Mask.GetAddressBytes() : new byte[] { 255, 255, 255, 0 };
                        var b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | ~mask[i]);
                        list.Add(new IPEndPoint(new IPAddress(b), Port));
                    }
                }
            }
            catch
            {
                // some phones refuse interface queries: fall back to a /24 guess from our own address
                if (IPAddress.TryParse(LocalAddress, out var own))
                {
                    var b = own.GetAddressBytes();
                    b[3] = 255;
                    list.Add(new IPEndPoint(new IPAddress(b), Port));
                }
            }
            return list;
        }

        // Android drops broadcast packets unless the app holds a multicast lock.
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject multicastLock;
#endif
        static void AcquireMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (multicastLock != null) return;
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi");
                multicastLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "borrowed-gravity");
                multicastLock.Call("acquire");
            }
            catch (Exception e) { Debug.LogWarning("[Net] multicast lock unavailable: " + e.Message); multicastLock = null; }
#endif
        }

        static void ReleaseMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { multicastLock?.Call("release"); } catch { }
            multicastLock = null;
#endif
        }
    }
}
