using UnityEditor;
using UnityEngine;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Android texture import settings for phones: the 4K skyboxes ship at 2048 in ASTC, the HUD sprites in ASTC 4x4
    /// (they were uncompressed RGBA). PC keeps its own settings. The palette textures stay uncompressed on purpose:
    /// the low-poly art reads exact colours from them.
    /// </summary>
    public static class MobileTextureSettings
    {
        [MenuItem("BorrowGravity/Optimize Textures For Phones", priority = 70)]
        public static void Apply()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art", "Assets/icon" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti) || path.Contains("/Textures/BG_Palette")) continue;
                bool sky = path.Contains("/Skyboxes/");
                var android = ti.GetPlatformTextureSettings("Android");
                android.overridden = true;
                android.maxTextureSize = sky ? 2048 : path.Contains("/icon/") ? 512 : 256;
                android.format = sky ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;
                android.compressionQuality = 50;
                ti.SetPlatformTextureSettings(android);
                ti.SaveAndReimport();
                changed++;
            }
            Debug.Log("[MobileTextureSettings] Android settings applied to " + changed + " textures");
        }
    }
}
