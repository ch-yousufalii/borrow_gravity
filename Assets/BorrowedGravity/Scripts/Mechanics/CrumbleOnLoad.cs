using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Weak support (e.g. a broken bridge bracket): cracks shortly after a loose physics body with the given key lands
    /// on it. Bodies held by Player B (kinematic) don't load it.
    /// </summary>
    [RequireComponent(typeof(CrumblePlatform))]
    public class CrumbleOnLoad : MonoBehaviour
    {
        public string key = "bridge";
        public float crackTime = 0.4f;

        CrumblePlatform crumble;

        void Awake() => crumble = GetComponent<CrumblePlatform>();

        void OnCollisionStay(Collision c)
        {
            var rb = c.rigidbody;
            if (rb == null || rb.isKinematic) return;
            var k = rb.GetComponent<SocketKey>();
            if (k != null && k.key == key) crumble.Trigger(crackTime);
        }
    }
}
