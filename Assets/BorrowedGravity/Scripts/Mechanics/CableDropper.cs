using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A damaged cable that hangs high, sparks as a warning, then slams down through the passage and winches back up.
    /// </summary>
    public class CableDropper : MonoBehaviour
    {
        public float raiseHeight = 6f;
        public float period = 4.5f;
        public float offset;
        public float warnTime = 1.1f;
        public float slamTime = 0.18f;
        public float holdTime = 0.9f;
        public float retractTime = 1.2f;
        public Light warnLight;
        public Renderer warnRenderer;

        Rigidbody rb;
        Vector3 restPos;
        Hazard[] hazards;
        Material warnMat;
        int lastPhase = -1;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            restPos = transform.position;
            hazards = GetComponentsInChildren<Hazard>();
            if (warnRenderer != null) { warnMat = warnRenderer.material; warnMat.EnableKeyword("_EMISSION"); }
        }

        void FixedUpdate()
        {
            float c = Mathf.Repeat(Time.time + offset, period);
            float slamStart = period - slamTime - holdTime - retractTime;
            float warnStart = slamStart - warnTime;
            float up; // 1 = raised, 0 = down
            int phase;
            if (c < warnStart) { up = 1f; phase = 0; }
            else if (c < slamStart) { up = 1f; phase = 1; }
            else if (c < slamStart + slamTime) { float k = (c - slamStart) / slamTime; up = 1f - k * k; phase = 2; }
            else if (c < slamStart + slamTime + holdTime) { up = 0f; phase = 3; }
            else { up = (c - slamStart - slamTime - holdTime) / retractTime; phase = 4; }

            if (phase != lastPhase)
            {
                bool near = NearPlayer();
                if (phase == 1 && near) Sfx.Play(SfxId.Spark, 0.5f);
                if (phase == 3 && near) { Sfx.Play(SfxId.Impact, 0.5f, 1.3f); CoopCamera.Shake(0.6f, 0.25f); }
                lastPhase = phase;
            }
            bool armed = phase == 2 || phase == 3;
            foreach (var h in hazards) h.armed = armed;

            float warn = phase == 1 ? (Mathf.Repeat(c * 8f, 1f) > 0.5f ? 1f : 0.15f) : (armed ? 1f : 0f);
            if (warnLight != null) { warnLight.enabled = warn > 0f; warnLight.intensity = 5f * warn; }
            if (warnMat != null) warnMat.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.1f) * 4f * warn);
            rb.MovePosition(restPos + Vector3.up * raiseHeight * up);
        }

        bool NearPlayer()
        {
            foreach (var p in Players.All)
                if (p != null && Mathf.Abs(p.transform.position.x - restPos.x) < 20f) return true;
            return false;
        }
    }
}
