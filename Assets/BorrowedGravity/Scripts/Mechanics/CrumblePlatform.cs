using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A weak floor piece: cracks (shakes and flashes red) when stepped on or triggered, then falls away and,
    /// optionally, regenerates. Used for weak bridge panels, powered-down platforms and collapsing bridges.
    /// </summary>
    public class CrumblePlatform : MonoBehaviour, IResettable
    {
        public enum State { Solid, Warning, Falling, Gone }

        public int section;
        public bool triggerOnStep = true;
        public float crackTime = 1.1f;
        public float fallTime = 1.6f;
        public float fallDistance = 28f;
        [Tooltip("Seconds after falling before it comes back (<= 0: never).")]
        public float regenerateAfter = 5f;
        public Color warnColor = new Color(1f, 0.15f, 0.1f);

        public State Current { get; private set; } = State.Solid;

        Vector3 startPos;
        Quaternion startRot;
        Rigidbody rb;
        Collider[] cols;
        Renderer[] rends;
        readonly List<Material> mats = new List<Material>();
        readonly List<Color> baseEmission = new List<Color>();
        float stateTime, stateDuration;
        Transform visual;
        Vector3 visualRest;

        public int Section => section;

        void Awake()
        {
            startPos = transform.position;
            startRot = transform.rotation;
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            cols = GetComponentsInChildren<Collider>();
            rends = GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
                foreach (var m in r.materials)
                {
                    m.EnableKeyword("_EMISSION");
                    mats.Add(m);
                    baseEmission.Add(m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black);
                }
            visual = rends.Length > 0 && rends[0].transform != transform ? rends[0].transform : null;
            if (visual != null) visualRest = visual.localPosition;
        }

        void OnCollisionStay(Collision c)
        {
            if (!triggerOnStep || Current != State.Solid) return;
            var p = Players.From(c.collider);
            bool carrier = c.collider.GetComponentInParent<EngineCarrier>() != null;
            if (p == null && !carrier) return;
            // only when standing on top
            if (c.collider.bounds.center.y < GetBoundsTop() - 0.3f) return;
            Trigger(crackTime);
        }

        float GetBoundsTop()
        {
            float top = transform.position.y;
            foreach (var col in cols) if (col.enabled) top = Mathf.Max(top, col.bounds.max.y);
            return top;
        }

        /// <summary>Start cracking now; it falls after <paramref name="warnTime"/> seconds.</summary>
        public void Trigger(float warnTime)
        {
            if (Current != State.Solid) return;
            SetState(State.Warning, warnTime);
            Sfx.Play(SfxId.Crack, 0.7f);
        }

        void SetState(State s, float duration)
        {
            Current = s;
            stateTime = 0f;
            stateDuration = duration;
        }

        void FixedUpdate()
        {
            stateTime += Time.fixedDeltaTime;
            switch (Current)
            {
                case State.Warning:
                {
                    float k = stateTime / Mathf.Max(0.01f, stateDuration);
                    float blink = Mathf.Repeat(stateTime * (4f + 10f * k), 1f) > 0.5f ? 1f : 0.2f;
                    SetEmission(warnColor * 3f * blink);
                    Vector3 shake = Random.insideUnitSphere * 0.06f * k;
                    rb.MovePosition(startPos + shake);
                    if (stateTime >= stateDuration)
                    {
                        SetState(State.Falling, fallTime);
                        Sfx.Play(SfxId.Impact, 0.4f, 1.4f);
                    }
                    break;
                }
                case State.Falling:
                {
                    float k = stateTime / fallTime;
                    rb.MovePosition(startPos + Vector3.down * fallDistance * k * k);
                    rb.MoveRotation(startRot * Quaternion.Euler(k * 25f, 0f, k * 15f));
                    if (k >= 0.35f) SetSolid(false);
                    if (stateTime >= fallTime)
                    {
                        SetVisible(false);
                        SetState(State.Gone, regenerateAfter);
                    }
                    break;
                }
                case State.Gone:
                    if (regenerateAfter > 0f && stateTime >= regenerateAfter) Restore();
                    break;
            }
        }

        void Restore()
        {
            rb.position = startPos;
            rb.rotation = startRot;
            transform.SetPositionAndRotation(startPos, startRot);
            SetSolid(true);
            SetVisible(true);
            SetEmission(Color.black);
            SetState(State.Solid, 0f);
        }

        void SetSolid(bool on) { foreach (var c in cols) c.enabled = on; }
        void SetVisible(bool on) { foreach (var r in rends) r.enabled = on; }

        void SetEmission(Color add)
        {
            for (int i = 0; i < mats.Count; i++) mats[i].SetColor("_EmissionColor", baseEmission[i] + add);
        }

        public void ResetState() => Restore();
    }
}
