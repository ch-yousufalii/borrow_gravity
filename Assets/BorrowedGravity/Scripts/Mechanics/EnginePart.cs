using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// One half of the replacement engine (Stage 3). Each player has their own half (A orange, B cyan): walk up to it
    /// to pick it up, then it rides in your hands — arms forward, a little slower — all the way back to the ship,
    /// respawns included. Walk into the ship's <see cref="EngineBay"/> to install it.
    /// The half is never re-parented (it follows the hands by position), so online mirroring stays simple.
    /// </summary>
    public class EnginePart : MonoBehaviour
    {
        public enum PartState { Waiting, Held, Installed }

        static readonly List<EnginePart> all = new List<EnginePart>();
        public static IReadOnlyList<EnginePart> All => all;

        [Tooltip("0 = Player A's half, 1 = Player B's half.")]
        public int owner;
        public string displayName = "engine half";
        public float pickupRange = 1.7f;
        [Tooltip("Where it sits relative to the player: x = sideways, y = along up, z = along facing.")]
        public Vector3 holdOffset = new Vector3(0f, 0.05f, 0.65f);
        public float carrySpeedFactor = 0.85f;

        public PartState State { get; private set; } = PartState.Waiting;
        public PlayerController Holder { get; private set; }

        Collider[] colliders;

        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() => all.Remove(this);

        void Awake() => colliders = GetComponentsInChildren<Collider>(true);

        void LateUpdate()
        {
            if (NetSession.IsClient) return; // the host moves it; the joining phone mirrors it
            switch (State)
            {
                case PartState.Waiting:
                {
                    var p = Players.Get(owner);
                    if (p != null && p.InputEnabled && Vector3.Distance(p.transform.position, transform.position) < pickupRange) PickUp(p);
                    break;
                }
                case PartState.Held:
                    if (Holder == null) { State = PartState.Waiting; break; }
                    Follow();
                    Holder.CarryLoad = carrySpeedFactor;
                    Holder.CarryingUntil = Time.time + 0.2f;
                    break;
            }
        }

        void Follow()
        {
            Vector3 up = Holder.Up;
            Vector3 fwd = Vector3.ProjectOnPlane(Holder.Facing, up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(Holder.transform.forward, up);
            fwd.Normalize();
            Vector3 side = Vector3.Cross(up, fwd);
            transform.SetPositionAndRotation(
                Holder.transform.position + side * holdOffset.x + up * holdOffset.y + fwd * holdOffset.z,
                Quaternion.LookRotation(fwd, up));
        }

        void PickUp(PlayerController p)
        {
            Holder = p;
            State = PartState.Held;
            foreach (var c in colliders) if (c != null) c.enabled = false;
            Follow();
            Sfx.Play(SfxId.Plate, 0.8f, 1.1f);
            GameUI.Toast(p, "Got the " + displayName + "!");
            if (all.TrueForAll(e => e.State != PartState.Waiting))
                GameUI.ShowMessage("Both engine halves in hand — get them back to the ship!", 3f);
        }

        /// <summary>Called by the bay: the half leaves the player's hands and locks into the ship.</summary>
        public void Install(Transform slot)
        {
            if (State != PartState.Held) return;
            if (Holder != null) { Holder.CarryLoad = 1f; Holder.CarryingUntil = 0f; }
            Holder = null;
            State = PartState.Installed;
            transform.SetPositionAndRotation(slot.position, slot.rotation);
            Sfx.Play(SfxId.Success, 0.7f, 1.2f);
        }
    }
}
