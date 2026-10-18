using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>A loose platform that tips toward whoever stands on it (and springs back when empty).</summary>
    public class TiltPlatform : MonoBehaviour
    {
        public Vector3 axis = Vector3.forward;
        public float degreesPerMetre = 3f;
        public float maxAngle = 9f;
        public float response = 2f;

        Rigidbody rb;
        Quaternion baseRot;
        float angle;
        readonly HashSet<PlayerController> riders = new HashSet<PlayerController>();

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            baseRot = transform.rotation;
        }

        void OnCollisionStay(Collision c)
        {
            var p = Players.From(c.collider);
            if (p != null) riders.Add(p);
        }

        void FixedUpdate()
        {
            float torque = 0f;
            foreach (var p in riders)
            {
                if (p == null) continue;
                Vector3 off = p.transform.position - transform.position;
                // positive offset along +X tips the right side down
                torque += Vector3.Dot(off, Vector3.Cross(axis, Vector3.up).normalized) * degreesPerMetre;
            }
            riders.Clear();
            float target = Mathf.Clamp(torque, -maxAngle, maxAngle);
            angle = Mathf.MoveTowards(angle, target, response * maxAngle * Time.fixedDeltaTime);
            rb.MoveRotation(baseRot * Quaternion.AngleAxis(angle, axis));
        }
    }
}
