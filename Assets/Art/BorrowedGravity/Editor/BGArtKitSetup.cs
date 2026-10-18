using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowGravity.ArtKit
{
    /// <summary>
    /// Import rules for the Borrowed Gravity art kit (Assets/Art/BorrowedGravity), plus a menu that builds
    /// the palette material, the three skybox materials and a prefab per model.
    /// </summary>
    public class BGArtKitImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(BGArtKitSetup.Root + "/Models")) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f;
            mi.importNormals = ModelImporterNormals.Import; // faceted normals come from the file
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.isReadable = true; // MeshColliders on the prefabs
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(BGArtKitSetup.Root)) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.Contains("/Textures/"))
            {
                // palette swatches: keep colours crisp
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.wrapMode = TextureWrapMode.Clamp;
            }
            else if (assetPath.Contains("/Skyboxes/"))
            {
                ti.maxTextureSize = 4096;
                ti.mipmapEnabled = false; // avoids the seam line on panoramic skies
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            else if (assetPath.Contains("/UI/"))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                string name = Path.GetFileNameWithoutExtension(assetPath);
                if (name.StartsWith("Banner_")) settings.spriteBorder = new Vector4(40, 40, 40, 40); // 9-slice
                if (name == "Tilt_Needle")
                {
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(0.5f, 0.09f); // hub of the needle
                }
                ti.SetTextureSettings(settings);
            }
        }
    }

    public static class BGArtKitSetup
    {
        public const string Root = "Assets/Art/BorrowedGravity";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";

        // Physics-driven props get a convex collider so a Rigidbody can be added.
        static readonly HashSet<string> Dynamic = new HashSet<string>
        {
            "Rock_Debris_S", "Rock_Debris_M", "Floating_Block", "Crate_1m", "Crate_Heavy_2m", "Counterweight",
            "Power_Cell", "Shield_Panel", "Engine_Core", "Debris_Plate_A", "Debris_Plate_B", "Docking_Plug",
        };

        // Background scenery: no collider.
        static readonly HashSet<string> Scenery = new HashSet<string> { "Mesa_A", "Mesa_B" };

        static readonly string[] Stages =
            { "Stage1_ShatteredOrbit", "Stage2_UpsideDownStation", "Stage3_CollapsingReturn" };

        [MenuItem("Borrowed Gravity/Art Kit/1. Build Materials + Prefabs")]
        public static void BuildAll()
        {
            AssetDatabase.Refresh();
            // re-import so the rules above apply even if the files were imported before this script existed
            foreach (var p in Directory.GetFiles(Root, "*.*", SearchOption.AllDirectories))
                if (p.EndsWith(".obj") || p.EndsWith(".png"))
                    AssetDatabase.ImportAsset(p.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            var palette = BuildPaletteMaterial();
            for (int i = 0; i < Stages.Length; i++) BuildSkyMaterial(Stages[i]);
            int count = BuildPrefabs(palette);
            BuildHudIcons();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Borrowed Gravity] Art kit ready: {count} prefabs in {PrefabDir}, materials in {MatDir}.");
        }

        [MenuItem("Borrowed Gravity/Art Kit/2. Apply Skybox: Stage 1")]
        static void Sky1() => ApplySky(0);

        [MenuItem("Borrowed Gravity/Art Kit/2. Apply Skybox: Stage 2")]
        static void Sky2() => ApplySky(1);

        [MenuItem("Borrowed Gravity/Art Kit/2. Apply Skybox: Stage 3")]
        static void Sky3() => ApplySky(2);

        static Material BuildPaletteMaterial()
        {
            string path = MatDir + "/BG_Palette.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/BG_Palette_Albedo.png");
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/BG_Palette_Emission.png");
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetTexture("_EmissionMap", emission);
            mat.SetColor("_EmissionColor", Color.white * 2.2f); // HDR so bloom picks up the glow strips
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material BuildSkyMaterial(string stage)
        {
            string path = $"{MatDir}/Sky_{stage}.mat";
            var shader = Shader.Find("Skybox/Panoramic");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Skyboxes/Sky_{stage}.png"));
            mat.SetFloat("_Mapping", 1f);   // latitude-longitude layout
            mat.SetFloat("_ImageType", 0f); // 360 degrees
            mat.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void ApplySky(int stage)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Sky_{Stages[stage]}.mat");
            if (mat == null) mat = BuildSkyMaterial(Stages[stage]);
            RenderSettings.skybox = mat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[Borrowed Gravity] Skybox set to {Stages[stage]}. Tip: rotate it via the material's Rotation slider.");
        }

        /// <summary>Creates/updates HudIcons.asset (field name == PNG name) and hands it to every GameUI in open scenes.</summary>
        static void BuildHudIcons()
        {
            string path = Root + "/HudIcons.asset";
            var icons = AssetDatabase.LoadAssetAtPath<BorrowGravity.HudIcons>(path);
            if (icons == null)
            {
                icons = ScriptableObject.CreateInstance<BorrowGravity.HudIcons>();
                AssetDatabase.CreateAsset(icons, path);
            }
            foreach (var f in typeof(BorrowGravity.HudIcons).GetFields())
            {
                if (f.FieldType != typeof(Texture2D)) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/UI/{f.Name}.png");
                if (tex == null) Debug.LogWarning($"[Borrowed Gravity] Missing HUD icon UI/{f.Name}.png");
                f.SetValue(icons, tex);
            }
            EditorUtility.SetDirty(icons);
            foreach (var ui in Object.FindObjectsByType<BorrowGravity.GameUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                ui.icons = icons;
                EditorUtility.SetDirty(ui);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
            }
        }

        static int BuildPrefabs(Material palette)
        {
            int count = 0;
            var objs = Directory.GetFiles(Root + "/Models", "*.obj", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/')).OrderBy(p => p);
            foreach (var objPath in objs)
            {
                var mesh = AssetDatabase.LoadAllAssetsAtPath(objPath).OfType<Mesh>().FirstOrDefault();
                if (mesh == null) continue;
                string name = Path.GetFileNameWithoutExtension(objPath);
                string category = Path.GetFileName(Path.GetDirectoryName(objPath));
                string dir = $"{PrefabDir}/{category}";
                Directory.CreateDirectory(dir);

                var go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = palette;
                if (Scenery.Contains(name))
                {
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                }
                else
                {
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mesh;
                    mc.convex = Dynamic.Contains(name);
                }
                PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{name}.prefab");
                Object.DestroyImmediate(go);
                count++;
            }
            return count;
        }
    }
}
