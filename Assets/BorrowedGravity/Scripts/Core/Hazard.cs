using UnityEngine;

namespace BorrowGravity
{
    public enum HazardEffect { Respawn, Knockback }

    /// <summary>
    /// Hurts players that touch it (trigger or solid collider). Knockback shoves them along <see cref="knockDirection"/>
    /// (or away from the hazard); Respawn sends them to the last checkpoint. Can be armed/disarmed by timed hazards.
    /// Also damages the engine carrier.
    /// </summary>
    public class Hazard : MonoBehaviour
    {
        public HazardEffect effect = HazardEffect.Respawn;
        public bool armed = true;
        public float knockSpeed = 11f;
        public float knockUp = 5f;
        [Tooltip("World direction for knockback. Zero = away from the hazard's centre.")]
        public Vector3 knockDirection;
        public bool useOwnVelocity;
        public float carrierDamage = 25f;

        Vector3 lastPos, velocity;

        void FixedUpdate()
        {
            velocity = (transform.position - lastPos) / Time.fixedDeltaTime;
            lastPos = transform.position;
        }

        void OnTriggerEnter(Collider other) => Touch(other);
        void OnTriggerStay(Collider other) => Touch(other);
        void OnCollisionEnter(Collision c) => Touch(c.collider);
        void OnCollisionStay(Collision c) => Touch(c.collider);

        void Touch(Collider other)
        {
            if (!armed) return;
            var p = Players.From(other);
            if (p != null)
            {
                if (!p.CanBeHurt) return;
                if (effect == HazardEffect.Respawn)
                {
                    p.Kill();
                    return;
                }
                Vector3 dir = knockDirection;
                if (useOwnVelocity && velocity.sqrMagnitude > 0.5f) dir = velocity;
                if (dir.sqrMagnitude < 0.01f) dir = p.transform.position - transform.position;
                dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
                p.Knockback(dir * knockSpeed + Vector3.up * knockUp);
                return;
            }
            var carrier = other.GetComponentInParent<EngineCarrier>();
            if (carrier != null) carrier.Damage(carrierDamage * Time.fixedDeltaTime * 10f, "Hit by debris!");
        }
    }
}
