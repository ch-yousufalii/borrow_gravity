using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Drifting wreckage that periodically crosses the route on a straight path. Warning lights blink (and beep)
    /// before every pass; the piece only hurts while it is moving.
    /// </summary>
    public class PathSweeper : MonoBehaviour
    {
        public Transform mover;
        public Vector3 from;
        public Vector3 to;
        public float travelTime = 2.2f;
        public float period = 7f;
        public float offset;
        public float warnTime = 1.6f;
        public Light[] warnLights;
        public Renderer[] warnRenderers;
        public Vector3 spin = new Vector3(20f, 35f, 10f);

        Rigidbody rb;
        Hazard[] hazards;
        Material[] warnMats;
        int lastPhase = -1;

        void Awake()
        {
            rb = mover.GetComponent<Rigidbody>();
            if (rb == null) rb = mover.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            hazards = mover.GetComponentsInChildren<Hazard>();
            warnMats = new Material[warnRenderers != null ? warnRenderers.Length : 0];
            for (int i = 0; i < warnMats.Length; i++) { warnMats[i] = warnRenderers[i].material; warnMats[i].EnableKeyword("_EMISSION"); }
        }

        void FixedUpdate()
        {
            float c = Mathf.Repeat(Time.time + offset, period);
            float warnStart = period - travelTime - warnTime;
            int phase = c < warnStart ? 0 : c < warnStart + warnTime ? 1 : 2;
            float k = phase == 2 ? (c - warnStart - warnTime) / travelTime : 0f;

            if (phase != lastPhase)
            {
                if (phase == 1 && NearPlayer()) Sfx.Play(SfxId.Warning, 0.5f);
                lastPhase = phase;
            }
            foreach (var h in hazards) h.armed = phase == 2;
            float blink = phase == 1 ? (Mathf.Repeat(c * 6f, 1f) > 0.5f ? 1f : 0.1f) : phase == 2 ? 1f : 0f;
            if (warnLights != null) foreach (var l in warnLights) if (l != null) { l.enabled = blink > 0f; l.intensity = 6f * blink; }
            foreach (var m in warnMats) m.SetColor("_EmissionColor", new Color(1f, 0.25f, 0.1f) * 4f * blink);

            rb.MovePosition(Vector3.Lerp(from, to, phase == 2 ? k : 0f));
            rb.MoveRotation(rb.rotation * Quaternion.Euler(spin * Time.fixedDeltaTime));
        }

        bool NearPlayer()
        {
            foreach (var p in Players.All)
                if (p != null && Mathf.Abs(p.transform.position.x - to.x) < 25f) return true;
            return false;
        }
    }
}
