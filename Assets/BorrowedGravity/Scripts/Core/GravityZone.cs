using System;
using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Gravity direction of a zone. Forward = +Z (toward the back wall), Back = -Z (toward the camera).</summary>
    public enum GravityMode { Down, Up, Zero, Forward, Back, Left, Right, RadialOut, RadialIn }

    /// <summary>
    /// A marked box where gravity can differ from normal. Player A cycles controllable zones through <see cref="cycle"/>;
    /// non-controllable zones can auto-cycle on a timer (with a warning before every shift).
    /// </summary>
    public class GravityZone : MonoBehaviour, IResettable
    {
        static readonly List<GravityZone> zones = new List<GravityZone>();
        public static IReadOnlyList<GravityZone> All => zones;

        public string zoneName = "Gravity Zone";
        public int section;
        [Tooltip("Size of the zone box in local space, centred on this transform.")]
        public Vector3 size = new Vector3(10f, 10f, 10f);
        [Tooltip("Higher priority wins where zones overlap.")]
        public int priority;
        [SerializeField] GravityMode mode = GravityMode.Down;
        [Tooltip("Modes Player A steps through, in order.")]
        public GravityMode[] cycle = { GravityMode.Down, GravityMode.Zero };
        public bool controllable = true;
        public float strength = 1f;
        public bool affectsPlayers = true;
        public bool affectsObjects = true;

        [Header("Zero-g")]
        [Tooltip(">0: players in this zone drift upward at this speed while gravity is Zero.")]
        public float playerZeroGLift;

        [Header("Radial (ring walkways)")]
        public Transform radialCenter;
        [Tooltip("Axis of the ring; radial gravity ignores motion along it.")]
        public Vector3 radialAxis = Vector3.forward;
        [Tooltip("Radial zones only apply within this distance of the centre (0 = whole box).")]
        public float radialMaxDistance;

        [Header("Auto cycle (not player controlled)")]
        public bool autoCycle;
        public GravityMode[] autoSequence = { GravityMode.Down, GravityMode.Zero };
        public float[] autoDurations = { 6f, 4f };
        public float autoWarning = 2f;

        [Header("Feedback")]
        public Renderer[] indicatorRenderers;
        public Transform[] arrows;
        public bool drawOutline = true;

        public GravityMode Mode => mode;
        public bool IsHighlighted { get; private set; }
        public event Action<GravityZone> Changed;

        /// <summary>Seconds until the next automatic shift (auto zones only).</summary>
        public float AutoTimeLeft { get; private set; }
        public bool AutoWarningActive => autoCycle && AutoTimeLeft <= autoWarning;

        GravityMode initialMode;
        int autoIndex;
        LineRenderer outline;
        Material[] indicatorMats;

        void OnEnable() => zones.Add(this);
        void OnDisable() => zones.Remove(this);

        void Awake()
        {
            initialMode = mode;
            if (indicatorRenderers != null)
            {
                var list = new List<Material>();
                foreach (var r in indicatorRenderers)
                    if (r != null) { var m = r.material; m.EnableKeyword("_EMISSION"); list.Add(m); }
                indicatorMats = list.ToArray();
            }
            if (drawOutline) BuildOutline();
            if (autoCycle) StartAuto(0);
        }

        void Start() => ApplyFeedback();

        /// <summary>Joining phone: show the host's mode (the host runs the timers).</summary>
        public void ApplyRemote(GravityMode m, float autoTimeLeft)
        {
            AutoTimeLeft = autoTimeLeft;
            SetMode(m, false);
        }

        void Update()
        {
            if (autoCycle && !NetSession.IsClient)
            {
                AutoTimeLeft -= Time.deltaTime;
                if (AutoTimeLeft <= 0f) StartAuto(autoIndex + 1);
            }
            AnimateFeedback();
        }

        void StartAuto(int index)
        {
            autoIndex = index % autoSequence.Length;
            SetMode(autoSequence[autoIndex], autoIndex != 0 || Time.timeSinceLevelLoad > 0.5f);
            AutoTimeLeft = autoDurations[Mathf.Min(autoIndex, autoDurations.Length - 1)];
        }

        public void SetMode(GravityMode m, bool notify = true)
        {
            if (mode == m) return;
            mode = m;
            ApplyFeedback();
            if (notify) Changed?.Invoke(this);
        }

        public void CycleNext()
        {
            if (cycle == null || cycle.Length == 0) return;
            int i = Array.IndexOf(cycle, mode);
            SetMode(cycle[(i + 1) % cycle.Length]);
        }

        public void SetHighlighted(bool on)
        {
            if (IsHighlighted == on) return;
            IsHighlighted = on;
            ApplyFeedback();
        }

        public int Section => section;

        public void ResetState()
        {
            mode = initialMode;
            if (autoCycle) StartAuto(0);
            ApplyFeedback();
        }

        // ---------- queries ----------

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 p = transform.InverseTransformPoint(worldPoint);
            bool inBox = Mathf.Abs(p.x) <= size.x * 0.5f && Mathf.Abs(p.y) <= size.y * 0.5f && Mathf.Abs(p.z) <= size.z * 0.5f;
            if (!inBox || radialMaxDistance <= 0f) return inBox;
            Vector3 c = radialCenter != null ? radialCenter.position : transform.position;
            return Vector3.ProjectOnPlane(worldPoint - c, radialAxis).magnitude <= radialMaxDistance;
        }

        public float DistanceTo(Vector3 worldPoint)
        {
            Vector3 p = transform.InverseTransformPoint(worldPoint);
            Vector3 h = size * 0.5f;
            Vector3 d = new Vector3(Mathf.Max(0, Mathf.Abs(p.x) - h.x), Mathf.Max(0, Mathf.Abs(p.y) - h.y), Mathf.Max(0, Mathf.Abs(p.z) - h.z));
            return d.magnitude;
        }

        public float TopY => transform.TransformPoint(new Vector3(0, size.y * 0.5f, 0)).y;

        /// <summary>Unit gravity direction at a point (zero vector = weightless).</summary>
        public Vector3 Direction(Vector3 worldPoint) => DirectionFor(mode, worldPoint);

        public Vector3 DirectionFor(GravityMode m, Vector3 worldPoint)
        {
            switch (m)
            {
                case GravityMode.Down: return Vector3.down;
                case GravityMode.Up: return Vector3.up;
                case GravityMode.Forward: return Vector3.forward;
                case GravityMode.Back: return Vector3.back;
                case GravityMode.Left: return Vector3.left;
                case GravityMode.Right: return Vector3.right;
                case GravityMode.RadialIn:
                case GravityMode.RadialOut:
                    Vector3 c = radialCenter != null ? radialCenter.position : transform.position;
                    Vector3 d = Vector3.ProjectOnPlane(worldPoint - c, radialAxis);
                    if (d.sqrMagnitude < 1e-4f) return Vector3.down;
                    d.Normalize();
                    // RadialIn pulls toward the centre: you walk on the outside of a ring.
                    return m == GravityMode.RadialIn ? -d : d;
                default: return Vector3.zero;
            }
        }

        public static GravityZone FindZone(Vector3 worldPoint, bool forPlayer)
        {
            GravityZone best = null;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (forPlayer ? !z.affectsPlayers : !z.affectsObjects) continue;
                if (!z.Contains(worldPoint)) continue;
                if (best == null || z.priority > best.priority) best = z;
            }
            return best;
        }

        /// <summary>The controllable zone Player A would change from here: the one containing them, else the nearest in range.</summary>
        public static GravityZone FindControllable(Vector3 worldPoint, float range)
        {
            GravityZone best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < zones.Count; i++)
            {
                var z = zones[i];
                if (!z.controllable || !z.isActiveAndEnabled) continue;
                float d = z.DistanceTo(worldPoint);
                if (d > range) continue;
                if (d < bestDist - 0.01f || (Mathf.Abs(d - bestDist) < 0.01f && best != null && z.priority > best.priority))
                {
                    bestDist = d;
                    best = z;
                }
            }
            return best;
        }

        // ---------- feedback ----------

        public static Color ColorFor(GravityMode m)
        {
            switch (m)
            {
                case GravityMode.Down: return new Color(0.3f, 0.65f, 1f);
                case GravityMode.Up: return new Color(1f, 0.55f, 0.2f);
                case GravityMode.Zero: return new Color(1f, 0.3f, 0.85f);
                case GravityMode.Forward:
                case GravityMode.Back: return new Color(0.35f, 1f, 0.55f);
                case GravityMode.Left:
                case GravityMode.Right: return new Color(1f, 0.9f, 0.3f);
                default: return new Color(0.3f, 1f, 1f);
            }
        }

        public static string LabelFor(GravityMode m)
        {
            switch (m)
            {
                case GravityMode.Down: return "DOWN";
                case GravityMode.Up: return "UP";
                case GravityMode.Zero: return "ZERO-G";
                case GravityMode.Forward: return "TO WALL";
                case GravityMode.Back: return "TO FRONT";
                case GravityMode.Left: return "LEFT";
                case GravityMode.Right: return "RIGHT";
                case GravityMode.RadialIn: return "TO RING";
                default: return "OUTWARD";
            }
        }

        void ApplyFeedback()
        {
            Color c = ColorFor(mode);
            float intensity = IsHighlighted ? 4f : 2.2f;
            if (indicatorMats != null)
                foreach (var m in indicatorMats)
                {
                    m.SetColor("_EmissionColor", c * intensity);
                    m.SetColor("_BaseColor", Color.Lerp(Color.white, c, 0.6f));
                }
            if (outline != null)
            {
                var oc = c;
                oc.a = IsHighlighted ? 0.95f : (controllable ? 0.45f : 0.3f);
                outline.startColor = outline.endColor = oc;
                outline.widthMultiplier = IsHighlighted ? 0.12f : 0.06f;
            }
        }

        void AnimateFeedback()
        {
            if (arrows == null) return;
            float t = Time.time;
            for (int i = 0; i < arrows.Length; i++)
            {
                var a = arrows[i];
                if (a == null) continue;
                Vector3 dir = Direction(a.position);
                bool zero = dir.sqrMagnitude < 0.5f;
                if (zero)
                {
                    // weightless: arrows tumble slowly
                    a.rotation = Quaternion.Euler(0f, 0f, t * 40f + i * 90f);
                    a.localScale = Vector3.one * (0.8f + 0.1f * Mathf.Sin(t * 3f + i));
                }
                else
                {
                    Vector3 face = Mathf.Abs(Vector3.Dot(dir, Vector3.forward)) > 0.9f ? Vector3.up : Vector3.forward;
                    // The kit arrow points along its local +Y; it should point where gravity pulls.
                    a.rotation = Quaternion.LookRotation(face, dir);
                    a.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 4f + i));
                }
            }
            if (outline != null && AutoWarningActive)
            {
                float blink = Mathf.PingPong(t * 4f, 1f);
                var c = Color.Lerp(ColorFor(mode), Color.red, blink);
                c.a = 0.9f;
                outline.startColor = outline.endColor = c;
            }
        }

        void BuildOutline()
        {
            var go = new GameObject("ZoneOutline");
            go.transform.SetParent(transform, false);
            outline = go.AddComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.material = new Material(Shader.Find("Sprites/Default"));
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.numCornerVertices = 2;
            Vector3 h = size * 0.5f;
            Vector3 B(int i) => new Vector3(i == 1 || i == 2 ? h.x : -h.x, -h.y, i >= 2 ? h.z : -h.z);
            Vector3 T(int i) => B(i) + new Vector3(0, size.y, 0);
            // A single path that covers all 12 box edges.
            var pts = new[] { B(0), B(1), B(2), B(3), B(0), T(0), T(1), B(1), T(1), T(2), B(2), T(2), T(3), B(3), T(3), T(0) };
            outline.positionCount = pts.Length;
            outline.SetPositions(pts);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            var c = ColorFor(mode);
            c.a = 0.6f;
            Gizmos.color = c;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
