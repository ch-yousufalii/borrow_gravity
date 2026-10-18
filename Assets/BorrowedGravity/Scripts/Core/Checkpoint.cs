using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Trigger that becomes the respawn point when any player reaches it. Checkpoints only move forward:
    /// a higher <see cref="section"/> replaces a lower one.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        public static Checkpoint Active { get; private set; }

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
        static void ResetStatics() => Active = null;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (isStart && (Active == null || Active.section > section)) Activate(false);
            UpdateVisual();
        }

        void OnTriggerEnter(Collider other)
        {
            bool arrived = carrierActivates ? other.GetComponentInParent<EngineCarrier>() != null : Players.From(other) != null;
            if (!arrived) return;
            if (Active != null && Active.section >= section) return;
            Activate(true);
        }

        void Activate(bool announce)
        {
            var previous = Active;
            Active = this;
            if (previous != null) previous.UpdateVisual();
            UpdateVisual();
            if (announce)
            {
                Sfx.Play(SfxId.Checkpoint, 0.8f);
                GameUI.ShowCheckpoint(label);
            }
        }

        void UpdateVisual()
        {
            if (beaconLight == null) return;
            bool on = Active == this;
            beaconLight.color = on ? new Color(0.35f, 1f, 0.6f) : new Color(1f, 0.6f, 0.3f);
            beaconLight.intensity = on ? 6f : 1.5f;
        }

        public Vector3 GetSpawn(int playerIndex)
        {
            Vector3 off = spawnOffsets != null && spawnOffsets.Length > 0
                ? spawnOffsets[Mathf.Clamp(playerIndex, 0, spawnOffsets.Length - 1)] : Vector3.up;
            return transform.position + off;
        }

        public static Vector3 GetSpawnPoint(int playerIndex, Vector3 fallback) =>
            Active != null ? Active.GetSpawn(playerIndex) : fallback;
    }
}
