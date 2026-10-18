using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Relays must be activated in the order shown by the hint lights. Each correct relay switches a linked zone;
    /// a wrong one flashes red and resets the sequence (it never traps anyone). ON once solved.
    /// </summary>
    public class RelaySequence : SignalSource, IResettable
    {
        public int section;
        public PressurePlate[] relays;
        public int[] order = { 2, 0, 1 };
        public Color[] relayColors = { new Color(1f, 0.3f, 0.3f), new Color(0.3f, 1f, 0.4f), new Color(0.3f, 0.6f, 1f) };
        public Renderer[] relayLights;
        [Tooltip("Lights that loop through the correct order as a clue.")]
        public Renderer[] hintLights;
        public GravityZone[] linkedZones;
        public GravityMode[] linkedModes;
        public string solvedMessage = "Relays online — the bridge field is active!";

        int progress;
        bool solved;
        bool[] wasOn;
        float errorFlash;
        Material[] relayMats, hintMats;

        public override bool IsOn => solved;
        public int Section => section;

        void Awake()
        {
            wasOn = new bool[relays.Length];
            relayMats = Mats(relayLights);
            hintMats = Mats(hintLights);
        }

        static Material[] Mats(Renderer[] rs)
        {
            if (rs == null) return new Material[0];
            var m = new Material[rs.Length];
            for (int i = 0; i < rs.Length; i++) { m[i] = rs[i].material; m[i].EnableKeyword("_EMISSION"); }
            return m;
        }

        void Update()
        {
            for (int i = 0; i < relays.Length; i++)
            {
                bool on = relays[i] != null && relays[i].IsOn;
                if (on && !wasOn[i] && !solved) Activate(i);
                wasOn[i] = on;
            }
            errorFlash = Mathf.Max(0f, errorFlash - Time.deltaTime);

            for (int i = 0; i < relayMats.Length; i++)
            {
                bool done = solved || IndexInSolvedPart(i);
                Color c = errorFlash > 0f ? Color.red * (Mathf.Repeat(errorFlash * 6f, 1f) > 0.5f ? 3f : 0.3f)
                        : relayColors[i % relayColors.Length] * (done ? 4f : 0.8f);
                relayMats[i].SetColor("_EmissionColor", c);
            }
            // hint: light the order one after another, then pause
            float cycle = Mathf.Repeat(Time.time, order.Length * 0.8f + 1.2f);
            int step = Mathf.FloorToInt(cycle / 0.8f);
            for (int i = 0; i < hintMats.Length; i++)
            {
                int relay = order[Mathf.Min(i, order.Length - 1)];
                bool lit = solved || step == i;
                hintMats[i].SetColor("_EmissionColor", relayColors[relay % relayColors.Length] * (lit ? 4f : 0.25f));
            }
        }

        bool IndexInSolvedPart(int relayIndex)
        {
            for (int k = 0; k < progress; k++) if (order[k] == relayIndex) return true;
            return false;
        }

        void Activate(int relayIndex)
        {
            if (order[progress] == relayIndex)
            {
                if (linkedZones != null && progress < linkedZones.Length && linkedZones[progress] != null)
                    linkedZones[progress].SetMode(linkedModes[Mathf.Min(progress, linkedModes.Length - 1)]);
                progress++;
                Sfx.Play(SfxId.Checkpoint, 0.6f, 0.9f + progress * 0.15f);
                if (progress >= order.Length)
                {
                    solved = true;
                    GameUI.ShowMessage(solvedMessage, 3.5f);
                    Sfx.Play(SfxId.Success, 0.7f);
                }
                return;
            }
            if (IndexInSolvedPart(relayIndex)) return; // stepping back on a done relay is harmless
            progress = 0;
            errorFlash = 1.2f;
            if (linkedZones != null) foreach (var z in linkedZones) if (z != null) z.ResetState();
            Sfx.Play(SfxId.Denied, 0.8f);
            GameUI.ShowWarning(HudHazard.Electric, "Wrong relay — sequence reset. Watch the clue lights.", 0f, 2.5f);
        }

        public void ResetState()
        {
            progress = 0;
            solved = false;
            errorFlash = 0f;
            for (int i = 0; i < wasOn.Length; i++) wasOn[i] = false;
        }
    }
}
