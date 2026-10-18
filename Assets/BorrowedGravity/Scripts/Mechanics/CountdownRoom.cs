using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A room that starts losing pressure once a player enters: the zone goes weightless and a countdown runs.
    /// Solving <see cref="successSignal"/> in time stops it; otherwise both players go back to the checkpoint.
    /// As a signal it is ON while the countdown is running (use it to seal the entry door).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CountdownRoom : SignalSource, IResettable
    {
        public int section;
        public float duration = 50f;
        public SignalSource successSignal;
        public GravityZone roomZone;
        public GravityMode decompressMode = GravityMode.Zero;
        public string label = "DECOMPRESSION";
        public string startMessage = "Hull breach! Seat the power cell and hold both consoles!";
        public string successMessage = "Pressure stabilised — airlock open!";

        [Tooltip("The countdown starts once this many players are inside the room.")]
        public int requiredPlayers = 2;

        public bool Running { get; private set; }
        public bool Solved { get; private set; }
        public float TimeLeft { get; private set; }

        public override bool IsOn => Running;
        public int Section => section;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        readonly System.Collections.Generic.HashSet<PlayerController> inside = new System.Collections.Generic.HashSet<PlayerController>();

        void OnTriggerEnter(Collider other)
        {
            var p = Players.From(other);
            if (p != null) inside.Add(p);
        }

        void OnTriggerExit(Collider other)
        {
            var p = Players.From(other);
            if (p != null) inside.Remove(p);
        }

        void FixedUpdate()
        {
            inside.RemoveWhere(p => p == null);
            if (!Running && !Solved && inside.Count >= requiredPlayers) Begin();
        }

        void Begin()
        {
            Running = true;
            TimeLeft = duration;
            if (roomZone != null) roomZone.SetMode(decompressMode);
            GameUI.ShowWarning(HudHazard.Decompression, startMessage, 0f, 4f);
            GameUI.StartCountdown(label, HudHazard.Decompression, () => TimeLeft);
            Sfx.SetLoop(LoopId.Alarm, true, 0.18f);
        }

        void Update()
        {
            if (!Running) return;
            if (successSignal != null && successSignal.IsOn)
            {
                Running = false;
                Solved = true;
                GameUI.StopCountdown();
                Sfx.SetLoop(LoopId.Alarm, false);
                Sfx.Play(SfxId.Success, 0.7f);
                GameUI.ShowMessage(successMessage, 3f);
                return;
            }
            float before = TimeLeft;
            TimeLeft -= Time.deltaTime;
            if (TimeLeft < 10f && Mathf.Floor(before) != Mathf.Floor(TimeLeft)) Sfx.Play(SfxId.Tick, 0.7f);
            if (TimeLeft <= 0f)
            {
                Running = false;
                GameUI.StopCountdown();
                Sfx.SetLoop(LoopId.Alarm, false);
                GameUI.ShowWarning(HudHazard.Decompression, "Room decompressed! Try again.", 0f, 3f);
                if (StageManager.Instance != null) StageManager.Instance.ResetToCheckpoint();
            }
        }

        public void ResetState()
        {
            inside.Clear();
            if (Running) { GameUI.StopCountdown(); Sfx.SetLoop(LoopId.Alarm, false); }
            Running = false;
            Solved = false;
        }
    }
}
