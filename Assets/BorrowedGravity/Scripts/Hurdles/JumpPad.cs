using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Gravity booster: launches players (and anything loose) up and forward. Glows and pulses when used.</summary>
    [RequireComponent(typeof(Collider))]
    public class JumpPad : MonoBehaviour
    {
        public float launchUp = 15f;
        public Vector3 launchForward = new Vector3(5f, 0f, 0f);
        public Renderer glow;
        public Color color = new Color(0.35f, 1f, 0.8f);
        public float cooldown = 0.4f;

        Material mat;
        float lastFire = -10f;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (glow != null) { mat = glow.material; mat.EnableKeyword("_EMISSION"); }
        }

        void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            var p = Players.From(other);
            if (p == null && rb.GetComponent<GravityAffected>() == null) return;
            Vector3 v = rb.linearVelocity;
            v = Vector3.ProjectOnPlane(v, Vector3.up) * 0.3f + launchForward;
            v.y = launchUp;
            rb.linearVelocity = v;
            if (Time.time - lastFire > cooldown)
            {
                lastFire = Time.time;
                Sfx.Play(SfxId.GravityShift, 0.6f, 1.6f);
            }
        }

        void Update()
        {
            if (mat == null) return;
            float k = Mathf.Clamp01(1f - (Time.time - lastFire) * 2f);
            float idle = 1.2f + 0.6f * Mathf.Sin(Time.time * 4f);
            mat.SetColor("_EmissionColor", color * (idle + k * 5f));
        }
    }
}
