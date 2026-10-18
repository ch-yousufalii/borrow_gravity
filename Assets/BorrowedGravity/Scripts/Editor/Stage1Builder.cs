using UnityEditor;
using UnityEngine;
using static BorrowGravity.EditorTools.BuilderKit;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Stage 1 — The Shattered Orbit (PRD 5.1–5.8), built on the art kit layout Layouts/Stage1.json.
    /// Route runs along +X; lane depth is roughly z = -2.2..2.2.
    /// </summary>
    public static class Stage1Builder
    {
        public const string ScenePath = "Assets/Scenes/Stage1.unity";

        public static void Build()
        {
            var ctx = BeginStage("Stage1");
            var metal = Lit("BracketMetal", new Color(0.32f, 0.3f, 0.36f), null, 0.5f);

            // ---------- 5.1 Broken Launch Platform ----------
            var start = Checkpoint(ctx, ctx.Piece("5.1", "Checkpoint_Beacon"), new Vector3(0.6f, 0f, -1.8f), 0, "Launch Platform",
                new Vector3(8f, 4f, 6f), isStart: true, spawnX: 2.8f);
            DeckCollider(ctx.Piece("5.1", "Docking_Platform"));
            NoColliders(ctx.Piece("5.1", "Island_L"));
            var ship = ctx.Piece("5.1", "Spaceship");
            ship.transform.position = new Vector3(4f, 2.6f, 4.4f);
            NoColliders(ship);

            var tilt = Adopt(ctx, ctx.Piece("5.1", "Platform_Weak_4x4"), "5.1 Launch", "TiltingPlatform");
            tilt.AddComponent<TiltPlatform>();

            // the loose "slab": a 1.1 m metal plate, low enough to use as a step (a jump clears ~1.4 m)
            Remove(ctx.Piece("5.1", "Crate_Heavy_2m"));
            var slab = Spawn("Platform_Metal_2x2", new Vector3(11.3f, 8.6f, 0f), Vector3.zero, ctx.Group("5.1 Launch"), null, "LooseSlab");
            MakeDynamic(slab, 40f);
            Gravity(slab, ZeroGravityResponse.Hover);
            Nullify(slab, "Loose Slab");
            Respawn(slab, 0);
            slab.AddComponent<PlateWeight>();
            Zone(ctx, "Slab Field", 0, new Vector3(12.8f, 6.6f, 0f), new Vector3(6.8f, 8f, 4.4f), GravityMode.Zero,
                new[] { GravityMode.Zero, GravityMode.Down }, players: false,
                arrows: new[] { new Vector3(10.2f, 4.2f, -1.7f), new Vector3(15.4f, 4.2f, -1.7f) });

            var ledge = ctx.Piece("5.2", "Island_S", 0);
            ledge.transform.position = new Vector3(18.2f, 1.8f, 0.35f);

            Hint(ctx, "5.1  Broken Launch Platform",
                "A slab floats in zero-g over the tilting platform.\nA: press E to switch the zone's gravity DOWN.  B: face the slab, tap Right Shift to freeze it, HOLD to steer it next to the ledge.",
                new Vector3(7f, 2f, 0f), new Vector3(3f, 6f, 6f), 10f);

            // ---------- 5.2 Floating Debris Field ----------
            Checkpoint(ctx, null, new Vector3(18.6f, 1.8f, -1.1f), 1, "Debris Field", new Vector3(3f, 4f, 4.4f), spawnX: 18.2f);
            Sweeper(ctx, "Rock_Debris_L", 1f, new Vector3(27.5f, 2.4f, 0f), 2.6f, 6.5f, 0f, ctx.Piece("5.2", "Warning_Light", 0));
            Sweeper(ctx, "Rock_Debris_L", 0.8f, new Vector3(36.6f, 3.0f, 0f), 2.4f, 7f, 3.2f, ctx.Piece("5.2", "Warning_Light", 1));
            // extra fun on the debris field: a pulsing laser fence and a spinning sweeper arm
            LaserAt(ctx, "5.2 Debris", 22.5f, 1.2f, 2.6f, -2.2f, 2.2f, 1.4f, 2.0f, 0.5f);
            SweeperArm(ctx, "5.2 Debris", new Vector3(29.4f, 0.75f, 0f), 5.2f, 65f);
            Hint(ctx, "5.2  Floating Debris Field", "Drifting wreckage, a pulsing laser fence and a spinning arm — watch the warning lights, time your dash, jump the arm.",
                new Vector3(21f, 3f, 0f), new Vector3(2f, 6f, 6f));

            // ---------- 5.3 The Cable Drop ----------
            Checkpoint(ctx, ctx.Piece("5.3", "Checkpoint_Beacon"), new Vector3(42f, 0f, -1.6f), 2, "Cable Drop", new Vector3(4f, 4f, 4.4f));
            var weight = Adopt(ctx, ctx.Piece("5.3", "Counterweight"), "5.3 Cable", "Counterweight");
            weight.transform.SetPositionAndRotation(new Vector3(45.6f, 6.2f, 0f), Quaternion.identity);
            MakeDynamic(weight, 6f);
            Gravity(weight, ZeroGravityResponse.Hover);
            Nullify(weight, "Counterweight");
            Respawn(weight, 2);
            weight.AddComponent<SocketKey>().key = "counterweight";
            var arrow0 = ctx.Piece("5.3", "Gravity_Arrow");
            arrow0.transform.position = new Vector3(47.4f, 3.6f, -1.7f);
            Zone(ctx, "Counterweight Field", 2, new Vector3(46.5f, 6.5f, 0f), new Vector3(7f, 7f, 4.4f), GravityMode.Zero,
                new[] { GravityMode.Zero, GravityMode.Down }, players: false,
                arrows: new[] { new Vector3(44.0f, 3.6f, -1.7f) }, existingArrows: new[] { arrow0.transform });

            var pad = Adopt(ctx, ctx.Piece("5.3", "GravityZone_Pad"), "5.3 Cable", "ClampPad");
            pad.transform.position = new Vector3(47.6f, 0f, 0f);
            NoColliders(pad);
            var socketGo = TriggerBox(ctx.Group("5.3 Cable"), "ClampSocket", new Vector3(47.6f, 1.0f, 0f), new Vector3(1.9f, 2f, 3f));
            var socket = socketGo.AddComponent<Socket>();
            socket.section = 2;
            socket.key = "counterweight";
            socket.indicator = pad.GetComponent<Renderer>();
            socket.acceptedMessage = "Counterweight clamped!";
            var snap = new GameObject("Snap").transform;
            snap.SetParent(socketGo.transform, false);
            snap.position = new Vector3(47.6f, 2.07f, 0f);
            socket.snapPoint = snap;

            var cable0 = ctx.Piece("5.3", "Cable_Hanging", 0);
            cable0.transform.position = new Vector3(44.2f, 10.5f, 0.8f);
            NoColliders(cable0);
            var swingRoot = new GameObject("SwingingCable");
            swingRoot.transform.SetParent(ctx.Group("5.3 Cable"), false);
            swingRoot.transform.position = new Vector3(54f, 10.5f, 0f);
            var cable = ctx.Piece("5.3", "Cable_Hanging", 1);
            cable.transform.SetParent(swingRoot.transform, false);
            cable.transform.localPosition = Vector3.zero;
            cable.transform.localRotation = Quaternion.identity;
            cable.transform.localScale = new Vector3(1.2f, 1.25f, 1.2f);
            GameObjectUtility.SetStaticEditorFlags(cable, 0);
            NoColliders(cable);
            var hz = HazardBox(swingRoot.transform, "CableHit", swingRoot.transform.position + new Vector3(0f, -8.3f, 0f), new Vector3(1.2f, 4f, 1.8f), HazardEffect.Knockback);
            hz.useOwnVelocity = true;
            hz.knockSpeed = 12f;
            hz.knockUp = 6f;
            var pend = swingRoot.AddComponent<Pendulum>();
            pend.section = 2;
            pend.amplitude = 30f;
            pend.period = 4.4f;
            pend.stopSignal = socket;
            Hint(ctx, "5.3  The Cable Drop",
                "A damaged cable swings across the platform. Pin it with the counterweight!\nB: steer the floating counterweight over the glowing clamp pad.  A: switch its zone DOWN.",
                new Vector3(41f, 2f, 0f), new Vector3(2f, 5f, 6f), 10f);

            // ---------- 5.4 The Shaking Bridge ----------
            for (int i = 0; i < 3; i++)
            {
                var weak = Adopt(ctx, ctx.Piece("5.4", "Bridge_Section_Weak", i), "5.4 Bridge", "WeakPanel" + i);
                var cp = weak.AddComponent<CrumblePlatform>();
                cp.section = 3;
                cp.crackTime = 1.2f;
                cp.regenerateAfter = 4.5f;
            }
            Checkpoint(ctx, null, new Vector3(85.6f, 0.55f, -1.3f), 3, "Broken Bridge", new Vector3(3.5f, 4f, 4.4f));
            var loose = Adopt(ctx, ctx.Piece("5.4", "Bridge_Section", 4), "5.4 Bridge", "LooseBridgeSection");
            loose.transform.SetPositionAndRotation(new Vector3(90.8f, 4.2f, 0f), Quaternion.identity);
            MakeDynamic(loose, 15f, RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation);
            Gravity(loose, ZeroGravityResponse.Hover);
            var ln = Nullify(loose, "Bridge Section", 8f);
            ln.allowSteer = false;
            Respawn(loose, 3, -20f);
            loose.AddComponent<SocketKey>().key = "bridge";
            foreach (float bx in new[] { 89.2f, 92.4f })
            {
                var br = SolidBox(ctx.Group("5.4 Bridge"), "WeakBracket", new Vector3(bx, 0f, 0f), new Vector3(0.9f, 0.3f, 3f), metal);
                var crumble = br.AddComponent<CrumblePlatform>();
                crumble.section = 3;
                crumble.triggerOnStep = false;
                crumble.regenerateAfter = 4f;
                br.AddComponent<CrumbleOnLoad>().crackTime = 0.7f;
            }
            Zone(ctx, "Bridge Gap Field", 3, new Vector3(90.9f, 3f, 0f), new Vector3(5f, 10f, 4.4f), GravityMode.Zero,
                new[] { GravityMode.Zero, GravityMode.Down }, players: false,
                arrows: new[] { new Vector3(89.2f, 6.6f, -1.7f), new Vector3(92.6f, 6.6f, -1.7f) });
            Hint(ctx, "5.4  The Shaking Bridge", "Weak panels crack as soon as you step on them — keep moving!",
                new Vector3(57f, 2f, 0f), new Vector3(2f, 5f, 6f));
            Hint(ctx, "Broken Gap",
                "The last section floats loose. A: switch its zone DOWN to drop it onto the brackets.  B: freeze it the moment it lands — the brackets snap fast!",
                new Vector3(86.5f, 2f, 0f), new Vector3(2f, 5f, 6f), 10f);

            // ---------- 5.5 Gravity Flip Tower ----------
            Checkpoint(ctx, ctx.Piece("5.5", "Checkpoint_Beacon"), new Vector3(94.6f, -0.2f, -1.6f), 4, "Gravity Flip Tower", new Vector3(4f, 4f, 4.4f));
            ctx.Piece("5.5", "Platform_Metal_2x2", 1).transform.position = new Vector3(100.9f, 9.5f, 0.8f);
            ctx.Piece("5.5", "Platform_Metal_2x2", 2).transform.position = new Vector3(98.4f, 15.5f, 0.8f);
            var tower = Zone(ctx, "Flip Tower", 4, new Vector3(98.4f, 10.5f, -0.1f), new Vector3(7.8f, 21f, 4.6f), GravityMode.Down,
                null, controllable: false, objects: false,
                existingArrows: new[] { ctx.Piece("5.5", "Gravity_Arrow", 0).transform, ctx.Piece("5.5", "Gravity_Arrow", 1).transform, ctx.Piece("5.5", "Gravity_Arrow", 2).transform });
            tower.autoCycle = true;
            tower.autoSequence = new[] { GravityMode.Down, GravityMode.Zero };
            tower.autoDurations = new[] { 5f, 3.6f };
            tower.autoWarning = 2f;
            tower.playerZeroGLift = 2.6f;
            var wall = Spawn("Monolith_A", new Vector3(105.6f, -0.5f, 0f), new Vector3(0f, 20f, 0f), ctx.Group("5.5 Tower"), new Vector3(1f, 1.6f, 1.25f), "TowerWall");
            Spawn("Platform_Metal_6x3", new Vector3(104.5f, 16.2f, 0f), Vector3.zero, ctx.Group("5.5 Tower"), null, "TowerExit");
            Hint(ctx, "5.5  Gravity Flip Tower",
                "The tower's gravity shifts on its own. When it goes weightless you float up — steer over the next grip before gravity returns. Exit from the top!",
                new Vector3(95.5f, 1.5f, 0f), new Vector3(2f, 5f, 6f), 10f);

            // ---------- 5.6 Falling Wreck ----------
            Checkpoint(ctx, null, new Vector3(108.6f, 0f, -1.4f), 5, "Falling Wreck", new Vector3(3f, 4f, 4.4f), spawnX: 109.6f);
            var island = Adopt(ctx, ctx.Piece("5.6", "Island_M", 1), "5.6 Wreck", "ImpactIsland");
            var islandRb = MakeDynamic(island, 30f, RigidbodyConstraints.FreezeRotation, kinematic: true);
            Gravity(island, ZeroGravityResponse.Float, 2f, 1.4f, 7f);
            var inull = Nullify(island, "Drifting Island", 9f, false, 2.6f);
            inull.enabled = false;
            Respawn(island, 5, -25f);
            SolidBox(ctx.Group("5.6 Wreck"), "RubbleLedge", new Vector3(118f, -7.6f, 7f), new Vector3(6f, 1f, 5f));
            Spawn("Rock_Debris_M", new Vector3(116f, -7f, 8.6f), new Vector3(20, 40, 10), ctx.Group("5.6 Wreck"));
            Spawn("Rock_Debris_M", new Vector3(120.4f, -7.2f, 5.6f), new Vector3(-10, 120, 30), ctx.Group("5.6 Wreck"));
            Zone(ctx, "Impact Field", 5, new Vector3(118f, 1.5f, 3.5f), new Vector3(8f, 14f, 11f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Zero }, players: false,
                arrows: new[] { new Vector3(114.8f, 3.6f, -1.7f), new Vector3(121.2f, 3.6f, -1.7f) });

            var wreck = Adopt(ctx, ctx.Piece("5.6", "Wreck_Hull_A"), "5.6 Wreck", "FallingWreck");
            NoColliders(wreck);
            HazardBox(wreck.transform, "WreckHit", wreck.transform.position + new Vector3(0f, -2f, 0f), new Vector3(5f, 5f, 5f), HazardEffect.Respawn, false);
            var fallTrigger = TriggerBox(ctx.Group("5.6 Wreck"), "WreckTrigger", new Vector3(110.5f, 2f, 0f), new Vector3(3f, 6f, 6f));
            var fe = fallTrigger.AddComponent<FallingEvent>();
            fe.section = 5;
            fe.faller = wreck.transform;
            fe.fallStart = new Vector3(118f, 19f, 0.5f);
            fe.fallEnd = new Vector3(118f, 2f, 0.5f);
            fe.endEuler = new Vector3(10f, -30f, 30f);
            fe.warnTime = 3f;
            fe.shadowMarker = ShadowMarker(ctx.Group("5.6 Wreck"), new Vector3(118f, 0.45f, 0f), 3.4f);
            fe.alsoDrop = new[] { island.transform };
            fe.alsoDropOffset = new Vector3(0f, -3.5f, 7f);
            fe.releaseAfter = new[] { islandRb };
            fe.message = "Wreckage breaking loose overhead — stay clear of the red zone!";
            Hint(ctx, "5.6  Falling Wreck",
                "The impact knocked the island out of reach!\nA: zero its zone so it floats up.  B: freeze it level with the path and steer it back in line.",
                new Vector3(113.2f, 2f, 0f), new Vector3(1.2f, 5f, 6f), 10f);

            // ---------- 5.7 Timed Platform Run ----------
            Remove(ctx.Piece("5.7", "Platform_Metal_2x2", 0));
            Remove(ctx.Piece("5.7", "Platform_Metal_2x2", 1));
            Spawn("Platform_Metal_6x3", new Vector3(124.5f, 0.6f, 0f), Vector3.zero, ctx.Group("5.7 Run"), null, "RunStart");
            Checkpoint(ctx, ctx.Piece("5.7", "Checkpoint_Beacon"), new Vector3(123.0f, 0.6f, -1.2f), 6, "Timed Platform Run", new Vector3(5f, 4f, 4.4f), spawnX: 124.2f);
            var chain = new System.Collections.Generic.List<CrumblePlatform>();
            foreach (int i in new[] { 2, 3, 5, 6 })
            {
                var p = Adopt(ctx, ctx.Piece("5.7", "Platform_Metal_2x2", i), "5.7 Run", "PowerPlatform" + i);
                var cp = p.AddComponent<CrumblePlatform>();
                cp.section = 6;
                cp.triggerOnStep = false;
                cp.regenerateAfter = 5f;
                chain.Add(cp);
            }
            var runTrigger = TriggerBox(ctx.Group("5.7 Run"), "RunTrigger", new Vector3(130.8f, 2.6f, 0f), new Vector3(2.4f, 2f, 3f));
            var tpd = runTrigger.AddComponent<TimedPowerDown>();
            tpd.section = 6;
            tpd.platforms = chain.ToArray();
            tpd.interval = 1.6f;
            tpd.warnTime = 1.4f;
            tpd.startDelay = 0.6f;
            tpd.warningText = "Platforms powering down — run!";
            var key = Adopt(ctx, ctx.Piece("5.7", "Platform_Metal_2x2", 4), "5.7 Run", "KeyPlatform");
            key.transform.SetPositionAndRotation(new Vector3(137.6f, -4.06f, 4.5f), Quaternion.identity);
            MakeDynamic(key, 8f);
            Gravity(key, ZeroGravityResponse.Float, 2f, 1.5f, 7.5f);
            Nullify(key, "Key Platform", 9f, false, 2.8f);
            Respawn(key, 6, -20f);
            SolidBox(ctx.Group("5.7 Run"), "KeyRest", new Vector3(137.6f, -5.6f, 4.5f), new Vector3(3f, 1f, 3f));
            Zone(ctx, "Run Field", 6, new Vector3(137.6f, 1.5f, 3.2f), new Vector3(4.4f, 15f, 4.2f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Zero }, players: false,
                arrows: new[] { new Vector3(135.8f, 4.6f, 1.6f), new Vector3(139.4f, 4.6f, 1.6f) });
            Hint(ctx, "5.7  Timed Platform Run",
                "These platforms power down one by one once someone steps on the first. Set up the key platform first:\nA zeroes its zone, B freezes it at the right height and steers it into the gap — then run together!",
                new Vector3(124.5f, 2f, 0f), new Vector3(3f, 5f, 6f), 11f);

            // ---------- 5.8 Station Airlock ----------
            Checkpoint(ctx, ctx.Piece("5.8", "Checkpoint_Beacon"), new Vector3(148.2f, 3.9f, -1.3f), 7, "Station Airlock", new Vector3(3f, 4f, 4.4f), spawnX: 148.6f);
            // the station walkway is broken: a short platform, a drifting fragment, then the airlock deck — jump it
            Remove(ctx.Piece("5.8", "Platform_Metal_6x3", 0));
            Remove(ctx.Piece("5.8", "Platform_Metal_6x3", 1));
            Spawn("Platform_Metal_4x4", new Vector3(149.2f, 3.9f, 0f), Vector3.zero, ctx.Group("5.8 Airlock"), null, "BrokenWalkwayA");
            var frag = Spawn("Platform_Metal_2x2", new Vector3(153.3f, 3.7f, 0f), new Vector3(0f, 0f, -4f), ctx.Group("5.8 Airlock"), null, "WalkwayFragment");
            Spawn("Platform_Metal_4x4", new Vector3(158.6f, 3.9f, 0f), Vector3.zero, ctx.Group("5.8 Airlock"), null, "BrokenWalkwayB");
            Spawn("Bridge_End_Broken", new Vector3(151.9f, 3.0f, 2.6f), new Vector3(20f, 10f, -25f), ctx.Group("5.8 Airlock"), Vector3.one * 0.8f, "BrokenEdgeDebris");
            ctx.Piece("5.8", "Pressure_Plate", 0).transform.position = new Vector3(149.8f, 3.9f, -0.6f);
            var plates = new PressurePlate[2];
            for (int i = 0; i < 2; i++)
            {
                var plateMesh = Adopt(ctx, ctx.Piece("5.8", "Pressure_Plate", i), "5.8 Airlock", "Plate" + i);
                NoColliders(plateMesh);
                var tgo = TriggerBox(ctx.Group("5.8 Airlock"), "PlateTrigger" + i, plateMesh.transform.position + new Vector3(0f, 1f, 0f), new Vector3(1.9f, 2f, 2.2f));
                var pp = tgo.AddComponent<PressurePlate>();
                pp.section = 7;
                pp.plateTop = plateMesh.transform;
                pp.indicator = plateMesh.GetComponent<Renderer>();
                pp.label = "Airlock Plate";
                plates[i] = pp;
            }
            var latch = Spawn("Vault_LockBar", new Vector3(163f, 7.0f, -0.5f), Vector3.zero, ctx.Group("5.8 Airlock"), new Vector3(0.6f, 1f, 1f), "DoorLatch");
            MakeDynamic(latch, 20f, RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ);
            Gravity(latch, ZeroGravityResponse.Hover);
            Nullify(latch, "Door Latch", 8f, false, 2.5f);
            Respawn(latch, 7);
            latch.AddComponent<SocketKey>().key = "latch";
            foreach (float hx in new[] { 161.4f, 164.5f, 167f })
                SolidBox(ctx.Group("5.8 Airlock"), "LatchHook", new Vector3(hx, 6.75f, -0.5f), new Vector3(0.8f, 0.3f, 1.2f), metal);
            SolidBox(ctx.Group("5.8 Airlock"), "LatchBracket", new Vector3(162f, 8.0f, -0.5f), new Vector3(1f, 0.5f, 1.2f), metal);
            SolidBox(ctx.Group("5.8 Airlock"), "LatchCeiling", new Vector3(164.3f, 10.85f, -0.5f), new Vector3(8f, 0.5f, 1.4f), metal);
            Zone(ctx, "Latch Field", 7, new Vector3(164.3f, 8.5f, -0.5f), new Vector3(8f, 4f, 2f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Up }, players: false,
                arrows: new[] { new Vector3(160.6f, 9.4f, -1.6f), new Vector3(168f, 9.4f, -1.6f) });
            var slot = TriggerBox(ctx.Group("5.8 Airlock"), "LatchSlot", new Vector3(165.6f, 9.9f, -0.5f), new Vector3(5f, 1.4f, 1.6f));
            var latchSocket = slot.AddComponent<Socket>();
            latchSocket.section = 7;
            latchSocket.key = "latch";
            latchSocket.lockObject = true;
            latchSocket.acceptedMessage = "Latch raised!";
            var gate = new GameObject("AirlockLogic").AddComponent<LogicGate>();
            gate.transform.SetParent(ctx.Group("5.8 Airlock"), false);
            gate.section = 7;
            gate.inputs = new SignalSource[] { plates[0], plates[1], latchSocket };
            gate.latch = true;
            var doorL = Adopt(ctx, ctx.Piece("5.8", "Airlock_Door_L"), "5.8 Airlock");
            var doorR = Adopt(ctx, ctx.Piece("5.8", "Airlock_Door_R"), "5.8 Airlock");
            var door = gate.gameObject.AddComponent<Door>();
            door.section = 7;
            door.source = gate;
            door.parts = new[] { doorL.transform, doorR.transform };
            door.openOffsets = new[] { new Vector3(-3f, 0f, 0f), new Vector3(3f, 0f, 0f) };
            door.openMessage = "The airlock is open — get inside together!";
            var light = ctx.Piece("5.8", "Warning_Light");
            var sl = light.AddComponent<SignalLight>();
            sl.source = gate;
            sl.renderers = new[] { light.GetComponent<Renderer>() };
            sl.lightSource = PointLight(light.transform, light.transform.position + Vector3.up * 2.6f, Color.red, 6f, 3f);
            foreach (float z in new[] { 4.6f, 8.6f })
                Spawn("Station_Floor_4x4", new Vector3(163f, 3.9f, z), Vector3.zero, ctx.Group("5.8 Airlock"), new Vector3(2.5f, 1f, 1f), "AirlockFloor");
            var goal = TriggerBox(ctx.Group("5.8 Airlock"), "Goal", new Vector3(163f, 5.5f, 7f), new Vector3(7f, 3f, 5f));
            goal.AddComponent<GoalTrigger>();
            Hint(ctx, "5.8  Station Airlock",
                "The airlock needs both pressure plates held at once — and the heavy latch slid out of its bracket (B) and lifted into its slot (A).",
                new Vector3(150f, 5f, 0f), new Vector3(2f, 5f, 6f), 10f);

            // ---------- lane walls ----------
            Wall(ctx, -2f, 178f, -2.2f);
            Wall(ctx, -2f, 113.5f, 2.2f);
            Wall(ctx, 122.5f, 135.2f, 2.2f);
            Wall(ctx, 140.0f, 158.4f, 2.2f);
            Wall(ctx, 167.6f, 178f, 2.2f);
            SolidBox(ctx.Group("InvisibleWalls"), "AirlockSideL", new Vector3(158.2f, 6f, 6.5f), new Vector3(1f, 12f, 9f));
            SolidBox(ctx.Group("InvisibleWalls"), "AirlockSideR", new Vector3(167.8f, 6f, 6.5f), new Vector3(1f, 12f, 9f));
            SolidBox(ctx.Group("InvisibleWalls"), "AirlockBack", new Vector3(163f, 6f, 11.2f), new Vector3(10f, 12f, 1f));
            EndWall(ctx, -1.6f);
            EndWall(ctx, 178f);

            // ---------- players, camera, systems ----------
            Astronaut(ctx, 0, start.GetSpawn(0));
            Astronaut(ctx, 1, start.GetSpawn(1));
            CoreObjects(ctx, 1, "STAGE 1 — THE SHATTERED ORBIT", HudObjective.ReachStation, "Reach the Station",
                "Your ship's engine has died near a ruined orbital structure. The only replacement is inside an abandoned station — " +
                "on the far side of a field of shattered wreckage.\n\nA bends gravity inside the glowing zones. B can freeze one object at a time and steer it.\nNeither of you can make it alone.",
                "STATION REACHED",
                "The airlock seals behind you. Somewhere inside this upside-down station is the engine vault...",
                -25f, times: new[] { TimeOfDay.At(TimeOfDay.Day(), 0f), TimeOfDay.At(TimeOfDay.Day(), 0.45f), TimeOfDay.At(TimeOfDay.Evening(), 1f) },
                routeStartX: 0f, routeEndX: 170f);
            Save(ctx, ScenePath);
        }

        static void Sweeper(StageContext ctx, string prefab, float scale, Vector3 crossing, float travel, float period, float offset, GameObject warn)
        {
            var group = ctx.Group("5.2 Debris");
            var root = new GameObject("DebrisSweeper");
            root.transform.SetParent(group, false);
            var mover = Spawn(prefab, crossing + new Vector3(0f, 0f, 45f), new Vector3(10f, 30f, 0f), root.transform, Vector3.one * scale, "Drifter");
            NoColliders(mover);
            var mf = mover.GetComponent<MeshFilter>();
            var size = mf != null ? Vector3.Scale(mf.sharedMesh.bounds.size, Vector3.one * scale) : Vector3.one * 3f;
            var h = HazardBox(mover.transform, "DrifterHit", mover.transform.position, size * 0.85f, HazardEffect.Respawn, false);
            var sw = root.AddComponent<PathSweeper>();
            sw.mover = mover.transform;
            sw.from = crossing + new Vector3(0f, 0f, 45f);
            sw.to = crossing + new Vector3(0f, 0f, -45f);
            sw.travelTime = travel;
            sw.period = period;
            sw.offset = offset;
            sw.warnRenderers = new[] { warn.GetComponent<Renderer>() };
            sw.warnLights = new[] { PointLight(warn.transform, warn.transform.position + Vector3.up * 2.6f, new Color(1f, 0.3f, 0.1f), 7f, 0f) };
        }
    }
}
