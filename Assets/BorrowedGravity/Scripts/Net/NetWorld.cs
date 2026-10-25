using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowGravity
{
    /// <summary>
    /// Keeps the joining phone's copy of a stage in step with the host. Both phones load the same scene, so every
    /// transform gets the same index (scene order). The host sends whatever moved, appeared or lit up; the client has
    /// its gameplay scripts, physics and colliders switched off and just shows what it is told. Also sets up the
    /// one-player-per-screen camera and streams the HUD values.
    /// </summary>
    public class NetWorld : MonoBehaviour
    {
        public static NetWorld Instance { get; private set; }

        const float SendRate = 30f, HudRate = 10f;
        const int MaxPacket = 1150;

        // gameplay scripts the client keeps running (everything else in the BorrowGravity namespace is switched off)
        static readonly HashSet<string> ClientKeeps = new HashSet<string>
        {
            nameof(PlayerController), nameof(GameUI), nameof(CoopCamera), nameof(VirtualControls), nameof(SuitAnimator),
            nameof(TimeOfDay), nameof(PlayerCameraLink), nameof(StageManager), nameof(NetWorld), nameof(GravityZone),
            nameof(Cutscene), nameof(GameStages), nameof(StageRoot), nameof(InfoBoard)
        };

        readonly List<Transform> list = new List<Transform>();
        readonly Dictionary<Transform, int> indexOf = new Dictionary<Transform, int>();
        Vector3[] sentPos, sentScale, initialScale;
        Quaternion[] sentRot;
        byte[] sentFlags;
        float[] sentLight;
        Renderer[] renderers;
        Light[] lights;
        readonly List<int> everMoved = new List<int>();
        readonly HashSet<int> everMovedSet = new HashSet<int>();
        int roll;
        GravityZone[] zones;

        // dynamic objects spawned at runtime (storm debris, conveyor crates)
        readonly Dictionary<int, Transform> spawned = new Dictionary<int, Transform>();
        int nextSpawnId;

        // client smoothing
        readonly Dictionary<Transform, (Vector3 pos, Quaternion rot)> targets = new Dictionary<Transform, (Vector3, Quaternion)>();
        float nextSend, nextHud;

        public static void Setup(Scene scene)
        {
            var go = new GameObject("NetWorld");
            SceneManager.MoveGameObjectToScene(go, scene);
            var w = go.AddComponent<NetWorld>();
            w.Build(scene);
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == gameObject) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.GetComponentInParent<Camera>(true) != null) continue;
                    if (t.GetComponentInParent<InfoBoard>(true) != null) continue; // each device turns its boards to its own camera
                    indexOf[t] = list.Count;
                    list.Add(t);
                }
            }
            int n = list.Count;
            sentPos = new Vector3[n];
            sentScale = new Vector3[n];
            initialScale = new Vector3[n];
            sentRot = new Quaternion[n];
            sentFlags = new byte[n];
            sentLight = new float[n];
            renderers = new Renderer[n];
            lights = new Light[n];
            for (int i = 0; i < n; i++)
            {
                var t = list[i];
                renderers[i] = t.GetComponent<Renderer>();
                lights[i] = t.GetComponent<Light>();
                sentPos[i] = t.localPosition;
                sentRot[i] = t.localRotation;
                sentScale[i] = initialScale[i] = t.localScale;
                sentFlags[i] = Flags(i);
                sentLight[i] = lights[i] != null ? lights[i].intensity : 0f;
            }
            nextSpawnId = n;
            ComputeFingerprint();
            zones = FindObjectsByType<GravityZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(zones, (a, b) => Index(a.transform).CompareTo(Index(b.transform)));

            SetupScreens();
            if (NetSession.IsClient) MakePuppet(scene);
        }

        /// <summary>Same on two devices only when both run the same build of the scene (objects are matched by order).</summary>
        uint fingerprint;

        void ComputeFingerprint()
        {
            uint h = 2166136261u;
            void Mix(int v) { h = (h ^ (uint)v) * 16777619u; }
            Mix(list.Count);
            foreach (var t in list) foreach (char c in t.name) Mix(c); // own hash: string.GetHashCode differs between runtimes
            fingerprint = h;
        }

        int Index(Transform t) => t != null && indexOf.TryGetValue(t, out int i) ? i : -1;

        byte Flags(int i)
        {
            var t = list[i];
            byte f = 0;
            if (t == null) return 32;
            if (t.gameObject.activeSelf) f |= 1;
            if (renderers[i] != null && renderers[i].enabled) f |= 2;
            if (lights[i] != null && lights[i].enabled) f |= 4;
            return f;
        }

        /// <summary>A new level came on inside the same scene: its players have their own cameras to set up.</summary>
        public void OnStageShown()
        {
            targets.Clear();
            SetupScreens();
        }

        /// <summary>One player per phone: this phone's camera fills the screen, the other players' are switched off.</summary>
        void SetupScreens()
        {
            var mine = Players.Get(NetSession.LocalPlayer);
            var myCam = mine != null ? mine.ViewCamera : null;
            if (myCam == null) return;
            myCam.gameObject.SetActive(true);
            myCam.rect = new Rect(0f, 0f, 1f, 1f);
            myCam.tag = "MainCamera";
            bool hadListener = false;
            foreach (var p in Players.All)
            {
                var cam = p != null ? p.ViewCamera : null;
                if (cam == null || cam == myCam) continue;
                if (cam.CompareTag("MainCamera")) cam.tag = "Untagged";
                hadListener |= cam.GetComponent<AudioListener>() != null;
                cam.gameObject.SetActive(false);
            }
            if (hadListener && myCam.GetComponent<AudioListener>() == null) myCam.gameObject.AddComponent<AudioListener>();
        }

        /// <summary>Client: the host decides everything, so stop local gameplay, physics and triggers.</summary>
        void MakePuppet(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var type = mb.GetType();
                    if (type.Namespace == "BorrowGravity" && !ClientKeeps.Contains(type.Name)) mb.enabled = false;
                }
                foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                    rb.detectCollisions = false;
                }
                foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            }
        }

        // ---------- host ----------

        /// <summary>Host: a runtime copy of a scene object (e.g. storm debris) — create the same on the client.</summary>
        public static void Spawned(GameObject template, GameObject instance)
        {
            var w = Instance;
            if (w == null || !NetSession.IsHost || template == null || instance == null) return;
            int ti = w.Index(template.transform);
            if (ti < 0) return;
            int id = w.nextSpawnId++;
            w.spawned[id] = instance.transform;
            var bw = NetSession.Write(NetMsg.Spawn);
            bw.Write(id);
            bw.Write(ti);
            bw.Write(w.Index(instance.transform.parent));
            NetSession.Send();
        }

        void LateUpdate()
        {
            if (NetSession.IsHost && NetSession.Connected)
            {
                float now = Time.unscaledTime;
                if (now >= nextSend) { nextSend = now + 1f / SendRate; SendWorld(); }
                if (now >= nextHud) { nextHud = now + 1f / HudRate; SendHud(); }
            }
            else if (NetSession.IsClient) Smooth();
        }

        BinaryWriter packet;
        int packetCount;

        void BeginPacket()
        {
            packet = NetSession.Write(NetMsg.World);
            packet.Write((ushort)0); // entry count, patched on flush
            packet.Write(fingerprint);
            packetCount = 0;
        }

        void FlushPacket()
        {
            if (packetCount == 0) return;
            packet.Flush();
            var ms = (MemoryStream)packet.BaseStream;
            long end = ms.Position;
            ms.Position = 2;
            packet.Write((ushort)packetCount);
            ms.Position = end;
            NetSession.Send();
            BeginPacket();
        }

        void SendWorld()
        {
            BeginPacket();
            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                var t = list[i];
                byte f = Flags(i);
                if (t == null)
                {
                    if (sentFlags[i] != 32) { sentFlags[i] = 32; WriteEntry(i, null, 32, -1f); }
                    continue;
                }
                Vector3 p = t.localPosition, s = t.localScale;
                Quaternion q = t.localRotation;
                float li = lights[i] != null ? lights[i].intensity : 0f;
                bool changed = f != sentFlags[i] || (p - sentPos[i]).sqrMagnitude > 1e-6f || Quaternion.Dot(q, sentRot[i]) < 0.99999f
                               || (s - sentScale[i]).sqrMagnitude > 1e-6f || Mathf.Abs(li - sentLight[i]) > 0.02f;
                if (!changed) continue;
                sentFlags[i] = f; sentPos[i] = p; sentRot[i] = q; sentScale[i] = s; sentLight[i] = li;
                if (everMovedSet.Add(i)) everMoved.Add(i);
                WriteEntry(i, t, f, li);
            }
            // resend a few of everything that has ever changed, so a lost packet heals within a second or so
            for (int k = 0; k < Mathf.Min(24, everMoved.Count); k++)
            {
                roll = (roll + 1) % everMoved.Count;
                int i = everMoved[roll];
                var t = list[i];
                WriteEntry(i, t, t == null ? (byte)32 : sentFlags[i], sentLight[i]);
            }
            // spawned objects move every frame; send them all, and say goodbye to the ones that are gone
            List<int> gone = null;
            foreach (var kv in spawned)
            {
                if (kv.Value == null) { (gone ??= new List<int>()).Add(kv.Key); continue; }
                WriteEntry(kv.Key, kv.Value, (byte)(kv.Value.gameObject.activeSelf ? 3 : 0), -1f);
            }
            FlushPacket();
            if (gone != null)
                foreach (int id in gone)
                {
                    spawned.Remove(id);
                    NetSession.Write(NetMsg.Despawn).Write(id);
                    NetSession.Send();
                }
        }

        void WriteEntry(int i, Transform t, byte flags, float light)
        {
            if (NetSession.WrittenBytes > MaxPacket) FlushPacket();
            bool scaled = t != null && i < list.Count && (t.localScale - initialScale[i]).sqrMagnitude > 1e-6f;
            bool lit = light >= 0f && i < list.Count && lights[i] != null;
            if (scaled) flags |= 8;
            if (lit) flags |= 16;
            packet.Write(i);
            packet.Write(flags);
            if (t == null) { packetCount++; return; }
            var p = t.localPosition;
            packet.Write(p.x); packet.Write(p.y); packet.Write(p.z);
            var q = t.localRotation;
            packet.Write((short)(q.x * 32767f)); packet.Write((short)(q.y * 32767f));
            packet.Write((short)(q.z * 32767f)); packet.Write((short)(q.w * 32767f));
            if (scaled) { var s = t.localScale; packet.Write(s.x); packet.Write(s.y); packet.Write(s.z); }
            if (lit) packet.Write(light);
            packetCount++;
        }

        void SendHud()
        {
            var w = NetSession.Write(NetMsg.Hud);
            var sm = StageManager.Instance;
            w.Write((byte)(sm != null ? sm.State : StageState.Playing));
            w.Write(sm != null ? sm.StageTime : 0f);
            w.Write((short)(sm != null ? sm.Deaths : 0));
            w.Write(sm != null ? sm.ResetHold : 0f);

            // every player's power panel (each joined device draws its own)
            for (int i = 0; i < 4; i++)
            {
                var info = GameUI.PanelFor(Players.Get(i));
                w.Write(info.role ?? "");
                w.Write(info.line1 ?? "");
                w.Write(info.line2 ?? "");
                w.Write(info.fill);
                w.Write(info.active);
            }

            var e = EngineCarrier.Instance;
            w.Write(e != null);
            w.Write(e != null ? e.Stability01 : 1f);
            w.Write(e != null ? e.Tilt : 0f);
            w.Write(e != null ? e.SignedTilt : 0f);
            w.Write(e != null ? e.tiltLimit : 30f);
            w.Write(GameUI.CountdownLeft);
            w.Write((byte)GameUI.PartsMask());

            for (int p = 0; p < 4; p++)
            {
                var pc = Players.Get(p);
                w.Write((byte)((pc != null && pc.IsPushing ? 1 : 0) | (pc != null && pc.IsCarrying ? 2 : 0)));
            }
            w.Write((byte)zones.Length);
            foreach (var z in zones)
            {
                w.Write((byte)(z != null ? z.Mode : GravityMode.Down));
                w.Write(z != null ? z.AutoTimeLeft : 0f);
            }
            NetSession.Send();
        }

        // ---------- client ----------

        public void ReadWorld(BinaryReader r)
        {
            int count = r.ReadUInt16();
            if (r.ReadUInt32() != fingerprint)
            {
                // a different build of the game: every update would land on the wrong object
                NetSession.VersionMismatch();
                return;
            }
            for (int k = 0; k < count; k++)
            {
                int i = r.ReadInt32();
                byte f = r.ReadByte();
                Transform t = i < list.Count ? list[i] : (spawned.TryGetValue(i, out var st) ? st : null);
                if ((f & 32) != 0)
                {
                    if (t != null) t.gameObject.SetActive(false);
                    continue;
                }
                var p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var q = new Quaternion(r.ReadInt16() / 32767f, r.ReadInt16() / 32767f, r.ReadInt16() / 32767f, r.ReadInt16() / 32767f);
                Vector3 s = Vector3.zero;
                if ((f & 8) != 0) s = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                float li = (f & 16) != 0 ? r.ReadSingle() : -1f;
                if (t == null) continue;

                q = q.normalized;
                bool active = (f & 1) != 0;
                if (t.gameObject.activeSelf != active) t.gameObject.SetActive(active);
                if ((f & 8) != 0) t.localScale = s;
                else if (i < list.Count) t.localScale = initialScale[i];
                if (i < list.Count)
                {
                    if (renderers[i] != null) renderers[i].enabled = (f & 2) != 0;
                    if (lights[i] != null) { lights[i].enabled = (f & 4) != 0; if (li >= 0f) lights[i].intensity = li; }
                }
                // big jumps (respawns, resets) snap; small steps are smoothed between packets
                if ((t.localPosition - p).sqrMagnitude > 9f) { t.localPosition = p; t.localRotation = q; }
                targets[t] = (p, q);
            }
        }

        void Smooth()
        {
            float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            List<Transform> dead = null;
            foreach (var kv in targets)
            {
                var t = kv.Key;
                if (t == null) { (dead ??= new List<Transform>()).Add(t); continue; }
                t.localPosition = Vector3.Lerp(t.localPosition, kv.Value.pos, k);
                t.localRotation = Quaternion.Slerp(t.localRotation, kv.Value.rot, k);
            }
            if (dead != null) foreach (var t in dead) targets.Remove(t);
        }

        public void ReadHud(BinaryReader r)
        {
            var state = (StageState)r.ReadByte();
            float time = r.ReadSingle();
            int deaths = r.ReadInt16();
            NetHud.resetHold = r.ReadSingle();
            StageManager.Instance?.ApplyRemote(state, time, deaths, NetHud.resetHold);

            for (int i = 0; i < 4; i++)
                NetHud.SetPanel(i, new GameUI.PanelInfo { role = r.ReadString(), line1 = r.ReadString(), line2 = r.ReadString(), fill = r.ReadSingle(), active = r.ReadBoolean() });

            NetHud.hasEngine = r.ReadBoolean();
            NetHud.engineStability01 = r.ReadSingle();
            NetHud.engineTilt = r.ReadSingle();
            NetHud.engineSignedTilt = r.ReadSingle();
            NetHud.engineTiltLimit = r.ReadSingle();
            NetHud.countdownLeft = r.ReadSingle();
            NetHud.partsMask = r.ReadByte();

            for (int p = 0; p < 4; p++)
            {
                byte f = r.ReadByte();
                Players.Get(p)?.SetRemoteFlags((f & 1) != 0, (f & 2) != 0);
            }
            int zc = r.ReadByte();
            for (int z = 0; z < zc; z++)
            {
                var mode = (GravityMode)r.ReadByte();
                float left = r.ReadSingle();
                if (z < zones.Length && zones[z] != null) zones[z].ApplyRemote(mode, left);
            }
        }

        public void ReadSpawn(BinaryReader r)
        {
            int id = r.ReadInt32(), ti = r.ReadInt32(), pi = r.ReadInt32();
            if (ti < 0 || ti >= list.Count || list[ti] == null || spawned.ContainsKey(id)) return;
            var parent = pi >= 0 && pi < list.Count ? list[pi] : list[ti].parent;
            var go = Instantiate(list[ti].gameObject, parent);
            go.SetActive(true);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb.GetType().Namespace == "BorrowGravity") mb.enabled = false;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.detectCollisions = false; }
            Destroy(go, 15f); // in case the goodbye packet is lost
            spawned[id] = go.transform;
        }

        public void ReadDespawn(BinaryReader r)
        {
            int id = r.ReadInt32();
            if (spawned.TryGetValue(id, out var t))
            {
                if (t != null) Destroy(t.gameObject);
                spawned.Remove(id);
            }
        }
    }
}
