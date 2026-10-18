using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// An object Player B can nullify: while active it ignores gravity and holds still (kinematic) for
    /// <see cref="holdDuration"/> seconds. B can steer it sideways (never up or down — vertical moves need A's gravity).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Nullifiable : MonoBehaviour
    {
        static readonly List<Nullifiable> all = new List<Nullifiable>();
        public static IReadOnlyList<Nullifiable> All => all;

        public string displayName = "Object";
        public float holdDuration = 8f;
        [Tooltip("Heavy objects can only be nullified while they are already weightless (Player A must zero their zone).")]
        public bool requiresWeightless;
        public bool allowSteer = true;
        public float steerSpeed = 3f;
        [Tooltip("When steering, keep the object inside this world-space box (size zero = unlimited).")]
        public Bounds steerLimits;

        public Color highlightColor = new Color(1f, 0.85f, 0.2f);
        public Color activeColor = new Color(0.3f, 0.9f, 1f);
        public Light glowLight;

        public bool IsActive { get; private set; }
        public bool IsHighlighted { get; private set; }
        public float Remaining { get; private set; }
        public float RemainingFraction => holdDuration <= 0f ? 0f : Remaining / holdDuration;
        public Rigidbody Body => rb;

        Rigidbody rb;
        Material[] mats;
        Color[] baseEmission;
        bool wasKinematic;
        Vector3 steerInput;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            var list = new List<Material>();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer) continue;
                foreach (var m in r.materials) { m.EnableKeyword("_EMISSION"); list.Add(m); }
            }
            mats = list.ToArray();
            baseEmission = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                baseEmission[i] = mats[i].HasProperty("_EmissionColor") ? mats[i].GetColor("_EmissionColor") : Color.black;
            if (glowLight == null)
            {
                var lgo = new GameObject("NullifyGlow");
                lgo.transform.SetParent(transform, false);
                glowLight = lgo.AddComponent<Light>();
                glowLight.type = LightType.Point;
                glowLight.range = 6f;
                glowLight.intensity = 3f;
                glowLight.color = activeColor;
            }
            glowLight.enabled = false;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public Bounds WorldBounds
        {
            get
            {
                var c = GetComponentInChildren<Collider>();
                return c != null ? c.bounds : new Bounds(transform.position, Vector3.one);
            }
        }

        public bool CanNullify(out string reason)
        {
            reason = null;
            if (requiresWeightless)
            {
                var g = GetComponent<GravityAffected>();
                var z = GravityZone.FindZone(transform.position, false);
                bool weightless = z != null && z.Direction(transform.position).sqrMagnitude < 0.5f;
                if (!weightless && (g == null || !g.IsWeightless))
                {
                    reason = $"{displayName} is too heavy — zero its gravity first";
                    return false;
                }
            }
            return true;
        }

        public void Activate()
        {
            if (IsActive) return;
            wasKinematic = rb.isKinematic;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
            IsActive = true;
            Remaining = holdDuration;
            Refresh();
        }

        /// <summary>Ends the effect. The object falls again under its zone's gravity.</summary>
        public void ForceRelease(bool playSound = true)
        {
            steerInput = Vector3.zero;
            if (!IsActive) return;
            IsActive = false;
            rb.isKinematic = wasKinematic;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            Refresh();
            if (playSound) Sfx.Play(SfxId.Release, 0.6f);
        }

        /// <summary>Horizontal steering input in world space (x/z). Ignored if steering isn't allowed.</summary>
        public void SetSteer(Vector3 dir) => steerInput = allowSteer ? new Vector3(dir.x, 0f, dir.z) : Vector3.zero;

        public void SetHighlighted(bool on)
        {
            if (IsHighlighted == on) return;
            IsHighlighted = on;
            Refresh();
        }

        void Update()
        {
            if (!IsActive) return;
            Remaining -= Time.deltaTime;
            if (Remaining <= 0f) ForceRelease();
            else if (Remaining < 2f) Refresh(); // blink warning
        }

        void FixedUpdate()
        {
            if (!IsActive || steerInput.sqrMagnitude < 0.01f) return;
            Vector3 delta = steerInput.normalized * steerSpeed * Time.fixedDeltaTime;
            // stop at walls instead of tunnelling through them (players don't block)
            var hits = rb.SweepTestAll(delta.normalized, delta.magnitude + 0.05f, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (Players.From(h.collider) != null) continue;
                // sideways steering only collides with walls, not with the floor/ceiling it rests against
                if (Mathf.Abs(Vector3.Dot(h.normal, Vector3.up)) > 0.7f) continue;
                if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;
                delta = delta.normalized * Mathf.Max(0f, h.distance - 0.05f);
            }
            Vector3 target = rb.position + delta;
            if (steerLimits.size.sqrMagnitude > 0.01f)
            {
                target.x = Mathf.Clamp(target.x, steerLimits.min.x, steerLimits.max.x);
                target.z = Mathf.Clamp(target.z, steerLimits.min.z, steerLimits.max.z);
            }
            rb.MovePosition(target);
        }

        void Refresh()
        {
            Color e;
            if (IsActive)
            {
                bool blinkOff = Remaining < 2f && Mathf.Repeat(Time.time * 6f, 1f) > 0.5f;
                e = activeColor * (blinkOff ? 0.6f : 2.5f);
            }
            else e = IsHighlighted ? highlightColor * 1.2f : Color.black;
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetColor("_EmissionColor", e == Color.black ? baseEmission[i] : baseEmission[i] + e);
            if (glowLight != null) glowLight.enabled = IsActive;
        }
    }
}
