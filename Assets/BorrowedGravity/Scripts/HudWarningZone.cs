using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Trigger that shows a HUD hazard warning (optionally with a countdown) when a player walks in.</summary>
    [RequireComponent(typeof(Collider))]
    public class HudWarningZone : MonoBehaviour
    {
        public HudHazard hazard = HudHazard.General;
        public string message = "Incoming debris!";
        [Tooltip("Seconds shown on the countdown ring. 0 = no countdown, the warning shows for 'duration'.")]
        public float countdown = 0f;
        public float duration = 3f;
        [Tooltip("Show only the first time a player enters.")]
        public bool once = true;

        bool fired;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (once && fired) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            fired = true;
            GameUI.ShowWarning(hazard, message, countdown, duration);
        }
    }
}
