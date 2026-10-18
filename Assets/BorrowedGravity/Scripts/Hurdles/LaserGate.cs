using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A laser fence across the route that pulses on and off. It flickers as a warning before switching on; touching
    /// it while on sends players back to the checkpoint (and hurts the engine).
    /// </summary>
    public class LaserGate : MonoBehaviour
    {
        public float onTime = 1.6f;
        public float offTime = 2.2f;
        public float warnTime = 0.7f;
        public float offset;
        public Renderer[] beams;
        public Color color = new Color(1f, 0.15f, 0.2f);

        Hazard hazard;
        Material[] mats;
        bool wasOn;

        void Awake()
        {
            hazard = GetComponentInChildren<Hazard>();
            mats = new Material[beams != null ? beams.Length : 0];
            for (int i = 0; i < mats.Length; i++) { mats[i] = beams[i].material; mats[i].EnableKeyword("_EMISSION"); }
        }

        void Update()
        {
            float period = onTime + offTime;
            float c = Mathf.Repeat(Time.time + offset, period);
            bool on = c < onTime;
            bool warning = !on && c > period - warnTime;
            if (hazard != null) hazard.armed = on;
            float k = on ? 1f : warning ? (Mathf.Repeat(Time.time * 14f, 1f) > 0.5f ? 0.35f : 0.05f) : 0f;
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i].SetColor("_EmissionColor", color * 6f * k);
                mats[i].SetColor("_BaseColor", new Color(color.r, color.g, color.b, 0.15f + 0.85f * k));
                beams[i].enabled = k > 0f;
            }
            if (on && !wasOn && Near()) Sfx.Play(SfxId.Spark, 0.35f, 1.4f);
            wasOn = on;
        }

        bool Near()
        {
            foreach (var p in Players.All)
                if (p != null && (p.transform.position - transform.position).sqrMagnitude < 400f) return true;
            return false;
        }
    }
}
