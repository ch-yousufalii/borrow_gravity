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
        [Tooltip("Run again whenever the trigger is re-entered after the sequence ended.")]
        public bool rearm = true;
        public HudHazard hazard = HudHazard.BridgeCrack;
        public string warningText = "Platforms powering down — move!";

        Coroutine running;

        public bool Running => running != null;
        public int Section => section;

        void OnTriggerEnter(Collider other)
        {
            if (running != null) return;
            bool player = Players.From(other) != null;
            bool carrier = other.GetComponentInParent<EngineCarrier>() != null;
            if ((triggeredByPlayers && player) || (triggeredByCarrier && carrier)) Begin();
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
