using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Turns a kinematic body around an axis — continuously, or in steps with a warning before each turn.
    /// Players standing on it are carried (see PlayerController's platform carry).
    /// </summary>
    public class Rotator : MonoBehaviour, IResettable
    {
        public int section;
        public Vector3 axis = Vector3.forward;
        public float degreesPerSecond = 8f;
        [Header("Stepped mode")]
        public bool stepped;
        public float stepAngle = 90f;
        public float stepInterval = 6f;
        public float stepDuration = 1.2f;
        public float warnTime = 2f;
        public Renderer[] warnRenderers;

        Rigidbody rb;
        Quaternion startRot;
        float angle, timer, stepFrom, stepTo, stepT = -1f;
        bool warned;
        Material[] warnMats;

        public int Section => section;
        /// <summary>Seconds until the next step turn starts (stepped mode).</summary>
        public float TimeToStep => stepped ? Mathf.Max(0f, stepInterval - timer) : 0f;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            startRot = transform.rotation;
            warnMats = new Material[warnRenderers != null ? warnRenderers.Length : 0];
            for (int i = 0; i < warnMats.Length; i++) { warnMats[i] = warnRenderers[i].material; warnMats[i].EnableKeyword("_EMISSION"); }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (!stepped)
            {
                angle += degreesPerSecond * dt;
            }
            else if (stepT >= 0f)
            {
                stepT += dt / stepDuration;
                angle = Mathf.Lerp(stepFrom, stepTo, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, stepT)));
                if (stepT >= 1f) { stepT = -1f; timer = 0f; warned = false; }
            }
            else
            {
                timer += dt;
                if (!warned && stepInterval - timer <= warnTime)
                {
                    warned = true;
                    if (NearPlayer()) { Sfx.Play(SfxId.Warning, 0.5f); GameUI.ShowWarning(HudHazard.GravityShift, "Hall rotating — get to a safe pad!", warnTime, warnTime + 0.3f); }
                }
                if (timer >= stepInterval)
                {
                    stepFrom = angle;
                    stepTo = angle + stepAngle;
                    stepT = 0f;
                    if (NearPlayer()) Sfx.Play(SfxId.Door, 0.6f, 0.6f);
                }
            }
            float blink = stepped && warned && stepT < 0f ? (Mathf.Repeat(Time.time * 6f, 1f) > 0.5f ? 1f : 0.1f) : 0f;
            foreach (var m in warnMats) m.SetColor("_EmissionColor", new Color(1f, 0.3f, 0.1f) * 4f * blink);
            rb.MoveRotation(Quaternion.AngleAxis(angle, axis) * startRot);
        }

        bool NearPlayer()
        {
            foreach (var p in Players.All)
                if (p != null && (p.transform.position - transform.position).sqrMagnitude < 900f) return true;
            return false;
        }

        public void ResetState()
        {
            angle = 0f;
            timer = 0f;
            stepT = -1f;
            warned = false;
            transform.rotation = startRot;
            rb.rotation = startRot;
        }
    }
}
