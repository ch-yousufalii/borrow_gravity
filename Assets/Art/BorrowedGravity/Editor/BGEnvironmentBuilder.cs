using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BorrowGravity.ArtKit
{
    /// <summary>
    /// Builds the three stage environments from Layouts/*.json (written by ArtTools/gen_layouts.py):
    /// one scene per stage (Assets/Scenes/Env_StageN.unity) with sky, sun, fog and bloom, plus an
    /// Env_StageN prefab you can drop into a gameplay scene.
    /// </summary>
    public static class BGEnvironmentBuilder
    {
        const string Root = BGArtKitSetup.Root;
        const string EnvPrefabDir = Root + "/Prefabs/Environments";

        [Serializable] class Item { public string prefab; public Vector3 pos, rot, scl; }
        [Serializable] class Section { public string name; public Item[] items; }
        [Serializable] class Layout { public string stage, title, sky; public Section[] sections; }

        static readonly HashSet<string> Dynamic = new HashSet<string>
        {
            "Rock_Debris_S", "Rock_Debris_M", "Crate_1m", "Crate_Heavy_2m", "Counterweight", "Power_Cell",
            "Shield_Panel", "Engine_Core", "Engine_Carrier", "Debris_Plate_A", "Debris_Plate_B", "Docking_Plug",
            "Cargo_Lift", "Airlock_Door_L", "Airlock_Door_R", "Cable_Hanging", "Ring_Walkway", "Vault_LockBar",
        };

        static readonly Dictionary<string, Color> Fog = new Dictionary<string, Color>
        {
            { "Stage1", new Color(0.42f, 0.14f, 0.31f) },
            { "Stage2", new Color(0.25f, 0.10f, 0.33f) },
            { "Stage3", new Color(0.36f, 0.10f, 0.15f) },
        };

        [MenuItem("Borrowed Gravity/Art Kit/3. Build Stage Environments")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;

            if (!Directory.Exists(Root + "/Prefabs/Shared")) BGArtKitSetup.BuildAll();
            var prefabs = new Dictionary<string, GameObject>();
            foreach (var p in Directory.GetFiles(Root + "/Prefabs", "*.prefab", SearchOption.AllDirectories))
            {
                var path = p.Replace('\\', '/');
                if (path.StartsWith(EnvPrefabDir)) continue;
                prefabs[Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            Directory.CreateDirectory(EnvPrefabDir);
            var volume = BuildVolumeProfile();

            int built = 0;
            foreach (var file in Directory.GetFiles(Root + "/Layouts", "*.json"))
            {
                var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(file));
                BuildStage(layout, prefabs, volume);
                built++;
            }
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            Debug.Log($"[Borrowed Gravity] Built {built} stage environments: Assets/Scenes/Env_Stage*.unity and {EnvPrefabDir}.");
        }

        static void BuildStage(Layout layout, Dictionary<string, GameObject> prefabs, VolumeProfile volume)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Env_" + layout.stage);
            int missing = 0;
            foreach (var section in layout.sections)
            {
                var group = new GameObject(section.name).transform;
                group.SetParent(root.transform, false);
                bool scenery = section.name == "Backdrop" || section.name.Contains("Exterior");
                foreach (var it in section.items)
                {
                    if (!prefabs.TryGetValue(it.prefab, out var prefab) || prefab == null) { missing++; continue; }
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                    go.transform.localPosition = it.pos;
                    go.transform.localRotation = Quaternion.Euler(it.rot);
                    go.transform.localScale = it.scl;
                    if (scenery)
                    {
                        foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
                        foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    if (!Dynamic.Contains(it.prefab)) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                }
            }
            if (missing > 0) Debug.LogWarning($"[Borrowed Gravity] {layout.stage}: {missing} pieces skipped (prefab not found). Run step 1 first.");

            // sky, ambient, fog
            var sky = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/Sky_{layout.sky}.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.15f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0035f;
            RenderSettings.fogColor = Fog.TryGetValue(layout.stage, out var fc) ? fc : new Color(0.4f, 0.15f, 0.3f);

            // warm key light from the camera side, plus a cool rim from behind
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.8f, 0.66f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, 28f, 0f);
            var rim = new GameObject("Rim Light").AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.color = new Color(0.55f, 0.6f, 1f);
            rim.intensity = 0.35f;
            rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(20f, 200f, 0f);
            RenderSettings.sun = sun;

            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = volume;

            // preview camera framed like the co-op camera at the start of the route
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 2000f;
            camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camGo.transform.position = new Vector3(14f, 12f, -26f);
            camGo.transform.LookAt(new Vector3(20f, 1f, 0f));

            string prefabPath = $"{EnvPrefabDir}/Env_{layout.stage}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/Env_{layout.stage}.unity");
        }

        static VolumeProfile BuildVolumeProfile()
        {
            string path = Root + "/Materials/BG_PostFX.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.color.Override(new Color(0.12f, 0.02f, 0.12f));
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(8f);
            color.contrast.Override(6f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
