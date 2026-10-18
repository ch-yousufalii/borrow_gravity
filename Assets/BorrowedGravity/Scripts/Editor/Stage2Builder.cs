using System.Collections.Generic;
using UnityEngine;
using static BorrowGravity.EditorTools.BuilderKit;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Stage 2 — The Upside-Down Station (PRD 6.1–6.8). A long station corridor: floor at y≈0 for 6.1–6.2, y≈5 after.
    /// Back walls sit at z≈3.85, the front is open (invisible wall at z=-3.6).
    /// </summary>
    public static class Stage2Builder
    {
        public const string ScenePath = "Assets/Scenes/Stage2.unity";
        const float Upper = 5.03f;

        public static void Build()
        {
            var ctx = BeginStage("Stage2");
            var metal = Lit("BracketMetal", new Color(0.32f, 0.3f, 0.36f), null, 0.5f);
            var socketGlow = Lit("SocketGlow", new Color(0.3f, 0.3f, 0.35f), new Color(1f, 0.35f, 0.2f) * 2f);

            // ---------- 6.1 Rotating Entry Hall ----------
            var start = Checkpoint(ctx, ctx.Piece("6.1", "Checkpoint_Beacon"), new Vector3(5.2f, 0.03f, -2.6f), 0, "Entry Hall",
                new Vector3(6f, 4f, 7f), isStart: true, spawnX: 4.6f);
            // the floor tiles at x=10 become a slab that flips over every few seconds
            foreach (var tile in ctx.Pieces("6.1", "Station_Floor_4x4"))
                if (Mathf.Abs(tile.transform.position.x - 10f) < 0.1f) Remove(tile);
            var slab = Spawn("Station_Floor_4x4", new Vector3(10f, 0f, 0f), Vector3.zero, ctx.Group("6.1 Hall"), new Vector3(1f, 1f, 2.05f), "FlipSlab");
            var w1 = Spawn("Warning_Light", new Vector3(8.2f, 0.03f, -3.3f), Vector3.zero, ctx.Group("6.1 Hall"));
            var w2 = Spawn("Warning_Light", new Vector3(11.8f, 0.03f, -3.3f), Vector3.zero, ctx.Group("6.1 Hall"));
            var rot = slab.AddComponent<Rotator>();
            rot.section = 0;
            rot.axis = Vector3.right;
            rot.stepped = true;
            rot.stepAngle = 180f;
            rot.stepInterval = 6f;
            rot.stepDuration = 1.2f;
            rot.warnTime = 2f;
            rot.warnRenderers = new[] { w1.GetComponent<Renderer>(), w2.GetComponent<Renderer>() };
            var safePad = ctx.Piece("6.1", "GravityZone_Pad");
            safePad.transform.position = new Vector3(6f, 0.03f, 0f);
            NoColliders(safePad);

            // upside-down field holding a handrail against a ceiling
            Spawn("Station_Floor_4x4", new Vector3(16f, 5.6f, 0f), new Vector3(180f, 0f, 0f), ctx.Group("6.1 Hall"), new Vector3(2f, 1f, 2.05f), "Ceiling");
            var rail = Adopt(ctx, ctx.Piece("6.1", "Handrail_4m", 1), "6.1 Hall", "Handrail");
            rail.transform.SetPositionAndRotation(new Vector3(14f, 4.4f, 0f), Quaternion.identity);
            MakeDynamic(rail, 15f);
            Gravity(rail, ZeroGravityResponse.Hover);
            var rn = Nullify(rail, "Handrail", 9f);
            rn.steerLimits = new Bounds(new Vector3(15f, 3f, 0f), new Vector3(6f, 20f, 6f));
            Respawn(rail, 0);
            var a0 = ctx.Piece("6.1", "Gravity_Arrow", 0);
            var a1 = ctx.Piece("6.1", "Gravity_Arrow", 1);
            a0.transform.position = new Vector3(13f, 2.8f, 3.5f);
            a1.transform.position = new Vector3(19f, 2.8f, 3.5f);
            Zone(ctx, "Upside-Down Field", 0, new Vector3(16f, 4.4f, 0f), new Vector3(8f, 2.4f, 7.6f), GravityMode.Up,
                new[] { GravityMode.Up, GravityMode.Down }, players: false, existingArrows: new[] { a0.transform, a1.transform });
            // the exit ledge: the 6.2 floor at x=22 is raised
            foreach (var tile in ctx.Pieces("6.2", "Station_Floor_4x4"))
                if (Mathf.Abs(tile.transform.position.x - 22f) < 0.1f) tile.transform.position += new Vector3(0f, 2.2f, 0f);
            Hint(ctx, "6.1  Rotating Entry Hall",
                "The hall floor flips over every few seconds — wait on the green pad, then dash across when it settles.",
                new Vector3(5f, 1.5f, 0f), new Vector3(2f, 4f, 7f), 7f);
            Hint(ctx, "Upside-Down Field",
                "A handrail is stuck to the ceiling in an upside-down field. A: flip the field DOWN to drop it. B: steer it against the ledge to use as a step.",
                new Vector3(13f, 1.5f, 0f), new Vector3(1.5f, 4f, 7f), 10f);

            // ---------- 6.2 Cargo Lift Failure ----------
            Checkpoint(ctx, null, new Vector3(21f, 2.23f, -2.8f), 1, "Cargo Lift", new Vector3(3.8f, 4f, 7f), spawnX: 21.4f, spawnZ: 0.5f);
            var lift = Adopt(ctx, ctx.Piece("6.2", "Cargo_Lift"), "6.2 Lift", "CargoLift");
            lift.transform.rotation = Quaternion.identity;
            NoColliders(lift);
            var liftFloor = lift.AddComponent<BoxCollider>();
            liftFloor.center = new Vector3(0f, -0.12f, 0f);
            liftFloor.size = new Vector3(4.3f, 0.3f, 4.3f);
            var mover = lift.AddComponent<SignalMover>();
            mover.section = 1;
            mover.offPosition = new Vector3(27f, 3.4f, 0f);
            mover.onPosition = new Vector3(27f, Upper, 0f);
            mover.speed = 1.2f;
            mover.latch = true;
            mover.arrivedMessage = "Lift balanced — ride it up!";
            var scaleGo = TriggerBox(lift.transform, "WeightSensor", new Vector3(27f, 4.4f, 0f), new Vector3(4f, 2f, 4f));
            scaleGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            var scale = scaleGo.AddComponent<PressurePlate>();
            scale.section = 1;
            scale.acceptObjects = true;
            scale.acceptPlayers = false;

            var crate = Adopt(ctx, ctx.Piece("6.2", "Crate_Heavy_2m"), "6.2 Lift", "HeavyCargo");
            crate.transform.SetPositionAndRotation(new Vector3(27.4f, 3.45f, 0.6f), Quaternion.identity);
            MakeDynamic(crate, 40f);
            Gravity(crate, ZeroGravityResponse.Hover);
            Nullify(crate, "Heavy Cargo", 8f, true, 2.5f);
            Respawn(crate, 1);
            crate.AddComponent<PlateWeight>();

            var weight = Adopt(ctx, ctx.Piece("6.2", "Counterweight"), "6.2 Lift", "Counterweight");
            weight.transform.SetPositionAndRotation(new Vector3(28.3f, 9f, 0f), Quaternion.identity);
            MakeDynamic(weight, 6f);
            Gravity(weight, ZeroGravityResponse.Hover);
            Nullify(weight, "Counterweight");
            Respawn(weight, 1);
            weight.AddComponent<SocketKey>().key = "lift";
            var tray = Spawn("Pressure_Plate", new Vector3(22f, 2.23f, -1.6f), Vector3.zero, ctx.Group("6.2 Lift"), null, "CounterweightTray");
            NoColliders(tray);
            var trayTrig = TriggerBox(ctx.Group("6.2 Lift"), "TraySocket", new Vector3(22f, 3.3f, -1.6f), new Vector3(1.9f, 2.2f, 2.2f));
            var traySocket = trayTrig.AddComponent<Socket>();
            traySocket.section = 1;
            traySocket.key = "lift";
            traySocket.indicator = tray.GetComponent<Renderer>();
            traySocket.acceptedMessage = "Counterweight on the tray!";
            traySocket.snapPoint = Child(trayTrig.transform, new Vector3(22f, 4.28f, -1.6f));

            var clear = new GameObject("CargoClear").AddComponent<LogicGate>();
            clear.transform.SetParent(ctx.Group("6.2 Lift"), false);
            clear.section = 1;
            clear.inputs = new SignalSource[] { scale };
            clear.mode = GateMode.Any;
            clear.invert = true;
            var liftGate = clear.gameObject.AddComponent<LogicGate>();
            liftGate.section = 1;
            liftGate.inputs = new SignalSource[] { traySocket, clear };
            liftGate.latch = true;
            mover.source = liftGate;
            var liftLight = ctx.Piece("6.2", "Warning_Light");
            liftLight.transform.position = new Vector3(23.6f, 2.23f, -3.3f);
            var ll = liftLight.AddComponent<SignalLight>();
            ll.source = liftGate;
            ll.renderers = new[] { liftLight.GetComponent<Renderer>() };
            ll.lightSource = PointLight(liftLight.transform, liftLight.transform.position + Vector3.up * 2.6f, Color.red, 6f, 3f);
            Zone(ctx, "Lift Shaft", 1, new Vector3(25.5f, 8f, 0f), new Vector3(11f, 8.6f, 7.6f), GravityMode.Zero,
                new[] { GravityMode.Zero, GravityMode.Down }, players: false,
                arrows: new[] { new Vector3(21f, 6.6f, 3.4f), new Vector3(30f, 6.6f, 3.4f) });
            Hint(ctx, "6.2  Cargo Lift Failure",
                "The lift is stuck: it's overloaded and its counterweight is adrift.\nB: steer the heavy crate off the lift and the counterweight over the tray (the shaft is weightless).  A: restore gravity to set them down.",
                new Vector3(20.8f, 3.5f, 0f), new Vector3(1.4f, 4f, 7f), 11f);

            // ---------- 6.3 Magnetic Cargo Lane ----------
            var cargoCp = Checkpoint(ctx, ctx.Piece("6.3", "Checkpoint_Beacon"), new Vector3(35.2f, Upper, -2.6f), 2, "Cargo Lane", new Vector3(3f, 4f, 7f), spawnX: 35.2f);
            cargoCp.transform.position = new Vector3(32.2f, Upper, -2.6f); // trigger a little earlier (Feeza's correction)
            foreach (var c in ctx.Pieces("6.3", "Crate_Heavy_2m")) Remove(c);
            foreach (var c in ctx.Pieces("6.3", "Cargo_Container")) Remove(c);
            foreach (var c in ctx.Pieces("6.3", "Crate_1m")) Remove(c);
            // back bay for the jammed container
            RemoveWallsAt(ctx, "6.3", 54f, 5f);
            RemoveWallsAt(ctx, "6.4", 58f, 5f);
            Spawn("Station_Floor_4x4", new Vector3(55f, 4.7f, 6.5f), Vector3.zero, ctx.Group("6.3 Lane"), new Vector3(2f, 1f, 1f), "BayFloor");
            // pushed toward the back bay, so there is room to walk past it on the camera side
            var jam = Spawn("Cargo_Container", new Vector3(55f, Upper, 4.2f), Vector3.zero, ctx.Group("6.3 Lane"), null, "JammedContainer");
            MakeDynamic(jam, 60f);
            Gravity(jam, ZeroGravityResponse.Hover);
            Nullify(jam, "Jammed Container", 9f, true, 2.4f);
            Respawn(jam, 2);
            Zone(ctx, "Lane Exit Field", 2, new Vector3(55f, 7.5f, 3f), new Vector3(8f, 5f, 9f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Zero }, players: false,
                arrows: new[] { new Vector3(51.6f, 8.6f, -1.4f), new Vector3(58.4f, 8.6f, -1.4f) });
            // conveyor loads (templates are cloned at runtime)
            var templates = new List<GameObject>();
            foreach (var (prefab, name) in new[] { ("Cargo_Container", "LoadContainer"), ("Crate_Heavy_2m", "LoadCrate"), ("Crate_1m", "LoadSmallCrate") })
            {
                var t = Spawn(prefab, new Vector3(47.5f, Upper, 1.3f), Vector3.zero, ctx.Group("6.3 Lane"), null, name);
                BoxFit(t);
                var mf = t.GetComponent<MeshFilter>();
                var h = HazardBox(t.transform, "Push", t.transform.TransformPoint(mf.sharedMesh.bounds.center) + new Vector3(-0.3f, 0f, 0f), mf.sharedMesh.bounds.size + new Vector3(0.4f, 0.1f, 0.2f), HazardEffect.Knockback);
                h.knockDirection = Vector3.left;
                h.knockSpeed = 9f;
                h.knockUp = 3f;
                t.SetActive(false);
                templates.Add(t);
            }
            var conveyorGo = new GameObject("CargoConveyor");
            conveyorGo.transform.SetParent(ctx.Group("6.3 Lane"), false);
            var conv = conveyorGo.AddComponent<Conveyor>();
            conv.section = 2;
            conv.loadTemplates = templates.ToArray();
            conv.start = new Vector3(47.5f, Upper, 1.3f);
            conv.end = new Vector3(36.4f, Upper, 1.3f);
            conv.speed = 2.6f;
            conv.spawnInterval = 4.2f;
            Hint(ctx, "6.3  Magnetic Cargo Lane",
                "Cargo slides down the lane — wait in a side bay (toward the camera) until it passes, then run.\nAt the exit, walk past the jammed container on the near side. A: press E to make its field ZERO-G if you want it to float out of the way.",
                new Vector3(36f, 6.5f, 0f), new Vector3(1.5f, 4f, 7f), 11f);

            // ---------- 6.4 Airlock Timer ----------
            Checkpoint(ctx, null, new Vector3(57.4f, Upper, -2.6f), 3, "Airlock Timer", new Vector3(2.4f, 4f, 7f), spawnX: 57.4f, spawnZ: -1.2f);
            var consoleA = ctx.Piece("6.4", "Relay_Console_A");
            consoleA.transform.position = new Vector3(59.4f, Upper, -3.0f);
            var consoleB = ctx.Piece("6.4", "Relay_Console_B");
            var padA = ConsolePad(ctx, "6.4 Airlock", new Vector3(59.4f, Upper, -1.9f), consoleA, 3);
            var padB = ConsolePad(ctx, "6.4 Airlock", new Vector3(64.5f, Upper, 1.4f), consoleB, 3);
            var cell = Adopt(ctx, ctx.Piece("6.4", "Power_Cell"), "6.4 Airlock", "PowerCell");
            cell.transform.position = new Vector3(60.5f, Upper, -0.5f);
            MakeDynamic(cell, 2f);
            Gravity(cell, ZeroGravityResponse.Hover);
            Nullify(cell, "Power Cell");
            Respawn(cell, 3);
            cell.AddComponent<SocketKey>().key = "aircell";
            var cellSocket = FloorSocket(ctx, "6.4 Airlock", new Vector3(63.0f, Upper, 2.2f), "aircell", 3, "Power cell seated!");
            var success = new GameObject("AirlockSuccess").AddComponent<LogicGate>();
            success.transform.SetParent(ctx.Group("6.4 Airlock"), false);
            success.section = 3;
            success.inputs = new SignalSource[] { padA, padB, cellSocket };
            success.latch = true;
            var roomZone = Zone(ctx, "Pressure Room", 3, new Vector3(62.2f, 8f, 0f), new Vector3(7.6f, 6f, 7.6f), GravityMode.Down,
                new[] { GravityMode.Zero, GravityMode.Down },
                arrows: new[] { new Vector3(59f, 9f, 3.4f), new Vector3(65f, 9f, 3.4f) });
            var roomTrig = TriggerBox(ctx.Group("6.4 Airlock"), "CountdownTrigger", new Vector3(62.4f, 7f, 0f), new Vector3(7f, 4f, 7.4f));
            var room = roomTrig.AddComponent<CountdownRoom>();
            room.section = 3;
            room.duration = 80f;
            room.successSignal = success;
            room.roomZone = roomZone;
            var entry = SlidingDoor(ctx, "6.4 Airlock", 58.4f, room, invert: true, latch: false);
            entry.speed = 6f;
            SlidingDoor(ctx, "6.4 Airlock", 66.2f, success, invert: false, latch: true).openMessage = "Airlock open!";
            Hint(ctx, "6.4  Airlock Timer",
                "Hull breach — the room is ZERO-G and a countdown starts!\n1) B: aim at the floating power cell, hold Right Shift and steer it over the glowing floor socket.\n2) A: press E to switch the room's gravity DOWN so the cell drops in.\n3) Stand on both console pads to open the door.",
                new Vector3(57.6f, 6.5f, 0f), new Vector3(1f, 4f, 7f), 11f);

            // ---------- 6.5 Broken Wall Walk ----------
            Checkpoint(ctx, ctx.Piece("6.5", "Checkpoint_Beacon"), new Vector3(68f, Upper, -2.6f), 4, "Broken Wall Walk", new Vector3(4f, 4f, 7f));
            RemoveWallsAt(ctx, "6.5", 72f, 5f);
            RemoveWallsAt(ctx, "6.5", 76f, 5f);
            RemoveWallsAt(ctx, "6.5", 80f, 5f);
            Spawn("Station_Wall_4x4", new Vector3(71.4f, 5f, 4.2f), Vector3.zero, ctx.Group("6.5 Wall Walk"), null, "WalkWallA");
            Spawn("Station_Wall_4x4", new Vector3(80.6f, 5f, 4.2f), Vector3.zero, ctx.Group("6.5 Wall Walk"), null, "WalkWallC");
            NoColliders(ctx.Piece("6.5", "Station_Floor_4x4", 6));
            var panel = Adopt(ctx, ctx.Piece("6.5", "Platform_Metal_2x2"), "6.5 Wall Walk", "LoosePanel");
            panel.transform.SetPositionAndRotation(new Vector3(76f, 7f, 1.0f), Quaternion.FromToRotation(Vector3.up, Vector3.back));
            MakeDynamic(panel, 6f);
            Gravity(panel, ZeroGravityResponse.Hover);
            Nullify(panel, "Wall Panel", 9f, false, 2.5f);
            Respawn(panel, 4);
            var dock = Zone(ctx, "Panel Dock", 4, new Vector3(76f, 7f, 0.8f), new Vector3(3f, 3f, 3f), GravityMode.Zero, null,
                controllable: false, players: false);
            dock.priority = 1;
            dock.drawOutline = false;
            var arrows = new[] { ctx.Piece("6.5", "Gravity_Arrow", 0).transform, ctx.Piece("6.5", "Gravity_Arrow", 1).transform, ctx.Piece("6.5", "Gravity_Arrow", 2).transform };
            arrows[1].position = new Vector3(76f, 8.8f, 3.6f);
            Zone(ctx, "Wall Walk Field", 4, new Vector3(76f, 7f, 0.6f), new Vector3(12f, 10f, 9f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Forward }, existingArrows: arrows);
            Hint(ctx, "6.5  Broken Wall Walk",
                "No floor ahead — but the wall can be one! A: switch the corridor's gravity TOWARD THE WALL. B: steer the floating panel into the hole in the wall and hold it there while you cross.",
                new Vector3(67f, 6.5f, 0f), new Vector3(1.5f, 4f, 7f), 11f);

            // stepping stones across the hole, so it can also be crossed with (double) jumps
            Spawn("Platform_Metal_2x2", new Vector3(74f, Upper, 0f), Vector3.zero, ctx.Group("6.5 Wall Walk"), null, "SteppingStoneA");
            Spawn("Platform_Metal_2x2", new Vector3(78.5f, Upper, 0f), Vector3.zero, ctx.Group("6.5 Wall Walk"), null, "SteppingStoneB");

            SweeperArm(ctx, "6.5 Wall Walk", new Vector3(84.6f, Upper + 0.75f, 0f), 6f, 75f);

            // ---------- 6.6 Power Relay Puzzle ----------
            Checkpoint(ctx, null, new Vector3(86.6f, Upper, -2.6f), 5, "Power Relays", new Vector3(3f, 4f, 7f));
            var relays = new PressurePlate[3];
            var relayRenderers = new Renderer[3];
            string[] consoles = { "Relay_Console_A", "Relay_Console_B", "Relay_Console_C" };
            for (int i = 0; i < 3; i++)
            {
                var con = ctx.Piece("6.6", consoles[i]);
                relays[i] = ConsolePad(ctx, "6.6 Relays", con.transform.position + new Vector3(0f, 0f, -1.4f), null, 5);
                relayRenderers[i] = con.GetComponent<Renderer>();
            }
            BreakFloor(ctx, "6.6", 92f, (92.75f, 2.5f));
            LaserAt(ctx, "6.6 Relays", 96.8f, Upper, 2.8f, -3.6f, 3.8f, 1.5f, 2.3f, 0f);
            var hints = new Renderer[3];
            for (int i = 0; i < 3; i++)
            {
                var hl = SolidBox(ctx.Group("6.6 Relays"), "ClueLight" + i, new Vector3(100.8f + i * 0.9f, 9.6f, 3.5f), new Vector3(0.6f, 0.6f, 0.2f), Lit("ClueLight" + i, Color.white, Color.white));
                hints[i] = hl.GetComponent<Renderer>();
            }
            var seq = new GameObject("RelaySequence").AddComponent<RelaySequence>();
            seq.transform.SetParent(ctx.Group("6.6 Relays"), false);
            seq.section = 5;
            seq.relays = relays;
            seq.order = new[] { 2, 0, 1 };
            seq.relayLights = relayRenderers;
            seq.hintLights = hints;
            SlidingDoor(ctx, "6.6 Relays", 102.4f, seq, invert: false, latch: true).openMessage = "Relays online — the bulkhead opens!";
            Hint(ctx, "6.6  Power Relay Puzzle",
                "Three relays, one correct order. The clue lights above the bulkhead blink the colours in order — stand on the matching relay pads in that order.",
                new Vector3(87.5f, 6.5f, 0f), new Vector3(1.5f, 4f, 7f), 10f);

            // ---------- 6.7 Debris Storm ----------
            Checkpoint(ctx, ctx.Piece("6.7", "Checkpoint_Beacon"), new Vector3(104f, Upper, -1.4f), 6, "Debris Storm", new Vector3(3f, 4f, 3.4f));
            BreakFloor(ctx, "6.7", 116f, (116f, 2f));
            BreakFloor(ctx, "6.7", 120f, (120.5f, 2f));
            Spawn("Crate_Heavy_2m", new Vector3(112f, Upper, 0.75f), Vector3.zero, ctx.Group("6.7 Storm"), null, "Cover1");
            Spawn("Crate_Heavy_2m", new Vector3(108.6f, Upper, -0.75f), Vector3.zero, ctx.Group("6.7 Storm"), null, "Cover2");
            var shield = Adopt(ctx, ctx.Piece("6.7", "Shield_Panel"), "6.7 Storm", "ShieldPanel");
            // half-width panel on the left of the hall: the lane stays open, duck behind it (z < 0) or push/steer it ahead as cover
            shield.transform.SetPositionAndRotation(new Vector3(105.2f, Upper, -0.95f), Quaternion.Euler(0f, 90f, 0f));
            shield.transform.localScale = Vector3.Scale(shield.transform.localScale, new Vector3(0.7f, 1f, 1f));
            MakeDynamic(shield, 10f).collisionDetectionMode = CollisionDetectionMode.Discrete; // thin + CCD bounced players off it
            Gravity(shield, ZeroGravityResponse.Hover);
            var shieldPush = shield.AddComponent<Pushable>(); // walk into it to shove it down the hall as moving cover
            shieldPush.speedOnePusher = 2.2f;
            shieldPush.speedTwoPushers = 3.4f;
            Nullify(shield, "Shield Panel", 10f, false, 3.5f);
            Respawn(shield, 6);
            var projTemplates = new List<GameObject>();
            foreach (var p in new[] { "Rock_Debris_S", "Debris_Plate_A", "Crate_1m" })
            {
                var t = Spawn(p, new Vector3(126f, 6f, 0f), Vector3.zero, ctx.Group("6.7 Storm"), Vector3.one * 0.8f, "Projectile_" + p);
                NoColliders(t);
                t.SetActive(false);
                projTemplates.Add(t);
            }
            var storm = new GameObject("DebrisStorm").AddComponent<StormEmitter>();
            storm.transform.SetParent(ctx.Group("6.7 Storm"), false);
            storm.projectileTemplates = projTemplates.ToArray();
            storm.spawnMin = new Vector3(126f, 5.7f, -1.3f);
            storm.spawnMax = new Vector3(126.6f, 7.3f, 1.3f);
            storm.velocity = new Vector3(-15f, 0f, 0f);
            storm.burstCount = 3;
            storm.hallStartX = 104f;
            storm.hallEndX = 125f;
            var wl0 = ctx.Piece("6.7", "Warning_Light", 0);
            var wl1 = ctx.Piece("6.7", "Warning_Light", 1);
            storm.warnRenderers = new[] { wl0.GetComponent<Renderer>(), wl1.GetComponent<Renderer>() };
            storm.warnLights = new[]
            {
                PointLight(wl0.transform, wl0.transform.position + Vector3.up * 2.6f, Color.red, 8f, 0f),
                PointLight(wl1.transform, wl1.transform.position + Vector3.up * 2.6f, Color.red, 8f, 0f)
            };
            Hint(ctx, "6.7  Debris Storm",
                "Debris blasts down the hall in bursts and the floor ahead is broken. Duck behind the shield panel when the lights flash, push it along (or B steers it — hold Right Shift), and jump the gaps between bursts.",
                new Vector3(104.5f, 6.5f, 0f), new Vector3(1.5f, 4f, 4f), 10f);

            // ---------- 6.8 Engine Vault Door ----------
            Checkpoint(ctx, ctx.Piece("6.8", "Checkpoint_Beacon"), new Vector3(127.5f, Upper, -2.4f), 7, "Engine Vault", new Vector3(3f, 4f, 7f));
            // the end: the vault stands open, the engine is inside — step into the finish beacon
            foreach (var piece in new[] { ctx.Piece("6.8", "Power_Node", 0), ctx.Piece("6.8", "Power_Node", 1), ctx.Piece("6.8", "Power_Cell"), ctx.Piece("6.8", "Vault_LockBar") })
                Remove(piece);
            var vdoor = Adopt(ctx, ctx.Piece("6.8", "Vault_Door"), "6.8 Vault", "VaultDoor");
            vdoor.transform.position += new Vector3(0f, 8.5f, 0f);
            Finish(ctx, "6.8 Vault", new Vector3(136f, Upper, 0f), "Engine Vault");
            Hint(ctx, "6.8  Engine Vault",
                "The engine vault is open! Step into the finish beacon to complete the stage.",
                new Vector3(128.5f, 6.5f, 0f), new Vector3(1.5f, 4f, 7f), 8f);

            // ---------- walls ----------
            Wall(ctx, -1f, 146f, -3.6f);
            Wall(ctx, -1f, 69.4f, 3.8f);
            Wall(ctx, 82.6f, 103f, 3.8f);
            Wall(ctx, 126f, 131.2f, 3.8f);
            Wall(ctx, 140.8f, 146f, 3.8f);
            // narrow cargo lane with side bays toward the camera
            Wall(ctx, 37f, 51.5f, 2.9f);
            foreach (var seg in new[] { (37f, 40f), (42.5f, 45.5f), (48f, 50.5f), (52f, 58.4f) })
                Wall(ctx, seg.Item1, seg.Item2, -0.3f);
            SolidBox(ctx.Group("InvisibleWalls"), "BaySideL", new Vector3(51.2f, 8f, 6f), new Vector3(1f, 8f, 6f));
            SolidBox(ctx.Group("InvisibleWalls"), "BaySideR", new Vector3(58.8f, 8f, 6f), new Vector3(1f, 8f, 6f));
            SolidBox(ctx.Group("InvisibleWalls"), "BayBack", new Vector3(55f, 8f, 9f), new Vector3(8.6f, 8f, 1f));
            // storm hall is narrow
            Wall(ctx, 103f, 126f, -1.8f);
            Wall(ctx, 103f, 126f, 1.8f);
            // vault room
            SolidBox(ctx.Group("InvisibleWalls"), "VaultSideL", new Vector3(130f, 8f, 8.5f), new Vector3(1f, 8f, 8f));
            SolidBox(ctx.Group("InvisibleWalls"), "VaultSideR", new Vector3(142f, 8f, 8.5f), new Vector3(1f, 8f, 8f));
            SolidBox(ctx.Group("InvisibleWalls"), "VaultBack", new Vector3(136f, 8f, 12.6f), new Vector3(12f, 8f, 1f));
            EndWall(ctx, -0.6f);
            EndWall(ctx, 146f);

            Astronaut(ctx, 0, start.GetSpawn(0));
            Astronaut(ctx, 1, start.GetSpawn(1));
            Astronaut(ctx, 2, start.GetSpawn(2));
            Astronaut(ctx, 3, start.GetSpawn(3));
            CoreObjects(ctx, 2, "STAGE 2 — THE UPSIDE-DOWN STATION", HudObjective.FindEngine, "Find the Engine",
                "The station's gravity generators have gone haywire. Rooms flip, cargo drifts, walls become floors.\n\n" +
                "Fight your way through the facility to the engine vault. A still bends gravity in the marked fields; B still freezes and steers loose objects.",
                "ENGINE FOUND",
                "The replacement engine is free — and the whole station shudders. Alarms everywhere: it's entering a critical state.\nGet the engine back to the ship before it falls apart!",
                -15f, times: new[] { TimeOfDay.At(TimeOfDay.Evening(), 0f), TimeOfDay.At(TimeOfDay.Dusk(), 0.55f), TimeOfDay.At(TimeOfDay.Night(), 1f) },
                routeStartX: 0f, routeEndX: 142f);
            Save(ctx, ScenePath);
        }

        /// <summary>Removes the corridor floor tiles at x and puts back narrow hazard-striped pieces (centre, width) — gaps to jump.</summary>
        static void BreakFloor(StageContext ctx, string section, float x, params (float center, float width)[] pieces)
        {
            foreach (var t in ctx.Pieces(section, "Station_Floor_4x4"))
                if (Mathf.Abs(t.transform.position.x - x) < 0.1f) Remove(t);
            foreach (var (c, w) in pieces)
                Spawn("Station_Floor_Hazard", new Vector3(c, Upper - 0.03f, 0f), Vector3.zero, ctx.Group("Broken Floors"), new Vector3(w / 4f, 1f, 2.05f), "BrokenFloor");
        }

        static Transform Child(Transform parent, Vector3 world)
        {
            var t = new GameObject("Snap").transform;
            t.SetParent(parent, false);
            t.position = world;
            return t;
        }

        static void RemoveWallsAt(StageContext ctx, string section, float x, float y)
        {
            foreach (var prefab in new[] { "Station_Wall_4x4", "Station_Wall_Window" })
                foreach (var w in ctx.Pieces(section, prefab))
                    if (Mathf.Abs(w.transform.position.x - x) < 0.1f && Mathf.Abs(w.transform.position.y - y) < 0.1f) Remove(w);
        }

        /// <summary>A stand-on pad in front of a console / node; the console itself glows with the pad state.</summary>
        static PressurePlate ConsolePad(StageContext ctx, string group, Vector3 at, GameObject console, int section)
        {
            var visual = Spawn("Pressure_Plate", at, Vector3.zero, ctx.Group(group), null, "Pad");
            NoColliders(visual);
            var trig = TriggerBox(ctx.Group(group), "PadTrigger", at + new Vector3(0f, 1f, 0f), new Vector3(1.8f, 2f, 2f));
            var pp = trig.AddComponent<PressurePlate>();
            pp.section = section;
            pp.plateTop = visual.transform;
            pp.indicator = visual.GetComponent<Renderer>();
            if (console != null)
            {
                var sl = console.AddComponent<SignalLight>();
                sl.source = pp;
                sl.renderers = new[] { console.GetComponent<Renderer>() };
            }
            return pp;
        }

        static Socket FloorSocket(StageContext ctx, string group, Vector3 at, string key, int section, string message)
        {
            var ring = Spawn("Pressure_Plate", at, Vector3.zero, ctx.Group(group), null, "SocketRing");
            NoColliders(ring);
            var trig = TriggerBox(ctx.Group(group), "Socket_" + key, at + new Vector3(0f, 0.7f, 0f), new Vector3(1.4f, 1.4f, 1.4f));
            var s = trig.AddComponent<Socket>();
            s.section = section;
            s.key = key;
            s.indicator = ring.GetComponent<Renderer>();
            s.acceptedMessage = message;
            s.snapPoint = Child(trig.transform, at + new Vector3(0f, 0.27f, 0f));
            return s;
        }

        /// <summary>A bulkhead across the corridor (two wall panels) that slides up when its signal is on.</summary>
        static Door SlidingDoor(StageContext ctx, string group, float x, SignalSource source, bool invert, bool latch)
        {
            var g = ctx.Group(group);
            var root = new GameObject("Bulkhead");
            root.transform.SetParent(g, false);
            var p1 = Spawn("Station_Wall_4x4", new Vector3(x, Upper, -2f), new Vector3(0f, 90f, 0f), root.transform, null, "BulkheadA");
            var p2 = Spawn("Station_Wall_4x4", new Vector3(x, Upper, 2f), new Vector3(0f, 90f, 0f), root.transform, null, "BulkheadB");
            var d = root.AddComponent<Door>();
            d.source = source;
            d.invert = invert;
            d.latch = latch;
            d.parts = new[] { p1.transform, p2.transform };
            d.openOffsets = new[] { new Vector3(0f, 4.2f, 0f) };
            d.speed = 3f;
            return d;
        }
    }
}
