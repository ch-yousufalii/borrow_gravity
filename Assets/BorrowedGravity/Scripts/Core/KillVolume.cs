using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Anything that falls below <see cref="killY"/> (or enters this trigger) is respawned / reset.</summary>
    public class KillVolume : MonoBehaviour
    {
        public float killY = -25f;

        void FixedUpdate()
        {
            foreach (var p in Players.All)
                if (p != null && p.transform.position.y < killY) p.Respawn();
        }

        void OnTriggerEnter(Collider other)
        {
            var p = Players.From(other);
            if (p != null) p.Respawn();
        }
    }
}
