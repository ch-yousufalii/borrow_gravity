using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// The replacement engine on its carrier (Stage 3). Stability drops while it tilts too far or takes hard hits;
    /// at zero the engine slides off and the carrier returns to the last checkpoint.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EngineCarrier : MonoBehaviour, IResettable
    {
        public static EngineCarrier Instance { get; private set; }

        public int section;
        public float maxStability = 100f;
        public float tiltLimit = 22f;
        public float tiltDrainPerSecond = 35f;
        public float recoverPerSecond = 8f;
        public float impactThreshold = 7f;
        public float impactDamagePerUnit = 6f;
        public float killY = -25f;

        public float Stability { get; private set; }
        public float Stability01 => Stability / maxStability;
        public float Tilt { get; private set; }
        /// <summary>Signed tilt (degrees) around the camera axis, for the HUD gauge.</summary>
        public float SignedTilt { get; private set; }
        public bool Failing { get; private set; }

        Rigidbody rb;
        GravityAffected grav;
        Vector3 startPos;
        Quaternion startRot;
        float lastDamageSound;

        public int Section => section;

        void Awake()
        {
            Instance = this;
            rb = GetComponent<Rigidbody>();
            grav = GetComponent<GravityAffected>();
            startPos = transform.position;
            startRot = transform.rotation;
            Stability = maxStability;
        }

        void OnEnable() => Sfx.SetLoop(LoopId.EngineHum, true, 0.25f);
        void OnDisable() { if (Instance == this) Sfx.SetLoop(LoopId.EngineHum, false); }

        void FixedUpdate()
        {
            Vector3 up = grav != null ? grav.Up : Vector3.up;
            Tilt = Vector3.Angle(transform.up, up);
            SignedTilt = Vector3.SignedAngle(up, transform.up, Vector3.forward);
            if (rb.position.y < killY) { Fail("The engine fell into space!"); return; }
            var n = GetComponent<Nullifiable>();
            bool held = n != null && n.IsActive;
            if (!held && Tilt > tiltLimit) Damage(tiltDrainPerSecond * Time.fixedDeltaTime, null);
            else Stability = Mathf.Min(maxStability, Stability + recoverPerSecond * Time.fixedDeltaTime);
        }

        void OnCollisionEnter(Collision c)
        {
            if (Players.From(c.collider) != null) return;
            float hit = c.relativeVelocity.magnitude;
            if (hit > impactThreshold) Damage((hit - impactThreshold) * impactDamagePerUnit, "Hard landing!");
        }

        public void Damage(float amount, string reason)
        {
            if (Failing || amount <= 0f) return;
            Stability -= amount;
            if (reason != null && Time.time - lastDamageSound > 0.6f)
            {
                lastDamageSound = Time.time;
                Sfx.Play(SfxId.Hit, 0.6f, 0.7f);
                GameUI.ShowMessage(reason + " Engine stability dropping", 1.5f);
            }
            if (Stability <= 0f) Fail("The engine slid off the carrier!");
        }

        void Fail(string message)
        {
            if (Failing) return;
            Failing = true;
            Sfx.Play(SfxId.Impact, 0.9f);
            GameUI.ShowWarning(HudHazard.General, message + " Back to the checkpoint.", 0f, 3f);
            ResetState();
        }

        public void ResetState()
        {
            var carry = GetComponent<EngineCarry>();
            if (carry != null) carry.Drop(false);
            var n = GetComponent<Nullifiable>();
            if (n != null) n.ForceRelease(false);
            Vector3 p = startPos;
            Quaternion r = startRot;
            if (Checkpoint.Active != null && Checkpoint.Active.carrierSpawn != null)
            {
                p = Checkpoint.Active.carrierSpawn.position;
                r = Checkpoint.Active.carrierSpawn.rotation;
            }
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.position = p;
            rb.rotation = r;
            transform.SetPositionAndRotation(p, r);
            Stability = maxStability;
            Failing = false;
        }
    }
}
