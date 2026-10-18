using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// The ship's engine bay: a player carrying an engine half walks into it and the half is installed in its slot.
    /// On (for the final repair logic) once every half is in.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EngineBay : SignalSource
    {
        [Tooltip("Slot for Player A's half (index 0) and Player B's half (index 1).")]
        public Transform[] slots = new Transform[2];
        public Renderer indicator;
        public string bothInMessage = "Both engine halves installed!";

        int installed;
        Material indicatorMat;

        /// <summary>On when every engine part in play (one per carrying player) is installed.</summary>
        public override bool IsOn => installed > 0 && installed >= EnginePart.All.Count;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (indicator != null) { indicatorMat = indicator.material; indicatorMat.EnableKeyword("_EMISSION"); }
            Refresh();
        }

        void OnTriggerEnter(Collider other) => OnTriggerStay(other);

        void OnTriggerStay(Collider other)
        {
            var p = Players.From(other);
            if (p == null) return;
            foreach (var part in EnginePart.All)
            {
                if (part.State != EnginePart.PartState.Held || part.Holder != p) continue;
                var slot = slots != null && part.owner < slots.Length ? slots[part.owner] : null;
                part.Install(slot != null ? slot : transform);
                installed++;
                GameUI.Toast(p, "Engine half installed");
                if (IsOn) GameUI.ShowMessage(bothInMessage, 2.5f);
                Refresh();
            }
        }

        void Refresh()
        {
            if (indicatorMat == null) return;
            Color c = IsOn ? new Color(0.3f, 1f, 0.55f) : installed > 0 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.2f);
            indicatorMat.SetColor("_EmissionColor", c * 2.5f);
            if (indicatorMat.HasProperty("_BaseColor")) indicatorMat.SetColor("_BaseColor", c);
        }
    }
}
