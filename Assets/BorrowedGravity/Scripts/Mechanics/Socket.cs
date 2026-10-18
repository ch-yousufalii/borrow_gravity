using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Accepts an object carrying a matching <see cref="SocketKey"/> once it settles inside (not nullified, nearly still),
    /// then snaps and locks it in place.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Socket : SignalSource, IResettable
    {
        public int section;
        public string key = "cell";
        public Transform snapPoint;
        public float settleTime = 0.4f;
        public float maxSpeed = 1.2f;
        [Tooltip("Accept while the object is still nullified (B is holding it).")]
        public bool acceptWhileNullified;
        public bool lockObject = true;
        [Tooltip("Parent the locked object to the snap point (sockets on moving platforms).")]
        public bool parentToSnap;
        public Renderer indicator;
        public string acceptedMessage;

        SocketKey candidate;
        float settled;
        bool filled;
        SocketKey lockedKey;
        Transform originalParent;
        Material indicatorMat;

        public override bool IsOn => filled;
        public int Section => section;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (indicator != null) { indicatorMat = indicator.material; indicatorMat.EnableKeyword("_EMISSION"); }
            Refresh();
        }

        void OnTriggerStay(Collider c)
        {
            if (filled) return;
            var k = c.GetComponentInParent<SocketKey>();
            if (k == null || k.key != key) return;
            candidate = k;
        }

        void FixedUpdate()
        {
            if (filled || candidate == null) { settled = 0f; candidate = null; return; }
            var rb = candidate.GetComponent<Rigidbody>();
            var n = candidate.GetComponent<Nullifiable>();
            bool nullified = n != null && n.IsActive;
            bool slow = rb == null || rb.isKinematic || rb.linearVelocity.magnitude < maxSpeed;
            if ((acceptWhileNullified || !nullified) && slow) settled += Time.fixedDeltaTime;
            else settled = 0f;
            if (settled >= settleTime) Fill(candidate);
            candidate = null;
        }

        void Fill(SocketKey k)
        {
            filled = true;
            lockedKey = k;
            originalParent = k.transform.parent;
            var carry = k.GetComponent<EngineCarry>();
            if (carry != null) carry.Drop(false);
            if (lockObject)
            {
                var n = k.GetComponent<Nullifiable>();
                if (n != null) { n.ForceRelease(false); n.enabled = false; }
                var rb = k.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    rb.isKinematic = true;
                }
                if (snapPoint != null)
                {
                    k.transform.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);
                    if (parentToSnap) k.transform.SetParent(snapPoint, true);
                }
            }
            Sfx.Play(SfxId.Checkpoint, 0.6f, 1.3f);
            if (!string.IsNullOrEmpty(acceptedMessage)) GameUI.ShowMessage(acceptedMessage, 2.5f);
            Refresh();
        }

        void Refresh()
        {
            if (indicatorMat != null)
                indicatorMat.SetColor("_EmissionColor", (filled ? new Color(0.3f, 1f, 0.5f) : new Color(1f, 0.35f, 0.2f)) * 2.5f);
        }

        public void ResetState()
        {
            if (lockedKey != null)
            {
                if (parentToSnap && lockedKey.transform.parent == snapPoint) lockedKey.transform.SetParent(originalParent, true);
                var n = lockedKey.GetComponent<Nullifiable>();
                if (n != null) n.enabled = true;
                var r = lockedKey.GetComponent<Respawnable>();
                if (r != null) r.ResetState();
            }
            lockedKey = null;
            filled = false;
            settled = 0f;
            Refresh();
        }
    }
}
