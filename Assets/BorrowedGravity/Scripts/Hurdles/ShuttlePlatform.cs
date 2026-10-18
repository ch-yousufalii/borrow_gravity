using UnityEngine;

namespace BorrowGravity
{
    /// <summary>A platform that shuttles between two points (pausing at each end). Riders are carried along.</summary>
    public class ShuttlePlatform : MonoBehaviour
    {
        public Vector3 pointA, pointB;
        public float travelTime = 3f;
        public float pause = 1f;
        public float offset;

        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            float cycle = (travelTime + pause) * 2f;
            float c = Mathf.Repeat(Time.time + offset, cycle);
            float k;
            if (c < pause) k = 0f;
            else if (c < pause + travelTime) k = (c - pause) / travelTime;
            else if (c < pause * 2f + travelTime) k = 1f;
            else k = 1f - (c - pause * 2f - travelTime) / travelTime;
            rb.MovePosition(Vector3.Lerp(pointA, pointB, Mathf.SmoothStep(0f, 1f, k)));
        }
    }
}
