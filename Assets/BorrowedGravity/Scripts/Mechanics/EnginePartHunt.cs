using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Level 7: the engine lies in pieces. Walking up to a part picks it up, and it appears on the engine taking shape in
    /// that player's hands. Parts are kept through respawns. Collecting the last one completes the level.
    /// Host only: joining devices mirror the pickups and hand pieces, and read the count from the HUD stream.
    /// </summary>
    public class EnginePartHunt : MonoBehaviour
    {
        /// <summary>The hunt in the level being played (null in other levels).</summary>
        public static EnginePartHunt Instance { get; private set; }

        public GameObject[] pickups;
        [Tooltip("The pieces in Player A's hands, in the order they appear.")]
        public GameObject[] handPiecesA;
        [Tooltip("The pieces in Player B's hands, in the order they appear.")]
        public GameObject[] handPiecesB;
        public float pickupRange = 1.9f;
        public float spinSpeed = 70f;

        int heldA, heldB;

        public int Total => pickups != null ? pickups.Length : 0;
        public int Collected => heldA + heldB;

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        void Update()
        {
            var sm = StageManager.Instance;
            if (sm == null || sm.State != StageState.Playing) return;
            foreach (var part in pickups)
            {
                if (part == null || !part.activeSelf) continue;
                part.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
                foreach (var p in Players.All)
                {
                    if (p == null || (p.transform.position - part.transform.position).sqrMagnitude > pickupRange * pickupRange) continue;
                    Collect(part, p);
                    break;
                }
            }
            // arms stay out while there's something in them
            foreach (var p in Players.All)
                if (p != null && (p.playerIndex == 0 ? heldA : heldB) > 0) p.CarryingUntil = Time.time + 0.2f;
        }

        void Collect(GameObject part, PlayerController p)
        {
            part.SetActive(false);
            if (p.playerIndex == 0) Show(handPiecesA, heldA++);
            else Show(handPiecesB, heldB++);
            Sfx.Play(SfxId.Plate, 0.8f, 1.1f + Collected * 0.03f);
            if (Collected >= Total)
            {
                StageManager.Instance.CompleteStage();
                return;
            }
            GameUI.ShowMessage(Collected == Total - 1 ? "One part left!" : "Engine part " + Collected + " of " + Total, 2f);
        }

        static void Show(GameObject[] pieces, int index)
        {
            if (pieces != null && index < pieces.Length && pieces[index] != null) pieces[index].SetActive(true);
        }
    }
}
