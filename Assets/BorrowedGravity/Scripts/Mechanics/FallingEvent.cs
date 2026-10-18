using System.Collections;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A heavy piece that breaks loose and crashes onto the route. Before impact: shaking, a growing red shadow on the
    /// landing spot and a HUD warning. Fires once when players enter the trigger, or repeats on a timer.
    /// </summary>
    public class FallingEvent : MonoBehaviour, IResettable
    {
        public int section;
        public Transform faller;
        public Vector3 fallStart;
        public Vector3 fallEnd;
        public Vector3 endEuler;
        public float fallTime = 0.9f;
        public float warnTime = 2.5f;
        [Tooltip("Keep the piece where it lands (it becomes part of the route). Otherwise it keeps falling away.")]
        public bool staysAfterLanding;
        public bool repeat;
        public float repeatPeriod = 6f;
        public float repeatOffset;
        public Transform shadowMarker;
        public Transform[] alsoDrop;
        public Vector3 alsoDropOffset = new Vector3(0f, -2.5f, 0f);
        [Tooltip("Bodies (from alsoDrop) that become loose physics objects after the impact.")]
        public Rigidbody[] releaseAfter;
        public HudHazard hazard = HudHazard.FallingDebris;
        public string message = "Wreckage breaking loose — take cover!";
        public float impactShake = 1.6f;

        Coroutine running;
        bool fired;
        Rigidbody rb;
        Hazard[] hazards;
        Quaternion startRot;
        Vector3[] dropStart;
        Vector3 shadowScale;

        public int Section => section;

        void Awake()
        {
            rb = faller.GetComponent<Rigidbody>();
            if (rb == null) rb = faller.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            hazards = faller.GetComponentsInChildren<Hazard>();
            startRot = faller.rotation;
            if (alsoDrop != null)
            {
                dropStart = new Vector3[alsoDrop.Length];
                for (int i = 0; i < alsoDrop.Length; i++) dropStart[i] = alsoDrop[i].position;
            }
            if (shadowMarker != null) { shadowScale = shadowMarker.localScale; shadowMarker.gameObject.SetActive(false); }
            Place(fallStart, startRot);
            Arm(false);
        }

        void Start()
        {
            if (repeat) running = StartCoroutine(Loop());
        }

        void OnTriggerEnter(Collider other)
        {
            if (repeat || fired || Players.From(other) == null) return;
            fired = true;
            running = StartCoroutine(Sequence());
        }

        IEnumerator Loop()
        {
            yield return new WaitForSeconds(repeatOffset);
            while (true)
            {
                yield return Sequence();
                yield return new WaitForSeconds(Mathf.Max(0.5f, repeatPeriod - warnTime - fallTime));
            }
        }

        IEnumerator Sequence()
        {
            Place(fallStart, startRot);
            bool near = NearPlayer();
            if (near)
            {
                GameUI.ShowWarning(hazard, message, warnTime, warnTime + 0.5f);
                Sfx.Play(SfxId.Alarm, 0.5f);
                CoopCamera.Shake(0.5f, warnTime);
            }
            if (shadowMarker != null) shadowMarker.gameObject.SetActive(true);
            for (float t = 0; t < warnTime; t += Time.deltaTime)
            {
                if (shadowMarker != null)
                    shadowMarker.localScale = Vector3.Scale(shadowScale, new Vector3(1f, 1f, 1f) * Mathf.Lerp(0.3f, 1f, t / warnTime))
                        * (1f + 0.08f * Mathf.Sin(t * 20f));
                yield return null;
            }
            Arm(true);
            for (float t = 0; t < fallTime; t += Time.fixedDeltaTime)
            {
                float k = t / fallTime;
                Place(Vector3.Lerp(fallStart, fallEnd, k * k), Quaternion.Slerp(startRot, Quaternion.Euler(endEuler), k));
                yield return new WaitForFixedUpdate();
            }
            Place(fallEnd, Quaternion.Euler(endEuler));
            if (NearPlayer())
            {
                Sfx.Play(SfxId.Impact, 1f);
                CoopCamera.Shake(impactShake, 0.6f);
            }
            if (alsoDrop != null)
                for (int i = 0; i < alsoDrop.Length; i++) alsoDrop[i].position = dropStart[i] + alsoDropOffset;
            if (releaseAfter != null)
                foreach (var rbody in releaseAfter)
                {
                    if (rbody == null) continue;
                    rbody.isKinematic = false;
                    var n = rbody.GetComponent<Nullifiable>();
                    if (n != null) n.enabled = true;
                    var r = rbody.GetComponent<Respawnable>();
                    if (r != null) r.SetHome(rbody.transform.position, rbody.transform.rotation);
                }
            yield return new WaitForSeconds(0.25f);
            Arm(false);
            if (shadowMarker != null) shadowMarker.gameObject.SetActive(false);
            if (!staysAfterLanding)
            {
                Vector3 away = fallEnd + (fallEnd - fallStart).normalized * 40f;
                for (float t = 0; t < 1.2f; t += Time.fixedDeltaTime)
                {
                    Place(Vector3.Lerp(fallEnd, away, t / 1.2f), faller.rotation);
                    yield return new WaitForFixedUpdate();
                }
                Place(fallStart, startRot);
            }
        }

        void Place(Vector3 p, Quaternion r)
        {
            rb.MovePosition(p);
            rb.MoveRotation(r);
            if (!Application.isPlaying || Time.timeSinceLevelLoad < 0.1f) faller.SetPositionAndRotation(p, r);
        }

        void Arm(bool on) { foreach (var h in hazards) h.armed = on; }

        bool NearPlayer()
        {
            foreach (var p in Players.All)
                if (p != null && Mathf.Abs(p.transform.position.x - fallEnd.x) < 30f) return true;
            return false;
        }

        public void ResetState()
        {
            if (repeat) return;
            if (running != null) StopCoroutine(running);
            running = null;
            fired = false;
            Arm(false);
            faller.SetPositionAndRotation(fallStart, startRot);
            rb.position = fallStart;
            rb.rotation = startRot;
            if (releaseAfter != null)
                foreach (var rbody in releaseAfter)
                {
                    if (rbody == null) continue;
                    var n = rbody.GetComponent<Nullifiable>();
                    if (n != null) { n.ForceRelease(false); n.enabled = false; }
                    if (!rbody.isKinematic) { rbody.linearVelocity = Vector3.zero; rbody.angularVelocity = Vector3.zero; }
                    rbody.isKinematic = true;
                }
            if (alsoDrop != null)
                for (int i = 0; i < alsoDrop.Length; i++)
                {
                    alsoDrop[i].position = dropStart[i];
                    var r = alsoDrop[i].GetComponent<Respawnable>();
                    if (r != null) r.SetHome(dropStart[i], alsoDrop[i].rotation);
                }
            if (shadowMarker != null) shadowMarker.gameObject.SetActive(false);
        }
    }
}
