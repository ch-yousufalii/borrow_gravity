using UnityEngine;

namespace BorrowGravity
{
    public enum ZeroGravityResponse
    {
        /// <summary>Drifts upward until the zone ceiling (loose platforms, debris).</summary>
        Float,
        /// <summary>Stops, then glides back to the height it started floating at (boards rise again when gravity goes off).</summary>
        Hover,
        /// <summary>Keeps falling with reduced gravity (players).</summary>
        Reduced
    }

    /// <summary>
    /// Replaces Unity gravity with zone-aware gravity. Kinematic bodies (e.g. nullified ones) are skipped.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class GravityAffected : MonoBehaviour
    {
        public const float BaseGravity = 9.81f;

        public float gravityScale = 2f;
        public bool isPlayer;
        public ZeroGravityResponse zeroGResponse = ZeroGravityResponse.Hover;

        [Header("Float")]
        public float floatSpeed = 1.5f;
        public float floatAcceleration = 6f;
        [Tooltip("World Y where floating stops. Leave very high to use the zone's ceiling.")]
        public float floatCeilingY = 100000f;
        public float floatHorizontalDamping = 1.5f;

        [Header("Hover")]
        public float hoverDamping = 2.5f;
        [Tooltip("In zero-g, glide back to the starting height, so a board that fell when gravity came on rises again when it goes off.")]
        public bool returnToHomeHeight = true;
        public float homeSpeed = 2.6f;

        float homeY;
        GravityZone homeZone;

        [Header("Reduced")]
        [Range(0f, 1f)] public float reducedGravityFactor = 0.6f;

        /// <summary>Acceleration applied this step (zero when weightless).</summary>
        public Vector3 Gravity { get; private set; } = Vector3.down * BaseGravity;
        /// <summary>"Up" for this body: opposite of the last non-zero gravity.</summary>
        public Vector3 Up { get; private set; } = Vector3.up;
        public bool IsWeightless { get; private set; }
        public GravityZone CurrentZone { get; private set; }

        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
        }

        void Start()
        {
            homeY = rb.position.y;
            if (!isPlayer) homeZone = GravityZone.FindZone(rb.position, false);
        }

        /// <summary>Gravity direction at an arbitrary point for this body (unit, or zero if weightless).</summary>
        public Vector3 DirectionAt(Vector3 point)
        {
            var z = GravityZone.FindZone(point, isPlayer);
            return z == null ? Vector3.down : z.Direction(point);
        }

        void FixedUpdate()
        {
            Vector3 pos = rb.position;
            CurrentZone = GravityZone.FindZone(pos, isPlayer);
            // fell out through the bottom of its own field? that field still decides, so switching it back to
            // zero-g lifts the board up again
            if (CurrentZone == null && homeZone != null && homeZone.isActiveAndEnabled && homeZone.IsBelow(pos)) CurrentZone = homeZone;
            Vector3 dir = CurrentZone == null ? Vector3.down : CurrentZone.Direction(pos);
            float mag = BaseGravity * gravityScale * (CurrentZone != null ? CurrentZone.strength : 1f);
            IsWeightless = dir.sqrMagnitude < 0.5f;
            if (!IsWeightless) Up = -dir;

            if (rb.isKinematic)
            {
                Gravity = IsWeightless ? Vector3.zero : dir * mag;
                return;
            }

            if (!IsWeightless)
            {
                Gravity = dir * mag;
                rb.AddForce(Gravity, ForceMode.Acceleration);
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector3 v = rb.linearVelocity;
            if (isPlayer && CurrentZone != null && CurrentZone.playerZeroGLift > 0f)
            {
                float ceil = CurrentZone.TopY - 1.2f;
                float target = Mathf.Min(CurrentZone.playerZeroGLift, (ceil - pos.y) * 2f);
                v.y = Mathf.MoveTowards(v.y, target, floatAcceleration * dt);
                rb.linearVelocity = v;
                Gravity = Vector3.zero;
                Up = Vector3.up;
                return;
            }

            switch (zeroGResponse)
            {
                case ZeroGravityResponse.Reduced:
                    Gravity = -Up * mag * reducedGravityFactor;
                    rb.AddForce(Gravity, ForceMode.Acceleration);
                    break;
                case ZeroGravityResponse.Float:
                {
                    Gravity = Vector3.zero;
                    float ceil = Mathf.Min(floatCeilingY, CurrentZone != null ? CurrentZone.TopY - 0.5f : floatCeilingY);
                    float targetVy = Mathf.Min(floatSpeed, (ceil - pos.y) * 2f);
                    v.y = Mathf.MoveTowards(v.y, targetVy, floatAcceleration * dt);
                    float damp = Mathf.Exp(-floatHorizontalDamping * dt);
                    v.x *= damp;
                    v.z *= damp;
                    rb.linearVelocity = v;
                    break;
                }
                default:
                {
                    Gravity = Vector3.zero;
                    float damp = Mathf.Exp(-hoverDamping * dt);
                    if (returnToHomeHeight && !isPlayer)
                    {
                        // ease back up (or down) to the floating height, keeping wherever it was moved sideways
                        float targetVy = Mathf.Clamp((homeY - pos.y) * 1.5f, -homeSpeed, homeSpeed);
                        v.y = Mathf.MoveTowards(v.y, targetVy, floatAcceleration * dt);
                        v.x *= damp;
                        v.z *= damp;
                        rb.linearVelocity = v;
                    }
                    else rb.linearVelocity = v * damp;
                    rb.angularVelocity *= damp;
                    break;
                }
            }
        }
    }
}
