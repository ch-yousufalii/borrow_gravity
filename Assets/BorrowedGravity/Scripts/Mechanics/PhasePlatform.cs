using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A platform that is solid for a while, blinks as a warning, then fades out of existence and comes back
    /// (Level 4, Event Horizon Run). <see cref="offset"/> staggers a row of them so they come and go in a wave.
    /// </summary>
    public class PhasePlatform : MonoBehaviour, IResettable
    {
        public int section;
        public float solidTime = 3f;
        public float warnTime = 1f;
        public float goneTime = 1.2f;
        [Tooltip("Seconds into the cycle at the start (staggers neighbours).")]
        public float offset;

        Renderer[] renderers;
        Collider[] colliders;
        float t;
        bool solid = true, visible = true;

        public int Section => section;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            t = offset;
        }

        void Update()
        {
            t += Time.deltaTime;
            float cycle = solidTime + warnTime + goneTime;
            float p = Mathf.Repeat(t, cycle);
            bool nowSolid = p < solidTime + warnTime;
            // warning: blink faster as the end gets closer
            bool nowVisible = nowSolid && (p < solidTime || Mathf.Repeat(p - solidTime, 0.25f) < 0.16f);
            if (nowSolid != solid)
            {
                solid = nowSolid;
                foreach (var c in colliders) c.enabled = solid;
                if (solid) Sfx.Play(SfxId.Tick, 0.25f, 0.8f);
            }
            if (nowVisible != visible)
            {
                visible = nowVisible;
                foreach (var r in renderers) r.enabled = visible;
            }
        }

        public void ResetState() => t = offset;
    }
}
