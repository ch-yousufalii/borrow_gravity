using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Volume levels chosen in Settings, saved on this device: master, sound effects and music (0..1 each).
    /// </summary>
    public static class SoundSettings
    {
        public const float Step = 0.1f;
        const string KeyMaster = "bg_vol_master", KeyEffects = "bg_vol_effects", KeyMusic = "bg_vol_music";

        static bool loaded;
        static float master = 0.8f, effects = 1f, music = 0.6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => loaded = false;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            master = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMaster, 0.8f));
            effects = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyEffects, 1f));
            music = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusic, 0.6f));
        }

        public static float Master { get { Load(); return master; } set { Load(); master = Snap(value); Save(); } }
        public static float Effects { get { Load(); return effects; } set { Load(); effects = Snap(value); Save(); } }
        public static float Music { get { Load(); return music; } set { Load(); music = Snap(value); Save(); } }

        static float Snap(float v) => Mathf.Clamp01(Mathf.Round(v * 10f) / 10f);

        static void Save()
        {
            PlayerPrefs.SetFloat(KeyMaster, master);
            PlayerPrefs.SetFloat(KeyEffects, effects);
            PlayerPrefs.SetFloat(KeyMusic, music);
            PlayerPrefs.Save();
            Sfx.ApplyVolumes();
        }

        public static void ResetToDefaults()
        {
            Load();
            master = 0.8f;
            effects = 1f;
            music = 0.6f;
            Save();
        }
    }
}
