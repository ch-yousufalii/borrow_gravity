using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Control settings, saved on this device: on-screen joystick or arrow buttons, and the camera view. Button / joystick
    /// size and sensitivities are fixed at 1 (one size that suits every screen).
    /// </summary>
    public enum MoveStyle { Joystick, Arrows }

    /// <summary>Follow (close behind the player, default), third person (wider chase camera) or the side-on view that shows the level from the side.</summary>
    public enum CameraView { Follow, ThirdPerson, Side }

    public static class ControlSettings
    {
        public const float MinSize = 0.6f, MaxSize = 1.6f, SizeStep = 0.1f;
        public const float MinSens = 0.5f, MaxSens = 2.5f, SensStep = 0.1f;
        public const float DeadZone = 0.1f;

        // KeyView is a new key (v2) so old saved camera choices don't override the new Follow default
        const string KeyButton = "bg_btn_size", KeyStick = "bg_stick_size", KeyStickSens = "bg_stick_sens", KeyLook = "bg_look_sens", KeyStyle = "bg_move_style", KeyView = "bg_cam_view_v2";

        static bool loaded;
        static float buttonSize = 1f, stickSize = 1f, stickSensitivity = 1f, lookSensitivity = 1f;
        static MoveStyle moveStyle = MoveStyle.Joystick;
        static CameraView view = CameraView.Follow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => loaded = false;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            // one size for everyone: the old size / sensitivity choices are no longer offered, so saved ones are ignored
            buttonSize = stickSize = stickSensitivity = lookSensitivity = 1f;
            moveStyle = PlayerPrefs.GetInt(KeyStyle, 0) == 1 ? MoveStyle.Arrows : MoveStyle.Joystick;
            int v = Mathf.Clamp(PlayerPrefs.GetInt(KeyView, (int)CameraView.Follow), 0, 2);
            view = (CameraView)v;
        }

        /// <summary>Camera: Follow (default), third person or side view. Chosen in the pause menu, per device.</summary>
        public static CameraView View { get { Load(); return view; } set { Load(); view = value; Save(); } }

        /// <summary>Touch movement: floating joystick (left half of the screen) or four arrow buttons.</summary>
        public static MoveStyle Movement { get { Load(); return moveStyle; } set { Load(); moveStyle = value; Save(); } }

        /// <summary>Scale of the JUMP / POWER buttons (1 = default).</summary>
        public static float ButtonSize { get { Load(); return buttonSize; } set { Load(); buttonSize = Snap(value, MinSize, MaxSize); Save(); } }
        /// <summary>Scale of the joystick (1 = default).</summary>
        public static float StickSize { get { Load(); return stickSize; } set { Load(); stickSize = Snap(value, MinSize, MaxSize); Save(); } }
        /// <summary>Higher = less drag needed for full speed (1 = full speed at the joystick's edge).</summary>
        public static float StickSensitivity { get { Load(); return stickSensitivity; } set { Load(); stickSensitivity = Snap(value, MinSens, MaxSens); Save(); } }
        /// <summary>Multiplier on camera turning from finger drags and the mouse.</summary>
        public static float LookSensitivity { get { Load(); return lookSensitivity; } set { Load(); lookSensitivity = Snap(value, MinSens, MaxSens); Save(); } }

        static float Snap(float v, float min, float max) => Mathf.Clamp(Mathf.Round(v * 10f) / 10f, min, max);

        static void Save()
        {
            PlayerPrefs.SetFloat(KeyButton, buttonSize);
            PlayerPrefs.SetFloat(KeyStick, stickSize);
            PlayerPrefs.SetFloat(KeyStickSens, stickSensitivity);
            PlayerPrefs.SetFloat(KeyLook, lookSensitivity);
            PlayerPrefs.SetInt(KeyStyle, moveStyle == MoveStyle.Arrows ? 1 : 0);
            PlayerPrefs.SetInt(KeyView, (int)view);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            Load();
            buttonSize = stickSize = stickSensitivity = lookSensitivity = 1f;
            moveStyle = MoveStyle.Joystick;
            Save();
        }

        /// <summary>Raw joystick deflection (0..1+, unclamped) → movement amount with dead zone and sensitivity.</summary>
        public static Vector2 ApplyStick(Vector2 raw)
        {
            float m = raw.magnitude;
            if (m <= DeadZone) return Vector2.zero;
            float k = Mathf.Clamp01((m - DeadZone) / (1f - DeadZone) * StickSensitivity);
            return raw / m * k;
        }
    }
}