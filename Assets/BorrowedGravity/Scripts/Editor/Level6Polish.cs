using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static BorrowGravity.EditorTools.BuilderKit;
using Object = UnityEngine.Object;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Finishes Feeza's Level 6 inside the Game scene. Her Level6_Greybox keeps its layout, swings and tuning; this adds
    /// what her builder lists as next steps (players, checkpoints, the gravity zone) and connects the level to the game,
    /// then dresses it as a ship graveyard and layers extras on top: freezable swing seats, salvage cores, background
    /// events and a closing crusher. Safe to re-run: only "Graveyard" and "Graveyard_Gameplay" are rebuilt.
    /// </summary>
    public static class Level6Polish
    {
        const string Sky = "Stage6_ShipGraveyard";

        [MenuItem("BorrowGravity/Level 6/Polish Ship Graveyard", priority = 62)]
        public static void Run()
        {
            var stages = LevelArt.OpenGame();
            if (stages == null) return;
            var scene = stages.gameObject.scene;
            var rootGo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Level_Stage6");
            if (rootGo == null) { Debug.LogError("[Level6] Level_Stage6 not found."); return; }
            LevelArt.Unpack(rootGo);
            var grey = rootGo.transform.Find("Level6_Greybox");
            if (grey == null)
            {
                var top = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Level6_Greybox");
                if (top == null) { Debug.LogError("[Level6] Level6_Greybox not found."); return; }
                top.transform.SetParent(rootGo.transform, true); // a loose root stays on during every other level
                grey = top.transform;
            }

            EnsureArtKit();
            var env = LevelArt.FreshChild(rootGo.transform, "Graveyard");
            var play = LevelArt.FreshChild(rootGo.transform, "Graveyard_Gameplay");
            var ctx = new StageContext { scene = scene, environment = env, gameplay = play };

            var deck = Lit("G6_Deck", new Color(0.3f, 0.22f, 0.17f), null, 0.35f);
            var seat = Lit("G6_Seat", new Color(0.62f, 0.44f, 0.16f), null, 0.45f);
            var iron = Lit("G6_Iron", new Color(0.12f, 0.13f, 0.14f), null, 0.5f);
            var teal = Lit("G6_Teal", new Color(0.2f, 1f, 0.75f), new Color(0.2f, 1f, 0.75f) * 2.6f);
            var amber = Lit("G6_Amber", new Color(1f, 0.62f, 0.12f), new Color(1f, 0.55f, 0.1f) * 3f);
            var red = Lit("G6_Red", new Color(1f, 0.12f, 0.08f), new Color(1f, 0.1f, 0.05f) * 4f);

            DressPlatforms(grey, env, deck, teal, iron);
            DressSwings(grey, env, seat, iron, amber);
            Backdrop(env);
            AddCheckpoints(ctx, grey);
            AddGravityZone(ctx, grey, deck, teal);
            AddPlayers(ctx, rootGo.transform, grey);
            AddExtras(ctx, iron, teal, red);
            AddHints(ctx);
            SetupStage(rootGo, ctx);

            var root = rootGo.GetComponent<StageRoot>();
            ApplyLook(root);
            LevelArt.Wire(stages, 6, root);
            LevelArt.SaveAsPrefab(rootGo);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Level6] Ship Graveyard polished and connected as level 6.");
        }

        // ---------- her geometry, new look ----------

        static void DressPlatforms(Transform grey, Transform env, Material deck, Material trim, Material iron)
        {
            foreach (Transform p in grey.Find("FixedPlatforms"))
            {
                p.GetComponent<Renderer>().sharedMaterial = deck;
                var c = p.position;
                var s = p.localScale;
                float face = s.y * 0.5f + 0.02f;
                float y = p.name == "GravityCeilingWalkway" ? c.y - face : c.y + face;
                foreach (float z in new[] { -s.z * 0.5f + 0.15f, s.z * 0.5f - 0.15f })
                    LevelArt.Box(env, p.name + "_Trim", new Vector3(c.x, y, z), new Vector3(s.x, 0.04f, 0.14f), trim);
                LevelArt.Box(env, p.name + "_Keel", new Vector3(c.x, c.y - s.y * 0.5f - 0.9f, c.z), new Vector3(s.x * 0.8f, 1.6f, s.z * 0.45f), iron);
            }
        }

        static void DressSwings(Transform grey, Transform env, Material seat, Material iron, Material amber)
        {
            int n = 0;
            foreach (var sw in grey.GetComponentsInChildren<SwingPlatform>(true))
            {
                var p = sw.transform.position;
                sw.platform.GetComponent<Renderer>().sharedMaterial = seat;
                if (sw.ropeCube != null) sw.ropeCube.GetComponent<Renderer>().sharedMaterial = iron;
                var marker = sw.transform.Find("PivotMarker");
                if (marker != null) marker.GetComponent<Renderer>().sharedMaterial = amber;

                // Player B can freeze the seat; the swing's own numbers are untouched
                var null1 = sw.platform.GetComponent<Nullifiable>();
                if (null1 == null) null1 = sw.platform.gameObject.AddComponent<Nullifiable>();
                null1.displayName = "Swing";
                null1.holdDuration = 5f;
                null1.allowSteer = false;
                var rb = sw.platform.GetComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                // the crane each swing hangs from
                LevelArt.Box(env, sw.name + "_Crane", p + Vector3.up * 0.6f, new Vector3(1.1f, 1.1f, 17f), iron);
                foreach (float z in new[] { -8.5f, 8.5f })
                    LevelArt.Box(env, sw.name + "_Leg", new Vector3(p.x, (p.y + 0.6f - 34f) * 0.5f, z), new Vector3(1.3f, p.y + 34.6f, 1.3f), iron);
                LevelArt.Box(env, sw.name + "_Lamp", p + new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.2f, 3f), amber);
                if (n++ % 3 == 0) PointLight(env, p + Vector3.down * 1f, new Color(1f, 0.6f, 0.2f), 14f, 2.5f);
            }
        }

        static void Backdrop(Transform env)
        {
            var group = new GameObject("Wrecks").transform;
            group.SetParent(env, false);
            string[] big = { "Wreck_Hull_A", "Station_Chunk_A", "Wreck_Hull_B", "Station_Chunk_B", "Spaceship", "Wreck_Hull_A" };
            string[] small = { "Girder_Broken", "Rock_Debris_L", "Cargo_Container", "Debris_Plate_A", "Rock_Debris_M", "Debris_Plate_B" };
            var rng = new System.Random(66);
            float F(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            int i = 0;
            for (float x = -30f; x < 250f; x += 34f, i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                var hulk = Spawn(big[i % big.Length], new Vector3(x, F(-22f, 8f), side * F(52f, 88f)),
                    new Vector3(F(-25f, 25f), F(0f, 360f), F(-40f, 40f)), group, Vector3.one * F(3.5f, 6.5f));
                NoColliders(hulk);
                foreach (var r in hulk.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                for (int k = 0; k < 2; k++)
                {
                    var bit = Spawn(small[(i + k) % small.Length], new Vector3(x + F(-14f, 14f), F(-20f, 22f), -side * F(16f, 42f)),
                        new Vector3(F(0f, 360f), F(0f, 360f), F(0f, 360f)), group, Vector3.one * F(1.2f, 2.6f));
                    NoColliders(bit);
                    foreach (var r in bit.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (k == 0)
                    {
                        var spin = bit.AddComponent<Rotator>();
                        spin.axis = new Vector3(F(-1f, 1f), 1f, F(-1f, 1f)).normalized;
                        spin.degreesPerSecond = F(3f, 10f);
                    }
                }
            }
        }

        // ---------- the steps her builder leaves to do by hand ----------

        static void AddCheckpoints(StageContext ctx, Transform grey)
        {
            string[] labels = { "Crane Dock", "Hull Flip", "Breaker Yard", "Crusher Gate" };
            var markers = grey.Find("Markers");
            for (int i = 0; i < labels.Length; i++)
            {
                var slot = markers.Find($"Checkpoint_{i + 1}_slot");
                var cp = slot.GetComponent<Checkpoint>();
                if (cp == null) cp = slot.gameObject.AddComponent<Checkpoint>();
                var at = slot.position;
                cp.section = i;
                cp.label = labels[i];
                cp.isStart = i == 0;
                cp.spawnOffsets = new[]
                {
                    new Vector3(-0.9f, -0.45f, -1.2f), new Vector3(-0.9f, -0.45f, 1.2f),
                    new Vector3(0.9f, -0.45f, -1.2f), new Vector3(0.9f, -0.45f, 1.2f)
                };
                var beacon = Spawn("Checkpoint_Beacon", new Vector3(at.x, 0.03f, -2.6f), Vector3.zero, ctx.Group("Checkpoints"), null, "Beacon_" + labels[i].Replace(' ', '_'));
                NoColliders(beacon);
                var light = new GameObject("BeaconLight").AddComponent<Light>();
                light.transform.SetParent(beacon.transform, false);
                light.transform.localPosition = new Vector3(0f, 3.6f, 0f);
                light.type = LightType.Point;
                light.range = 8f;
                cp.beaconLight = light;
                EditorUtility.SetDirty(cp);
            }
        }

        static void AddGravityZone(StageContext ctx, Transform grey, Material deck, Material trim)
        {
            var slot = grey.Find("Markers/GravityZone_Slot");
            var box = slot.GetComponent<BoxCollider>();
            var zone = slot.GetComponent<GravityZone>();
            if (zone == null) zone = slot.gameObject.AddComponent<GravityZone>();
            zone.zoneName = "Hull Flip";
            zone.section = 1;
            zone.size = box.size;
            SetMode(zone, GravityMode.Down);
            zone.cycle = new[] { GravityMode.Down, GravityMode.Up };
            zone.controllable = true;
            zone.affectsPlayers = true;
            zone.affectsObjects = true;
            EditorUtility.SetDirty(zone);

            // ribs under her ceiling walkway, so the upside-down walk has something to hop over
            foreach (float x in new[] { 98.5f, 104.5f })
            {
                LevelArt.Box(ctx.Group("Hull Flip"), "CeilingRib", new Vector3(x, 7.65f, 0f), new Vector3(0.8f, 0.7f, 6f), deck, true);
                LevelArt.Box(ctx.Group("Hull Flip"), "CeilingRibLight", new Vector3(x, 7.28f, 0f), new Vector3(0.82f, 0.05f, 6f), trim);
            }
        }

        static void AddPlayers(StageContext ctx, Transform root, Transform grey)
        {
            var a = grey.Find("Markers/SpawnA").position;
            var b = grey.Find("Markers/SpawnB").position;
            Astronaut(ctx, 0, a);
            Astronaut(ctx, 1, b);
            // this level already has its cameras: point each at its astronaut
            foreach (var p in ctx.players)
            {
                var camT = root.Find("Camera_" + p.displayName);
                if (camT == null) continue;
                var cam = camT.GetComponent<Camera>();
                var coop = camT.GetComponent<CoopCamera>();
                coop.targets = new[] { p.transform };
                coop.minFocusY = -11f;
                var so = new SerializedObject(coop);
                var extra = so.FindProperty("extraTarget");
                if (extra != null) { extra.objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo(); }
                camT.position = p.transform.position + new Vector3(2.5f, 5f, -12.5f);
                camT.LookAt(p.transform.position);
                var link = p.GetComponent<PlayerCameraLink>();
                if (link == null) link = p.gameObject.AddComponent<PlayerCameraLink>();
                link.view = cam;
                EditorUtility.SetDirty(coop);
            }
            Wall(ctx, -6f, 222f, -1.95f);
            Wall(ctx, -6f, 222f, 1.95f);
            EndWall(ctx, -5f);
            EndWall(ctx, 220f);
        }

        // ---------- extras on top of her level ----------

        static void AddExtras(StageContext ctx, Material iron, Material teal, Material red)
        {
            // salvage cores, just off the safe line: a reason to take a risk
            var cores = ctx.Group("Salvage");
            foreach (var at in new[]
            {
                new Vector3(18.6f, 2.6f, 1.8f), new Vector3(59.4f, 2.6f, -1.8f), new Vector3(102f, 6.4f, -2f),
                new Vector3(144.9f, 2.6f, 1.8f), new Vector3(195.6f, 3.2f, 0f)
            })
            {
                var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
                core.name = "SalvageCore";
                core.transform.SetParent(cores, false);
                core.transform.position = at;
                core.transform.rotation = Quaternion.Euler(45f, 0f, 35f);
                core.transform.localScale = Vector3.one * 0.6f;
                core.GetComponent<Renderer>().sharedMaterial = teal;
                core.GetComponent<BoxCollider>().size = Vector3.one * 2.4f;
                core.AddComponent<SalvageCore>();
            }

            // something huge drifts past early on...
            var wreck = Spawn("Wreck_Hull_B", new Vector3(10f, 26f, 40f), new Vector3(10f, 70f, 25f), ctx.Group("Events"), Vector3.one * 5f, "DriftingWreck");
            NoColliders(wreck);
            Flyby(ctx, "WreckDrift", wreck.transform, new Vector3(10f, 26f, 40f), new Vector3(110f, 12f, 34f), 42f, 16f, 0.35f,
                "COMMS: Whole fleets died here. Stay on the cranes.");

            // ...and later the first look at the thing hunting them (it's Level 7's boss)
            var eye = new GameObject("WardenShadow").transform;
            eye.SetParent(ctx.Group("Events"), false);
            LevelArt.Shape(PrimitiveType.Sphere, eye, "Body", Vector3.zero, Vector3.one * 5f, iron);
            LevelArt.Shape(PrimitiveType.Sphere, eye, "Eye", new Vector3(0f, 0f, -2.2f), Vector3.one * 1.8f, red);
            Flyby(ctx, "WardenPass", eye, new Vector3(118f, 22f, 22f), new Vector3(172f, 14f, 20f), 121f, 7f, 0.5f,
                "COMMS: What was THAT? Move. Now.");

            // the closing crusher: it pushes the team through her last two swings
            var crusher = new GameObject("ScrapCrusher").transform;
            crusher.SetParent(ctx.Group("Events"), false);
            crusher.position = new Vector3(171f, 6f, 0f);
            LevelArt.Box(crusher, "Wall", crusher.position, new Vector3(6f, 26f, 16f), iron);
            for (int i = -2; i <= 2; i++)
                LevelArt.Box(crusher, "Tooth", crusher.position + new Vector3(3.3f, i * 4f, 0f), new Vector3(1f, 2.2f, 15f), red);
            var cr = crusher.gameObject.AddComponent<ScrapCrusher>();
            cr.section = 3;
            cr.triggerX = 178.4f;
            cr.stopX = 205f;
            cr.speed = 1.1f;
            cr.frontOffset = 3.8f;
            PointLight(crusher, crusher.position + new Vector3(5f, 0f, 0f), new Color(1f, 0.15f, 0.05f), 16f, 3f);
        }

        static void Flyby(StageContext ctx, string name, Transform mover, Vector3 from, Vector3 to, float triggerX, float seconds, float shake, string line)
        {
            var go = new GameObject(name);
            go.transform.SetParent(ctx.Group("Events"), false);
            var f = go.AddComponent<FlybyEvent>();
            f.mover = mover;
            f.from = from;
            f.to = to;
            f.triggerX = triggerX;
            f.duration = seconds;
            f.shake = shake;
            f.message = line;
        }

        static void AddHints(StageContext ctx)
        {
            var size = new Vector3(3f, 4f, 6f);
            Hint(ctx, "The Cranes", "Jump onto the swings. B: freeze a seat close to the ledge so your partner can walk across.", new Vector3(1.5f, 1.5f, 0f), size, 9f);
            Hint(ctx, "Hull Flip", "No way across down here. A: flip gravity and walk the ceiling together.", new Vector3(91f, 1.5f, 0f), size, 8f);
            Hint(ctx, "Breaker Yard", "Faster cranes. Time your jumps, or have B hold one still.", new Vector3(113f, 1.5f, 0f), size, 7f);
            Hint(ctx, "Crusher Gate", "Leave this deck and the crusher starts. Don't stop for anything.", new Vector3(176f, 1.5f, 0f), size, 7f);
        }

        static void SetupStage(GameObject root, StageContext ctx)
        {
            var sm = root.GetComponentInChildren<StageManager>(true);
            sm.stageNumber = 6;
            sm.stageTitle = "STAGE 6 — THE SHIP GRAVEYARD";
            sm.objective = HudObjective.ReturnToShip;
            sm.objectiveText = "Cross the graveyard";
            sm.introText = "The repair held just long enough to throw the ship into a graveyard of dead ships. You pull the engine halves back out to keep them safe.\n\n" +
                "The only way through is along the old salvage cranes. B can freeze a swinging seat in place. A can flip gravity where the ground runs out.";
            sm.completeTitle = "THROUGH THE GRAVEYARD";
            sm.completeText = "The far dock is clear and the engine is still in one piece. But whatever passed overhead has found you.";
            sm.isFinalStage = false;
            EditorUtility.SetDirty(sm);

            var old = sm.GetComponent<TimeOfDay>(); // copied in with the Stage 3 setup; this level has its own light
            if (old != null) Object.DestroyImmediate(old);

            var ui = sm.GetComponent<GameUI>();
            ui.playerA = ctx.playerA;
            ui.playerB = ctx.playerB;
            ui.gravityPower = ctx.gravityPower;
            ui.nullifyPower = ctx.nullifyPower;
            EditorUtility.SetDirty(ui);

            var kill = sm.GetComponent<KillVolume>();
            if (kill != null) { kill.killY = -25f; EditorUtility.SetDirty(kill); }
        }

        static void ApplyLook(StageRoot root)
        {
            root.skybox = LevelArt.Sky(Sky, new LevelArt.SkyStyle
            {
                deep = new Color(0.01f, 0.035f, 0.05f),
                mid = new Color(0.03f, 0.2f, 0.2f),
                glow = new Color(0.32f, 0.72f, 0.48f),
                sun = new Color(1f, 0.72f, 0.35f),
                sunDirection = new Vector3(0.5f, 0.35f, 0.8f),
                sunSize = 1f,
                starAmount = 0.004f,
                seed = 61
            });
            root.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            root.ambientIntensity = 1.2f;
            root.fog = true;
            root.fogMode = FogMode.ExponentialSquared;
            root.fogDensity = 0.0045f;
            root.fogColor = new Color(0.07f, 0.24f, 0.23f);
            var sun = root.transform.Find("Lighting/Sun")?.GetComponent<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, 0.78f, 0.52f);
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(38f, -30f, 0f);
                root.sun = sun;
                EditorUtility.SetDirty(sun);
            }
            var rim = root.transform.Find("Lighting/Rim Light")?.GetComponent<Light>();
            if (rim != null)
            {
                rim.color = new Color(0.3f, 1f, 0.8f);
                rim.intensity = 0.55f;
                EditorUtility.SetDirty(rim);
            }
        }
    }
}
