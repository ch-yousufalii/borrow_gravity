using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A swing (jhoola): a platform that hangs from this object (the pivot) and swings back and forth like a pendulum.
    /// Players standing on it are carried by PlayerController's moving-platform carry (like any other moving platform),
    /// so they can wait for the swing, jump on, ride it and jump off to the next ledge.
    ///
    /// Setup:
    ///   1. Put this script on an empty GameObject at the hanging point (the pivot, up in the air).
    ///   2. Make a platform object (cube / mesh with a Collider) hanging BELOW the pivot, and drag it into "Platform".
    ///      The rope length is taken from where you placed it in the scene.
    ///   3. Optional: a LineRenderer for the rope, drag it into "Rope".
    /// The Rigidbody on the platform is added automatically (kinematic).
    /// </summary>
    [DisallowMultipleComponent]
    public class SwingPlatform : MonoBehaviour
    {
        [Tooltip("The platform that swings. Place it below this pivot in the scene.")]
        public Transform platform;
        [Tooltip("Optional rope: a LineRenderer with 2 points (pivot -> platform).")]
        public LineRenderer rope;
        [Tooltip("Optional greybox rope: a thin cube (no collider) that is stretched from the pivot to the platform.")]
        public Transform ropeCube;

        [Header("Swing")]
        [Tooltip("How far it swings to each side, in degrees.")]
        [Range(5f, 80f)] public float maxAngle = 35f;
        [Tooltip("Seconds for one full swing (there and back).")]
        [Min(0.5f)] public float period = 4f;
        [Tooltip("Start offset 0..1, so two swings can be out of step.")]
        [Range(0f, 1f)] public float phase = 0f;
        [Tooltip("Pivot-local axis the swing rotates around. (0,0,1) swings left-right when the pivot faces forward.")]
        public Vector3 swingAxis = new Vector3(0f, 0f, 1f);
        [Tooltip("Keep the platform flat like a real swing seat, instead of tilting with the rope.")]
        public bool keepLevel = true;

        float length;
        Rigidbody body;
        Quaternion baseLocalRotation;
        Vector3 lastPosition;
        Nullifiable freeze;
        float clock;

        void Awake()
        {
            if (platform == null) { enabled = false; Debug.LogWarning("[SwingPlatform] no platform assigned on " + name); return; }
            length = Mathf.Max(0.5f, Vector3.Distance(transform.position, platform.position));
            baseLocalRotation = Quaternion.Inverse(transform.rotation) * platform.rotation;
            body = platform.GetComponent<Rigidbody>();
            if (body == null) body = platform.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            freeze = platform.GetComponent<Nullifiable>();
            clock = Time.fixedTime;
            Pose(clock, out var p, out var r);
            platform.SetPositionAndRotation(p, r);
            body.position = p;
            body.rotation = r;
            lastPosition = p;
        }

        /// <summary>Where the platform should be at time t.</summary>
        void Pose(float t, out Vector3 position, out Quaternion rotation)
        {
            float angle = maxAngle * Mathf.Sin((t / period + phase) * Mathf.PI * 2f);
            var swing = Quaternion.AngleAxis(angle, swingAxis.sqrMagnitude > 0f ? swingAxis.normalized : Vector3.forward);
            position = transform.position + transform.rotation * (swing * Vector3.down * length);
            rotation = keepLevel ? transform.rotation * baseLocalRotation
                                 : transform.rotation * swing * baseLocalRotation;
        }

        void FixedUpdate()
        {
            // a seat with a Nullifiable can be frozen by Player B: the swing holds still and picks up where it stopped
            if (freeze != null && freeze.IsActive) return;
            clock += Time.fixedDeltaTime;
            Pose(clock, out var pos, out var rot);
            body.MovePosition(pos);
            body.MoveRotation(rot);
        }

        void LateUpdate()
        {
            if (platform == null) return;
            if (rope != null)
            {
                rope.positionCount = 2;
                rope.useWorldSpace = true;
                rope.SetPosition(0, transform.position);
                rope.SetPosition(1, platform.position);
            }
            if (ropeCube != null)
            {
                // greybox rope: a thin cube stretched from the pivot to the platform
                Vector3 a = transform.position, d = platform.position - a;
                ropeCube.position = a + d * 0.5f;
                if (d.sqrMagnitude > 0.0001f) ropeCube.rotation = Quaternion.LookRotation(d);
                var s = ropeCube.localScale;
                ropeCube.localScale = new Vector3(s.x, s.y, d.magnitude);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            float len = platform != null ? Vector3.Distance(transform.position, platform.position) : 4f;
            var axis = swingAxis.sqrMagnitude > 0f ? swingAxis.normalized : Vector3.forward;
            Vector3 prev = Vector3.zero;
            for (int i = -20; i <= 20; i++)
            {
                float a = maxAngle * i / 20f;
                var pt = transform.position + transform.rotation * (Quaternion.AngleAxis(a, axis) * Vector3.down * len);
                if (i > -20) Gizmos.DrawLine(prev, pt);
                prev = pt;
            }
            Gizmos.DrawLine(transform.position, transform.position + transform.rotation * (Vector3.down * len));
        }
    }
}
