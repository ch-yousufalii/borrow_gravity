using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A last stand: the repair only runs while every player stands inside the ring, and something keeps striking the
    /// ring until it finishes. On (latched) once full, so a <see cref="GoalTrigger"/> can wait for it.
    /// </summary>
    public class RepairHold : SignalSource, IResettable
    {
        public int section;
        public float radius = 5.5f;
        public float seconds = 18f;
        [Tooltip("How fast the repair loses progress while someone is outside, compared to how fast it fills.")]
        public float drain = 0.25f;
        public WardenStrike[] strikes;
        public float strikeEvery = 1.8f;
        public float telegraph = 1.2f;
        [Tooltip("Grows with the progress, so the ring shows how far along it is.")]
        public Transform progressDisc;
        public float finishDelay = 1.6f;
        public string label = "HOLD THE REPAIR";
        [TextArea(1, 3)] public string startMessage;
        [TextArea(1, 3)] public string doneMessage;

        float progress, nextStrike, finishedAt = -1f, discY;
        bool started;

        public override bool IsOn => finishedAt >= 0f && Time.time >= finishedAt;
        public int Section => section;
        public float Progress => progress;

        void Awake()
        {
            if (progressDisc != null) discY = progressDisc.localScale.y;
            Refresh();
        }

        bool Inside(PlayerController p)
        {
            Vector3 d = p.transform.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        void Update()
        {
            if (finishedAt >= 0f) return;
            var sm = StageManager.Instance;
            if (sm == null || sm.State != StageState.Playing) return;

            int inside = 0;
            foreach (var p in Players.All) if (p != null && Inside(p)) inside++;

            if (!started)
            {
                if (inside == 0) return;
                started = true;
                nextStrike = Time.time + 1.5f;
                if (!string.IsNullOrEmpty(startMessage)) GameUI.ShowMessage(startMessage, 4f);
                Sfx.Play(SfxId.Rumble, 0.8f);
                CoopCamera.Shake(0.4f, 0.8f);
                GameUI.StartCountdown(label, HudHazard.General, () => (1f - progress) * seconds);
            }

            bool everyone = inside > 0 && inside >= Players.All.Count;
            progress = Mathf.Clamp01(progress + (everyone ? 1f : -drain) * Time.deltaTime / Mathf.Max(0.1f, seconds));
            Refresh();

            if (progress >= 1f)
            {
                finishedAt = Time.time + finishDelay;
                foreach (var s in strikes) if (s != null) s.Clear();
                GameUI.StopCountdown();
                if (!string.IsNullOrEmpty(doneMessage)) GameUI.ShowMessage(doneMessage, 3f);
                Sfx.Play(SfxId.Success, 0.8f);
                CoopCamera.Shake(0.5f, 1f);
                return;
            }

            if (Time.time >= nextStrike) Strike();
        }

        void Strike()
        {
            WardenStrike free = null;
            foreach (var s in strikes) if (s != null && !s.Busy) { free = s; break; }
            if (free == null) return;
            nextStrike = Time.time + strikeEvery;

            // aimed at someone in the ring, with enough time and room to step aside
            PlayerController target = null;
            int seen = 0;
            foreach (var p in Players.All)
            {
                if (p == null || !Inside(p)) continue;
                seen++;
                if (Random.Range(0, seen) == 0) target = p;
            }
            Vector3 at = target != null ? target.transform.position : transform.position;
            at += Random.insideUnitSphere * 1.2f;
            Vector3 fromCentre = at - transform.position;
            fromCentre.y = 0f;
            at = transform.position + Vector3.ClampMagnitude(fromCentre, radius);
            free.Fire(at, telegraph);
        }

        void Refresh()
        {
            if (progressDisc == null) return;
            float s = Mathf.Lerp(0.05f, radius * 2f, progress);
            progressDisc.localScale = new Vector3(s, discY, s);
        }

        public void ResetState()
        {
            progress = 0f;
            started = false;
            finishedAt = -1f;
            foreach (var s in strikes) if (s != null) s.Clear();
            GameUI.StopCountdown();
            Refresh();
        }
    }
}
