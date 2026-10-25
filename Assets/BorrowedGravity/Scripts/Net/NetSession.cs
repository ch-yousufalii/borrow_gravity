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
    /// Phone rooms for two players with no server of our own: the host creates a room (an internet code through Unity
    /// Relay, or a 4-digit same-Wi-Fi code found by UDP broadcast), the others type it in. The host runs the whole game
    /// and plays A; each guest gets the next letter (B, C, D), sends its controls and mirrors the host's world
    /// (<see cref="NetWorld"/>), HUD and sounds. Offline (the default) nothing here runs.
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
        /// <summary>Host: at least one guest is in. Client: in the room.</summary>
        public static bool Connected => Status == NetStatus.Connected;
        /// <summary>The player this device controls: A (0) on the host, the letter the host gave a guest otherwise.</summary>
        public static int LocalPlayer => IsClient ? localIndex : 0;
        /// <summary>Players in the room right now, the host included.</summary>
        public static int Joined => IsHost ? 1 + peers.Count : (Connected ? Mathf.Max(joined, localIndex + 1) : 0);
        /// <summary>Every seat the host opened is taken.</summary>
        public static bool Full => Joined >= GameFlow.PlayerCount;
        /// <summary>True while the client is loading a scene the host told it to.</summary>
        public static bool ApplyingRemote { get; private set; }

        /// <summary>A guest as the host sees it: its address, its player letter and its latest controls.</summary>
        class Peer
        {
            public IPEndPoint ep;
            public int index;
            public float lastReceive;
            public Vector2 move;
            public bool jump, power, jumpPulse, powerPulse, gotInput, lastJumpOut, lastPowerOut;
            public byte jumpCount, powerCount;
        }

        static NetSession runner;
        static UdpClient udp;
        static Thread thread;
        static volatile bool running;
        static readonly ConcurrentQueue<(byte[] data, IPEndPoint from)> inbox = new ConcurrentQueue<(byte[], IPEndPoint)>();
        static readonly List<Peer> peers = new List<Peer>(); // host: the guests
        static IPEndPoint hostEp;                              // client: the host
        static int localIndex = 1, joined;

        // internet rooms (Unity Relay): packets arrive through RelayLink, tagged with a stand-in address per slot
        static bool viaRelay;
        static readonly IPAddress RelayAddress = IPAddress.Parse("10.255.255.1");
        static IPEndPoint RelaySlot(int slot) => new IPEndPoint(RelayAddress, slot + 1);
        /// <summary>True while the room runs through Unity Relay (works across different networks).</summary>
        public static bool ViaRelay => viaRelay;
        /// <summary>True while an internet room is being created / joined.</summary>
        public static bool Busy { get; private set; }

        /// <summary>
        /// Host a room for <see cref="GameFlow.PlayerCount"/> players: an internet room through Unity Relay when it is
        /// available (code like "K7Q2XD"), otherwise a same-Wi-Fi room (4-digit code).
        /// </summary>
        public static async void HostRoom()
        {
            Stop();
            Busy = true;
            LastError = "";
            try
            {
                // without internet (a phone hotspot, an offline router) Unity Services can hang: don't wait forever
                var relay = RelayLink.HostAsync(GameFlow.PlayerCount - 1);
                if (await System.Threading.Tasks.Task.WhenAny(relay, System.Threading.Tasks.Task.Delay(6000)) != relay)
                    throw new TimeoutException("no answer from Unity Relay");
                string code = await relay;
                Mode = NetMode.Host;
                Status = NetStatus.Waiting;
                RoomCode = code.ToUpperInvariant();
                BeginRelay();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Net] Relay unavailable, using a same-Wi-Fi room: " + e.Message);
                StartHost();
                if (Online) LastError = "Internet room unavailable — this is a same-Wi-Fi room.";
            }
            Busy = false;
        }

        static async void JoinRelay(string code)
        {
            Mode = NetMode.Client;
            Status = NetStatus.Searching;
            RoomCode = code;
            Busy = true;
            try
            {
                var join = RelayLink.JoinAsync(code);
                if (await System.Threading.Tasks.Task.WhenAny(join, System.Threading.Tasks.Task.Delay(10000)) != join)
                    throw new TimeoutException("no answer from Unity Relay");
                await join;
                BeginRelay();
                hostEp = RelaySlot(0);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Net] Relay join failed: " + e.Message);
                Stop();
                LastError = "Couldn't join room " + code + " — check the code and your internet.";
            }
            Busy = false;
        }

        static void BeginRelay()
        {
            viaRelay = true;
            RelayLink.Received -= OnRelayData;
            RelayLink.Connected -= OnRelayConnected;
            RelayLink.Disconnected -= OnRelayDisconnected;
            RelayLink.Received += OnRelayData;
            RelayLink.Connected += OnRelayConnected;
            RelayLink.Disconnected += OnRelayDisconnected;
            LastError = "";
            Application.runInBackground = true;
            LocalAddress = "";
            ResetRoom();
            EnsureRunner();
        }

        static void OnRelayData(int slot, byte[] data)
        {
            if (data.Length >= 2 && data[0] == Magic) inbox.Enqueue((data, RelaySlot(slot)));
        }

        static void OnRelayConnected(int slot)
        {
            if (!IsClient) return; // the host waits for the guest's Hello
            hostEp = RelaySlot(slot);
            Write(NetMsg.Hello); writer.Write(joinTarget);
            SendTo(hostEp);
        }

        static void OnRelayDisconnected(int slot)
        {
            if (!Online) return;
            if (IsClient) { Lose("Lost connection to the room"); return; }
            var p = Find(RelaySlot(slot));
            if (p != null) Drop(p, "left the room");
        }

        static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("NetSession");
            DontDestroyOnLoad(go);
            runner = go.AddComponent<NetSession>();
        }
        static string joinTarget = "";
        static float lastReceive, nextSearch, nextSweep, nextPing, nextInput;

        // local input as sent by a guest
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
            peers.Clear();
            hostEp = null;
            localIndex = 1;
            joined = 0;
            runner = null;
        }

        static void ResetRoom()
        {
            peers.Clear();
            hostEp = null;
            localIndex = 1;
            joined = 0;
            lastReceive = Time.realtimeSinceStartup;
            nextSearch = nextSweep = 0f;
            NetHud.Clear();
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

        /// <summary>Join by room code (internet code, or a same-Wi-Fi code found on the network) or by the host's IP.</summary>
        public static bool StartClient(string codeOrAddress)
        {
            Stop();
            joinTarget = (codeOrAddress ?? "").Trim().ToUpperInvariant();
            if (joinTarget.Length == 0) { LastError = "Type the room code first"; return false; }
            // letters = an internet room code (Unity Relay); digits / an IP = a same-Wi-Fi room
            foreach (char ch in joinTarget)
                if (char.IsLetter(ch)) { ReadDevSwitches(); JoinRelay(joinTarget); return true; }
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
            ResetRoom();
            AcquireMulticastLock();
            running = true;
            thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "BG-Net" };
            thread.Start();
            EnsureRunner();
        }

        public static void Stop()
        {
            if (Mode == NetMode.Offline && udp == null && !RelayLink.Active) return;
            Write(NetMsg.Bye); Send();
            RelayLink.Shutdown();
            viaRelay = false;
            CloseSocket();
            ReleaseMulticastLock();
            Mode = NetMode.Offline;
            Status = NetStatus.Idle;
            RoomCode = "";
            ResetRoom();
            for (int i = 1; i < GameFlow.MaxPlayers; i++) VirtualInput.Set(i, Vector2.zero, false, false);
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
            if (viaRelay) RelayLink.Pump();
            float now = Time.realtimeSinceStartup;
            while (inbox.TryDequeue(out var m))
            {
                try { Handle(m.data, m.from); }
                catch (Exception e) { Debug.LogWarning("[Net] bad packet: " + e.Message); }
            }
            if (!Online) return;

            if (IsClient && !viaRelay && Status == NetStatus.Searching) Search(now);

            if (Connected && now >= nextPing) { nextPing = now + 1f; Write(NetMsg.Ping); Send(); }
            if (IsClient && Connected && now - lastReceive > TimeoutSeconds) { Lose("Lost connection to the room"); return; }
            if (IsHost)
            {
                for (int i = peers.Count - 1; i >= 0; i--)
                    if (now - peers[i].lastReceive > TimeoutSeconds) { Drop(peers[i], "lost connection"); if (!Online) return; }
                foreach (var p in peers) if (p.gotInput) FeedRemoteInput(p);
            }
            if (IsClient && Connected && now >= nextInput) { nextInput = now + 1f / 30f; SendInput(); }
        }

        /// <summary>Client, same-Wi-Fi: ask the network for the room (broadcast, plus every address on our subnet
        /// one by one — some phones and hotspots never pass broadcasts on).</summary>
        static void Search(float now)
        {
            if (now < nextSearch) return;
            nextSearch = now + 0.4f;
            if (joinTarget.Contains(".") && IPAddress.TryParse(joinTarget, out var ip))
            {
                Write(NetMsg.Hello); writer.Write("");
                SendTo(new IPEndPoint(ip, Port));
                return;
            }
            Write(NetMsg.Query); writer.Write(joinTarget);
            foreach (var b in BroadcastTargets()) SendTo(b);
            if (now < nextSweep) return;
            nextSweep = now + 1.5f;
            foreach (var e in SubnetTargets()) SendTo(e);
        }

        /// <summary>Client: the host runs a different build, so its world updates can't be shown here.</summary>
        public static void VersionMismatch() =>
            Lose("The host has a different version of the game. Install the same build on both devices.");

        static void Lose(string why)
        {
            LastError = why;
            Stop();
            Status = NetStatus.Lost;
            if (SceneManager.GetActiveScene().name != GameFlow.MenuScene) GameFlow.LoadMenu();
        }

        /// <summary>Host: a guest left. In the lobby its seat opens again; mid-game the level can't be finished
        /// without it, so everyone goes back to the menu.</summary>
        static void Drop(Peer p, string why)
        {
            peers.Remove(p);
            VirtualInput.Set(p.index, Vector2.zero, false, false);
            string who = "Player " + GameFlow.Letter(p.index) + " " + why;
            if (SceneManager.GetActiveScene().name != GameFlow.MenuScene) { Lose(who); return; }
            LastError = who;
            if (peers.Count == 0) Status = NetStatus.Waiting;
            SendLobby();
        }

        static Peer Find(IPEndPoint ep)
        {
            foreach (var p in peers) if (p.ep.Equals(ep)) return p;
            return null;
        }

        static int FreeSeat()
        {
            for (int i = 1; i < GameFlow.PlayerCount; i++)
            {
                bool taken = false;
                foreach (var p in peers) taken |= p.index == i;
                if (!taken) return i;
            }
            return -1;
        }

        /// <summary>Host: tell every guest how many are in and how many seats there are.</summary>
        static void SendLobby()
        {
            if (!IsHost) return;
            Write(NetMsg.Lobby); writer.Write((byte)Joined); writer.Write((byte)GameFlow.PlayerCount);
            Send();
        }

        /// <summary>Host: start with whoever is in (fewer seats than opened is fine).</summary>
        public static void LockRoom()
        {
            if (!IsHost) return;
            GameFlow.PlayerCount = Joined;
            SendLobby();
        }

        // ---------- messages in ----------

        static void Handle(byte[] data, IPEndPoint from)
        {
            using var ms = new MemoryStream(data, 2, data.Length - 2, false);
            using var r = new BinaryReader(ms, Encoding.UTF8);
            var type = (NetMsg)data[1];
            if (IsHost) HandleHost(type, r, from);
            else HandleClient(type, r, from);
        }

        static void HandleHost(NetMsg type, BinaryReader r, IPEndPoint from)
        {
            var peer = Find(from);
            if (peer != null) peer.lastReceive = Time.realtimeSinceStartup;
            switch (type)
            {
                case NetMsg.Query:
                    if (r.ReadString() == RoomCode) { Write(NetMsg.Answer); writer.Write(RoomCode); SendTo(from); }
                    return;
                case NetMsg.Hello:
                {
                    string code = r.ReadString();
                    if (code != RoomCode && code != "") return;
                    bool inGame = SceneManager.GetActiveScene().name != GameFlow.MenuScene;
                    if (peer == null)
                    {
                        int seat = FreeSeat();
                        if (seat < 0 || inGame) { Write(NetMsg.Full); SendTo(from); return; }
                        peer = new Peer { ep = from, index = seat, lastReceive = Time.realtimeSinceStartup };
                        peers.Add(peer);
                        Sfx.Play(SfxId.Checkpoint, 0.7f);
                    }
                    Status = NetStatus.Connected;
                    Write(NetMsg.Welcome); writer.Write(RoomCode); writer.Write((byte)peer.index); writer.Write((byte)GameFlow.PlayerCount);
                    SendTo(from);
                    SendLobby();
                    // already in a stage (the guest rejoined)? send it there
                    if (inGame)
                    {
                        Write(NetMsg.Scene); writer.Write(SceneManager.GetActiveScene().name); writer.Write(GameFlow.ShowIntro); writer.Write((byte)GameFlow.PlayerCount);
                        SendTo(from);
                        if (GameStages.Instance != null) SendStage(GameStages.Instance.Current, GameFlow.ShowIntro);
                    }
                    return;
                }
            }
            if (peer == null) return;
            switch (type)
            {
                case NetMsg.Input: ReadInput(peer, r); break;
                case NetMsg.Action: DoAction((NetAction)r.ReadByte(), r.ReadByte()); break;
                case NetMsg.Bye: Drop(peer, "left the room"); break;
            }
        }

        static void HandleClient(NetMsg type, BinaryReader r, IPEndPoint from)
        {
            bool fromHost = hostEp != null && hostEp.Equals(from);
            if (fromHost) lastReceive = Time.realtimeSinceStartup;
            switch (type)
            {
                case NetMsg.Answer:
                    if (Status == NetStatus.Searching && r.ReadString() == joinTarget)
                    {
                        hostEp = from;
                        Write(NetMsg.Hello); writer.Write(joinTarget);
                        SendTo(hostEp);
                    }
                    return;
                case NetMsg.Welcome:
                    if (hostEp == null || hostEp.Address.Equals(from.Address))
                    {
                        hostEp = from;
                        RoomCode = r.ReadString();
                        localIndex = Mathf.Clamp(r.ReadByte(), 1, GameFlow.MaxPlayers - 1);
                        GameFlow.PlayerCount = Mathf.Clamp(r.ReadByte(), 2, GameFlow.MaxPlayers);
                        if (Status != NetStatus.Connected) Sfx.Play(SfxId.Checkpoint, 0.7f);
                        Status = NetStatus.Connected;
                        lastReceive = Time.realtimeSinceStartup;
                    }
                    return;
                case NetMsg.Full:
                    if (Status == NetStatus.Searching)
                    {
                        Stop();
                        LastError = "Room " + joinTarget + " is full (or already playing)";
                    }
                    return;
            }
            if (!fromHost) return;
            switch (type)
            {
                case NetMsg.Lobby:
                    joined = r.ReadByte();
                    GameFlow.PlayerCount = Mathf.Clamp(r.ReadByte(), 1, GameFlow.MaxPlayers);
                    break;
                case NetMsg.Scene:
                {
                    string scene = r.ReadString();
                    GameFlow.ShowIntro = r.ReadBoolean();
                    if (r.BaseStream.Position < r.BaseStream.Length) GameFlow.PlayerCount = r.ReadByte();
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
                    if (r.BaseStream.Position < r.BaseStream.Length) GameFlow.PlayerCount = r.ReadByte();
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

        /// <summary>Guest: this device's controls (touch for its own letter, plus either keyboard set) to the host.</summary>
        static void SendInput()
        {
            int me = LocalPlayer;
            Vector2 move = VirtualInput.Move(me);
            bool jump = VirtualInput.JumpHeld(me), power = VirtualInput.PowerHeld(me);
            foreach (var p in Players.All)
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
            // the stick is relative to this player's own camera on this phone: tell the host which way it faces
            var mine = Players.Get(me);
            Vector3 view = mine != null && mine.ViewCamera != null ? mine.ViewCamera.transform.forward : Vector3.zero;
            writer.Write(view.x); writer.Write(view.y); writer.Write(view.z);
            SendTo(hostEp);
        }

        /// <summary>Guest: count presses as they happen so even a tap shorter than a packet reaches the host.</summary>
        void LateUpdate()
        {
            if (!IsClient || !Connected) return;
            int me = LocalPlayer;
            bool jumpDown = VirtualInput.JumpPressed(me), powerDown = VirtualInput.PowerPressed(me);
            foreach (var p in Players.All)
            {
                if (p == null) continue;
                jumpDown |= p.keys.JumpPressed();
                powerDown |= p.keys.PowerPressed();
            }
            if (jumpDown) lJumpCount++;
            if (powerDown) lPowerCount++;
            if (jumpDown || powerDown) SendInput();
        }

        static void ReadInput(Peer p, BinaryReader r)
        {
            p.move = new Vector2(r.ReadSByte() / 127f, r.ReadSByte() / 127f);
            byte f = r.ReadByte();
            p.jump = (f & 1) != 0;
            p.power = (f & 2) != 0;
            byte jc = r.ReadByte(), pc = r.ReadByte();
            if (p.gotInput && jc != p.jumpCount) p.jumpPulse = true;
            if (p.gotInput && pc != p.powerCount) p.powerPulse = true;
            p.jumpCount = jc;
            p.powerCount = pc;
            p.gotInput = true;
            if (r.BaseStream.Position + 12 <= r.BaseStream.Length)
            {
                var view = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var player = Players.Get(p.index);
                if (player != null) player.RemoteViewForward = view;
            }
        }

        static void FeedRemoteInput(Peer p)
        {
            // a press counted on the phone becomes one frame of "down" here, even if the button is already up again
            bool jump = p.jump || (p.jumpPulse && !p.lastJumpOut);
            bool power = p.power || (p.powerPulse && !p.lastPowerOut);
            if (jump && !p.lastJumpOut) p.jumpPulse = false;
            if (power && !p.lastPowerOut) p.powerPulse = false;
            p.lastJumpOut = jump;
            p.lastPowerOut = power;
            VirtualInput.Set(p.index, p.move, jump, power);
        }

        // ---------- actions (client -> host) ----------

        public static void SendAction(NetAction a, int arg = 0)
        {
            if (!IsClient || !Connected) return;
            Write(NetMsg.Action);
            writer.Write((byte)a);
            writer.Write((byte)arg);
            SendTo(hostEp);
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
            writer.Write((byte)GameFlow.PlayerCount);
            Send();
        }

        /// <summary>Host: a level was switched inside the Game scene.</summary>
        public static void SendStage(int stage, bool showIntro)
        {
            if (!IsHost || !Connected) return;
            Write(NetMsg.Stage);
            writer.Write(stage);
            writer.Write(showIntro);
            writer.Write((byte)GameFlow.PlayerCount);
            Send();
        }

        public static void UiWarning(HudHazard hazard, string text, float countdown, float duration)
        {
            if (!BeginUi(UiEvt.Warning)) return;
            writer.Write((byte)hazard); writer.Write(text ?? ""); writer.Write(countdown); writer.Write(duration);
            Send();
        }

        public static void UiText(int kind, string text, float duration = 0f)
        {
            if (!BeginUi((UiEvt)kind)) return;
            writer.Write(text ?? ""); writer.Write(duration);
            Send();
        }

        public static void UiToast(int player, string text)
        {
            if (!BeginUi(UiEvt.Toast)) return;
            writer.Write((byte)player); writer.Write(text ?? "");
            Send();
        }

        public static void UiObjective(HudObjective o, string text)
        {
            if (!BeginUi(UiEvt.Objective)) return;
            writer.Write((byte)o); writer.Write(text ?? "");
            Send();
        }

        public static void UiCountdown(string label, HudHazard hazard, bool start)
        {
            if (!BeginUi(start ? UiEvt.CountdownStart : UiEvt.CountdownStop)) return;
            writer.Write(label ?? ""); writer.Write((byte)hazard);
            Send();
        }

        public static void UiShake(float amount, float duration)
        {
            if (!BeginUi(UiEvt.Shake)) return;
            writer.Write(amount); writer.Write(duration);
            Send();
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
            Send();
        }

        public static void ForwardLoop(LoopId id, bool on, float volume)
        {
            if (!ForwardSounds) return;
            Write(NetMsg.Sfx);
            writer.Write((byte)SfxEvt.Loop); writer.Write((byte)id); writer.Write(on); writer.Write(volume);
            Send();
        }

        public static void ForwardStopLoops()
        {
            if (!ForwardSounds) return;
            Write(NetMsg.Sfx);
            writer.Write((byte)SfxEvt.StopLoops);
            Send();
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

        /// <summary>The packet just written goes to every guest (host) or to the host (guest).</summary>
        internal static void Send()
        {
            if (IsHost) { foreach (var p in peers) SendTo(p.ep); }
            else if (hostEp != null) SendTo(hostEp);
        }

        static void SendTo(IPEndPoint to)
        {
            if (to == null) return;
            writer.Flush();
            if (viaRelay)
            {
                if (to.Address.Equals(RelayAddress)) RelayLink.Send(to.Port - 1, outStream.GetBuffer(), (int)outStream.Length);
                return;
            }
            if (udp == null) return;
            try { udp.Send(outStream.GetBuffer(), (int)outStream.Length, to); }
            catch (Exception) { /* unreachable broadcast address etc. */ }
        }

        // ---------- addresses ----------

        /// <summary>
        /// This device's addresses on local networks (Wi-Fi, its own hotspot, Ethernet) with their prefix length —
        /// never the mobile-data one. A phone with mobile data on (or on Wi-Fi without internet) routes "everything"
        /// over mobile data, so rooms must be searched for on these interfaces explicitly.
        /// </summary>
        static List<(IPAddress ip, int prefix, string name)> lanCache;
        static float lanCacheTime = -10f;

        static List<(IPAddress ip, int prefix, string name)> LanAddresses()
        {
            // the search asks several times a second; the interfaces rarely change
            if (lanCache != null && Time.realtimeSinceStartup - lanCacheTime < 3f) return lanCache;
            lanCacheTime = Time.realtimeSinceStartup;
            var list = new List<(IPAddress, int, string)>();
#if UNITY_ANDROID && !UNITY_EDITOR
            // Mono's interface list is unreliable on Android; ask Java
            try
            {
                using var nic = new AndroidJavaClass("java.net.NetworkInterface");
                using var all = nic.CallStatic<AndroidJavaObject>("getNetworkInterfaces");
                while (all != null && all.Call<bool>("hasMoreElements"))
                {
                    using var ni = all.Call<AndroidJavaObject>("nextElement");
                    string name = ni.Call<string>("getName") ?? "";
                    if (!ni.Call<bool>("isUp") || ni.Call<bool>("isLoopback") || IsMobileData(name)) continue;
                    using var addrs = ni.Call<AndroidJavaObject>("getInterfaceAddresses");
                    int n = addrs.Call<int>("size");
                    for (int i = 0; i < n; i++)
                    {
                        using var ia = addrs.Call<AndroidJavaObject>("get", i);
                        using var inet = ia.Call<AndroidJavaObject>("getAddress");
                        string host = inet.Call<string>("getHostAddress");
                        if (host != null && host.Contains(".") && IPAddress.TryParse(host, out var ip))
                            list.Add((ip, ia.Call<short>("getNetworkPrefixLength"), name));
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[Net] interface list failed: " + e.Message); }
#endif
            if (list.Count == 0)
            {
                try
                {
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus != OperationalStatus.Up || IsMobileData(ni.Name)) continue;
                        foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ua.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ua.Address)) continue;
                            int prefix = 24;
                            if (ua.IPv4Mask != null)
                            {
                                prefix = 0;
                                foreach (byte m in ua.IPv4Mask.GetAddressBytes()) for (int bit = 7; bit >= 0; bit--) if ((m >> bit & 1) != 0) prefix++;
                            }
                            list.Add((ua.Address, prefix, ni.Name));
                        }
                    }
                }
                catch { }
            }
            // Wi-Fi and hotspot first
            list.Sort((x, y) => Rank(x.Item3).CompareTo(Rank(y.Item3)));
            lanCache = list;
            return list;
        }

        static bool IsMobileData(string name)
        {
            name = (name ?? "").ToLowerInvariant();
            foreach (var p in new[] { "rmnet", "ccmni", "pdp", "wwan", "clat", "dummy", "tun", "ppp", "ifb", "docker", "veth", "virbr" })
                if (name.StartsWith(p)) return true;
            return false;
        }

        static int Rank(string name)
        {
            name = (name ?? "").ToLowerInvariant();
            if (name.StartsWith("wlan") || name.StartsWith("swlan") || name.StartsWith("ap") || name.StartsWith("wl")) return 0;
            if (name.StartsWith("eth") || name.StartsWith("en")) return 1;
            return 2;
        }

        static string FindLocalAddress()
        {
            var lan = LanAddresses();
            if (lan.Count > 0) return lan[0].ip.ToString();
            try
            {
                using var s = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                s.Connect("10.255.255.255", 1); // no packet is sent; just picks the outgoing interface
                if (s.LocalEndPoint is IPEndPoint ep && !IPAddress.IsLoopback(ep.Address)) return ep.Address.ToString();
            }
            catch { }
            return "";
        }

        /// <summary>Where a room search goes: everyone, this machine, and each local network's own broadcast address.</summary>
        static List<IPEndPoint> BroadcastTargets()
        {
            var list = new List<IPEndPoint> { new IPEndPoint(IPAddress.Broadcast, Port), new IPEndPoint(IPAddress.Loopback, Port) };
            foreach (var (ip, prefix, _) in LanAddresses())
            {
                byte[] b = ip.GetAddressBytes();
                int p = Mathf.Clamp(prefix, 8, 30);
                uint mask = 0xFFFFFFFFu << (32 - p);
                uint addr = (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
                uint bc = addr | ~mask;
                list.Add(new IPEndPoint(new IPAddress(new[] { (byte)(bc >> 24), (byte)(bc >> 16), (byte)(bc >> 8), (byte)bc }), Port));
            }
            return list;
        }

        /// <summary>Every other address on each local /24, for phones and hotspots that drop broadcasts.</summary>
        static IEnumerable<IPEndPoint> SubnetTargets()
        {
            foreach (var (own, _, _) in LanAddresses())
            {
                var b = own.GetAddressBytes();
                for (int i = 1; i < 255; i++)
                {
                    if (i == b[3]) continue;
                    yield return new IPEndPoint(new IPAddress(new[] { b[0], b[1], b[2], (byte)i }), Port);
                }
            }
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
