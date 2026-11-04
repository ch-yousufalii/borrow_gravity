using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// One of the Warden's ground strikes: a warning disc shows where it lands, then a beam hits and shoves away anyone
    /// standing there. The boss reuses a small pool of these.
    /// </summary>
    public class WardenStrike : MonoBehaviour
    {
        public GameObject disc;
        public GameObject beam;
        public Hazard hit;
        public float beamSeconds = 0.35f;

        float fireAt = -1f, endAt = -1f;

        public bool Busy => fireAt >= 0f || endAt >= 0f;

        void Awake() => Clear();

        public void Fire(Vector3 at, float telegraph)
        {
            transform.position = at;
            disc.SetActive(true);
            disc.transform.localScale = new Vector3(0.2f, disc.transform.localScale.y, 0.2f);
            fireAt = Time.time + telegraph;
            telegraphSeconds = telegraph;
            endAt = -1f;
            Sfx.Play(SfxId.Warning, 0.35f, 1.3f);
        }

        float telegraphSeconds = 1f;
        const float DiscScale = 5.2f;

        void Update()
        {
            if (fireAt >= 0f)
            {
                float k = 1f - Mathf.Clamp01((fireAt - Time.time) / Mathf.Max(0.05f, telegraphSeconds));
                float s = Mathf.Lerp(0.2f, DiscScale, k);
                disc.transform.localScale = new Vector3(s, disc.transform.localScale.y, s);
                if (Time.time >= fireAt)
                {
                    fireAt = -1f;
                    endAt = Time.time + beamSeconds;
                    beam.SetActive(true);
                    hit.armed = true;
                    Sfx.Play(SfxId.Impact, 0.7f);
                    CoopCamera.Shake(0.2f, 0.25f);
                }
            }
            else if (endAt >= 0f && Time.time >= endAt) Clear();
        }

        public void Clear()
        {
            fireAt = -1f;
            endAt = -1f;
            if (disc != null) disc.SetActive(false);
            if (beam != null) beam.SetActive(false);
            if (hit != null) hit.armed = false;
        }
    }
}
