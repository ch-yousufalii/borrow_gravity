using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static BorrowGravity.EditorTools.BuilderKit;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Adds one adventure moment to each of Levels 1-5 inside the Game scene: a child named "Adventure" under the level's
    /// Gameplay. The levels' own routes stay as they are, and Levels 4 and 5 stay prefab instances (the additions live in
    /// the scene as overrides; the prefab assets are not written). Safe to re-run: only "Adventure" is rebuilt.
    /// </summary>
    public static class AdventureMoments
    {
        const string Group = "Adventure";

        static Material deck, iron, teal, amber, red;

        [MenuItem("BorrowGravity/Levels 1-5/Build Adventure Moments", priority = 64)]
        public static void Run()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.name != "Game")
            {
                if (LevelArt.OpenGame() == null) return;
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            }
            EnsureArtKit();
            deck = Lit("G6_Deck", new Color(0.3f, 0.22f, 0.17f), null, 0.35f);
            iron = Lit("G6_Iron", new Color(0.12f, 0.13f, 0.14f), null, 0.5f);
            teal = Lit("G6_Teal", new Color(0.2f, 1f, 0.75f), new Color(0.2f, 1f, 0.75f) * 2.6f);
            amber = Lit("G6_Amber", new Color(1f, 0.62f, 0.12f), new Color(1f, 0.55f, 0.1f) * 3f);
            red = Lit("G6_Red", new Color(1f, 0.12f, 0.08f), new Color(1f, 0.1f, 0.05f) * 4f);

            ScrapSky(Begin(scene, 1));
            VaultLockdown(Begin(scene, 2));
            CollapseChase(Begin(scene, 3));
            TimeStretch(Begin(scene, 4));
            LastStand(Begin(scene, 5), scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Adventure] Moments built for Levels 1-5.");
        }

        static Transform Begin(UnityEngine.SceneManagement.Scene scene, int level)
        {
            var root = scene.GetRootGameObjects().First(g => g.name == "Level_Stage" + level);
            return LevelArt.FreshChild(root.transform.Find("Gameplay"), Group);
        }

        static void Say(Transform parent, string title, string message, Vector3 at, float seconds = 7f)
        {
            var h = TriggerBox(parent, "Hint_" + title, at, new Vector3(3f, 5f, 8f)).AddComponent<HintTrigger>();
            h.sectionTitle = title;
            h.message = message;
            h.duration = seconds;
        }

        static void Flyby(Transform parent, string name, GameObject mover, Vector3 from, Vector3 to, float triggerX, float seconds, float shake, string line)
        {
            NoColliders(mover);
            foreach (var r in mover.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var f = new GameObject(name).AddComponent<FlybyEvent>();
            f.transform.SetParent(parent, false);
            f.mover = mover.transform;
            f.from = from;
            f.to = to;
            f.triggerX = triggerX;
            f.duration = seconds;
            f.shake = shake;
            f.message = line;
        }

        static void Core(Transform parent, Vector3 at)
        {
            var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.name = "SalvageCore";
            core.transform.SetParent(parent, false);
            core.transform.position = at;
            core.transform.rotation = Quaternion.Euler(45f, 0f, 35f);
            core.transform.localScale = Vector3.one * 0.6f;
            core.GetComponent<Renderer>().sharedMaterial = teal;
            core.GetComponent<BoxCollider>().size = Vector3.one * 2.4f;
            core.AddComponent<SalvageCore>();
        }

        // ---------- Level 1: an optional route through the sky above the bridge ----------

        const float SkyTop = 8.5f;

        static void Islet(Transform parent, string name, float x0, float x1)
        {
            float x = (x0 + x1) * 0.5f, w = x1 - x0;
            LevelArt.Box(parent, name, new Vector3(x, SkyTop - 0.25f, 0f), new Vector3(w, 0.5f, 5f), deck, true);
            foreach (float z in new[] { -2.35f, 2.35f })
                LevelArt.Box(parent, name + "_Trim", new Vector3(x, SkyTop + 0.02f, z), new Vector3(w, 0.04f, 0.14f), teal);
            var rock = Spawn("Rock_Debris_L", new Vector3(x, SkyTop - 1.6f, 0f), new Vector3(0f, x * 40f, 180f), parent, new Vector3(1.4f, 0.8f, 1.4f), name + "_Rock");
            NoColliders(rock);
        }

        static void ScrapSky(Transform g)
        {
            var field = new GameObject("Zone_Drift Lift").AddComponent<GravityZone>();
            field.transform.SetParent(g, false);
            field.transform.position = new Vector3(66f, 6.5f, 0f);
            field.zoneName = "Drift Lift";
            field.section = 2;
            field.size = new Vector3(4f, 12f, 5.6f);
            SetMode(field, GravityMode.Down);
            field.cycle = new[] { GravityMode.Down, GravityMode.Zero };
            field.playerZeroGLift = 3.2f;

            Islet(g, "SkyIsletA", 69f, 74f);
            Islet(g, "SkyIsletB", 82f, 87f);

            // drifts across the route; B pins it in the middle so the team can walk over
            var slab = LevelArt.Box(g, "DriftSlab", new Vector3(78f, SkyTop - 0.25f, -9f), new Vector3(4.4f, 0.5f, 4f), amber, true);
            slab.AddComponent<Rigidbody>().isKinematic = true;
            var shuttle = slab.AddComponent<ShuttlePlatform>();
            shuttle.pointA = new Vector3(78f, SkyTop - 0.25f, -9f);
            shuttle.pointB = new Vector3(78f, SkyTop - 0.25f, 9f);
            shuttle.travelTime = 3.2f;
            shuttle.pause = 0.4f;
            var pin = Nullify(slab, "Drifting Slab", 6f);
            pin.allowSteer = false;

            var cores = new GameObject("Salvage").transform;
            cores.SetParent(g, false);
            Core(cores, new Vector3(71.5f, SkyTop + 1.1f, 1.2f));
            Core(cores, new Vector3(78f, SkyTop + 2.2f, 0f));
            Core(cores, new Vector3(84.5f, SkyTop + 1.1f, -1.2f));

            Say(g, "Scrap Sky", "Optional: there's salvage up there. A: zero the Drift Lift to float up. B: freeze the slab so you can both cross.", new Vector3(60f, 3f, 0f));

            var chunk = Spawn("Station_Chunk_A", new Vector3(30f, 34f, 34f), new Vector3(15f, 60f, 30f), g, Vector3.one * 3f, "FallingStationChunk");
            Flyby(g, "StationChunkPass", chunk, new Vector3(30f, 34f, 34f), new Vector3(112f, 8f, 40f), 58f, 13f, 0.45f,
                "COMMS: That was part of the station. It's coming apart faster than we thought.");
        }

        // ---------- Level 2: the station locks the vault; only both powers together open it ----------

        static void VaultLockdown(Transform g)
        {
            var door = LevelArt.Box(g, "LockdownBulkhead", new Vector3(130f, 11f, -1.2f), new Vector3(0.7f, 12f, 11.8f), iron, true);
            LevelArt.Box(door.transform, "Stripe", new Vector3(129.6f, 7f, -1.2f), new Vector3(0.06f, 0.5f, 11.8f), red);
            LevelArt.Box(door.transform, "Stripe", new Vector3(129.6f, 9f, -1.2f), new Vector3(0.06f, 0.5f, 11.8f), red);
            PointLight(g, new Vector3(128.6f, 9.5f, -1f), new Color(1f, 0.15f, 0.08f), 12f, 3f);

            var ledge = LevelArt.Box(g, "PlateLedge", new Vector3(128f, 8f, -5.4f), new Vector3(3.4f, 0.4f, 2.2f), deck, true);
            LevelArt.Box(g, "LedgeLeg", new Vector3(128f, 6.4f, -6.1f), new Vector3(0.5f, 2.9f, 0.5f), iron);
            var lamp = LevelArt.Box(g, "PlateLamp", ledge.transform.position + Vector3.up * 0.22f, new Vector3(3f, 0.05f, 1.8f), amber);

            var plate = TriggerBox(g, "LockdownPlate", new Vector3(128f, 9.3f, -5.4f), new Vector3(3.4f, 2.2f, 2.2f)).AddComponent<PressurePlate>();
            plate.section = 7;
            plate.acceptObjects = true;
            plate.acceptPlayers = false;
            plate.label = "Override Plate";
            plate.indicator = lamp.GetComponent<Renderer>();

            var d = door.AddComponent<Door>();
            d.section = 7;
            d.source = plate;
            d.parts = new[] { door.transform };
            d.openOffsets = new[] { Vector3.up * 12f };
            d.speed = 4f;
            d.latch = true;
            d.openMessage = "COMMS: Override accepted. The vault is yours.";

            // only the crate feels this field, so nobody can float over the door
            var field = new GameObject("Zone_Vault Hall").AddComponent<GravityZone>();
            field.transform.SetParent(g, false);
            field.transform.position = new Vector3(127.9f, 10.5f, 0f);
            field.zoneName = "Vault Hall";
            field.section = 7;
            field.size = new Vector3(4.2f, 11f, 13.4f);
            SetMode(field, GravityMode.Down);
            field.cycle = new[] { GravityMode.Down, GravityMode.Zero };
            field.affectsPlayers = false;

            var crate = Spawn("Crate_Heavy_2m", new Vector3(128f, 6f, 3.4f), Vector3.zero, g, null, "OverrideCrate");
            if (crate.GetComponentInChildren<Collider>() == null) BoxFit(crate);
            MakeDynamic(crate, 40f);
            Gravity(crate, ZeroGravityResponse.Float, 2f, 1.6f, 11f);
            var n = Nullify(crate, "Override Crate", 9f, true, 2.5f);
            n.steerLimits = new Bounds(new Vector3(128f, 0f, -1f), new Vector3(2.6f, 1f, 13f));
            Respawn(crate, 7, -10f);
            crate.AddComponent<PlateWeight>();

            Say(g, "Vault Lockdown", "The station sealed the vault. A: zero the hall. B: freeze the crate and steer it over the ledge. A: gravity back on to drop it on the plate.", new Vector3(124.5f, 6.5f, 0f), 9f);
        }

        // ---------- Level 3: the station folds up behind them ----------

        static void CollapseChase(Transform g)
        {
            var wall = new GameObject("ScrapCrusher").transform;
            wall.SetParent(g, false);
            wall.position = new Vector3(106.2f, 6f, 0f);
            LevelArt.Box(wall, "Wall", wall.position, new Vector3(6f, 26f, 16f), iron);
            for (int i = -2; i <= 2; i++)
                LevelArt.Box(wall, "Tooth", wall.position + new Vector3(3.3f, i * 4f, 0f), new Vector3(1f, 2.2f, 15f), red);
            PointLight(wall, wall.position + new Vector3(5f, 0f, 0f), new Color(1f, 0.15f, 0.05f), 16f, 3f);

            var c = wall.gameObject.AddComponent<ScrapCrusher>();
            c.section = 5;
            c.triggerX = 118f;
            c.stopX = 133f;
            c.speed = 2.6f;
            c.frontOffset = 3.8f;
            c.waitForEveryone = true;
            c.riseDistance = 30f;
            c.warning = "THE STATION IS FOLDING UP BEHIND YOU — RUN!";
        }

        // ---------- Level 4: the horizon bends time while a dead ship falls past ----------

        static void TimeStretch(Transform g)
        {
            var zone = TriggerBox(g, "TimeDilation", new Vector3(253.8f, 4f, 0f), new Vector3(15f, 14f, 10f)).AddComponent<TimeDilationZone>();
            zone.slowScale = 0.5f;
            zone.message = "COMMS: Your signal's dragging... the horizon is bending time around you.";

            var wreck = Spawn("Wreck_Hull_A", new Vector3(238f, 52f, 34f), new Vector3(20f, 40f, 60f), g, Vector3.one * 6f, "FallingShip");
            Flyby(g, "ShipFallsIn", wreck, new Vector3(238f, 52f, 34f), new Vector3(272f, -46f, 34f), 247f, 7f, 0.35f, null);
        }

        // ---------- Level 5: stand your ground while the engine spins up ----------

        static WardenStrike Strike(Transform parent)
        {
            var root = new GameObject("Strike");
            root.transform.SetParent(parent, false);
            var disc = LevelArt.Shape(PrimitiveType.Cylinder, root.transform, "Disc", Vector3.zero, new Vector3(5.2f, 0.02f, 5.2f), red);
            var beam = LevelArt.Shape(PrimitiveType.Cylinder, root.transform, "Beam", new Vector3(0f, 15f, 0f), new Vector3(4f, 15f, 4f), red);
            var hitGo = new GameObject("Hit");
            hitGo.transform.SetParent(root.transform, false);
            hitGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var sphere = hitGo.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 2.5f;
            var h = hitGo.AddComponent<Hazard>();
            h.effect = HazardEffect.Knockback;
            h.knockSpeed = 7f;
            h.knockUp = 5f;
            h.armed = false;
            var s = root.AddComponent<WardenStrike>();
            s.disc = disc;
            s.beam = beam;
            s.hit = h;
            return s;
        }

        static void LastStand(Transform g, UnityEngine.SceneManagement.Scene scene)
        {
            var ctx = new StageContext { scene = scene, gameplay = g, environment = g };
            // the level's later checkpoints sit far behind this bay, and a knockback off the edge shouldn't cost the whole run
            Checkpoint(ctx, null, new Vector3(181f, -0.3f, -4f), 13, "Repair Bay", new Vector3(4f, 5f, 14f), false, null, -0.5f);

            foreach (var (name, at, size) in new[]
            {
                ("BayEdgeNear", new Vector3(181f, 5f, 7.9f), new Vector3(4f, 14f, 1f)),
                ("BayEdgeStep", new Vector3(182.5f, 5f, 10f), new Vector3(1f, 14f, 5f)),
                ("BayEdgeFar", new Vector3(189f, 5f, 12.5f), new Vector3(13f, 14f, 1f)),
                ("BayEdgeFront", new Vector3(187f, 5f, -7.9f), new Vector3(17f, 14f, 1f)),
                ("BayEdgeEnd", new Vector3(195.9f, 5f, 2f), new Vector3(1f, 14f, 21f)),
            })
                SolidBox(g, name, at, size);

            var ring = new GameObject("RepairRing").transform;
            ring.SetParent(g, false);
            ring.position = new Vector3(188.4f, -0.3f, -0.5f);
            var hold = ring.gameObject.AddComponent<RepairHold>();
            hold.section = 13;
            hold.radius = 5.5f;
            LevelArt.Shape(PrimitiveType.Cylinder, ring, "RingEdge", ring.position + Vector3.up * 0.02f, new Vector3(11.4f, 0.01f, 11.4f), amber);
            LevelArt.Shape(PrimitiveType.Cylinder, ring, "RingFloor", ring.position + Vector3.up * 0.03f, new Vector3(11f, 0.01f, 11f), iron);
            hold.progressDisc = LevelArt.Shape(PrimitiveType.Cylinder, ring, "RingProgress", ring.position + Vector3.up * 0.04f, new Vector3(0.05f, 0.01f, 0.05f), teal).transform;
            hold.strikes = new[] { Strike(ring), Strike(ring), Strike(ring) };
            hold.seconds = 18f;
            hold.strikeEvery = 1.8f;
            hold.startMessage = "COMMS: Engine's spinning up. Stay in the ring, both of you, and keep moving!";
            hold.doneMessage = "COMMS: She's running. You did it.";
            PointLight(ring, ring.position + Vector3.up * 5f, new Color(0.3f, 1f, 0.8f), 14f, 2.5f);

            Say(g, "Last Stand", "The repair only runs while you're BOTH inside the ring. Dodge the strikes, stay in it.", new Vector3(180f, 1.5f, 0f), 7f);

            var goal = g.parent.GetComponentInChildren<GoalTrigger>(true);
            goal.extraCondition = hold;
            goal.waitingForCondition = "Hold the repair first!";
            EditorUtility.SetDirty(goal);
            PrefabUtility.RecordPrefabInstancePropertyModifications(goal);
        }
    }
}
