using UnityEngine;

namespace BorrowGravity
{
    public enum StageState { Intro, Playing, Paused, Complete }

    /// <summary>
    /// Runs one stage: story intro, pause menu, checkpoint resets (hold R), stage completion and the hand-off to the
    /// next stage. The HUD (<see cref="GameUI"/>) draws whatever this exposes.
    /// </summary>
    public class StageManager : MonoBehaviour
    {
        public static StageManager Instance { get; private set; }

        public int stageNumber = 1;
        public string stageTitle = "STAGE 1 — THE SHATTERED ORBIT";
        public HudObjective objective = HudObjective.ReachStation;
        public string objectiveText = "Reach the Station";
        [TextArea(3, 8)] public string introText;
        public string completeTitle = "STAGE COMPLETE";
        [TextArea(2, 6)] public string completeText;
        public bool isFinalStage;
        public float holdToResetSeconds = 1f;
        public bool playAmbience = true;
        [Tooltip("Seconds the stage-complete card shows before moving on by itself.")]
        public float autoContinueAfter = 4f;
        [Tooltip("Seconds the stage instructions card shows before the stage starts by itself.")]
        public float autoStartAfter = 8f;
        float introShownAt;

        /// <summary>Seconds until the instructions card starts the stage by itself.</summary>
        public float IntroLeft => State == StageState.Intro ? Mathf.Max(0f, autoStartAfter - (Time.unscaledTime - introShownAt)) : 0f;

        /// <summary>Seconds until the complete card moves on by itself (for the HUD).</summary>
        public float AutoContinueLeft => State == StageState.Complete ? Mathf.Max(0f, autoContinueAfter - (Time.unscaledTime - completeTime)) : 0f;

        public StageState State { get; private set; } = StageState.Intro;
        public float ResetHold { get; private set; }
        public float StageTime { get; private set; }
        public int Deaths { get; private set; }
        public int PauseSelection { get; set; }

        PlayerController[] players;
        float completeTime;

        void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        void Start()
        {
            players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (var p in players) p.Respawned += _ => Deaths++;
            if (playAmbience) Sfx.SetLoop(LoopId.Ambience, true, 0.35f);
            Sfx.SetLoop(LoopId.Music, true, 0.8f);
            if (GameFlow.ShowIntro && !string.IsNullOrEmpty(introText)) SetState(StageState.Intro);
            else SetState(StageState.Playing);
            GameUI.SetObjective(objective, objectiveText);
        }

        /// <summary>The mission banner shows for a few seconds when a stage starts; nobody moves while it's up.</summary>
        public const float MissionSeconds = 3f;
        float missionUntil;
        public bool MissionShowing => State == StageState.Playing && Time.unscaledTime < missionUntil;
        public float MissionSecondsLeft => Mathf.Max(0f, missionUntil - Time.unscaledTime);

        void SetState(StageState s)
        {
            if (s == StageState.Intro) introShownAt = Time.unscaledTime;
            if (s == StageState.Playing && (State == StageState.Intro || players == null || StageTime <= 0f)) missionUntil = Time.unscaledTime + MissionSeconds;
            State = s;
            bool playing = s == StageState.Playing;
            if (players != null) foreach (var p in players) if (p != null) p.InputEnabled = playing;
            Time.timeScale = s == StageState.Paused ? 0f : 1f;
        }

        void Update()
        {
            if (NetSession.IsClient) { ClientUpdate(); return; }
            switch (State)
            {
                case StageState.Intro:
                    // the instructions card starts the stage by itself after a few seconds (tap / Space to go sooner)
                    if (AnyConfirm() || Tapped() || Time.unscaledTime - introShownAt > autoStartAfter) { SetState(StageState.Playing); Sfx.Play(SfxId.Checkpoint, 0.5f); }
                    break;
                case StageState.Playing:
                    StageTime += Time.deltaTime;
                    if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.Escape) || PlayerKeys.Pressed(UnityEngine.InputSystem.Key.P))
                    {
                        PauseSelection = 0;
                        SetState(StageState.Paused);
                        break;
                    }
                    if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.C)) CycleCamera(); // same as the camera button
                    if (PlayerKeys.Held(UnityEngine.InputSystem.Key.R))
                    {
                        ResetHold += Time.deltaTime;
                        if (ResetHold >= holdToResetSeconds) { ResetHold = 0f; ResetToCheckpoint(); }
                    }
                    else ResetHold = 0f;
                    break;
                case StageState.Paused:
                    if (GameUI.SettingsOpen) { if (SettingsPanel.HandleKeys()) GameUI.SettingsOpen = false; break; }
                    if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.Escape)) { Resume(); break; }
                    if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.W) || PlayerKeys.Pressed(UnityEngine.InputSystem.Key.UpArrow))
                        PauseSelection = (PauseSelection + PauseItems.Length - 1) % PauseItems.Length;
                    if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.S) || PlayerKeys.Pressed(UnityEngine.InputSystem.Key.DownArrow))
                        PauseSelection = (PauseSelection + 1) % PauseItems.Length;
                    if (AnyConfirm()) ChoosePauseItem(PauseSelection);
                    break;
                case StageState.Complete:
                    // straight on to the next level (or the ending) after a short moment; tap / Space to go sooner
                    if (Time.unscaledTime - completeTime > autoContinueAfter || (Time.unscaledTime - completeTime > 1.5f && (AnyConfirm() || Tapped()))) Continue();
                    break;
            }
        }

        /// <summary>A touch or click anywhere this frame (to get past the story and stage-complete cards on a phone).</summary>
        static bool Tapped()
        {
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) return true;
            var m = UnityEngine.InputSystem.Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
        }

        // ---------- online ----------

        /// <summary>Joining phone: the host runs the stage; this phone only asks it to do things.</summary>
        void ClientUpdate()
        {
            switch (State)
            {
                case StageState.Intro:
                case StageState.Complete:
                    if (AnyConfirm() || Tapped()) NetSession.SendAction(NetAction.Confirm);
                    break;
                case StageState.Playing:
                    if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.Escape) || PlayerKeys.Pressed(UnityEngine.InputSystem.Key.P))
                        NetSession.SendAction(NetAction.Pause);
                    if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.C)) CycleCamera(); // same as the camera button
                    if (PlayerKeys.Held(UnityEngine.InputSystem.Key.R))
                    {
                        ResetHold += Time.deltaTime;
                        if (ResetHold >= holdToResetSeconds) { ResetHold = 0f; NetSession.SendAction(NetAction.ResetCheckpoint); }
                    }
                    else ResetHold = 0f;
                    break;
                case StageState.Paused:
                    if (GameUI.SettingsOpen) { if (SettingsPanel.HandleKeys()) GameUI.SettingsOpen = false; break; }
                    if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.Escape)) NetSession.SendAction(NetAction.Pause);
                    if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.W) || PlayerKeys.Pressed(UnityEngine.InputSystem.Key.UpArrow))
                        PauseSelection = (PauseSelection + PauseItems.Length - 1) % PauseItems.Length;
                    if (PlayerKeys.Pressed(UnityEngine.InputSystem.Key.S) || PlayerKeys.Pressed(UnityEngine.InputSystem.Key.DownArrow))
                        PauseSelection = (PauseSelection + 1) % PauseItems.Length;
                    if (AnyConfirm()) ChoosePauseItem(PauseSelection);
                    break;
            }
        }

        /// <summary>Joining phone: mirror the host's stage state (no time-scale change; nothing simulates here).</summary>
        public void ApplyRemote(StageState state, float stageTime, int deaths, float resetHold)
        {
            if (state != State && state == StageState.Paused) PauseSelection = 0;
            if (state != State && state == StageState.Intro) introShownAt = Time.unscaledTime;
            if (state != State && state == StageState.Playing && State == StageState.Intro) missionUntil = Time.unscaledTime + MissionSeconds;
            if (state != State && state == StageState.Complete) completeTime = Time.unscaledTime; // for the next-level countdown
            State = state;
            StageTime = stageTime;
            Deaths = deaths;
        }

        /// <summary>Host: the other phone tapped "start" / "continue".</summary>
        public void RemoteConfirm()
        {
            if (State == StageState.Intro) { SetState(StageState.Playing); Sfx.Play(SfxId.Checkpoint, 0.5f); }
            else if (State == StageState.Complete && Time.unscaledTime - completeTime > 1.5f) Continue();
        }

        public void TogglePause()
        {
            if (NetSession.IsClient) { NetSession.SendAction(NetAction.Pause); return; }
            if (State == StageState.Playing) { PauseSelection = 0; SetState(StageState.Paused); }
            else if (State == StageState.Paused) Resume();
        }

        static bool AnyConfirm()
        {
            var k = UnityEngine.InputSystem.Key.Space;
            return PlayerKeys.Pressed(k) || PlayerKeys.Pressed(UnityEngine.InputSystem.Key.Enter)
                || PlayerKeys.Pressed(UnityEngine.InputSystem.Key.NumpadEnter) || PlayerKeys.Pressed(UnityEngine.InputSystem.Key.RightCtrl);
        }

        public static readonly string[] PauseItems = { "Resume", "Restart from checkpoint", "Restart stage", "Settings", "Main menu" };
        const int SettingsItem = 3;

        public static string PauseLabel(int i) => PauseItems[i];

        /// <summary>The camera button on the gameplay screen: Follow -> Third person -> Side view -> Follow (per device).</summary>
        public static void CycleCamera()
        {
            ControlSettings.View = (CameraView)(((int)ControlSettings.View + 1) % 3);
            Sfx.Play(SfxId.Tick, 0.7f, 1.2f);
        }

        public void ChoosePauseItem(int i)
        {
            if (i == SettingsItem) { GameUI.SettingsOpen = true; return; } // per device, never sent to the host
            if (NetSession.IsClient) { NetSession.SendAction(NetAction.PauseItem, i); return; }
            switch (i)
            {
                case 0: Resume(); break;
                case 1: ResetToCheckpoint(); break;
                case 2: RestartStage(); break;
                default: QuitToMenu(); break;
            }
        }

        public void Resume() => SetState(StageState.Playing);

        /// <summary>Each player back to their own checkpoint; puzzles from the earlier one's section on return to their start state.</summary>
        public void ResetToCheckpoint()
        {
            var earliest = Checkpoint.Earliest; // each player goes back to their own checkpoint
            int section = earliest != null ? earliest.section : 0;
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb is IResettable r && r.Section >= section && !(mb is EngineCarrier)) r.ResetState();
            if (EngineCarrier.Instance != null) EngineCarrier.Instance.ResetState();
            if (players != null) foreach (var p in players) if (p != null) p.Respawn();
            CoopCamera.SnapAll();
            GameUI.StopCountdown();
            GameUI.ShowMessage("Back to the checkpoint — section reset", 2f);
            if (State == StageState.Paused) Resume();
        }

        public void CompleteStage()
        {
            if (State == StageState.Complete) return;
            SetState(StageState.Complete);
            completeTime = Time.unscaledTime;
            GameFlow.StageCompleted(stageNumber);
            Sfx.StopAllLoops();
            Sfx.Play(SfxId.Success, 1f);
        }

        public void Continue()
        {
            if (isFinalStage || !GameFlow.HasNext(stageNumber)) GameFlow.FinishGame();
            else GameFlow.LoadStage(stageNumber + 1);
        }

        public void RestartStage() => GameFlow.ReloadStage();
        public void QuitToMenu() => GameFlow.LoadMenu();
    }
}