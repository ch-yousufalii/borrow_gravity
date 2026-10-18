using UnityEngine;

namespace BorrowGravity
{
    /// <summary>A detached platform that drifts up and down until <see cref="alignSignal"/> locks it back in place.</summary>
    public class BobbingPlatform : MonoBehaviour, IResettable
    {
        public int section;
        public Vector3 amplitude = new Vector3(0f, 1.6f, 0f);
        public float period = 4f;
        public Vector3 tiltAmplitude = new Vector3(0f, 0f, 6f);
        public SignalSource alignSignal;
        public float alignTime = 2f;
        public string alignedMessage;

        Rigidbody rb;
        Vector3 restPos;
        Quaternion restRot;
        float t, align;
        bool aligned, announced;

        public int Section => section;
        public bool Aligned => aligned && align >= 1f;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            restPos = transform.position;
            restRot = transform.rotation;
        }

        void FixedUpdate()
        {
            if (!aligned && alignSignal != null && alignSignal.IsOn) aligned = true;
            if (aligned) align = Mathf.Min(1f, align + Time.fixedDeltaTime / alignTime);
            else t += Time.fixedDeltaTime;
            float s = Mathf.Sin(t / period * Mathf.PI * 2f) * (1f - align);
            float c = Mathf.Cos(t / period * Mathf.PI * 2f) * (1f - align);
            rb.MovePosition(restPos + amplitude * s);
            rb.MoveRotation(restRot * Quaternion.Euler(tiltAmplitude * c));
            if (Aligned && !announced)
            {
                announced = true;
                Sfx.Play(SfxId.Door, 0.7f);
                if (!string.IsNullOrEmpty(alignedMessage)) GameUI.ShowMessage(alignedMessage, 3f);
            }
        }

        public void ResetState()
        {
            aligned = false;
            announced = false;
            align = 0f;
            t = 0f;
        }
    }
}
