using System.Collections;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Makes a chain of <see cref="CrumblePlatform"/>s fail one after another (timed platform runs, collapsing bridges).
    /// Starts when a player (or the engine carrier) enters this trigger, or from code.
    /// </summary>
    public class TimedPowerDown : MonoBehaviour, IResettable
    {
        public int section;
        public CrumblePlatform[] platforms;
        public float startDelay = 0.6f;
        public float interval = 1.3f;
        public float warnTime = 1.2f;
        public bool triggeredByCarrier;
        public bool triggeredByPlayers = true;
        [Tooltip("How many players must be inside the trigger at once (2 = only when both are on it).")]
        public int requiredPlayers = 1;
        [Tooltip("Run again whenever the trigger is re-entered after the sequence ended.")]
        public bool rearm = true;
        public HudHazard hazard = HudHazard.BridgeCrack;
        public string warningText = "Platforms powering down — move!";

        Coroutine running;
        readonly System.Collections.Generic.HashSet<PlayerController> inside = new System.Collections.Generic.HashSet<PlayerController>();

        public bool Running => running != null;
        public int Section => section;

        void OnTriggerEnter(Collider other)
        {
            var p = Players.From(other);
            if (p != null) inside.Add(p);
            if (running != null) return;
            bool carrier = other.GetComponentInParent<EngineCarrier>() != null;
            if ((triggeredByPlayers && p != null && inside.Count >= requiredPlayers) || (triggeredByCarrier && carrier)) Begin();
        }

        void OnTriggerExit(Collider other)
        {
            var p = Players.From(other);
            if (p != null) inside.Remove(p);
        }

        public void Begin()
        {
            if (running != null) return;
            running = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            GameUI.ShowWarning(hazard, warningText, 0f, 2.5f);
            yield return new WaitForSeconds(startDelay);
            foreach (var p in platforms)
            {
                if (p != null) p.Trigger(warnTime);
                yield return new WaitForSeconds(interval);
            }
            // let the last pieces finish before allowing a rerun
            yield return new WaitForSeconds(warnTime + 2f);
            running = rearm ? null : running;
        }

        public void ResetState()
        {
            if (running != null) StopCoroutine(running);
            running = null;
        }
    }
}
