using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Shared helpers for Levels 6 and 7: generated skies, simple box geometry, and wiring a level into the Game scene.
    /// The Game scene holds hand-made levels (4, 5, 6), so these only ever add to it or replace the named level.
    /// </summary>
    public static class LevelArt
    {
        public struct SkyStyle
        {
            public Color deep, mid, glow, sun;
            public Vector3 sunDirection;
            public float sunSize, starAmount;
            public int seed;
        }

        static float Noise3(Vector3 d, float f, float offset)
        {
            float a = Mathf.PerlinNoise(d.x * f + offset, d.y * f + offset * 0.7f);
            float b = Mathf.PerlinNoise(d.y * f + offset * 1.3f, d.z * f + offset);
            float c = Mathf.PerlinNoise(d.z * f + offset * 0.4f, d.x * f + offset * 1.9f);
            return (a + b + c) / 3f;
        }

        /// <summary>Paints a latitude-longitude sky once and returns its panoramic material (named as BeginStage expects).</summary>
        public static Material Sky(string name, SkyStyle s)
        {
            string png = $"{BuilderKit.ArtRoot}/Skyboxes/Sky_{name}.png";
            if (!File.Exists(png))
            {
                const int W = 2048, H = 1024;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                var rng = new System.Random(s.seed);
                var sunDir = s.sunDirection.normalized;
                var pixels = new Color[W * H];
                for (int y = 0; y < H; y++)
                {
                    float lat = (y + 0.5f) / H * Mathf.PI - Mathf.PI * 0.5f;
                    for (int x = 0; x < W; x++)
                    {
                        float lon = (x + 0.5f) / W * Mathf.PI * 2f;
                        var d = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                        float n = Noise3(d, 2.2f, s.seed) * 0.55f + Noise3(d, 5f, s.seed + 9f) * 0.3f + Noise3(d, 11f, s.seed + 31f) * 0.15f;
                        float cloud = Mathf.SmoothStep(0.32f, 0.72f, n);
                        var c = Color.Lerp(s.deep, s.mid, cloud);
                        c = Color.Lerp(c, s.glow, Mathf.Pow(cloud, 4f) * 0.75f);
                        float a = Mathf.Max(0f, Vector3.Dot(d, sunDir));
                        c += s.sun * (Mathf.Pow(a, 900f / Mathf.Max(0.1f, s.sunSize)) * 2.5f + Mathf.Pow(a, 18f) * 0.45f);
                        if (rng.NextDouble() < s.starAmount * (1f - cloud)) c += Color.white * (float)rng.NextDouble();
                        c.a = 1f;
                        pixels[y * W + x] = c;
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                File.WriteAllBytes(png, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(png);
                var imp = (TextureImporter)AssetImporter.GetAtPath(png);
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            string path = $"{BuilderKit.ArtRoot}/Materials/Sky_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Panoramic"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(png));
            mat.SetFloat("_Mapping", 1f);
            mat.SetFloat("_ImageType", 0f);
            mat.SetFloat("_Exposure", 1f);
            mat.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>A plain box. Solid boxes keep their collider; scenery ones lose it and don't cast shadows.</summary>
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool solid = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = WorldSize(parent, size);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!solid)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        // sizes are given in world units, even under a scaled parent such as a floor tile
        static Vector3 WorldSize(Transform parent, Vector3 size)
        {
            var s = parent.lossyScale;
            return new Vector3(size.x / s.x, size.y / s.y, size.z / s.z);
        }

        public static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>Removes a named child, so re-running a builder doesn't pile up copies.</summary>
        public static Transform FreshChild(Transform parent, string name)
        {
            var old = parent.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ---------- the Game scene ----------

        /// <summary>Opens the Game scene (without losing edits to whatever was open) and returns its level switcher.</summary>
        public static GameStages OpenGame()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            var scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Single);
            return scene.GetRootGameObjects().Select(g => g.GetComponent<GameStages>()).FirstOrDefault(g => g != null);
        }

        /// <summary>Puts a level in its slot and keeps the ending in the last slot, then makes sure Level 5 hands over to Level 6.</summary>
        public static void Wire(GameStages stages, int index, StageRoot root)
        {
            var old = stages.roots ?? new StageRoot[0];
            var roots = new StageRoot[GameStages.Outro + 1];
            StageRoot outro = old.FirstOrDefault(r => r != null && r.name == "Level_Outro");
            for (int i = 0; i < Mathf.Min(old.Length, roots.Length); i++)
                if (old[i] != null && old[i] != outro) roots[i] = old[i];
            if (outro != null)
            {
                outro.index = GameStages.Outro;
                roots[GameStages.Outro] = outro;
                EditorUtility.SetDirty(outro);
            }
            root.index = index;
            roots[index] = root;
            stages.roots = roots;
            root.gameObject.SetActive(false);
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(stages);

            if (roots[5] != null)
                foreach (var sm in roots[5].GetComponentsInChildren<StageManager>(true))
                    if (sm.isFinalStage) { sm.isFinalStage = false; EditorUtility.SetDirty(sm); }
        }

        const string PrefabDir = "Assets/feeza prelevels prefabs";

        /// <summary>Saves a level as a prefab next to the other level prefabs; the Game scene keeps an instance of it.</summary>
        public static void SaveAsPrefab(GameObject root) =>
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, $"{PrefabDir}/{root.name}.prefab", InteractionMode.AutomatedAction);

        /// <summary>A prefab instance can't lose children, so a re-run works on an unpacked copy and saves the prefab again.</summary>
        public static void Unpack(GameObject root)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        }

        static void RemovePlayers34(Transform root)
        {
            foreach (var pc in root.GetComponentsInChildren<PlayerController>(true).Where(p => p.playerIndex >= GameFlow.MaxPlayers).ToArray())
            {
                var link = pc.GetComponent<PlayerCameraLink>();
                if (link != null && link.view != null) Object.DestroyImmediate(link.view.gameObject);
                Object.DestroyImmediate(pc.gameObject);
            }
            foreach (var part in root.GetComponentsInChildren<EnginePart>(true).Where(p => p.owner >= GameFlow.MaxPlayers).ToArray())
                Object.DestroyImmediate(part.gameObject);
            foreach (var name in new[] { "Camera_C", "Camera_D" })
            {
                var cam = root.Find(name);
                if (cam != null) Object.DestroyImmediate(cam.gameObject);
            }
        }

        /// <summary>The game is two players now, so players 3 and 4 and their cameras go from every level and level prefab.</summary>
        [MenuItem("BorrowGravity/Remove Players 3 and 4", priority = 64)]
        public static void RemoveExtraPlayers()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (contents.GetComponentsInChildren<PlayerController>(true).Length > 0 || contents.transform.Find("Camera_C") != null)
                    {
                        RemovePlayers34(contents.transform);
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            var stages = OpenGame();
            if (stages == null) return;
            foreach (var root in stages.roots)
                if (root != null && !PrefabUtility.IsPartOfPrefabInstance(root.gameObject)) RemovePlayers34(root.transform);
            EditorSceneManager.MarkSceneDirty(stages.gameObject.scene);
            EditorSceneManager.SaveScene(stages.gameObject.scene);
        }

        /// <summary>Moves a level built in its own scene into the Game scene, replacing an older copy of that level only.</summary>
        public static void InsertScene(int index, string rootName, string path)
        {
            var stages = OpenGame();
            if (stages == null) return;
            var game = stages.gameObject.scene;
            foreach (var g in game.GetRootGameObjects())
                if (g.name == rootName) Object.DestroyImmediate(g);

            var src = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(src);
            var rootGo = new GameObject(rootName);
            SceneManager.MoveGameObjectToScene(rootGo, src);
            var root = rootGo.AddComponent<StageRoot>();
            root.skybox = RenderSettings.skybox;
            root.ambientMode = RenderSettings.ambientMode;
            root.ambientIntensity = RenderSettings.ambientIntensity;
            root.ambientLight = RenderSettings.ambientLight;
            root.fog = RenderSettings.fog;
            root.fogMode = RenderSettings.fogMode;
            root.fogDensity = RenderSettings.fogDensity;
            root.fogColor = RenderSettings.fogColor;
            root.sun = RenderSettings.sun;
            foreach (var go in src.GetRootGameObjects().Where(g => g != rootGo))
                go.transform.SetParent(rootGo.transform, true);
            SceneManager.MoveGameObjectToScene(rootGo, game);
            EditorSceneManager.CloseScene(src, true);
            SceneManager.SetActiveScene(game);

            Wire(stages, index, root);
            SaveAsPrefab(rootGo);
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
        }
    }
}
