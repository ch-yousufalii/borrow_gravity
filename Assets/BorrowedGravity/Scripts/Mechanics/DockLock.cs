using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Clamps a body into a dock pose when it is close enough, not nullified, and the dock zone is in the required mode
    /// (Player A "enables gravity at the landing zone"). ON once locked.
    /// </summary>
    public class DockLock : SignalSource, IResettable
    {
        public int section;
        public Rigidbody target;
        public Transform dockPose;
        public GravityZone zone;
        public GravityMode requiredMode = GravityMode.Up;
        public float captureRadius = 2.5f;
        public float moveSpeed = 2f;
        public string lockedMessage = "Docked and locked!";

        bool capturing, locked, startKinematic;
        Nullifiable targetNull;

        public override bool IsOn => locked;
        public int Section => section;

        void Awake()
        {
            targetNull = target != null ? target.GetComponent<Nullifiable>() : null;
            startKinematic = target != null && target.isKinematic;
        }

        void FixedUpdate()
        {
            if (locked || target == null) return;
            if (!capturing)
            {
                bool held = targetNull != null && targetNull.IsActive;
                Vector3 d = target.position - dockPose.position;
                d.y = 0f;
                if (!held && zone != null && zone.Mode == requiredMode && d.magnitude < captureRadius)
                {
                    capturing = true;
                    if (!target.isKinematic) { target.linearVelocity = Vector3.zero; target.angularVelocity = Vector3.zero; }
                    target.isKinematic = true;
                    if (targetNull != null) targetNull.enabled = false;
                    Sfx.Play(SfxId.Door, 0.7f, 1.2f);
                }
                return;
            }
            Vector3 next = Vector3.MoveTowards(target.position, dockPose.position, moveSpeed * Time.fixedDeltaTime);
            target.MovePosition(next);
            target.MoveRotation(Quaternion.RotateTowards(target.rotation, dockPose.rotation, 90f * Time.fixedDeltaTime));
            if ((next - dockPose.position).sqrMagnitude < 0.0004f)
            {
                locked = true;
                Sfx.Play(SfxId.Checkpoint, 0.7f);
                if (!string.IsNullOrEmpty(lockedMessage)) GameUI.ShowMessage(lockedMessage, 3f);
            }
        }

        public void ResetState()
        {
            capturing = false;
            locked = false;
            if (targetNull != null) targetNull.enabled = true;
            if (target != null) target.isKinematic = startKinematic;
        }
    }
}
