using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Player A — Gravity Controller. Changes gravity inside marked zones: the zone A stands in, otherwise the nearest
    /// controllable zone in range. Each press steps the zone to its next mode.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class GravityPower : MonoBehaviour
    {
        public float range = 18f;
        public float cooldown = 0.6f;

        PlayerController player;
        float nextUseTime;

        public GravityZone Target { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, nextUseTime - Time.time);
        public float CooldownFraction => cooldown <= 0f ? 0f : CooldownRemaining / cooldown;

        void Awake() => player = GetComponent<PlayerController>();

        void OnDisable() => SetTarget(null);

        void Update()
        {
            SetTarget(GravityZone.FindControllable(transform.position, range));
            if (!player.PowerPressed()) return;
            if (Target == null || Time.time < nextUseTime)
            {
                Sfx.Play(SfxId.Denied, 0.5f);
                if (Target == null) GameUI.Toast(player, "No gravity zone in range");
                return;
            }
            Target.CycleNext();
            nextUseTime = Time.time + cooldown;
            Sfx.Play(SfxId.GravityShift, 0.8f, Target.Mode == GravityMode.Zero ? 1.25f : 1f);
        }

        void SetTarget(GravityZone z)
        {
            if (Target == z) return;
            if (Target != null) Target.SetHighlighted(false);
            Target = z;
            if (Target != null) Target.SetHighlighted(true);
        }
    }
}
