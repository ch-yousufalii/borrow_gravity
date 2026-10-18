using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Procedural walk cycle for the space-suit model: arms and legs swing with how fast the body moves.</summary>
    public class SuitAnimator : MonoBehaviour
    {
        public Transform leftArm, rightArm, leftLeg, rightLeg;
        public float swing = 38f;
        public float speedForFullSwing = 5f;
        public float cadence = 9f;

        Vector3 lastPos;
        float phase, amount, hold;
        PlayerController player;

        void Start()
        {
            lastPos = transform.position;
            player = GetComponent<PlayerController>();
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 v = (transform.position - lastPos) / dt;
            lastPos = transform.position;
            float speed = Vector3.ProjectOnPlane(v, transform.up).magnitude;
            amount = Mathf.MoveTowards(amount, Mathf.Clamp01(speed / speedForFullSwing), dt * 4f);
            phase += dt * cadence * Mathf.Lerp(0.4f, 1f, amount);
            float s = Mathf.Sin(phase) * swing * amount;
            // holding the engine: both arms reach forward and stay there
            hold = Mathf.MoveTowards(hold, player != null && (player.IsPushing || player.IsCarrying) ? 1f : 0f, dt * 6f);
            Set(leftArm, Mathf.Lerp(s, -80f, hold));
            Set(rightArm, Mathf.Lerp(-s, -80f, hold));
            Set(leftLeg, -s * 0.8f);
            Set(rightLeg, s * 0.8f);
        }

        static void Set(Transform t, float angle)
        {
            if (t != null) t.localRotation = Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
