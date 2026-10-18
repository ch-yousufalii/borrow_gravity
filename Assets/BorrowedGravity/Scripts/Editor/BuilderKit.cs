using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorrowGravity.ArtKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BorrowGravity.EditorTools
{
    /// <summary>Everything a stage scene needs while being built: the layout pieces, players and core objects.</summary>
    public class StageContext
    {
        public Scene scene;
        public Transform environment, gameplay;
        public readonly Dictionary<string, List<GameObject>> sections = new Dictionary<string, List<GameObject>>();
        /// <summary>"section|prefab|n" → piece, fixed at load time so removing pieces never shifts indices.</summary>
        public readonly Dictionary<string, GameObject> byKey = new Dictionary<string, GameObject>();
        public PlayerController playerA, playerB;
        public readonly List<PlayerController> players = new List<PlayerController>();
        public GravityPower gravityPower;
        public NullifyPower nullifyPower;
        public GameUI ui;
        public StageManager manager;
        public CoopCamera coopCamera;
        public readonly List<CoopCamera> cameras = new List<CoopCamera>();

        /// <summary>The n-th piece of a prefab in a layout section ("5.3", "Counterweight", 0).</summary>
        public GameObject Piece(string sectionPrefix, string prefab, int occurrence = 0)
        {
            var key = sections.Keys.FirstOrDefault(k => k.StartsWith(sectionPrefix));
            if (key == null) throw new Exception($"No layout section '{sectionPrefix}'");
            if (byKey.TryGetValue($"{key}|{prefab}|{occurrence}", out var go) && go != null) return go;
            throw new Exception($"Layout {sectionPrefix}: no {prefab}[{occurrence}] (missing or already removed)");
        }

        public IEnumerable<GameObject> Pieces(string sectionPrefix, string prefab)
        {
            var key = sections.Keys.FirstOrDefault(k => k.StartsWith(sectionPrefix));
            if (key == null) yield break;
            foreach (var go in sections[key])
                if (go != null && BuilderKit.PrefabName(go) == prefab) yield return go;
        }

        public Transform Group(string name)
        {
            var t = gameplay.Find(name);
            if (t != null) return t;
            t = new GameObject(name).transform;
            t.SetParent(gameplay, false);
            return t;
        }
    }

    public static class BuilderKit
    {
        public const string ArtRoot = BGArtKitSetup.Root;
        const string GameMatDir = "Assets/Materials/Game";

        [Serializable] class Item { public string prefab; public Vector3 pos, rot, scl; }
        [Serializable] class Section { public string name; public Item[] items; }
        [Serializable] class Layout { public string stage, title, sky; public Section[] sections; }

        static Dictionary<string, GameObject> prefabs;
        static readonly Dictionary<string, Color> Fog = new Dictionary<string, Color>
        {
            { "Stage1", new Color(0.42f, 0.14f, 0.31f) },
            { "Stage2", new Color(0.25f, 0.10f, 0.33f) },
            { "Stage3", new Color(0.36f, 0.10f, 0.15f) },
        };

        // ---------- art kit ----------

        public static void EnsureArtKit()
        {
            bool missing = !Directory.Exists(ArtRoot + "/Prefabs/Shared")
                || AssetDatabase.LoadAssetAtPath<HudIcons>(ArtRoot + "/HudIcons.asset") == null
                || AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/Materials/BG_Palette.mat") == null;
            if (missing) BGArtKitSetup.BuildAll();
            prefabs = new Dictionary<string, GameObject>();
            foreach (var p in Directory.GetFiles(ArtRoot + "/Prefabs", "*.prefab", SearchOption.AllDirectories))
            {
                var path = p.Replace('\\', '/');
                if (path.Contains("/Environments/")) continue;
                prefabs[Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        public static string PrefabName(GameObject go)
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (src != null) return src.name;
            int i = go.name.IndexOf('[');
            return i > 0 ? go.name.Substring(0, i) : go.name;
        }

        public static GameObject Spawn(string prefab, Vector3 pos, Vector3 euler, Transform parent, Vector3? scale = null, string name = null)
        {
            if (prefabs == null) EnsureArtKit();
            if (!prefabs.TryGetValue(prefab, out var p) || p == null) throw new Exception("Missing kit prefab " + prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            var s = scale ?? Vector3.one;
            if (Walkable.Contains(prefab)) s.z *= LaneWidth;
            go.transform.localScale = s;
            if (name != null) go.name = name;
            return go;
        }

        // ---------- scene ----------

        /// <summary>Walkways are this much wider than in the art kit layout, so there's room to move around.</summary>
        public const float LaneWidth = 1.5f;

        // things you walk on: stretched across the lane (z) by LaneWidth
        static readonly HashSet<string> Walkable = new HashSet<string>
        {
            "Bridge_Section", "Bridge_Section_Weak", "Bridge_End_Broken", "Platform_Metal_2x2", "Platform_Metal_4x4",
            "Platform_Metal_6x3", "Platform_Weak_4x4", "Station_Floor_4x4", "Docking_Platform", "Magnetic_Track_4m"
        };

        // far-away set dressing: left where the layout put it
        static bool Backdrop(string prefab) =>
            prefab.StartsWith("Island_") || prefab.StartsWith("Rock_Debris") || prefab.StartsWith("Mesa_") || prefab.StartsWith("Monolith") ||
            prefab.StartsWith("Floating_Block") || prefab.StartsWith("Station_Chunk") || prefab.StartsWith("Wreck_Hull") ||
            prefab.StartsWith("Debris_Plate") || prefab == "Spaceship";

        public static StageContext BeginStage(string stage)
        {
            EnsureArtKit();
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText($"{ArtRoot}/Layouts/{stage}.json"));
            var ctx = new StageContext { scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single) };
            ctx.environment = new GameObject("Environment").transform;
            ctx.gameplay = new GameObject("Gameplay").transform;

            foreach (var section in layout.sections)
            {
                var group = new GameObject(section.name).transform;
                group.SetParent(ctx.environment, false);
                bool scenery = section.name == "Backdrop" || section.name.Contains("Exterior");
                var list = new List<GameObject>();
                var counts = new Dictionary<string, int>();
                foreach (var it in section.items)
                {
                    if (!prefabs.TryGetValue(it.prefab, out var prefab) || prefab == null) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                    counts.TryGetValue(it.prefab, out int n);
                    counts[it.prefab] = n + 1;
                    go.name = $"{it.prefab}[{n}]";
                    ctx.byKey[$"{section.name}|{it.prefab}|{n}"] = go;
                    Vector3 pos = it.pos, scl = it.scl;
                    if (!scenery && !Backdrop(it.prefab) && Mathf.Abs(pos.z) <= 6f)
                    {
                        pos.z *= LaneWidth; // walls, rails and props move out with the wider floor
                        if (Walkable.Contains(it.prefab)) scl.z *= LaneWidth;
                    }
                    go.transform.localPosition = pos;
                    go.transform.localRotation = Quaternion.Euler(it.rot);
                    go.transform.localScale = scl;
                    if (scenery)
                    {
                        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                        foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    list.Add(go);
                }
                ctx.sections[section.name] = list;
            }
            SetupEnvironment(stage, layout.sky);
            return ctx;
        }

        static void SetupEnvironment(string stage, string sky)
        {
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>($"{ArtRoot}/Materials/Sky_{sky}.mat");
            if (skyMat != null) RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.15f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0032f;
            RenderSettings.fogColor = Fog.TryGetValue(stage, out var fc) ? fc : new Color(0.4f, 0.15f, 0.3f);

            var lights = new GameObject("Lighting").transform;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(lights);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.8f, 0.66f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, 28f, 0f);
            var rim = new GameObject("Rim Light").AddComponent<Light>();
            rim.transform.SetParent(lights);
            rim.type = LightType.Directional;
            rim.color = new Color(0.55f, 0.6f, 1f);
            rim.intensity = 0.35f;
            rim.shadows = LightShadows.None;
            rim.transform.rotation = Quaternion.Euler(20f, 200f, 0f);
            RenderSettings.sun = sun;

            var vol = new GameObject("Global Volume").AddComponent<Volume>();
            vol.transform.SetParent(lights);
            vol.isGlobal = true;
            vol.sharedProfile = PostFx();
        }

        static VolumeProfile PostFx()
        {
            string path = ArtRoot + "/Materials/BG_PostFX.asset";
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

        public static void Save(StageContext ctx, string path)
        {
            EditorSceneManager.MarkSceneDirty(ctx.scene);
            EditorSceneManager.SaveScene(ctx.scene, path);
        }

        /// <summary>
        /// Puts our scenes first (enabled, in this order — the main menu is what a build opens), drops the scenes in
        /// <paramref name="remove"/>, and keeps any other scenes already listed after them.
        /// </summary>
        public static void SetBuildScenes(string[] paths, params string[] remove)
        {
            var list = paths.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            list.AddRange(EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path) && !remove.Contains(s.path)));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ---------- materials ----------

        public static Material Lit(string name, Color color, Color? emission = null, float smoothness = 0.4f)
        {
            Directory.CreateDirectory(GameMatDir);
            string path = $"{GameMatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            m.SetColor("_EmissionColor", emission ?? Color.black);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Transparent(string name, Color color)
        {
            Directory.CreateDirectory(GameMatDir);
            string path = $"{GameMatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- physics helpers ----------

        /// <summary>Replaces mesh colliders with a box fitted to the mesh, adds a Rigidbody.</summary>
        public static Rigidbody MakeDynamic(GameObject go, float mass, RigidbodyConstraints constraints = RigidbodyConstraints.FreezeRotation, bool kinematic = false)
        {
            BoxFit(go);
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.isKinematic = kinematic;
            rb.useGravity = false;
            rb.constraints = constraints;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = kinematic ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.ContinuousDynamic;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return rb;
        }

        public static BoxCollider BoxFit(GameObject go)
        {
            foreach (var mc in go.GetComponents<MeshCollider>()) Object.DestroyImmediate(mc);
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                box.center = mf.sharedMesh.bounds.center;
                box.size = mf.sharedMesh.bounds.size;
            }
            return box;
        }

        public static GravityAffected Gravity(GameObject go, ZeroGravityResponse zeroG, float scale = 2f, float floatSpeed = 1.5f, float ceiling = 100000f)
        {
            var g = go.GetComponent<GravityAffected>() ?? go.AddComponent<GravityAffected>();
            g.gravityScale = scale;
            g.zeroGResponse = zeroG;
            g.floatSpeed = floatSpeed;
            g.floatCeilingY = ceiling;
            return g;
        }

        public static Nullifiable Nullify(GameObject go, string displayName, float hold = 8f, bool requiresWeightless = false, float steerSpeed = 3f)
        {
            var n = go.GetComponent<Nullifiable>() ?? go.AddComponent<Nullifiable>();
            n.displayName = displayName;
            n.holdDuration = hold;
            n.requiresWeightless = requiresWeightless;
            n.steerSpeed = steerSpeed;
            return n;
        }

        public static Respawnable Respawn(GameObject go, int section, float killY = -30f)
        {
            var r = go.GetComponent<Respawnable>() ?? go.AddComponent<Respawnable>();
            r.section = section;
            r.killY = killY;
            return r;
        }

        // ---------- gameplay objects ----------

        public static GravityZone Zone(StageContext ctx, string name, int section, Vector3 center, Vector3 size, GravityMode mode,
            GravityMode[] cycle, bool controllable = true, bool players = true, bool objects = true, IEnumerable<Vector3> arrows = null,
            Transform[] existingArrows = null)
        {
            var go = new GameObject("Zone_" + name);
            go.transform.SetParent(ctx.Group("Zones"), false);
            go.transform.position = center;
            var z = go.AddComponent<GravityZone>();
            z.zoneName = name;
            z.section = section;
            z.size = size;
            SetMode(z, mode);
            z.cycle = cycle ?? new[] { GravityMode.Down, GravityMode.Zero };
            z.controllable = controllable;
            z.affectsPlayers = players;
            z.affectsObjects = objects;
            var arrowList = new List<Transform>();
            if (existingArrows != null) arrowList.AddRange(existingArrows.Where(a => a != null));
            if (arrows != null)
                foreach (var p in arrows)
                {
                    var a = Spawn("Gravity_Arrow", p, Vector3.zero, go.transform, null, "Arrow");
                    foreach (var c in a.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    arrowList.Add(a.transform);
                }
            foreach (var a in arrowList)
                foreach (var c in a.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            z.arrows = arrowList.ToArray();
            z.indicatorRenderers = arrowList.SelectMany(a => a.GetComponentsInChildren<Renderer>()).ToArray();
            return z;
        }

        public static void SetMode(GravityZone z, GravityMode m)
        {
            var so = new SerializedObject(z);
            so.FindProperty("mode").enumValueIndex = (int)m;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Checkpoint Checkpoint(StageContext ctx, GameObject beacon, Vector3 at, int section, string label, Vector3 triggerSize,
            bool isStart = false, Transform carrierSpawn = null, float spawnZ = 0f, float spawnX = float.NaN, float spawnY = float.NaN)
        {
            if (beacon == null) beacon = Spawn("Checkpoint_Beacon", at, Vector3.zero, ctx.Group("Checkpoints"));
            beacon.transform.position = at;
            beacon.name = "Checkpoint_" + label.Replace(' ', '_');
            var go = new GameObject("CheckpointTrigger_" + label.Replace(' ', '_'));
            go.transform.SetParent(ctx.Group("Checkpoints"), false);
            go.transform.position = at;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, triggerSize.y * 0.5f, 0f);
            box.size = triggerSize;
            var cp = go.AddComponent<Checkpoint>();
            cp.section = section;
            cp.label = label;
            cp.isStart = isStart;
            cp.carrierSpawn = carrierSpawn;
            // spawn on the lane centre line (or a given spot), a little apart
            float sx = float.IsNaN(spawnX) ? at.x : spawnX;
            float sy = float.IsNaN(spawnY) ? at.y : spawnY;
            cp.spawnOffsets = new[]
            {
                new Vector3(sx - 0.9f - at.x, sy + 1.05f - at.y, spawnZ - at.z),
                new Vector3(sx + 0.9f - at.x, sy + 1.05f - at.y, spawnZ - at.z),
                new Vector3(sx - 2.4f - at.x, sy + 1.05f - at.y, spawnZ - at.z),
                new Vector3(sx + 2.4f - at.x, sy + 1.05f - at.y, spawnZ - at.z)
            };
            var light = new GameObject("BeaconLight").AddComponent<Light>();
            light.transform.SetParent(beacon.transform, false);
            light.transform.localPosition = new Vector3(0f, 3.6f, 0f);
            light.type = LightType.Point;
            light.range = 8f;
            cp.beaconLight = light;
            return cp;
        }

        public static GameObject TriggerBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var c = go.AddComponent<BoxCollider>();
            c.isTrigger = true;
            c.size = size;
            return go;
        }

        public static GameObject SolidBox(Transform parent, string name, Vector3 center, Vector3 size, Material visible = null)
        {
            GameObject go;
            if (visible != null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.GetComponent<Renderer>().sharedMaterial = visible;
                go.transform.SetParent(parent, false);
                go.transform.position = center;
                go.transform.localScale = size;
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.position = center;
                go.AddComponent<BoxCollider>().size = size;
            }
            go.name = name;
            return go;
        }

        /// <summary>Invisible wall from x0 to x1 at depth z (thickness 1), tall.</summary>
        public static void Wall(StageContext ctx, float x0, float x1, float z, float y0 = -40f, float y1 = 60f)
        {
            z *= LaneWidth;
            SolidBox(ctx.Group("InvisibleWalls"), $"Wall_{x0:0}_{x1:0}_z{z:0.0}", new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z + Mathf.Sign(z) * 0.5f), new Vector3(x1 - x0, y1 - y0, 1f));
        }

        public static void EndWall(StageContext ctx, float x, float z0 = -10f, float z1 = 10f)
        {
            z0 *= LaneWidth; z1 *= LaneWidth;
            SolidBox(ctx.Group("InvisibleWalls"), $"EndWall_{x:0}", new Vector3(x, 10f, (z0 + z1) * 0.5f), new Vector3(1f, 100f, z1 - z0));
        }

        public static Hazard HazardBox(Transform parent, string name, Vector3 center, Vector3 size, HazardEffect effect, bool armed = true)
        {
            var go = TriggerBox(parent, name, center, size);
            var h = go.AddComponent<Hazard>();
            h.effect = effect;
            h.armed = armed;
            return h;
        }

        public static HintTrigger Hint(StageContext ctx, string sectionTitle, string message, Vector3 center, Vector3 size, float duration = 7f)
        {
            var go = TriggerBox(ctx.Group("Hints"), "Hint_" + sectionTitle, center, size);
            var h = go.AddComponent<HintTrigger>();
            h.sectionTitle = sectionTitle;
            h.message = message;
            h.duration = duration;
            Board(ctx, sectionTitle, message, center + new Vector3(2.6f, 1.5f, 0f));
            return h;
        }

        /// <summary>
        /// The end of a stage: a checkpoint beacon with a light (the section's hint board points to it). Whoever
        /// steps into it finishes the stage for everyone.
        /// </summary>
        public static GoalTrigger Finish(StageContext ctx, string group, Vector3 at, string label)
        {
            var beacon = Spawn("Checkpoint_Beacon", at, Vector3.zero, ctx.Group(group), null, "Finish_" + label.Replace(' ', '_'));
            NoColliders(beacon);
            PointLight(beacon.transform, at + Vector3.up * 3f, new Color(0.55f, 1f, 0.8f), 10f, 4f);
            var trig = TriggerBox(ctx.Group(group), "FinishGoal", at + new Vector3(0f, 1.5f, 0f), new Vector3(3.5f, 3f, 4.4f));
            var goal = trig.AddComponent<GoalTrigger>();
            goal.requiredPlayers = 1;
            return goal;
        }

        /// <summary>A floating instruction board (title + text) that turns to face the camera — see <see cref="InfoBoard"/>.</summary>
        public static InfoBoard Board(StageContext ctx, string titleText, string bodyText, Vector3 at)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/Fonts/Lato-Bold.ttf");
            var root = new GameObject("Board_" + titleText);
            root.transform.SetParent(ctx.Group("Boards"), false);
            root.transform.position = at;
            var board = root.AddComponent<InfoBoard>();

            TextMesh Text(string name, string text, int size, Color color, float y)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, y, 0f);
                var tm = go.AddComponent<TextMesh>();
                tm.text = text;
                tm.font = font;
                tm.fontSize = size;
                tm.characterSize = 0.012f;
                tm.anchor = TextAnchor.UpperCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = color;
                if (font != null) go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                return tm;
            }
            board.title = Text("Title", titleText.ToUpperInvariant(), 72, Color.white, 0f);
            board.body = Text("Body", Wrap(bodyText, 40), 54, new Color(1f, 1f, 1f, 0.85f), -0.32f);

            Transform Quad(string name, Material mat)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = name;
                q.transform.SetParent(root.transform, false);
                q.GetComponent<Renderer>().sharedMaterial = mat;
                q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                return q.transform;
            }
            // the menu's look in the world: dark glass, white text, a white hairline edge
            board.panel = Quad("Panel", Transparent("BoardPanel", new Color(0.02f, 0.03f, 0.07f, 0.62f)));
            board.edge = Quad("Edge", Lit("BoardEdge", Color.white, Color.white * 1.2f));
            return board;
        }

        /// <summary>Word-wrap for 3D text (keeps existing line breaks).</summary>
        static string Wrap(string text, int width)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var para in text.Split('\n'))
            {
                int col = 0;
                foreach (var word in para.Split(' '))
                {
                    if (word.Length == 0) continue;
                    if (col > 0 && col + 1 + word.Length > width) { sb.Append('\n'); col = 0; }
                    else if (col > 0) { sb.Append(' '); col++; }
                    sb.Append(word);
                    col += word.Length;
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }

        public static Light PointLight(Transform parent, Vector3 pos, Color c, float range = 6f, float intensity = 3f)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(parent, true);
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            return l;
        }

        /// <summary>A flat translucent disc that marks where something will land.</summary>
        public static Transform ShadowMarker(Transform parent, Vector3 at, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "ImpactMarker";
            go.transform.SetParent(parent, true);
            go.transform.position = at + Vector3.up * 0.06f;
            go.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Transparent("ImpactMarker", new Color(1f, 0.1f, 0.05f, 0.45f));
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        // ---------- players, camera, UI ----------

        /// <summary>
        /// One half of the engine (A orange, B cyan). With startHeld it is in its owner's hands from the start of the
        /// stage — both players carry their half through the whole game.
        /// </summary>
        public static EnginePart EngineHalf(StageContext ctx, int owner, Vector3 at, bool startHeld)
        {
            var root = new GameObject(owner == 0 ? "EngineHalf_A" : "EngineHalf_B");
            root.transform.SetParent(ctx.Group("Engine"), false);
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 90f, 0f));
            var mesh = Spawn("Engine_Core", at, Vector3.zero, ctx.Group("Engine"));
            mesh.name = "Core";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            mesh.transform.localRotation = Quaternion.identity;
            mesh.transform.localScale = Vector3.one * 0.45f;
            NoColliders(root);
            Color c = owner == 0 ? new Color(1f, 0.62f, 0.36f) : new Color(0.36f, 0.88f, 1f);
            PointLight(root.transform, at + Vector3.up * 0.6f, c, 3.5f, 2.2f);
            var part = root.AddComponent<EnginePart>();
            part.owner = owner;
            part.startHeld = startHeld;
            part.displayName = owner == 0 ? "orange engine half" : "cyan engine half";
            return part;
        }

        public static PlayerController Astronaut(StageContext ctx, int index, Vector3 pos)
        {
            if (index <= 1) EngineHalf(ctx, index, pos + Vector3.right * 0.65f, true); // A and B carry the engine halves all game
            bool a = index == 0;
            Color suit = SuitColor(index);
            var root = new GameObject("Player_" + GameFlow.Letter(index));
            root.transform.SetParent(ctx.Group("Players"), false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.LookRotation(Vector3.right);
            var col = root.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.4f;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1f;
            var g = root.AddComponent<GravityAffected>();
            g.gravityScale = 2f;
            g.zeroGResponse = ZeroGravityResponse.Reduced;
            g.reducedGravityFactor = 0.6f;
            g.isPlayer = true;
            var pc = root.AddComponent<PlayerController>();
            pc.playerIndex = index;
            pc.displayName = GameFlow.Letter(index);
            pc.color = suit;
            pc.keys = index != 1
                ? new PlayerKeys { up = Key.W, down = Key.S, left = Key.A, right = Key.D, jump = Key.Space, power = Key.E }
                : new PlayerKeys { up = Key.UpArrow, down = Key.DownArrow, left = Key.LeftArrow, right = Key.RightArrow, jump = Key.RightCtrl, jumpAlt = Key.Enter, power = Key.RightShift };

            SuitModel(root.transform, index);

            // every astronaut has both powers; GameStages switches on the right one(s) for the player count
            var gp = root.AddComponent<GravityPower>();
            var np = root.AddComponent<NullifyPower>();
            gp.enabled = index % 2 == 0;
            np.enabled = index % 2 == 1;
            if (index == 0) { ctx.gravityPower = gp; ctx.playerA = pc; }
            if (index == 1) { ctx.nullifyPower = np; ctx.playerB = pc; }
            ctx.players.Add(pc);
            return pc;
        }

        /// <summary>A orange, B blue, C green, D purple.</summary>
        public static Color SuitColor(int index)
        {
            switch (index)
            {
                case 0: return new Color(1f, 0.55f, 0.15f);
                case 1: return new Color(0.25f, 0.6f, 1f);
                case 2: return new Color(0.3f, 0.9f, 0.45f);
                default: return new Color(0.72f, 0.42f, 1f);
            }
        }

        /// <summary>
        /// Space-suit astronaut built from primitives around a capsule-sized root (centre at the waist, 1.8 m tall):
        /// helmet + visor, suit, backpack with a coloured light, shoulder lamps, arms and legs that SuitAnimator swings.
        /// </summary>
        public static GameObject SuitModel(Transform root, int index)
        {
            Color suit = SuitColor(index);
            string letter = GameFlow.Letter(index);
            var white = Lit("Suit_White", new Color(0.9f, 0.9f, 0.93f), null, 0.45f);
            var trim = Lit("Suit_" + letter, suit, suit * 0.3f, 0.4f);
            var dark = Lit("Suit_Dark", new Color(0.16f, 0.15f, 0.2f), null, 0.3f);
            var visor = Lit("Visor", new Color(0.03f, 0.05f, 0.09f), new Color(0.35f, 0.85f, 1f) * 0.7f, 0.97f);
            var glow = Lit("Glow_" + letter, suit, suit * 3.5f);
            var model = new GameObject("SuitModel");
            model.transform.SetParent(root, false);
            var m = model.transform;
            Part(m, PrimitiveType.Capsule, "Torso", new Vector3(0f, 0.05f, 0f), new Vector3(0.72f, 0.45f, 0.58f), white);
            Part(m, PrimitiveType.Cube, "ChestPlate", new Vector3(0f, 0.18f, 0.24f), new Vector3(0.46f, 0.3f, 0.12f), trim);
            Part(m, PrimitiveType.Cube, "ChestLight", new Vector3(0.12f, 0.22f, 0.31f), new Vector3(0.1f, 0.06f, 0.02f), glow);
            Part(m, PrimitiveType.Cylinder, "Belt", new Vector3(0f, -0.2f, 0f), new Vector3(0.62f, 0.05f, 0.52f), trim);
            Part(m, PrimitiveType.Sphere, "Helmet", new Vector3(0f, 0.62f, 0.02f), Vector3.one * 0.6f, white);
            Part(m, PrimitiveType.Sphere, "Visor", new Vector3(0f, 0.63f, 0.17f), new Vector3(0.44f, 0.32f, 0.36f), visor);
            Part(m, PrimitiveType.Cube, "Backpack", new Vector3(0f, 0.15f, -0.38f), new Vector3(0.56f, 0.66f, 0.26f), dark);
            Part(m, PrimitiveType.Cube, "BackpackLight", new Vector3(0f, 0.32f, -0.52f), new Vector3(0.34f, 0.07f, 0.03f), glow);
            // two antennas on top of the helmet, splayed into a V, with glowing tips
            foreach (float side in new[] { -1f, 1f })
            {
                var basePos = new Vector3(side * 0.11f, 0.88f, -0.02f);
                var tilt = Quaternion.Euler(-12f, 0f, -side * 24f);
                Vector3 dir = tilt * Vector3.up;
                const float len = 0.34f;
                Part(m, PrimitiveType.Sphere, side < 0 ? "AntennaBaseL" : "AntennaBaseR", basePos, Vector3.one * 0.07f, dark);
                Part(m, PrimitiveType.Cylinder, side < 0 ? "AntennaL" : "AntennaR", basePos + dir * (len * 0.5f), new Vector3(0.024f, len * 0.5f, 0.024f), dark, tilt);
                Part(m, PrimitiveType.Sphere, side < 0 ? "AntennaTipL" : "AntennaTipR", basePos + dir * len, Vector3.one * 0.085f, glow);
            }
            Transform Limb(string name, Vector3 pivot, Vector3 size, Material mat)
            {
                var j = new GameObject(name).transform;
                j.SetParent(m, false);
                j.localPosition = pivot;
                Part(j, PrimitiveType.Capsule, name + "Mesh", new Vector3(0f, -size.y, 0f), size, mat);
                return j;
            }
            var la = Limb("LeftArm", new Vector3(-0.42f, 0.32f, 0f), new Vector3(0.2f, 0.28f, 0.2f), white);
            var ra = Limb("RightArm", new Vector3(0.42f, 0.32f, 0f), new Vector3(0.2f, 0.28f, 0.2f), white);
            var ll = Limb("LeftLeg", new Vector3(-0.17f, -0.3f, 0f), new Vector3(0.24f, 0.3f, 0.24f), white);
            var rl = Limb("RightLeg", new Vector3(0.17f, -0.3f, 0f), new Vector3(0.24f, 0.3f, 0.24f), white);
            Part(la, PrimitiveType.Sphere, "Glove", new Vector3(0f, -0.58f, 0f), Vector3.one * 0.17f, trim);
            Part(ra, PrimitiveType.Sphere, "Glove", new Vector3(0f, -0.58f, 0f), Vector3.one * 0.17f, trim);
            Part(ll, PrimitiveType.Cube, "Boot", new Vector3(0f, -0.58f, 0.05f), new Vector3(0.24f, 0.14f, 0.32f), dark);
            Part(rl, PrimitiveType.Cube, "Boot", new Vector3(0f, -0.58f, 0.05f), new Vector3(0.24f, 0.14f, 0.32f), dark);
            var anim = root.gameObject.AddComponent<SuitAnimator>();
            anim.leftArm = la; anim.rightArm = ra; anim.leftLeg = ll; anim.rightLeg = rl;
            var lamp = PointLight(m, root.position + new Vector3(0f, 0.6f, -0.6f), suit, 4f, 1.6f);
            lamp.name = "SuitGlow";
            return model;
        }

        static void Part(Transform parent, PrimitiveType type, string name, Vector3 local, Vector3 scale, Material mat, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        public static void CoreObjects(StageContext ctx, int stageNumber, string title, HudObjective objective, string objectiveText,
            string intro, string completeTitle, string complete, float killY, bool final = false,
            TimeOfDay.Key[] times = null, float routeStartX = 0f, float routeEndX = 170f)
        {
            // every astronaut has its own follow camera; GameStages / NetWorld decide which one this device shows
            ctx.cameras.Clear();
            foreach (var p in ctx.players)
            {
                bool a = p == ctx.playerA;
                var camGo = new GameObject("Camera_" + p.displayName);
                if (a) camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 60f;
                cam.farClipPlane = 2000f;
                cam.rect = p.playerIndex == 0 ? new Rect(0f, 0f, 0.5f, 1f) : new Rect(0.5f, 0f, 0.5f, 1f);
                if (p.playerIndex >= 2) camGo.SetActive(false);
                if (a) camGo.AddComponent<AudioListener>();
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                var coop = camGo.AddComponent<CoopCamera>();
                coop.targets = new[] { p.transform };
                coop.offset = new Vector3(0f, 5f, -12.5f);
                coop.focusOffset = new Vector3(2.5f, 0f, 0f);
                coop.minFocusY = killY + 14f;
                camGo.transform.position = p.transform.position + new Vector3(2.5f, 5f, -12.5f);
                camGo.transform.LookAt(p.transform.position);
                p.ViewCamera = cam;
                SetViewCamera(p, cam);
                ctx.cameras.Add(coop);
            }
            ctx.coopCamera = ctx.cameras[0];

            var sys = new GameObject("GameSystems");
            var sm = sys.AddComponent<StageManager>();
            sm.stageNumber = stageNumber;
            sm.stageTitle = title;
            sm.objective = objective;
            sm.objectiveText = objectiveText;
            sm.introText = intro;
            sm.completeTitle = completeTitle;
            sm.completeText = complete;
            sm.isFinalStage = final;
            ctx.manager = sm;
            var kill = sys.AddComponent<KillVolume>();
            kill.killY = killY;

            var ui = sys.AddComponent<GameUI>();
            ui.icons = AssetDatabase.LoadAssetAtPath<HudIcons>(ArtRoot + "/HudIcons.asset");
            ui.playerA = ctx.playerA;
            ui.playerB = ctx.playerB;
            ui.gravityPower = ctx.gravityPower;
            ui.nullifyPower = ctx.nullifyPower;
            ctx.ui = ui;
            sys.AddComponent<VirtualControls>().icons = ui.icons;

            if (times != null && times.Length > 0)
            {
                var tod = sys.AddComponent<TimeOfDay>();
                tod.sun = RenderSettings.sun;
                tod.keys = times;
                tod.routeStartX = routeStartX;
                tod.routeEndX = routeEndX;
            }
        }

        /// <summary>ViewCamera is a runtime property; store it so it is restored on load.</summary>
        static void SetViewCamera(PlayerController p, Camera cam)
        {
            var link = p.GetComponent<PlayerCameraLink>() ?? p.gameObject.AddComponent<PlayerCameraLink>();
            link.view = cam;
        }

        /// <summary>Removes the environment piece (e.g. replaced by a gameplay version).</summary>
        public static void Remove(GameObject go)
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        /// <summary>Moves a piece out of the static environment into the gameplay group (so it can move).</summary>
        public static GameObject Adopt(StageContext ctx, GameObject go, string group, string name = null)
        {
            go.transform.SetParent(ctx.Group(group), true);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            if (name != null) go.name = name;
            return go;
        }

        // ---------- fun hurdles ----------

        // These components are added by type name so the editor assembly never needs them at compile time
        // (Unity refreshes its runtime script list only after a successful editor compile).
        static Component AddByName(GameObject go, string type)
        {
            var t = System.Type.GetType($"BorrowGravity.{type}, Assembly-CSharp");
            if (t == null) { Debug.LogWarning($"[BorrowGravity] {type} not compiled yet — rebuild after Unity finishes compiling."); return null; }
            return go.AddComponent(t);
        }

        static void SetField(Component c, string field, object value)
        {
            if (c == null) return;
            var f = c.GetType().GetField(field);
            if (f != null) f.SetValue(c, value);
        }

        /// <summary>Gravity booster pad that launches players up and forward.</summary>
        public static Component JumpPadAt(StageContext ctx, string group, Vector3 at, float up = 15f, Vector3? forward = null)
        {
            var pad = Spawn("GravityZone_Pad", at, Vector3.zero, ctx.Group(group), null, "JumpPad");
            NoColliders(pad);
            var trig = TriggerBox(pad.transform, "JumpPadTrigger", at + Vector3.up * 0.6f, new Vector3(2.4f, 1.2f, 3f));
            var jp = AddByName(trig, "JumpPad");
            SetField(jp, "launchUp", up);
            SetField(jp, "launchForward", forward ?? new Vector3(4f, 0f, 0f));
            SetField(jp, "glow", pad.GetComponent<Renderer>());
            return jp;
        }

        /// <summary>Pulsing laser fence across the lane at x (beams from y0 up to y0+height, from z0 to z1).</summary>
        public static Component LaserAt(StageContext ctx, string group, float x, float y0, float height, float z0, float z1,
            float on = 1.6f, float off = 2.2f, float offset = 0f)
        {
            // easier: lasers stay off longer and fire for less time
            on *= 0.75f;
            off *= 1.35f;
            z0 *= LaneWidth; z1 *= LaneWidth; // still spans the whole (wider) walkway
            var root = new GameObject("LaserGate");
            root.transform.SetParent(ctx.Group(group), false);
            root.transform.position = new Vector3(x, y0, (z0 + z1) * 0.5f);
            var beamMat = Transparent("LaserBeam", new Color(1f, 0.2f, 0.25f, 0.7f));
            var beams = new List<Renderer>();
            for (int i = 0; i < 4; i++)
            {
                float y = y0 + 0.35f + i * (height - 0.5f) / 3f;
                var b = SolidBox(root.transform, "Beam" + i, new Vector3(x, y, (z0 + z1) * 0.5f), new Vector3(0.06f, 0.06f, z1 - z0), beamMat);
                Object.DestroyImmediate(b.GetComponent<Collider>());
                b.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                beams.Add(b.GetComponent<Renderer>());
            }
            foreach (float z in new[] { z0, z1 })
                Spawn("Warning_Light", new Vector3(x, y0, z), Vector3.zero, root.transform, new Vector3(1f, height / 2.33f, 1f), "LaserPost");
            var h = HazardBox(root.transform, "LaserHit", new Vector3(x, y0 + height * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.3f, height, z1 - z0), HazardEffect.Respawn, false);
            h.carrierDamage = 60f;
            var gate = AddByName(root, "LaserGate");
            SetField(gate, "onTime", on);
            SetField(gate, "offTime", off);
            SetField(gate, "offset", offset);
            SetField(gate, "beams", beams.ToArray());
            return gate;
        }

        /// <summary>A girder that spins flat at knee height around a vertical axis — jump it as it comes round.</summary>
        public static Rotator SweeperArm(StageContext ctx, string group, Vector3 pivot, float length = 5.5f, float degreesPerSecond = 70f)
        {
            degreesPerSecond *= 0.7f; // easier: slower sweep
            var root = new GameObject("SweeperArm");
            root.transform.SetParent(ctx.Group(group), false);
            root.transform.position = pivot;
            var arm = Spawn("Girder_6m", pivot, Vector3.zero, root.transform, new Vector3(length / 6.14f, 0.45f, 0.45f), "Arm");
            NoColliders(arm);
            Spawn("Station_Pillar", pivot - Vector3.up * 0.5f, Vector3.zero, root.transform, new Vector3(0.5f, 0.4f, 0.5f), "Hub");
            var h = HazardBox(root.transform, "ArmHit", pivot + Vector3.up * 0.25f, new Vector3(length, 0.6f, 0.6f), HazardEffect.Knockback);
            h.useOwnVelocity = false;
            h.knockSpeed = 9f;
            h.knockUp = 6f;
            var rot = root.AddComponent<Rotator>();
            rot.axis = Vector3.up;
            rot.degreesPerSecond = degreesPerSecond;
            return rot;
        }

        /// <summary>A platform that shuttles between two points.</summary>
        public static Component ShuttleAt(StageContext ctx, string group, string prefab, Vector3 a, Vector3 b, float travel = 3f, float pause = 1f, float offset = 0f)
        {
            var go = Spawn(prefab, a, Vector3.zero, ctx.Group(group), null, "Shuttle");
            var sh = AddByName(go, "ShuttlePlatform");
            SetField(sh, "pointA", a);
            SetField(sh, "pointB", b);
            SetField(sh, "travelTime", travel);
            SetField(sh, "pause", pause);
            SetField(sh, "offset", offset);
            return sh;
        }

        /// <summary>Swap a docking platform's detailed mesh collider for a flat deck box (top at y=0).</summary>
        public static void DeckCollider(GameObject dock)
        {
            NoColliders(dock);
            var box = dock.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.5f, 0f);
            box.size = new Vector3(10f, 1f, 6f);
        }

        public static void NoColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }
    }
}
