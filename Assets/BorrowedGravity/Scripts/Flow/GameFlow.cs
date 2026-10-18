using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowGravity
{
    /// <summary>Scene order and progress (unlocked stages are saved in PlayerPrefs).</summary>
    public static class GameFlow
    {
        public const string MenuScene = "MainMenu";
        public const string IntroScene = "Intro";
        public const string OutroScene = "Outro";
        public static readonly string[] StageScenes = { "Stage1", "Stage2", "Stage3" };
        const string UnlockKey = "bg_unlocked_stage";

        /// <summary>True when a stage was entered from the menu or a previous stage (shows the story intro).</summary>
        public static bool ShowIntro = true;

        public static int Unlocked
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(UnlockKey, 1), 1, StageScenes.Length);
            set { PlayerPrefs.SetInt(UnlockKey, Mathf.Clamp(value, 1, StageScenes.Length)); PlayerPrefs.Save(); }
        }

        public static void LoadStage(int stage)
        {
            Time.timeScale = 1f;
            ShowIntro = true;
            Sfx.StopAllLoops();
            Load(StageScenes[Mathf.Clamp(stage, 1, StageScenes.Length) - 1]);
        }

        /// <summary>New game: the opening cutscene, which then loads Stage 1.</summary>
        public static void StartNewGame()
        {
            Time.timeScale = 1f;
            Sfx.StopAllLoops();
            if (Application.CanStreamedLevelBeLoaded(IntroScene)) Load(IntroScene);
            else LoadStage(1);
        }

        /// <summary>Called when the opening cutscene ends: straight into Stage 1 (the cutscene already told the story).</summary>
        public static void AfterIntro()
        {
            Time.timeScale = 1f;
            ShowIntro = false;
            Sfx.StopAllLoops();
            Load(StageScenes[0]);
        }

        /// <summary>Last stage done: the ending cutscene (engine fixed, ship takes off), then the menu.</summary>
        public static void FinishGame()
        {
            Time.timeScale = 1f;
            Sfx.StopAllLoops();
            if (Application.CanStreamedLevelBeLoaded(OutroScene)) Load(OutroScene);
            else Load(MenuScene);
        }

        public static void AfterOutro() => LoadMenu();

        public static void ReloadStage()
        {
            Time.timeScale = 1f;
            ShowIntro = false;
            Sfx.StopAllLoops();
            Load(SceneManager.GetActiveScene().name);
        }

        public static void LoadMenu()
        {
            Time.timeScale = 1f;
            Sfx.StopAllLoops();
            Load(MenuScene);
        }

        /// <summary>
        /// Every scene change goes through here. Online the host decides and tells the other phone; the joining phone
        /// ignores its own requests (e.g. its copy of the cutscene ending) and waits for the host's.
        /// </summary>
        static void Load(string scene)
        {
            if (NetSession.IsClient) return;
            if (NetSession.IsHost) NetSession.SendScene(scene, ShowIntro);
            SceneManager.LoadScene(scene);
        }

        public static void StageCompleted(int stage)
        {
            if (stage + 1 > Unlocked) Unlocked = stage + 1;
        }

        public static bool HasNext(int stage) => stage < StageScenes.Length;
    }
}
