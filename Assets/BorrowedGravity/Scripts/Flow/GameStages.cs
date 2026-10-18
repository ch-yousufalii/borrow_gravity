using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Switches levels inside the single Game scene: the current level's root is on, all others are off, so moving
    /// on is instant (no scene load). Index 0 = opening cutscene, 1-3 = stages, 4 = ending cutscene.
    /// </summary>
    public class GameStages : MonoBehaviour
    {
        public const int Intro = 0, Outro = 4;

        public static GameStages Instance { get; private set; }

        public StageRoot[] roots;

        public int Current { get; private set; } = -1;

        void Awake()
        {
            Instance = this;
            foreach (var r in roots) if (r != null) r.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Start (not Awake) so the scene has finished loading when the first level comes on
        void Start()
        {
            if (Current < 0) Show(GameFlow.PendingStage);
        }

        public bool Has(int index) => index >= 0 && index < roots.Length && roots[index] != null;

        public void Show(int index)
        {
            if (!Has(index)) index = 1;
            Time.timeScale = 1f;
            Sfx.StopAllLoops();
            Checkpoint.ClearActive();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null && i != index && roots[i].gameObject.activeSelf) roots[i].gameObject.SetActive(false);
            var root = roots[index];
            root.ApplyLighting();
            root.gameObject.SetActive(true);
            Current = index;
            GameFlow.PendingStage = index;

            if (NetSession.IsHost) NetSession.SendStage(index, GameFlow.ShowIntro);
            if (NetSession.Online)
            {
                if (NetWorld.Instance == null)
                {
                    if (root.GetComponentInChildren<StageManager>() != null) NetWorld.Setup(gameObject.scene);
                }
                else NetWorld.Instance.OnStageShown();
            }
        }
    }
}
