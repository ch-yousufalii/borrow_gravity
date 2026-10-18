using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowGravity
{
    /// <summary>Keyboard bindings for one player on a shared keyboard.</summary>
    [Serializable]
    public class PlayerKeys
    {
        public Key up = Key.W, down = Key.S, left = Key.A, right = Key.D;
        public Key jump = Key.Space, jumpAlt = Key.None;
        public Key power = Key.E, powerAlt = Key.None;
        [Tooltip("Second power (solo play: nullify).")]
        public Key power2 = Key.Q;

        /// <summary>True if the key is held on any connected keyboard (so a second USB keyboard also works).</summary>
        public static bool Held(Key k)
        {
            if (k == Key.None) return false;
            foreach (var d in InputSystem.devices)
                if (d is Keyboard kb && kb.enabled && kb[k].isPressed) return true;
            return false;
        }

        public static bool Pressed(Key k)
        {
            if (k == Key.None) return false;
            foreach (var d in InputSystem.devices)
                if (d is Keyboard kb && kb.enabled && kb[k].wasPressedThisFrame) return true;
            return false;
        }

        public static bool Released(Key k)
        {
            if (k == Key.None) return false;
            foreach (var d in InputSystem.devices)
                if (d is Keyboard kb && kb.enabled && kb[k].wasReleasedThisFrame) return true;
            return false;
        }

        public Vector2 ReadMove()
        {
            var m = new Vector2((Held(right) ? 1 : 0) - (Held(left) ? 1 : 0),
                                (Held(up) ? 1 : 0) - (Held(down) ? 1 : 0));
            return m.sqrMagnitude > 1f ? m.normalized : m;
        }

        public bool JumpPressed() => Pressed(jump) || Pressed(jumpAlt);
        public bool PowerPressed() => Pressed(power) || Pressed(powerAlt);
        public bool PowerHeld() => Held(power) || Held(powerAlt);
        public bool PowerReleased() => Released(power) || Released(powerAlt);
    }

    /// <summary>
    /// Rigidbody astronaut. "Up" follows the local gravity, so players can stand on floors, walls, ceilings and rings.
    /// Input is camera-relative: Left/Right move along the screen, Up/Down move into the screen (or up a wall).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(GravityAffected))]
    public class PlayerController : MonoBehaviour
    {
        public int playerIndex;
        public string displayName = "A";
        public Color color = Color.white;
        public PlayerKeys keys = new PlayerKeys();

        [Header("Movement")]
        public float moveSpeed = 6f;
        public float acceleration = 60f;
        public float airAcceleration = 35f;
        public float jumpVelocity = 10f;
        public float jumpBufferTime = 0.15f;
        public float coyoteTime = 0.1f;
        [Tooltip("Extra jumps in the air (1 = double jump).")]
        public int airJumps = 1;
        [Tooltip("Strength of an air jump compared to the first one.")]
        public float airJumpScale = 0.9f;
        public float groundCheckDistance = 0.15f;
        public float turnSpeed = 12f;
        public LayerMask groundMask = ~0;
        public float respawnGrace = 1.2f;

        public bool InputEnabled { get; set; } = true;
        /// <summary>Set by powers that need the player to stand still (e.g. steering a nullified object).</summary>
        public bool MovementLocked { get; set; }
        public bool IsGrounded { get; private set; }
        public Collider GroundCollider { get; private set; }
        public Vector3 Facing { get; private set; } = Vector3.right;
        public Vector3 Up => grav != null ? grav.Up : Vector3.up;
        public Vector2 RawMove { get; private set; }
        public Vector3 MoveDirection { get; private set; }
        /// <summary>True while pushing / holding the engine carrier (or any Pushable) this frame.</summary>
        public bool IsPushing => Time.time - lastPushTime < 0.15f;
        /// <summary>Set each physics step by EngineCarry while this player holds an end of the engine.</summary>
        public float CarryingUntil { get; set; }
        public bool IsCarrying => Time.time < CarryingUntil;
        /// <summary>Movement multiplier while carrying (1 = free).</summary>
        public float CarryLoad { get; set; } = 1f;
        public bool CanBeHurt => Time.time >= invulnerableUntil;
        public float LastRespawnTime { get; private set; } = -10f;
        public event Action<PlayerController> Respawned;

        Rigidbody rb;
        CapsuleCollider col;
        GravityAffected grav;
        Camera cam;
        int airJumpsLeft;
        float jumpBuffer, lastGroundedTime = -1f, invulnerableUntil, stunnedUntil, lastPushTime = -1f;
        Vector3 spawnPosition;
        readonly RaycastHit[] hits = new RaycastHit[8];

        // moving-platform carry
        Transform carryTransform;
        Vector3 carryLocalPoint, carryWorldPoint;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            col = GetComponent<CapsuleCollider>();
            grav = GetComponent<GravityAffected>();
            grav.isPlayer = true;
            rb.useGravity = false;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            col.material = new PhysicsMaterial("PlayerFrictionless")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            spawnPosition = transform.position;
            Facing = transform.forward;
        }

        void OnEnable() => Players.Register(this);
        void OnDisable() => Players.Unregister(this);

        /// <summary>Online: the keyboard only drives this device's own player (the other one is driven over the network).</summary>
        bool KeyboardDrives => !NetSession.Online || playerIndex == NetSession.LocalPlayer;

        /// <summary>Online each device has one player, so the other player's keys (arrows / R-Ctrl / R-Shift) work too.</summary>
        PlayerKeys AltKeys => NetSession.Online && playerIndex == NetSession.LocalPlayer ? Players.Get(1 - playerIndex)?.keys : null;

        /// <summary>Joining phone: the host simulates this player; mirror its pose flags for the suit animation.</summary>
        public void SetRemoteFlags(bool pushing, bool carrying)
        {
            if (pushing) lastPushTime = Time.time;
            if (carrying) CarryingUntil = Time.time + 0.3f;
        }

        void Update()
        {
            if (NetSession.IsClient) return;
            if (ViewCamera != null) cam = ViewCamera;
            if (cam == null) cam = Camera.main;
            var stage = StageManager.Instance;
            if (!InputEnabled || (stage != null && stage.isActiveAndEnabled && stage.MissionShowing))
            {
                RawMove = Vector2.zero;
                return;
            }
            var alt = AltKeys;
            RawMove = Vector2.ClampMagnitude((KeyboardDrives ? keys.ReadMove() : Vector2.zero) + (alt != null ? alt.ReadMove() : Vector2.zero)
                                             + VirtualInput.Move(playerIndex), 1f);
            // age the buffer before (re)filling it, so a long frame can't eat a press before FixedUpdate sees it
            jumpBuffer -= Time.deltaTime;
            if (!MovementLocked && ((KeyboardDrives && keys.JumpPressed()) || (alt != null && alt.JumpPressed()) || VirtualInput.JumpPressed(playerIndex)))
                jumpBuffer = jumpBufferTime;
        }

        // power button state from keyboard or on-screen controls
        public bool PowerPressed() => InputEnabled && ((KeyboardDrives && keys.PowerPressed()) || (AltKeys?.PowerPressed() ?? false) || VirtualInput.PowerPressed(playerIndex));
        public bool PowerHeld() => InputEnabled && ((KeyboardDrives && keys.PowerHeld()) || (AltKeys?.PowerHeld() ?? false) || VirtualInput.PowerHeld(playerIndex));
        public bool PowerReleased() => (KeyboardDrives && keys.PowerReleased()) || (AltKeys?.PowerReleased() ?? false) || VirtualInput.PowerReleased(playerIndex);

        // second power button (solo play: nullify on Q / the second on-screen power button)
        public bool Power2Pressed() => InputEnabled && ((KeyboardDrives && PlayerKeys.Pressed(keys.power2)) || VirtualInput.Power2Pressed(playerIndex));
        public bool Power2Held() => InputEnabled && ((KeyboardDrives && PlayerKeys.Held(keys.power2)) || VirtualInput.Power2Held(playerIndex));
        public bool Power2Released() => (KeyboardDrives && PlayerKeys.Released(keys.power2)) || VirtualInput.Power2Released(playerIndex);

        /// <summary>The camera that shows this player (its own split-screen camera when there is one).</summary>
        public Camera ViewCamera { get; set; }

        /// <summary>Host, online: the direction the other phone's camera faces for this player (its stick is relative to it).</summary>
        public Vector3 RemoteViewForward { get; set; }

        /// <summary>
        /// Camera-relative movement axes on the plane perpendicular to "up": forward is where the (third-person) camera
        /// looks, right is to its side — on floors, walls and ceilings alike.
        /// </summary>
        public void GetMoveAxes(Vector3 up, out Vector3 right, out Vector3 forward)
        {
            Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.right;
            if (NetSession.IsHost && playerIndex != NetSession.LocalPlayer && RemoteViewForward.sqrMagnitude > 0.01f) camFwd = RemoteViewForward;
            Vector3 camUp = cam != null ? cam.transform.up : Vector3.up;
            forward = Vector3.ProjectOnPlane(camFwd, up);
            // camera looking straight along "up" (e.g. just after a gravity flip): use its up vector instead
            if (forward.sqrMagnitude < 0.04f) forward = Vector3.ProjectOnPlane(camUp, up);
            forward.Normalize();
            right = Vector3.Cross(up, forward).normalized;
        }

        void FixedUpdate()
        {
            if (NetSession.IsClient) return;
            float dt = Time.fixedDeltaTime;
            Vector3 up = Up;
            CheckGround(up);
            if (IsGrounded) { lastGroundedTime = Time.time; airJumpsLeft = airJumps; }
            Vector3 carry = ComputeCarry();

            GetMoveAxes(up, out var right, out var fwd);
            Vector2 input = MovementLocked || Time.time < stunnedUntil ? Vector2.zero : RawMove;
            Vector3 moveDir = right * input.x + fwd * input.y;
            if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();
            MoveDirection = moveDir;

            Vector3 v = rb.linearVelocity;
            float vUp = Vector3.Dot(v, up);
            Vector3 vPlane = v - up * vUp;
            Vector3 carryPlane = Vector3.ProjectOnPlane(carry, up);
            float speed = moveSpeed * (IsCarrying ? CarryLoad : 1f);
            Vector3 target = moveDir * speed + (IsGrounded ? carryPlane : Vector3.zero);
            float accel = IsGrounded ? acceleration : airAcceleration;
            if (Time.time < stunnedUntil) accel = 4f;
            vPlane = Vector3.MoveTowards(vPlane, target, accel * dt);

            if (IsGrounded && jumpBuffer <= 0f)
            {
                // stick to platforms that move along "up" (lifts) instead of bouncing on them
                float carryUp = Vector3.Dot(carry, up);
                if (Mathf.Abs(carryUp) > 0.01f && vUp <= carryUp + 0.5f) vUp = carryUp;
            }

            if (jumpBuffer > 0f && Time.time - lastGroundedTime <= coyoteTime && !MovementLocked)
            {
                vUp = jumpVelocity + Mathf.Max(0f, Vector3.Dot(carry, up));
                jumpBuffer = 0f;
                lastGroundedTime = -1f;
                IsGrounded = false;
                Sfx.Play(SfxId.Jump, 0.35f);
            }
            else if (jumpBuffer > 0f && airJumpsLeft > 0 && !MovementLocked && !IsGrounded)
            {
                // double jump: a second push in the air (never weaker than the current rise)
                vUp = Mathf.Max(vUp, jumpVelocity * airJumpScale);
                jumpBuffer = 0f;
                airJumpsLeft--;
                Sfx.Play(SfxId.Jump, 0.3f, 1.25f);
            }
            rb.linearVelocity = vPlane + up * vUp;

            if (moveDir.sqrMagnitude > 0.01f) Facing = moveDir.normalized;
            else Facing = Vector3.ProjectOnPlane(Facing, up).sqrMagnitude > 0.01f ? Vector3.ProjectOnPlane(Facing, up).normalized : right;
            var targetRot = Quaternion.LookRotation(Facing, up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * dt)));
        }

        Vector3 ComputeCarry()
        {
            Vector3 carry = Vector3.zero;
            Transform t = IsGrounded && GroundCollider != null ? GroundCollider.transform : null;
            if (t != null && t == carryTransform)
            {
                Vector3 now = t.TransformPoint(carryLocalPoint);
                carry = (now - carryWorldPoint) / Time.fixedDeltaTime;
                if (carry.sqrMagnitude > 400f) carry = Vector3.zero; // teleports / resets
            }
            carryTransform = t;
            if (t != null)
            {
                carryWorldPoint = rb.position;
                carryLocalPoint = t.InverseTransformPoint(carryWorldPoint);
            }
            return carry;
        }

        void CheckGround(Vector3 up)
        {
            float radius = col.radius * 0.9f;
            float dist = col.height * 0.5f - radius + groundCheckDistance;
            Vector3 origin = transform.TransformPoint(col.center);
            int n = Physics.SphereCastNonAlloc(origin, radius, -up, hits, dist, groundMask, QueryTriggerInteraction.Ignore);
            IsGrounded = false;
            GroundCollider = null;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var hit = hits[i];
                if (hit.collider == col || hit.distance <= 0f || Vector3.Dot(hit.normal, up) < 0.5f) continue;
                if (hit.collider.attachedRigidbody == rb) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    IsGrounded = true;
                    GroundCollider = hit.collider;
                }
            }
        }

        void OnCollisionStay(Collision c)
        {
            if (!IsGrounded || MoveDirection.sqrMagnitude < 0.1f) return;
            var pushable = c.collider.GetComponentInParent<Pushable>();
            if (pushable == null || pushable.gameObject == gameObject) return;
            // direction from us to the contact point (independent of which way the contact normal is reported)
            Vector3 toContact = Vector3.ProjectOnPlane(c.GetContact(0).point - transform.position, Up);
            if (toContact.sqrMagnitude < 0.0025f) return;
            if (Vector3.Dot(MoveDirection.normalized, toContact.normalized) > 0.5f)
            {
                pushable.AddPush(MoveDirection.normalized, this);
                lastPushTime = Time.time;
            }
        }

        /// <summary>Shove the player (e.g. a swinging cable). Control is reduced briefly.</summary>
        public void Knockback(Vector3 velocity, float stun = 0.45f)
        {
            if (!CanBeHurt) return;
            rb.linearVelocity = velocity;
            stunnedUntil = Time.time + stun;
            Sfx.Play(SfxId.Hit, 0.7f);
        }

        public void Respawn()
        {
            if (Time.time - LastRespawnTime < 0.2f) return;
            Vector3 p = Checkpoint.GetSpawnPoint(playerIndex, spawnPosition);
            rb.linearVelocity = Vector3.zero;
            rb.position = p;
            transform.position = p;
            rb.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            Facing = Vector3.right;
            jumpBuffer = 0f;
            stunnedUntil = 0f;
            carryTransform = null;
            invulnerableUntil = Time.time + respawnGrace;
            LastRespawnTime = Time.time;
            Sfx.Play(SfxId.Respawn, 0.6f);
            Respawned?.Invoke(this);
        }

        /// <summary>Called by hazards. Ignored during the short grace period after respawning.</summary>
        public void Kill()
        {
            if (!CanBeHurt) return;
            Respawn();
        }

        public void TeleportTo(Vector3 p)
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = p;
            transform.position = p;
            carryTransform = null;
        }
    }
}
