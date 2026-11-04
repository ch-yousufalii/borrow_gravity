using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static BorrowGravity.EditorTools.BuilderKit;
using Object = UnityEngine.Object;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Level 7 — The Warden: a boss fight in an open arena instead of a route of hurdles, then a timed escape to the ship.
    /// Built in its own scene and moved into the Game scene; no other level is touched.
    /// </summary>
    public static class Stage7Builder
    {
        public const string ScenePath = "Assets/Scenes/Stage7.unity";
        const float TileSize = 6f;

        [MenuItem("BorrowGravity/Level 7/Build The Warden", priority = 63)]
        public static void BuildAndInsert()
        {
            Build();
            LevelArt.InsertScene(7, "Level_Stage7", ScenePath);
            Level7LastStand.Apply();
            Debug.Log("[Level7] The Warden built and connected as level 7.");
        }

        static void Build()
        {
            LevelArt.Sky("Stage7_DyingStar", new LevelArt.SkyStyle
            {
                deep = new Color(0.025f, 0.006f, 0.015f),
                mid = new Color(0.24f, 0.03f, 0.05f),
                glow = new Color(0.95f, 0.3f, 0.08f),
                sun = new Color(1f, 0.45f, 0.15f),
                sunDirection = new Vector3(0.85f, 0.3f, 0.45f),
                sunSize = 14f,
                starAmount = 0.002f,
                seed = 72
            });
            var ctx = BeginStage("Stage7");
            RenderSettings.fogColor = new Color(0.22f, 0.04f, 0.05f);
            RenderSettings.fogDensity = 0.004f;
            RenderSettings.sun.color = new Color(1f, 0.5f, 0.3f);
            RenderSettings.sun.intensity = 1.3f;

            var hull = Lit("G7_Hull", new Color(0.09f, 0.08f, 0.1f), null, 0.6f);
            var tileMat = Lit("G7_Tile", new Color(0.17f, 0.14f, 0.17f), null, 0.5f);
            var ember = Lit("G7_Ember", new Color(1f, 0.32f, 0.08f), new Color(1f, 0.3f, 0.06f) * 3f);
            var danger = Lit("G7_Danger", new Color(1f, 0.05f, 0.05f), new Color(1f, 0.05f, 0.04f) * 5f);
            var field = Lit("G7_Field", new Color(1f, 0.1f, 0.3f), new Color(1f, 0.1f, 0.3f) * 2f);
            var safe = Lit("G7_Safe", new Color(0.3f, 1f, 0.8f), new Color(0.3f, 1f, 0.8f) * 2.5f);

            var tiles = BuildArena(ctx, tileMat, ember);
            var zone = Zone(ctx, "Warden Arena", 0, new Vector3(0f, 12f, 0f), new Vector3(46f, 40f, 46f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Zero }, arrows: new[] { new Vector3(-19f, 1f, -13f), new Vector3(19f, 1f, 13f) });
            zone.drawOutline = false; // the arena edge already shows where it ends

            var start = Checkpoint(ctx, null, new Vector3(-15f, 0.03f, -3f), 0, "Arena Gate", new Vector3(6f, 4f, 8f), isStart: true, spawnZ: 0f);
            var barrier = LevelArt.Box(ctx.Group("Arena"), "ExitBarrier", new Vector3(21.3f, 5f, 0f), new Vector3(0.5f, 10f, 7f), field, true);
            PointLight(barrier.transform, barrier.transform.position + Vector3.left * 2f, new Color(1f, 0.1f, 0.3f), 10f, 2.5f);

            var sweep = BuildSweep(ctx, danger);
            var strikes = new List<WardenStrike>();
            for (int i = 0; i < 4; i++) strikes.Add(BuildStrike(ctx, danger));
            BuildBoss(ctx, zone, tiles, strikes, sweep, barrier, hull, ember, danger);
            BuildEscape(ctx, tileMat, ember, safe);
            Backdrop(ctx);

            Hint(ctx, "The Warden", "B: freeze it (get close and face it). A: change gravity while it's frozen. Three hits.",
                new Vector3(-16f, 1.5f, 0f), new Vector3(3f, 4f, 8f), 9f);

            Astronaut(ctx, 0, start.GetSpawn(0));
            Astronaut(ctx, 1, start.GetSpawn(1));

            CoreObjects(ctx, 7, "STAGE 7 — THE WARDEN", HudObjective.ReturnToShip, "Destroy the Warden",
                "Your ship is just past this platform, the engine is in your hands, and the graveyard's old security machine has you cornered.\n\n" +
                "Nothing you have can scratch it, unless you strike together. B freezes it. A changes gravity while it's frozen, and the shift tears at its frame.\n\nThree hits. Then run.",
                "YOU MADE IT", "The Warden is scrap and the ship is right there. Fit the engine, and it's time to go home.",
                -25f, final: true);
            Save(ctx, ScenePath);
        }

        static CrumblePlatform[] BuildArena(StageContext ctx, Material tileMat, Material ember)
        {
            var group = ctx.Group("Arena");
            var drop = new List<CrumblePlatform>();
            for (int ix = -3; ix <= 3; ix++)
                for (int iz = -3; iz <= 3; iz++)
                {
                    if (Mathf.Abs(ix) == 3 && Mathf.Abs(iz) == 3) continue; // rounded corners
                    var at = new Vector3(ix * TileSize, -0.5f, iz * TileSize);
                    var tile = LevelArt.Box(group, $"Tile_{ix}_{iz}", at, new Vector3(TileSize - 0.1f, 1f, TileSize - 0.1f), tileMat, true);
                    LevelArt.Box(tile.transform, "Rim", at + Vector3.up * 0.52f, new Vector3(TileSize - 0.5f, 0.03f, 0.1f), ember);
                    bool keep = (ix == -3 && iz == 0) || (ix == 3 && iz == 0) || (ix == -2 && iz == 0);
                    if (keep) continue; // the way in and out never drops
                    var c = tile.AddComponent<CrumblePlatform>();
                    c.section = 0;
                    c.triggerOnStep = false;
                    c.regenerateAfter = 6f;
                    drop.Add(c);
                }

            // invisible walls, with an opening on the +X side for the exit
            const float E = 21.5f;
            SolidBox(group, "WallW", new Vector3(-E, 15f, 0f), new Vector3(1f, 60f, 44f));
            SolidBox(group, "WallN", new Vector3(0f, 15f, E), new Vector3(44f, 60f, 1f));
            SolidBox(group, "WallS", new Vector3(0f, 15f, -E), new Vector3(44f, 60f, 1f));
            SolidBox(group, "WallE1", new Vector3(E, 15f, 12.5f), new Vector3(1f, 60f, 18f));
            SolidBox(group, "WallE2", new Vector3(E, 15f, -12.5f), new Vector3(1f, 60f, 18f));
            return drop.ToArray();
        }

        static GameObject BuildSweep(StageContext ctx, Material danger)
        {
            var pivot = new GameObject("SweepBeam");
            pivot.transform.SetParent(ctx.Group("Arena"), false);
            pivot.transform.position = new Vector3(0f, 0.65f, 0f);
            var spin = pivot.AddComponent<Rotator>();
            spin.axis = Vector3.up;
            spin.degreesPerSecond = 32f;
            var arm = LevelArt.Box(pivot.transform, "Beam", pivot.transform.position, new Vector3(36f, 0.28f, 0.4f), danger);
            var box = arm.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1f, 2.6f, 1.6f);
            var h = arm.AddComponent<Hazard>();
            h.effect = HazardEffect.Knockback;
            h.knockSpeed = 8f;
            h.knockUp = 6f;
            return pivot;
        }

        static WardenStrike BuildStrike(StageContext ctx, Material danger)
        {
            var root = new GameObject("Strike");
            root.transform.SetParent(ctx.Group("Warden"), false);
            var disc = LevelArt.Shape(PrimitiveType.Cylinder, root.transform, "Disc", Vector3.zero, new Vector3(5.2f, 0.02f, 5.2f), danger);
            var beam = LevelArt.Shape(PrimitiveType.Cylinder, root.transform, "Beam", new Vector3(0f, 15f, 0f), new Vector3(4f, 15f, 4f), danger);
            var hitGo = new GameObject("Hit");
            hitGo.transform.SetParent(root.transform, false);
            hitGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var sphere = hitGo.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 2.5f;
            var h = hitGo.AddComponent<Hazard>();
            h.effect = HazardEffect.Knockback;
            h.knockSpeed = 9f;
            h.knockUp = 7f;
            h.armed = false;
            var s = root.AddComponent<WardenStrike>();
            s.disc = disc;
            s.beam = beam;
            s.hit = h;
            return s;
        }

        static void BuildBoss(StageContext ctx, GravityZone zone, CrumblePlatform[] tiles, List<WardenStrike> strikes, GameObject sweep,
            GameObject barrier, Material hull, Material ember, Material danger)
        {
            var go = new GameObject("Warden");
            go.transform.SetParent(ctx.Group("Warden"), false);
            go.transform.position = new Vector3(0f, 5f, 0f);
            go.AddComponent<SphereCollider>().radius = 1.9f;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var shell = go.AddComponent<Nullifiable>();
            shell.displayName = "The Warden";
            shell.holdDuration = 4f;
            shell.allowSteer = false;

            var model = new GameObject("Model");
            model.transform.SetParent(go.transform, false);
            var c = go.transform.position;
            LevelArt.Shape(PrimitiveType.Sphere, model.transform, "Body", c, Vector3.one * 3.6f, hull);
            LevelArt.Shape(PrimitiveType.Sphere, model.transform, "Eye", c + new Vector3(0f, 0f, -1.45f), Vector3.one * 1.3f, danger);
            var ring = new GameObject("Ring");
            ring.transform.SetParent(model.transform, false);
            ring.transform.position = c;
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f * Mathf.Deg2Rad;
                var seg = LevelArt.Box(ring.transform, "Segment", c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3f, new Vector3(1.5f, 0.3f, 0.5f), i % 2 == 0 ? ember : hull);
                seg.transform.rotation = Quaternion.Euler(0f, -i * 36f, 0f);
            }
            var spin = ring.AddComponent<Rotator>();
            spin.axis = Vector3.up;
            spin.degreesPerSecond = 60f;
            PointLight(model.transform, c + Vector3.down * 2f, new Color(1f, 0.25f, 0.1f), 14f, 3f);

            var lights = new GameObject[3];
            for (int i = 0; i < lights.Length; i++)
                lights[i] = LevelArt.Shape(PrimitiveType.Sphere, model.transform, "HealthLight", c + new Vector3(-0.8f + i * 0.8f, 1.95f, 0f), Vector3.one * 0.5f, ember);

            var boss = go.AddComponent<WardenBoss>();
            boss.section = 0;
            boss.arenaZone = zone;
            boss.model = model;
            boss.healthLights = lights;
            boss.strikes = strikes.ToArray();
            boss.sweepArm = sweep;
            boss.tiles = tiles;
            boss.exitBarrier = barrier;
            boss.arenaCenter = Vector3.zero;
        }

        static void BuildEscape(StageContext ctx, Material tileMat, Material ember, Material safe)
        {
            var group = ctx.Group("Escape");
            // a falling bridge: each piece drops soon after it's stepped on, and comes back for a second try
            for (int i = 0; i < 3; i++)
            {
                var piece = LevelArt.Box(group, "BridgePiece", new Vector3(24.5f + i * 6f, -0.5f, 0f), new Vector3(5.9f, 1f, 6f), tileMat, true);
                LevelArt.Box(piece.transform, "Rim", piece.transform.position + Vector3.up * 0.52f, new Vector3(5f, 0.03f, 0.1f), ember);
                var c = piece.AddComponent<CrumblePlatform>();
                c.section = 1;
                c.crackTime = 0.9f;
                c.regenerateAfter = 5f;
            }
            Checkpoint(ctx, null, new Vector3(24f, 0.03f, -2.4f), 1, "Bridge", new Vector3(3f, 4f, 7f), spawnX: 23.5f);

            LevelArt.Box(group, "PadDeck", new Vector3(45f, -0.5f, 0f), new Vector3(12f, 1f, 7f), tileMat, true);
            JumpPadAt(ctx, "Escape", new Vector3(49.5f, 0.02f, 0f), 15f, new Vector3(7f, 0f, 0f));
            LevelArt.Box(group, "HighDeck", new Vector3(58.25f, 2.5f, 0f), new Vector3(12.5f, 1f, 7f), tileMat, true); // players carry the engine, so they land short

            ShuttleAt(ctx, "Escape", "Platform_Metal_4x4", new Vector3(68f, 2.94f, 0f), new Vector3(84f, 2.94f, 0f), 3.2f, 1f);

            LevelArt.Box(group, "ShipDeck", new Vector3(100f, 2.5f, 0f), new Vector3(24f, 1f, 12f), tileMat, true);
            LevelArt.Box(group, "ShipDeckLight", new Vector3(100f, 3.02f, -5.5f), new Vector3(22f, 0.03f, 0.15f), safe);
            var ship = Spawn("Spaceship", new Vector3(102f, 5.5f, 10f), new Vector3(0f, -90f, 0f), group, Vector3.one * 1.4f, "HomeShip");
            NoColliders(ship);
            Finish(ctx, "Escape", new Vector3(103f, 3f, 0f), "The Ship");

            // keep the run on the route
            foreach (float z in new[] { -6.5f, 6.5f })
                SolidBox(group, "EscapeWall", new Vector3(65f, 15f, z), new Vector3(90f, 60f, 1f));
            SolidBox(group, "EscapeEnd", new Vector3(112.5f, 15f, 0f), new Vector3(1f, 60f, 14f));
        }

        static void Backdrop(StageContext ctx)
        {
            var group = ctx.Group("Backdrop");
            var rng = new System.Random(77);
            float F(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            string[] pieces = { "Monolith_A", "Rock_Debris_L", "Monolith_Broken", "Wreck_Hull_A", "Mesa_A", "Monolith_B", "Station_Chunk_B", "Rock_Debris_M" };
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * Mathf.Deg2Rad;
                float r = F(45f, 95f);
                var at = new Vector3(30f + Mathf.Cos(a) * r * 1.3f, F(-28f, 14f), Mathf.Sin(a) * r);
                if (Mathf.Abs(at.z) < 26f) at.z = Mathf.Sign(at.z == 0f ? 1f : at.z) * (26f + F(0f, 30f));
                var go = Spawn(pieces[i % pieces.Length], at, new Vector3(F(-20f, 20f), F(0f, 360f), F(-20f, 20f)), group, Vector3.one * F(1.6f, 3.6f));
                NoColliders(go);
                foreach (var rend in go.GetComponentsInChildren<Renderer>()) rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // a wrecked ring slowly turning around the whole arena
            var pivot = new GameObject("RingPivot");
            pivot.transform.SetParent(group, false);
            pivot.transform.position = new Vector3(0f, 6f, 0f);
            var spin = pivot.AddComponent<Rotator>();
            spin.axis = Vector3.up;
            spin.degreesPerSecond = 2.5f;
            var ring = Spawn("Ring_Walkway", pivot.transform.position, new Vector3(0f, 0f, 90f), pivot.transform, Vector3.one * 2.2f);
            NoColliders(ring);
        }
    }
}
