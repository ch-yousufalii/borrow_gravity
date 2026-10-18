using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Two-player carrying ("Carry the Glass" style): when A and B both reach the two ends of the engine stretcher it
    /// lifts between their hands and follows them. They walk slower while carrying; letting the span get too long or
    /// too short, or tilting it, drains engine stability (see <see cref="EngineCarrier"/>); separate too far and it drops.
    /// Player B nullifying the engine, a player respawning, or a socket accepting it ends the carry.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EngineCarry : MonoBehaviour
    {
        public Vector3 handleA = new Vector3(-2.35f, 0.75f, 0f);
        public Vector3 handleB = new Vector3(2.35f, 0.75f, 0f);
        public float grabRange = 1.9f;
        public float minSpan = 2.8f, maxSpan = 5.6f, dropSpan = 7.2f;
        public float spanDrainPerSecond = 30f;
        public float carrySpeedFactor = 0.78f;
        public float handHeight = 0.05f;
        public float regrabDelay = 1.2f;

        public bool IsCarried { get; private set; }
        public float Span { get; private set; }
        /// <summary>0..1: how far outside the comfortable span the players are (for the HUD).</summary>
        public float Strain { get; private set; }

        Rigidbody rb;
        EngineCarrier engine;
        Nullifiable nullifiable;
        PlayerController left, right;
        float droppedAt = -10f;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            engine = GetComponent<EngineCarrier>();
            nullifiable = GetComponent<Nullifiable>();
        }

        PlayerController hookedA, hookedB;

        void Hook(PlayerController a, PlayerController b)
        {
            if (a != hookedA) { if (hookedA != null) hookedA.Respawned -= OnRespawn; hookedA = a; if (a != null) a.Respawned += OnRespawn; }
            if (b != hookedB) { if (hookedB != null) hookedB.Respawned -= OnRespawn; hookedB = b; if (b != null) b.Respawned += OnRespawn; }
        }

        void OnDisable()
        {
            Hook(null, null);
            if (IsCarried) Drop(false);
        }

        void OnRespawn(PlayerController p) { if (IsCarried) Drop(true, "Someone fell — the engine slipped!"); }

        public Vector3 HandleWorld(bool a) => transform.TransformPoint(a ? handleA : handleB);

        void FixedUpdate()
        {
            var a = Players.Get(0);
            var b = Players.Get(1);
            if (a == null || b == null) return;
            Hook(a, b);
            if (!IsCarried) { TryGrab(a, b); return; }

            if ((nullifiable != null && nullifiable.IsActive) || !enabled) { Drop(false); return; }

            Vector3 pa = Hand(left), pb = Hand(right);
            Vector3 up = (left.Up + right.Up).normalized;
            Vector3 flat = Vector3.ProjectOnPlane(pb - pa, up);
            Span = flat.magnitude;
            Strain = Span > maxSpan ? Mathf.InverseLerp(maxSpan, dropSpan, Span) : Span < minSpan ? Mathf.InverseLerp(minSpan, minSpan - 1.5f, Span) : 0f;
            if (Span > dropSpan) { Drop(true, "Too far apart — the engine dropped!"); return; }
            if (Strain > 0f && engine != null) engine.Damage(spanDrainPerSecond * Strain * Time.fixedDeltaTime, null);

            // orientation: local X runs from A's handle to B's handle, local Y follows gravity
            Vector3 dir = (pb - pa).sqrMagnitude > 0.01f ? (pb - pa).normalized : transform.right;
            Vector3 z = Vector3.Cross(dir, up);
            if (z.sqrMagnitude < 0.01f) z = Vector3.forward;
            z.Normalize();
            Vector3 y = Vector3.Cross(z, dir).normalized;
            var rot = Quaternion.LookRotation(z, y);
            Vector3 mid = (pa + pb) * 0.5f;
            Vector3 handleMid = (handleA + handleB) * 0.5f;
            Vector3 pos = mid - rot * handleMid;
            rb.MovePosition(Vector3.Lerp(rb.position, pos, 0.5f));
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, rot, 0.35f));

            left.CarryLoad = carrySpeedFactor;
            right.CarryLoad = carrySpeedFactor;
            left.CarryingUntil = right.CarryingUntil = Time.time + 0.15f;
        }

        Vector3 Hand(PlayerController p) => p.transform.position + p.Up * handHeight;

        void TryGrab(PlayerController a, PlayerController b)
        {
            if (Time.time - droppedAt < regrabDelay) return;
            if (nullifiable != null && nullifiable.IsActive) return;
            if (rb.isKinematic && (nullifiable == null || !nullifiable.enabled)) return; // locked in a socket
            if (!a.IsGrounded || !b.IsGrounded) return;
            Vector3 ha = HandleWorld(true), hb = HandleWorld(false);
            float straight = Vector3.Distance(a.transform.position, ha) + Vector3.Distance(b.transform.position, hb);
            float swapped = Vector3.Distance(a.transform.position, hb) + Vector3.Distance(b.transform.position, ha);
            bool swap = swapped < straight;
            var l = swap ? b : a;
            var r = swap ? a : b;
            if (Vector3.Distance(l.transform.position, ha) > grabRange || Vector3.Distance(r.transform.position, hb) > grabRange) return;
            left = l;
            right = r;
            IsCarried = true;
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
            Sfx.Play(SfxId.Plate, 0.7f, 0.8f);
            GameUI.ShowMessage("Engine lifted — stay together and keep it level!", 2.5f);
        }

        /// <summary>Let go. With <paramref name="hard"/> it falls (and may take a hit when it lands).</summary>
        public void Drop(bool hard, string message = null)
        {
            if (!IsCarried) return;
            IsCarried = false;
            droppedAt = Time.time;
            if (left != null) left.CarryLoad = 1f;
            if (right != null) right.CarryLoad = 1f;
            left = right = null;
            Strain = 0f;
            bool held = nullifiable != null && nullifiable.IsActive;
            if (!held) rb.isKinematic = false;
            if (hard)
            {
                Sfx.Play(SfxId.Hit, 0.7f, 0.7f);
                if (!string.IsNullOrEmpty(message)) GameUI.ShowWarning(HudHazard.General, message, 0f, 2.5f);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.TransformPoint(handleA), 0.2f);
            Gizmos.DrawWireSphere(transform.TransformPoint(handleB), 0.2f);
        }
    }
}
