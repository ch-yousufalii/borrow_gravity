using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Each player's own camera, with shake. Three views (picked in the pause menu via ControlSettings.View):
    ///  - Follow (default): close camera just behind the player, player stays visible, view is mostly ahead.
    ///  - ThirdPerson: the original wider chase camera behind and above the player.
    ///  - Side: the older side-on camera that frames all targets.
    /// Follow and ThirdPerson both slowly turn to follow where the player walks (not when they step sideways),
    /// stay out of walls and follow the player's gravity "up" onto walls, ceilings and the ring.
    /// With thirdPerson off it is always the side-on camera.
    /// </summary>
    public class CoopCamera : MonoBehaviour
    {
        static readonly System.Collections.Generic.List<CoopCamera> all = new System.Collections.Generic.List<CoopCamera>();

        public Transform[] targets;
        [Tooltip("Optional extra target (e.g. the engine carrier) that is kept in frame with a lower weight.")]
        public Transform extraTarget;
        public Vector3 offset = new Vector3(0f, 6.5f, -17f);
        public Vector3 lookOffset = new Vector3(0f, 0.8f, 0f);
        [Tooltip("Shifts the focus point, e.g. toward the goal so players see what is ahead.")]
        public Vector3 focusOffset = new Vector3(3f, 0f, 0f);
        public float extraDistancePerUnit = 0.55f;
        public float maxExtraDistance = 18f;
        public float minFocusY = -6f;
        public float smoothTime = 0.35f;

        [Header("Follow view (default): close behind the player")]
        [Tooltip("How far behind the player the camera sits.")]
        public float followDistance = 3.4f;
        [Tooltip("Camera height above the player's feet.")]
        public float followHeight = 1.7f;
        [Tooltip("Height of the point the camera looks at (player's upper body).")]
        public float followLookHeight = 1.5f;
        [Tooltip("How far in front of the player the camera aims, so you see what is ahead.")]
        public float followLookAhead = 8f;

        [Header("Third person")]
        public bool thirdPerson = true;
        public float distance = 6.2f;
        public float height = 2.6f;
        public float lookHeight = 1.3f;
        public float lookAhead = 2.2f;
        [Tooltip("How fast the camera swings round behind the player while they walk forward (per second).")]
        public float followRate = 1.6f;
        [Tooltip("Direction the camera faces at the start: the way the level goes.")]
        public Vector3 startHeading = Vector3.right;
        public float collisionRadius = 0.3f;
        [Tooltip("Lowest / highest camera tilt in degrees (negative = from below).")]
        public float minPitch = -12f, maxPitch = 60f;
        [Tooltip("After the player turns the camera by hand, wait this long before auto-follow takes over again.")]
        public float manualHold = 1.6f;

        Vector3 heading, camUp = Vector3.up, tpVelocity, lastTargetPos;
        float pitch = float.NaN, lastManualLook = -10f;
        Vector3 lastSnapCheck;
        PlayerController player;
        readonly RaycastHit[] hits = new RaycastHit[12];

        Vector3 velocity, focus;
        bool initialised;
        float shakeAmount, shakeUntil;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static void Shake(float amount, float duration)
        {
            NetSession.UiShake(amount, duration);
            foreach (var c in all)
            {
                c.shakeAmount = Mathf.Max(c.shakeAmount * (Time.time < c.shakeUntil ? 1f : 0f), amount);
                c.shakeUntil = Mathf.Max(c.shakeUntil, Time.time + duration);
            }
        }

        public void SnapNow() => initialised = false;

        public static void SnapAll() { foreach (var c in all) c.SnapNow(); }

        CameraView shownView;

        void LateUpdate()
        {
            // the pause menu can switch between Follow, third person and the side view at any time
            var view = thirdPerson ? ControlSettings.View : CameraView.Side;
            if (view != shownView)
            {
                shownView = view;
                initialised = false;
                pitch = float.NaN; // re-pick the tilt that suits the new view
            }
            if ((view == CameraView.Follow || view == CameraView.ThirdPerson) && FollowPlayer()) return;
            if (!TryGetBounds(out Bounds b)) return;
            Vector3 c = b.center + focusOffset;
            c.y = Mathf.Max(c.y, minFocusY);
            float spread = Mathf.Max(b.size.x, b.size.y * 1.6f, b.size.z * 1.2f);
            float extra = Mathf.Min(maxExtraDistance, spread * extraDistancePerUnit);
            Vector3 desired = c + offset.normalized * (offset.magnitude + extra);

            if (!initialised)
            {
                transform.position = desired;
                focus = c;
                velocity = Vector3.zero;
                initialised = true;
            }
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            focus = Vector3.Lerp(focus, c, 1f - Mathf.Exp(-8f * Time.deltaTime));
            var rot = Quaternion.LookRotation(focus + lookOffset - transform.position, Vector3.up);
            if (Time.time < shakeUntil)
            {
                float k = shakeAmount * Mathf.Clamp01((shakeUntil - Time.time) * 2f);
                rot *= Quaternion.Euler(Mathf.PerlinNoise(Time.time * 25f, 0f) * 2f * k - k, Mathf.PerlinNoise(0f, Time.time * 25f) * 2f * k - k, 0f);
            }
            transform.rotation = rot;
        }

        bool FollowPlayer()
        {
            var t = targets != null && targets.Length > 0 ? targets[0] : null;
            if (t == null || !t.gameObject.activeInHierarchy) return false;
            if (player == null || player.transform != t) player = t.GetComponent<PlayerController>();
            float dt = Time.deltaTime;
            Vector3 up = player != null ? player.Up : Vector3.up;

            // pick the numbers for the current view: close Follow camera or the wider third person one
            bool close = shownView == CameraView.Follow;
            float vDistance = close ? followDistance : distance;
            float vHeight = close ? followHeight : height;
            float vLookHeight = close ? followLookHeight : lookHeight;
            float vLookAhead = close ? followLookAhead : lookAhead;

            if (!initialised)
            {
                camUp = up;
                heading = Vector3.ProjectOnPlane(startHeading, camUp);
                if (heading.sqrMagnitude < 0.01f) heading = Vector3.ProjectOnPlane(Vector3.forward, camUp);
                heading.Normalize();
                lastTargetPos = t.position;
            }
            float dist = Mathf.Sqrt(vDistance * vDistance + (vHeight - vLookHeight) * (vHeight - vLookHeight));
            if (float.IsNaN(pitch)) pitch = Mathf.Atan2(vHeight - vLookHeight, vDistance) * Mathf.Rad2Deg;
            // gravity changes swing the camera's up smoothly (walls, ceilings, the ring)
            camUp = Vector3.Slerp(camUp, up, 1f - Mathf.Exp(-5f * dt)).normalized;
            Vector3 h = Vector3.ProjectOnPlane(heading, camUp);
            if (h.sqrMagnitude < 0.01f) h = Vector3.ProjectOnPlane(transform.forward, camUp);
            heading = h.normalized;

            // turned by hand (mouse / finger drag): yaw around "up", tilt up and down
            Vector2 turn = player != null ? VirtualInput.ConsumeLook(player.playerIndex) : Vector2.zero;
            if (turn.sqrMagnitude > 0f)
            {
                heading = (Quaternion.AngleAxis(turn.x, camUp) * heading).normalized;
                pitch = Mathf.Clamp(pitch + turn.y, minPitch, maxPitch);
                lastManualLook = Time.unscaledTime;
            }

            // follow the walking direction, but only when heading mostly forward (sideways steps don't spin it).
            // Movement is read from the actual motion, so it also works on the joining phone (where the host moves us).
            Vector3 vel = dt > 0f ? (t.position - lastTargetPos) / dt : Vector3.zero;
            lastTargetPos = t.position;
            Vector3 move = Vector3.ProjectOnPlane(vel, camUp) / 5f;
            bool teleported = move.sqrMagnitude > 16f;
            if (move.sqrMagnitude > 1f) move.Normalize();
            if (!teleported && Time.unscaledTime - lastManualLook > manualHold && move.sqrMagnitude > 0.04f
                && Vector3.Dot(move.normalized, heading) > 0.35f)
                heading = Vector3.Slerp(heading, move.normalized, 1f - Mathf.Exp(-followRate * move.magnitude * dt)).normalized;

            Vector3 pivot = t.position + camUp * vLookHeight;
            float pr = pitch * Mathf.Deg2Rad;
            Vector3 desired = pivot + (-heading * Mathf.Cos(pr) + camUp * Mathf.Sin(pr)) * dist;
            desired = AvoidWalls(pivot, desired, t);
            Vector3 look = pivot + heading * vLookAhead;

            if (!initialised)
            {
                transform.position = desired;
                tpVelocity = Vector3.zero;
                initialised = true;
            }
            if ((t.position - lastSnapCheck).sqrMagnitude > 64f) transform.position = desired; // respawn / teleport
            lastSnapCheck = t.position;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref tpVelocity, 0.12f);
            var rot = Quaternion.LookRotation(look - transform.position, camUp);
            if (Time.time < shakeUntil)
            {
                float k = shakeAmount * Mathf.Clamp01((shakeUntil - Time.time) * 2f);
                rot *= Quaternion.Euler(Mathf.PerlinNoise(Time.time * 25f, 0f) * 2f * k - k, Mathf.PerlinNoise(0f, Time.time * 25f) * 2f * k - k, 0f);
            }
            transform.rotation = rot;
            return true;
        }

        /// <summary>Pull the camera in front of any wall between it and the player.</summary>
        Vector3 AvoidWalls(Vector3 pivot, Vector3 desired, Transform target)
        {
            Vector3 dir = desired - pivot;
            float len = dir.magnitude;
            if (len < 0.01f) return desired;
            dir /= len;
            int n = Physics.SphereCastNonAlloc(pivot, collisionRadius, dir, hits, len, ~0, QueryTriggerInteraction.Ignore);
            float best = len;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || hits[i].distance <= 0f) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue; // crates, the engine, players
                if (c.transform.IsChildOf(target) || Players.From(c) != null) continue;
                best = Mathf.Min(best, hits[i].distance);
            }
            return pivot + dir * Mathf.Max(0.8f, best - 0.1f);
        }

        bool TryGetBounds(out Bounds b)
        {
            b = default;
            bool any = false;
            if (targets != null)
                foreach (var t in targets)
                {
                    if (t == null || !t.gameObject.activeInHierarchy) continue;
                    if (!any) { b = new Bounds(t.position, Vector3.zero); any = true; }
                    else b.Encapsulate(t.position);
                }
            if (any && extraTarget != null && extraTarget.gameObject.activeInHierarchy)
            {
                // keep the carrier roughly in frame without letting it dominate
                Vector3 p = Vector3.Lerp(b.center, extraTarget.position, 0.6f);
                b.Encapsulate(p);
            }
            return any;
        }
    }
}