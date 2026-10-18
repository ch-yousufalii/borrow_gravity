using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using static BorrowGravity.EditorTools.BuilderKit;

namespace BorrowGravity.EditorTools
{
    /// <summary>Menu entries that (re)build every scene of the game from the art kit layouts.</summary>
    public static class GameBuilder
    {
        public const string MenuPath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("BorrowGravity/Build All Scenes", priority = 0)]
        public static void BuildAll()
        {
            if (!CanBuild()) return;
            EnsureArtKit();
            BuildMenuScene();
            IntroBuilder.Build();
            Stage1Builder.Build();
            Stage2Builder.Build();
            Stage3Builder.Build();
            OutroBuilder.Build();
            AddScenes();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MenuPath);
            Debug.Log("[BorrowGravity] Built MainMenu, Intro, Stage1, Stage2, Stage3, Outro and updated Build Settings.");
        }

        [MenuItem("BorrowGravity/Build Main Menu", priority = 20)]
        static void MenuOnly() { if (CanBuild()) { BuildMenuScene(); AddScenes(); } }

        [MenuItem("BorrowGravity/Build Intro Cutscene", priority = 20)]
        static void IntroOnly() { if (CanBuild()) { IntroBuilder.Build(); AddScenes(); } }

        [MenuItem("BorrowGravity/Build Ending Cutscene", priority = 20)]
        static void OutroOnly() { if (CanBuild()) { OutroBuilder.Build(); AddScenes(); } }

        [MenuItem("BorrowGravity/Build Stage 1", priority = 21)]
        static void S1() { if (CanBuild()) { Stage1Builder.Build(); AddScenes(); } }

        [MenuItem("BorrowGravity/Build Stage 2", priority = 22)]
        static void S2() { if (CanBuild()) { Stage2Builder.Build(); AddScenes(); } }

        [MenuItem("BorrowGravity/Build Stage 3", priority = 23)]
        static void S3() { if (CanBuild()) { Stage3Builder.Build(); AddScenes(); } }

        static void AddScenes() => SetBuildScenes(MenuPath, IntroBuilder.ScenePath, Stage1Builder.ScenePath, Stage2Builder.ScenePath, Stage3Builder.ScenePath, OutroBuilder.ScenePath);

        static bool CanBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[BorrowGravity] Exit Play mode before building scenes.");
                return false;
            }
            var active = SceneManager.GetActiveScene();
            if (active.isDirty)
            {
                if (string.IsNullOrEmpty(active.path))
                {
                    Debug.LogError("[BorrowGravity] The open scene is unsaved (Untitled). Save it first so nothing is lost.");
                    return false;
                }
                EditorSceneManager.SaveScene(active);
            }
            return true;
        }

        public static void BuildMenuScene()
        {
            EnsureArtKit();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/Materials/Sky_Stage1_ShatteredOrbit.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.004f;
            RenderSettings.fogColor = new Color(0.42f, 0.14f, 0.31f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.8f, 0.66f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(35f, 40f, 0f);
            RenderSettings.sun = sun;
            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ArtRoot + "/Materials/BG_PostFX.asset");

            var set = new GameObject("TitleSet").transform;
            var center = new GameObject("OrbitTarget").transform;
            center.position = new Vector3(0f, 2f, 0f);
            Spawn("Spaceship", new Vector3(0f, 3f, 0f), new Vector3(0f, 70f, -8f), set);
            Spawn("Docking_Platform", new Vector3(0f, -0.5f, 0f), Vector3.zero, set);
            Spawn("Island_L", new Vector3(-3f, -1.2f, 1f), new Vector3(0, 40, 0), set);
            Spawn("Island_M", new Vector3(14f, -4f, 8f), new Vector3(0, 120, 0), set);
            Spawn("Island_S", new Vector3(-12f, 4f, -6f), new Vector3(0, 30, 0), set);
            Spawn("ServiceTower_Segment", new Vector3(-16f, -6f, 14f), Vector3.zero, set);
            Spawn("ServiceTower_Top", new Vector3(-16f, 0.15f, 14f), Vector3.zero, set);
            Spawn("Station_Chunk_A", new Vector3(18f, 9f, -4f), new Vector3(20, 30, 10), set);
            Spawn("Station_Chunk_B", new Vector3(-20f, 10f, 4f), new Vector3(-10, 60, 20), set);
            Spawn("Engine_Carrier", new Vector3(3.5f, -0.5f, -2.2f), new Vector3(0, -20, 0), set);
            Spawn("Engine_Core", new Vector3(3.5f, 0.25f, -2.2f), new Vector3(0, -20, 0), set);
            Spawn("Ring_Walkway", new Vector3(30f, 2f, 30f), new Vector3(0, 30, 15), set);
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                Spawn(i % 2 == 0 ? "Rock_Debris_M" : "Debris_Plate_A", new Vector3(Mathf.Cos(a) * 22f, 4f + (i % 3) * 3f, Mathf.Sin(a) * 22f),
                    new Vector3(i * 37, i * 53, i * 11), set);
            }
            foreach (var c in set.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.farClipPlane = 2000f;
            camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camGo.transform.position = new Vector3(0f, 12f, -34f);
            camGo.transform.LookAt(center);

            var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
            menu.icons = AssetDatabase.LoadAssetAtPath<HudIcons>(ArtRoot + "/HudIcons.asset");
            menu.orbitTarget = center;
            EditorSceneManager.SaveScene(scene, MenuPath);
        }
    }
}
