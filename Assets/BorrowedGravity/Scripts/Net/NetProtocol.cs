namespace BorrowGravity
{
    public enum NetMode { Offline, Host, Client }

    public enum NetStatus { Idle, Waiting, Searching, Connected, Lost }

    /// <summary>Things the joining phone asks the host to do (the host runs the game).</summary>
    public enum NetAction : byte { Confirm, Skip, Pause, PauseItem, ResetCheckpoint }

    enum NetMsg : byte
    {
        Query = 1, Answer = 2, Hello = 3, Welcome = 4, Ping = 5, Bye = 6,
        Input = 10, Action = 11,
        Scene = 20, World = 21, Hud = 22, Ui = 23, Sfx = 24, Spawn = 25, Despawn = 26, Stage = 27
    }

    enum UiEvt : byte { Warning, Checkpoint, Message, Hint, Toast, Objective, Section, CountdownStart, CountdownStop, Shake }

    enum SfxEvt : byte { Play, Loop, StopLoops }

    /// <summary>HUD values the host streams to the joining phone (whose own copies of the powers are switched off).</summary>
    public static class NetHud
    {
        public static string bTarget = "", bActive = "";
        public static float bActiveFraction, bCooldown;
        public static bool bCoolingDown;
        public static bool hasEngine;
        public static float engineStability01 = 1f, engineTilt, engineSignedTilt, engineTiltLimit = 30f;
        public static float countdownLeft = -1f;
        public static float resetHold;
        public static int partsMask;

        public static void Clear()
        {
            bTarget = bActive = "";
            bActiveFraction = bCooldown = 0f;
            bCoolingDown = hasEngine = false;
            engineStability01 = 1f;
            engineTilt = engineSignedTilt = 0f;
            countdownLeft = -1f;
            resetHold = 0f;
            partsMask = 0;
        }
    }
}
