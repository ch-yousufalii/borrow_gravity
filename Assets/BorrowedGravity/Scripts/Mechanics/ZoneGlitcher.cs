using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Makes a zone unstable: while <see cref="activeWhile"/> is on, every few seconds the zone glitches to
    /// <see cref="glitchMode"/> (with a short warning) and Player A has to switch it back.
    /// </summary>
    public class ZoneGlitcher : MonoBehaviour
    {
        public GravityZone zone;
        public SignalSource activeWhile;
        public GravityMode glitchMode = GravityMode.Zero;
        public float minInterval = 7f, maxInterval = 11f;
        public float warnTime = 1.5f;

        float next = -1f;
        bool warned;

        void Update()
        {
            bool active = zone != null && (activeWhile == null || activeWhile.IsOn);
            if (!active) { next = -1f; return; }
            if (next < 0f) { next = Time.time + Random.Range(minInterval, maxInterval); warned = false; }
            if (!warned && Time.time >= next - warnTime && zone.Mode != glitchMode)
            {
                warned = true;
                GameUI.ShowWarning(HudHazard.GravityShift, "Gravity field failing! A — switch it back!", warnTime, warnTime + 1f);
                Sfx.Play(SfxId.Warning, 0.6f);
            }
            if (Time.time >= next)
            {
                if (zone.Mode != glitchMode)
                {
                    zone.SetMode(glitchMode);
                    Sfx.Play(SfxId.GravityShift, 0.7f, 0.7f);
                }
                next = -1f;
            }
        }
    }
}
