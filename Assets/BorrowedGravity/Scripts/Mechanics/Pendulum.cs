using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A swinging cable (pivot = this transform). Child <see cref="Hazard"/>s knock players away. When
    /// <see cref="stopSignal"/> turns on, it settles and winches itself up out of the way.
    /// </summary>
    public class Pendulum : MonoBehaviour, IResettable
    {
        public int section;
        public float amplitude = 50f;
        public float period = 3.4f;
        public float phase;
        public Vector3 axis = Vector3.forward;
        public SignalSource stopSignal;
        public Vector3 retractOffset = new Vector3(0f, 7f, 0f);
        public float retractTime = 2.5f;
        public string stoppedMessage = "Cable pinned — path is clear!";

        Rigidbody rb;
        Quaternion baseRot;
        Vector3 basePos;
        float t, retract, lastAngle, lastVel;
        bool stopped;
        Hazard[] hazards;

        public int Section => section;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            baseRot = transform.rotation;
            basePos = transform.position;
            hazards = GetComponentsInChildren<Hazard>();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (!stopped && stopSignal != null && stopSignal.IsOn)
            {
                stopped = true;
                foreach (var h in hazards) h.armed = false;
                Sfx.Play(SfxId.Door, 0.6f, 0.7f);
                if (!string.IsNullOrEmpty(stoppedMessage)) GameUI.ShowMessage(stoppedMessage, 3f);
            }
            float angle;
            if (stopped)
            {
                retract = Mathf.Min(1f, retract + dt / retractTime);
                angle = Mathf.Lerp(lastAngle, 0f, retract);
                rb.MovePosition(basePos + retractOffset * Mathf.SmoothStep(0f, 1f, retract));
            }
            else
            {
                t += dt;
                angle = amplitude * Mathf.Sin((t / period + phase) * Mathf.PI * 2f);
                float vel = angle - lastAngle;
                // creak at the ends of each swing
                if (Mathf.Sign(vel) != Mathf.Sign(lastVel) && NearPlayer()) Sfx.Play(SfxId.Creak, 0.35f);
                lastVel = vel;
                lastAngle = angle;
            }
            rb.MoveRotation(Quaternion.AngleAxis(angle, axis) * baseRot);
        }

        bool NearPlayer()
        {
            foreach (var p in Players.All)
                if (p != null && (p.transform.position - basePos).sqrMagnitude < 400f) return true;
            return false;
        }

        public void ResetState()
        {
            stopped = false;
            retract = 0f;
            t = 0f;
            lastAngle = 0f;
            foreach (var h in hazards) h.armed = true;
            transform.SetPositionAndRotation(basePos, baseRot);
            rb.position = basePos;
            rb.rotation = baseRot;
        }
    }
}
