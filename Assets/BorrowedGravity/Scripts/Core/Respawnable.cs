using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Remembers a body's starting pose and puts it back on reset or when it falls out of the world.</summary>
    public class Respawnable : MonoBehaviour, IResettable
    {
        public int section;
        [Tooltip("Reset automatically when the object drops below this world Y.")]
        public float killY = -30f;

        Vector3 startPos;
        Quaternion startRot;
        Rigidbody rb;
        bool startKinematic;

        public int Section => section;

        /// <summary>Change where the object returns to (e.g. after it was knocked somewhere new).</summary>
        public void SetHome(Vector3 pos, Quaternion rot)
        {
            startPos = pos;
            startRot = rot;
            if (rb != null) startKinematic = rb.isKinematic;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            startPos = transform.position;
            startRot = transform.rotation;
            if (rb != null) startKinematic = rb.isKinematic;
        }

        void FixedUpdate()
        {
            if (transform.position.y < killY) ResetState();
        }

        public void ResetState()
        {
            var n = GetComponent<Nullifiable>();
            if (n != null) n.ForceRelease(false);
            if (rb != null)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = startKinematic;
                rb.position = startPos;
                rb.rotation = startRot;
            }
            transform.SetPositionAndRotation(startPos, startRot);
        }
    }
}
