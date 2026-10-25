using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Trigger that becomes the respawn point of the player who reaches it (each player has their own; the other one
    /// keeps theirs until they get there too). Checkpoints only move forward: a higher <see cref="section"/> replaces a
    /// lower one. A checkpoint the engine carrier activates (Stage 3) counts for both players, who carry it together.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        static readonly Checkpoint[] active = new Checkpoint[GameFlow.MaxPlayers];

        /// <summary>This player's respawn checkpoint (null = their start position).</summary>
        public static Checkpoint ActiveFor(int player) => player >= 0 && player < active.Length ? active[player] : null;

        /// <summary>The furthest checkpoint anyone has reached (where the engine carrier comes back).</summary>
        public static Checkpoint Active
        {
            get
            {
                Checkpoint best = null;
                foreach (var c in active) if (c != null && (best == null || c.section > best.section)) best = c;
                return best;
            }
        }

        /// <summary>The checkpoint of whoever is furthest behind (a section reset starts from there).</summary>
        public static Checkpoint Earliest
        {
            get
            {
                Checkpoint low = null;
                foreach (var c in active) if (c != null && (low == null || c.section < low.section)) low = c;
                return low;
            }
        }

        public int section;
        public string label = "Checkpoint";
        public bool isStart;
        [Tooltip("Spawn offsets (local space) indexed by PlayerController.playerIndex.")]
        public Vector3[] spawnOffsets = { new Vector3(-0.8f, 1f, -1.2f), new Vector3(0.8f, 1f, -1.2f) };
        [Tooltip("Where the engine carrier respawns from this checkpoint (Stage 3).")]
        public Transform carrierSpawn;
        public Light beaconLight;
        [Tooltip("Stage 3: activate when the engine carrier arrives, not the players.")]
        public bool carrierActivates;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ClearActive();

        /// <summary>A new level starts in the same scene: forget the previous level's checkpoints.</summary>
        public static void ClearActive() => System.Array.Clear(active, 0, active.Length);

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (isStart)
                for (int i = 0; i < active.Length; i++)
                    if (active[i] == null || active[i].section > section) Activate(i, false);
            UpdateVisual();
        }

        void OnTriggerEnter(Collider other)
        {
            if (carrierActivates)
            {
                if (other.GetComponentInParent<EngineCarrier>() == null) return;
                bool any = false;
                for (int i = 0; i < active.Length; i++)
                    if (active[i] == null || active[i].section < section) { Activate(i, false); any = true; }
                if (any) Announce();
                return;
            }
            var p = Players.From(other);
            if (p == null) return;
            var mine = ActiveFor(p.playerIndex);
            if (mine != null && mine.section >= section) return;
            Activate(p.playerIndex, true);
        }

        void Activate(int player, bool announce)
        {
            if (player < 0 || player >= active.Length) return;
            var previous = active[player];
            active[player] = this;
            if (previous != null) previous.UpdateVisual();
            UpdateVisual();
            if (announce) Announce();
        }

        void Announce()
        {
            Sfx.Play(SfxId.Checkpoint, 0.8f);
            GameUI.ShowCheckpoint(label);
        }

        void UpdateVisual()
        {
            if (beaconLight == null) return;
            bool on = System.Array.IndexOf(active, this) >= 0;
            beaconLight.color = on ? new Color(0.35f, 1f, 0.6f) : new Color(1f, 0.6f, 0.3f);
            beaconLight.intensity = on ? 6f : 1.5f;
        }

        public Vector3 GetSpawn(int playerIndex)
        {
            Vector3 off = spawnOffsets != null && spawnOffsets.Length > 0
                ? spawnOffsets[Mathf.Clamp(playerIndex, 0, spawnOffsets.Length - 1)] : Vector3.up;
            return transform.position + off;
        }

        public static Vector3 GetSpawnPoint(int playerIndex, Vector3 fallback)
        {
            var c = ActiveFor(playerIndex);
            return c != null ? c.GetSpawn(playerIndex) : fallback;
        }
    }
}
