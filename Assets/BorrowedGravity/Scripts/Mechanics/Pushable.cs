using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A heavy body players move by walking into it. One player pushes slowly; two pushing together go faster.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Pushable : MonoBehaviour
    {
        public float speedOnePusher = 1.4f;
        public float speedTwoPushers = 3f;
        public float acceleration = 10f;
        public float idleFriction = 6f;

        public int PusherCount { get; private set; }

        readonly Dictionary<PlayerController, Vector3> pushes = new Dictionary<PlayerController, Vector3>();
        Rigidbody rb;
        GravityAffected grav;
        Collider col;
        Transform groundT;
        Vector3 groundLocal, groundWorld;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            grav = GetComponent<GravityAffected>();
            col = GetComponentInChildren<Collider>();
        }

        /// <summary>Velocity of whatever moving platform we rest on (so idle friction doesn't fight a ferry or lift).</summary>
        Vector3 GroundVelocity(Vector3 up)
        {
            Vector3 v = Vector3.zero;
            Transform t = null;
            float half = col != null ? Vector3.Dot(col.bounds.extents, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z))) : 0.5f;
            Vector3 origin = col != null ? col.bounds.center : rb.position;
            if (Physics.Raycast(origin, -up, out var hit, half + 0.3f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != rb)
                t = hit.collider.transform;
            if (t != null && t == groundT)
            {
                v = (t.TransformPoint(groundLocal) - groundWorld) / Time.fixedDeltaTime;
                if (v.sqrMagnitude > 400f) v = Vector3.zero;
            }
            groundT = t;
            if (t != null)
            {
                groundWorld = rb.position;
                groundLocal = t.InverseTransformPoint(groundWorld);
            }
            return v;
        }

        public void AddPush(Vector3 dir, PlayerController p) => pushes[p] = dir;

        void FixedUpdate()
        {
            PusherCount = pushes.Count;
            if (rb.isKinematic) { pushes.Clear(); return; }
            Vector3 up = grav != null ? grav.Up : Vector3.up;
            Vector3 v = rb.linearVelocity;
            float vUp = Vector3.Dot(v, up);
            Vector3 plane = v - up * vUp;
            float dt = Time.fixedDeltaTime;
            Vector3 carry = Vector3.ProjectOnPlane(GroundVelocity(up), up);
            if (pushes.Count > 0)
            {
                Vector3 dir = Vector3.zero;
                foreach (var d in pushes.Values) dir += d;
                dir = Vector3.ProjectOnPlane(dir, up).normalized;
                float speed = pushes.Count >= 2 ? speedTwoPushers : speedOnePusher;
                plane = Vector3.MoveTowards(plane, carry + dir * speed, acceleration * dt);
            }
            else if (grav == null || !grav.IsWeightless)
            {
                plane = Vector3.MoveTowards(plane, carry, idleFriction * dt + carry.magnitude * 0.5f);
            }
            rb.linearVelocity = plane + up * vUp;
            pushes.Clear();
        }
    }
}
