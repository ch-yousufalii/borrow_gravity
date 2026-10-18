using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Side/three-quarter follow camera with shake. In split screen each player has one (targets = that player);
    /// with several targets it frames them all.
    /// </summary>
    public class CoopCamera : MonoBehaviour
    {
        static readonly System.Collections.Generic.List<CoopCamera> all = new System.Collections.Generic.List<CoopCamera>();

        public Transform[] targets;
        [Tooltip("Optional extra target (e.g. the engine carrier) that is kept in frame with a lower weight.")]
        public Transform extraTarget;
        public Vector3 offset = new Vector3(0f, 6.5f, -17f);
        public Vector3 lookOffset = new Vector3(0f, 0.8f, 0f);
        [Tooltip("Shifts the focus point, e.g. toward the goal so players see what is ahead.")]
        public Vector3 focusOffset = new Vector3(3f, 0f, 0f);
        public float extraDistancePerUnit = 0.55f;
        public float maxExtraDistance = 18f;
        public float minFocusY = -6f;
        public float smoothTime = 0.35f;

        Vector3 velocity, focus;
        bool initialised;
        float shakeAmount, shakeUntil;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static void Shake(float amount, float duration)
        {
            NetSession.UiShake(amount, duration);
            foreach (var c in all)
            {
                c.shakeAmount = Mathf.Max(c.shakeAmount * (Time.time < c.shakeUntil ? 1f : 0f), amount);
                c.shakeUntil = Mathf.Max(c.shakeUntil, Time.time + duration);
            }
        }

        public void SnapNow() => initialised = false;

        public static void SnapAll() { foreach (var c in all) c.SnapNow(); }

        void LateUpdate()
        {
            if (!TryGetBounds(out Bounds b)) return;
            Vector3 c = b.center + focusOffset;
            c.y = Mathf.Max(c.y, minFocusY);
            float spread = Mathf.Max(b.size.x, b.size.y * 1.6f, b.size.z * 1.2f);
            float extra = Mathf.Min(maxExtraDistance, spread * extraDistancePerUnit);
            Vector3 desired = c + offset.normalized * (offset.magnitude + extra);

            if (!initialised)
            {
                transform.position = desired;
                focus = c;
                velocity = Vector3.zero;
                initialised = true;
            }
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            focus = Vector3.Lerp(focus, c, 1f - Mathf.Exp(-8f * Time.deltaTime));
            var rot = Quaternion.LookRotation(focus + lookOffset - transform.position, Vector3.up);
            if (Time.time < shakeUntil)
            {
                float k = shakeAmount * Mathf.Clamp01((shakeUntil - Time.time) * 2f);
                rot *= Quaternion.Euler(Mathf.PerlinNoise(Time.time * 25f, 0f) * 2f * k - k, Mathf.PerlinNoise(0f, Time.time * 25f) * 2f * k - k, 0f);
            }
            transform.rotation = rot;
        }

        bool TryGetBounds(out Bounds b)
        {
            b = default;
            bool any = false;
            if (targets != null)
                foreach (var t in targets)
                {
                    if (t == null || !t.gameObject.activeInHierarchy) continue;
                    if (!any) { b = new Bounds(t.position, Vector3.zero); any = true; }
                    else b.Encapsulate(t.position);
                }
            if (any && extraTarget != null && extraTarget.gameObject.activeInHierarchy)
            {
                // keep the carrier roughly in frame without letting it dominate
                Vector3 p = Vector3.Lerp(b.center, extraTarget.position, 0.6f);
                b.Encapsulate(p);
            }
            return any;
        }
    }
}
