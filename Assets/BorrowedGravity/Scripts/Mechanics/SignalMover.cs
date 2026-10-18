using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Moves a kinematic body between two world positions depending on a signal (lifts, bridges).</summary>
    public class SignalMover : MonoBehaviour, IResettable
    {
        public int section;
        public SignalSource source;
        public Vector3 offPosition;
        public Vector3 onPosition;
        public bool rotate;
        public Vector3 offEuler;
        public Vector3 onEuler;
        public float speed = 1.6f;
        public bool latch;
        public string arrivedMessage;

        Rigidbody rb;
        bool on, announced;

        public int Section => section;
        public bool AtOn => (rb.position - onPosition).sqrMagnitude < 0.0025f;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            transform.position = offPosition;
            if (rotate) transform.rotation = Quaternion.Euler(offEuler);
        }

        void FixedUpdate()
        {
            bool want = source != null && source.IsOn;
            if (latch && on) want = true;
            if (want != on)
            {
                on = want;
                Sfx.Play(SfxId.Door, 0.6f, on ? 1f : 0.8f);
            }
            Vector3 target = on ? onPosition : offPosition;
            rb.MovePosition(Vector3.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime));
            if (rotate)
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.Euler(on ? onEuler : offEuler), 25f * speed * Time.fixedDeltaTime));
            if (on && !announced && AtOn && !string.IsNullOrEmpty(arrivedMessage))
            {
                announced = true;
                GameUI.ShowMessage(arrivedMessage, 2.5f);
            }
        }

        public void ResetState()
        {
            on = false;
            announced = false;
            rb.position = offPosition;
            transform.position = offPosition;
            if (rotate) { rb.rotation = Quaternion.Euler(offEuler); transform.rotation = rb.rotation; }
        }
    }
}
