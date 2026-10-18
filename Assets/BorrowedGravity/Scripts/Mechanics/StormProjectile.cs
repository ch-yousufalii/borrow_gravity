using UnityEngine;

namespace BorrowGravity
{
    /// <summary>One piece of storm debris: flies straight, knocks players over, breaks on anything solid.</summary>
    public class StormProjectile : MonoBehaviour
    {
        public Vector3 velocity;
        public float radius = 0.45f;
        public float lifetime = 7f;
        public float knockSpeed = 13f;

        Vector3 spin;
        readonly RaycastHit[] hits = new RaycastHit[6];

        void Start()
        {
            spin = Random.insideUnitSphere * 360f;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Destroy(gameObject, lifetime);
        }

        void FixedUpdate()
        {
            Vector3 step = velocity * Time.fixedDeltaTime;
            int n = Physics.SphereCastNonAlloc(transform.position, radius, step.normalized, hits, step.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider.transform.IsChildOf(transform)) continue;
                var p = Players.From(h.collider);
                if (p != null)
                {
                    if (!p.CanBeHurt) continue;
                    p.Knockback(velocity.normalized * knockSpeed + Vector3.up * 4f, 0.7f);
                }
                Sfx.Play(SfxId.Hit, 0.25f, 1.4f);
                Destroy(gameObject);
                return;
            }
            transform.position += step;
            transform.Rotate(spin * Time.fixedDeltaTime);
        }
    }
}
