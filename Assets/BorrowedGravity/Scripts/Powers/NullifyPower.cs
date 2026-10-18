using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Player B — Gravity Nullifier. Tap the power key to nullify the targeted object (nearest in front, in range).
    /// While an object is nullified: hold the key to steer it with the movement keys (B stands still), tap to drop it.
    /// One object at a time; a cooldown starts when the effect ends.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class NullifyPower : MonoBehaviour
    {
        public float range = 14f;
        [Range(-1f, 1f)] public float minFacingDot = 0.2f;
        public float cooldown = 2.5f;
        public float holdToSteerDelay = 0.2f;

        public Nullifiable Target { get; private set; }
        public Nullifiable Active { get; private set; }
        public bool Steering { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, cooldownUntil - Time.time);
        public float CooldownFraction => cooldown <= 0f ? 0f : CooldownRemaining / cooldown;

        PlayerController player;
        float cooldownUntil, pressTime = -1f;

        void Awake() => player = GetComponent<PlayerController>();

        void OnDisable()
        {
            SetTarget(null);
            StopSteering();
        }

        void Update()
        {
            if (Active != null && !Active.IsActive)
            {
                // ran out (or was reset)
                Active = null;
                StopSteering();
                cooldownUntil = Time.time + cooldown;
            }
            SetTarget(Active == null ? FindTarget() : null);
            if (!player.InputEnabled) return;

            if (player.PowerPressed())
            {
                pressTime = Time.time;
                if (Active == null) TryActivate();
            }
            else if (pressTime >= 0f && player.PowerHeld())
            {
                if (Active != null && Active.allowSteer && Time.time - pressTime >= holdToSteerDelay)
                    StartSteering();
            }

            if (pressTime >= 0f && player.PowerReleased())
            {
                bool wasTap = Time.time - pressTime < holdToSteerDelay;
                if (Steering) StopSteering();
                else if (wasTap && Active != null && !justActivated) Release();
                pressTime = -1f;
                justActivated = false;
            }

            if (Steering && Active != null)
            {
                player.GetMoveAxes(Vector3.up, out var right, out var fwd);
                Vector2 m = player.RawMove;
                Active.SetSteer(right * m.x + fwd * m.y);
            }
        }

        bool justActivated;

        void TryActivate()
        {
            if (Time.time < cooldownUntil)
            {
                Sfx.Play(SfxId.Denied, 0.5f);
                GameUI.Toast(player, "Nullifier recharging");
                return;
            }
            if (Target == null)
            {
                Sfx.Play(SfxId.Denied, 0.4f);
                GameUI.Toast(player, "Nothing to nullify — face an object");
                return;
            }
            if (!Target.CanNullify(out string reason))
            {
                Sfx.Play(SfxId.Denied, 0.5f);
                GameUI.Toast(player, reason);
                return;
            }
            Active = Target;
            SetTarget(null);
            Active.Activate();
            justActivated = true;
            Sfx.Play(SfxId.Nullify, 0.8f);
        }

        void Release()
        {
            if (Active == null) return;
            Active.ForceRelease();
            Active = null;
            StopSteering();
            cooldownUntil = Time.time + cooldown;
        }

        void StartSteering()
        {
            if (Steering) return;
            Steering = true;
            player.MovementLocked = true;
        }

        void StopSteering()
        {
            if (!Steering) return;
            Steering = false;
            if (player != null) player.MovementLocked = false;
            if (Active != null) Active.SetSteer(Vector3.zero);
        }

        Nullifiable FindTarget()
        {
            Vector3 pos = transform.position;
            Vector3 up = player.Up;
            Vector3 facing = Vector3.ProjectOnPlane(player.Facing, up).normalized;
            Nullifiable ground = player.GroundCollider != null ? player.GroundCollider.GetComponentInParent<Nullifiable>() : null;
            Nullifiable best = null;
            float bestScore = float.MaxValue;
            bool groundInRange = false;

            foreach (var n in Nullifiable.All)
            {
                if (!n.isActiveAndEnabled) continue;
                var ec = n.GetComponent<EngineCarry>();
                if (ec != null && ec.IsCarried) continue;
                Vector3 cp = n.WorldBounds.ClosestPoint(pos);
                Vector3 d = cp - pos;
                float dist = d.magnitude;
                if (dist > range) continue;
                if (n == ground) { groundInRange = true; continue; }
                Vector3 flat = Vector3.ProjectOnPlane(d, up);
                float flatDist = flat.magnitude;
                float dot = flatDist > 0.05f ? Vector3.Dot(facing, flat / flatDist) : 1f;
                if (dot < minFacingDot) continue;
                // prefer things straight ahead, then near
                float score = dist * (1.6f - dot);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = n;
                }
            }
            if (best != null) return best;
            if (groundInRange) return ground;
            // nothing in front: the engine we are carrying (so B can freeze it and steer it across a gap)
            var carry = Object.FindFirstObjectByType<EngineCarry>();
            if (carry != null && carry.IsCarried && player.IsCarrying)
            {
                var n = carry.GetComponent<Nullifiable>();
                if (n != null && n.isActiveAndEnabled) return n;
            }
            return null;
        }

        void SetTarget(Nullifiable t)
        {
            if (Target == t) return;
            if (Target != null) Target.SetHighlighted(false);
            Target = t;
            if (Target != null) Target.SetHighlighted(true);
        }
    }
}
