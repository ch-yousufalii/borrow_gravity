using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static BorrowGravity.EditorTools.BuilderKit;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Stage 3 — Engine Run: The Collapsing Return (PRD 7.1–7.8). The new engine comes in two halves: each player picks
    /// up their own half and carries it in their hands all the way back to the ship, where both are installed.
    /// </summary>
    public static class Stage3Builder
    {
        public const string ScenePath = "Assets/Scenes/Stage3.unity";

        public static void Build()
        {
            var ctx = BeginStage("Stage3");
            var metal = Lit("BracketMetal", new Color(0.32f, 0.3f, 0.36f), null, 0.5f);
            var charged = Lit("ChargedStrip", new Color(1f, 0.15f, 0.1f), new Color(1f, 0.15f, 0.1f) * 4f);
            var safe = Lit("SafeMarker", new Color(0.2f, 1f, 0.5f), new Color(0.2f, 1f, 0.5f) * 2f);

            // ---------- the two engine halves (one each, carried in the hands) ----------
            // each player already carries their engine half (given in Astronaut); the layout's engine props go
            Remove(ctx.Piece("7.1", "Engine_Carrier"));
            Remove(ctx.Piece("7.1", "Engine_Core"));

            // ---------- 7.1 Engine Handling ----------
            var start = Checkpoint(ctx, ctx.Piece("7.1", "Checkpoint_Beacon"), new Vector3(2f, 0.03f, -2.6f), 0, "Engine Bay",
                new Vector3(6f, 4f, 6f), isStart: true, spawnX: 4f);
            SolidBox(ctx.Group("7.1 Bay"), "Sill", new Vector3(11.5f, 0.5f, 0f), new Vector3(1f, 1f, 5f), metal);
            Zone(ctx, "Bay Lift Field", 0, new Vector3(13f, 2.3f, 0f), new Vector3(10f, 4.6f, 5f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Zero }, players: false,
                arrows: new[] { new Vector3(9f, 3.8f, -1.9f), new Vector3(17f, 3.8f, -1.9f) });
            Hint(ctx, "7.1  Engine Handling",
                "The repaired engine is in two halves — A carries the orange one, B the cyan one. Keep them safe all the way back to the ship.",
                new Vector3(4.5f, 1.5f, 0f), new Vector3(2f, 4f, 6f), 9f);
            Hint(ctx, "The Sill",
                "Jump the sill and the gap behind it — your engine half stays in your hands.",
                new Vector3(8f, 1.5f, 0f), new Vector3(1.2f, 4f, 6f), 10f);

            // ---------- 7.2 Collapsing Cargo Bridge ----------
            Remove(ctx.Piece("7.2", "Bridge_End_Broken"));
            ctx.Piece("7.2", "Platform_Metal_4x4").transform.position = new Vector3(45f, -0.06f, 0f);
            var collapse = new List<CrumblePlatform>();
            foreach (var (prefab, idx) in new[] { ("Bridge_Section", 0), ("Bridge_Section", 1), ("Bridge_Section_Weak", 0), ("Bridge_Section", 2) })
            {
                var piece = Adopt(ctx, ctx.Piece("7.2", prefab, idx), "7.2 Bridge");
                var cp = piece.AddComponent<CrumblePlatform>();
                cp.section = 1;
                cp.triggerOnStep = false;
                cp.regenerateAfter = -1f;
                cp.crackTime = 2.2f;
                collapse.Add(cp);
            }
            var collTrig = TriggerBox(ctx.Group("7.2 Bridge"), "CollapseTrigger", new Vector3(20.5f, 1.5f, 0f), new Vector3(1f, 4f, 4f));
            var tpd = collTrig.AddComponent<TimedPowerDown>();
            tpd.section = 1;
            tpd.platforms = collapse.ToArray();
            tpd.triggeredByCarrier = false;
            tpd.triggeredByPlayers = true;
            tpd.requiredPlayers = 2; // only once both are on the bridge, so nobody is left behind
            tpd.rearm = false;
            tpd.startDelay = 1f;
            tpd.interval = 3.6f;
            tpd.warnTime = 2.0f;
            tpd.warningText = "The bridge is collapsing behind you — push!";
            var cp36 = Checkpoint(ctx, null, new Vector3(35f, 0f, -1.2f), 1, "Bridge End", new Vector3(4f, 4f, 3f), spawnX: 33.2f);
            LaserAt(ctx, "7.2 Bridge", 31.2f, 0f, 3.2f, -1.7f, 1.7f, 1.5f, 2.6f, 0f);
            var plate = Adopt(ctx, ctx.Piece("7.2", "Bridge_Section", 4), "7.2 Bridge", "FloatingBridgePlate");
            plate.transform.SetPositionAndRotation(new Vector3(40.43f, -6.28f, 0f), Quaternion.identity);
            MakeDynamic(plate, 15f, RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation);
            Gravity(plate, ZeroGravityResponse.Float, 2f, 1.2f, 6f);
            var pn = Nullify(plate, "Bridge Plate", 10f);
            pn.allowSteer = false;
            Respawn(plate, 1, -20f);
            SolidBox(ctx.Group("7.2 Bridge"), "PlateRest", new Vector3(40.43f, -7f, 0f), new Vector3(4f, 0.6f, 3f));
            Zone(ctx, "Gap Field", 1, new Vector3(40.6f, -1.5f, 0f), new Vector3(4.6f, 13f, 5f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Zero }, players: false,
                arrows: new[] { new Vector3(38.8f, 3.5f, -1.9f), new Vector3(42.4f, 3.5f, -1.9f) });
            Hint(ctx, "7.2  Collapsing Cargo Bridge",
                "Once you're both on the bridge it starts collapsing from the back. Run across together — fast!",
                new Vector3(14.5f, 1.5f, 0f), new Vector3(1.5f, 4f, 4f), 8f);
            Hint(ctx, "Last Gap",
                "A: zero the gap field so the bridge plate floats up. B: freeze it level with the deck, then cross on it.",
                new Vector3(36.5f, 1.5f, 0f), new Vector3(1.5f, 4f, 4f), 9f);

            // ---------- 7.3 Falling Cable Trap ----------
            var cp49 = Checkpoint(ctx, ctx.Piece("7.3", "Checkpoint_Beacon"), new Vector3(46.6f, 0f, -1.6f), 2, "Cable Trap", new Vector3(4f, 4f, 4f), spawnX: 44.6f);
            float[] cableX = { 50f, 56f, 62f };
            float[] offsets = { 0f, 1.5f, 3.0f };
            var w0 = ctx.Piece("7.3", "Warning_Light", 0);
            var w1 = ctx.Piece("7.3", "Warning_Light", 1);
            w0.transform.position = new Vector3(53f, 0.03f, 2.6f);
            w1.transform.position = new Vector3(59f, 0.03f, 2.6f);
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject("CableDropper" + i);
                root.transform.SetParent(ctx.Group("7.3 Cables"), false);
                root.transform.position = new Vector3(cableX[i], 8.5f, 0f);
                var cab = Adopt(ctx, ctx.Piece("7.3", "Cable_Hanging", i), "7.3 Cables");
                cab.transform.SetParent(root.transform, false);
                cab.transform.localPosition = Vector3.zero;
                cab.transform.localRotation = Quaternion.identity;
                cab.transform.localScale = new Vector3(1.15f, 1f, 1.15f);
                NoColliders(cab);
                HazardBox(root.transform, "CableHit", root.transform.position + new Vector3(0f, -6.7f, 0f), new Vector3(1.1f, 3.2f, 2.6f), HazardEffect.Respawn, false).carrierDamage = 40f;
                var dropper = root.AddComponent<CableDropper>();
                dropper.raiseHeight = 6.5f;
                dropper.period = 5.8f;
                dropper.offset = offsets[i];
                dropper.warnRenderer = cab.GetComponent<Renderer>();
                dropper.warnLight = PointLight(root.transform, root.transform.position + new Vector3(0f, -1f, -1.2f), new Color(1f, 0.55f, 0.1f), 8f, 0f);
            }
            var spare = ctx.Piece("7.3", "Cable_Hanging", 3);
            spare.transform.position = new Vector3(65.5f, 12f, 1.5f);
            NoColliders(spare);
            Hint(ctx, "7.3  Falling Cable Trap",
                "Damaged cables slam down through the passage — sparks warn you first. Wait in the bays between them, then move together.",
                new Vector3(47.5f, 1.5f, 0f), new Vector3(1.5f, 4f, 4f), 9f);

            // ---------- 7.4 Rotating Ring Walkway ----------
            var cp67 = Checkpoint(ctx, null, new Vector3(66f, 0f, -1.5f), 3, "Ring Walkway", new Vector3(4f, 4f, 3.6f), spawnX: 65.6f);
            Spawn("Platform_Metal_4x4", new Vector3(72.6f, -0.3f, 0f), Vector3.zero, ctx.Group("7.4 Ring"), null, "RingLandingL");
            Spawn("Platform_Metal_4x4", new Vector3(87.4f, -0.3f, 0f), Vector3.zero, ctx.Group("7.4 Ring"), null, "RingLandingR");
            var ring = Adopt(ctx, ctx.Piece("7.4", "Ring_Walkway"), "7.4 Ring", "RingWalkway");
            var spin = ring.AddComponent<Rotator>();
            spin.section = 3;
            spin.axis = Vector3.forward;
            spin.degreesPerSecond = 9f;
            for (int i = 0; i < 3; i++)
            {
                float a = (60f + i * 120f) * Mathf.Deg2Rad;
                var strip = SolidBox(ring.transform, "ChargedStrip" + i, Vector3.zero, Vector3.one, charged);
                strip.transform.localPosition = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f) * 10.3f;
                strip.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg);
                strip.transform.localScale = new Vector3(0.6f, 0.25f, 3.3f);
                strip.GetComponent<Collider>().isTrigger = true;
                strip.AddComponent<Hazard>().carrierDamage = 30f;
                float b = (i * 120f) * Mathf.Deg2Rad;
                var marker = SolidBox(ring.transform, "SafeMarker" + i, Vector3.zero, Vector3.one, safe);
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.localPosition = new Vector3(Mathf.Sin(b), Mathf.Cos(b), 0f) * 10.27f;
                marker.transform.localRotation = Quaternion.Euler(0f, 0f, -b * Mathf.Rad2Deg);
                marker.transform.localScale = new Vector3(1.2f, 0.06f, 3.2f);
            }
            var ringCenter = new GameObject("RingCentre").transform;
            ringCenter.SetParent(ctx.Group("7.4 Ring"), false);
            ringCenter.position = new Vector3(80f, -10f, 0f);
            var radial = Zone(ctx, "Ring Gravity", 3, new Vector3(80f, -10f, 0f), new Vector3(28f, 26f, 5f), GravityMode.RadialIn, null,
                controllable: false, objects: false);
            radial.radialCenter = ringCenter;
            radial.radialMaxDistance = 11.7f;
            radial.priority = 1;
            HazardBox(ctx.Group("7.4 Ring"), "RingCore", new Vector3(80f, -10f, 0f), new Vector3(12f, 12f, 8f), HazardEffect.Respawn);
            Hint(ctx, "7.4  Rotating Ring Walkway",
                "The ring's gravity pulls you onto its surface — walk over the top, dodging the red charged strips as it turns.",
                new Vector3(65.5f, 1.5f, 0f), new Vector3(1.5f, 4f, 4f), 11f);

            // ---------- 7.5 Long Zero-Gravity Gap ----------
            var cp92 = Checkpoint(ctx, ctx.Piece("7.5", "Checkpoint_Beacon"), new Vector3(90.2f, 0f, -1.4f), 4, "Zero-G Gap", new Vector3(4f, 4f, 3.6f), spawnX: 90f);
            Remove(ctx.Piece("7.5", "Platform_Metal_4x4"));
            var ferry = Spawn("Platform_Metal_6x3", new Vector3(98.75f, 0f, 0f), Vector3.zero, ctx.Group("7.5 Gap"), null, "Ferry");
            var frb = MakeDynamic(ferry, 20f, RigidbodyConstraints.FreezeRotation, kinematic: true);
            Nullify(ferry, "Ferry", 8f, false, 2.5f);
            Respawn(ferry, 4);
            var landing = ctx.Piece("7.5", "Platform_Metal_6x3");
            landing.transform.position = new Vector3(111f, 1.6f, 0f);
            NoColliders(ctx.Piece("7.5", "GravityZone_Pad"));
            var dockArrow = ctx.Piece("7.5", "Gravity_Arrow");
            dockArrow.transform.position = new Vector3(104.75f, 3.6f, -1.9f);
            var dockZone = Zone(ctx, "Dock Field", 4, new Vector3(104.6f, 1f, 0f), new Vector3(7f, 6f, 5f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Up }, players: false, existingArrows: new[] { dockArrow.transform });
            var dockPose = new GameObject("DockPose").transform;
            dockPose.SetParent(ctx.Group("7.5 Gap"), false);
            dockPose.position = new Vector3(104.75f, 1.6f, 0f);
            var dl = new GameObject("DockLock").AddComponent<DockLock>();
            dl.transform.SetParent(ctx.Group("7.5 Gap"), false);
            dl.section = 4;
            dl.target = frb;
            dl.dockPose = dockPose;
            dl.zone = dockZone;
            dl.requiredMode = GravityMode.Up;
            dl.lockedMessage = "Ferry docked — hop off!";
            var gap = Zone(ctx, "Zero-G Gap", 4, new Vector3(101.8f, 0f, 0f), new Vector3(12.3f, 20f, 5f), GravityMode.Zero, null,
                controllable: false, objects: false);
            gap.drawOutline = false;
            Hint(ctx, "7.5  Long Zero-Gravity Gap",
                "No floor across the gap. Step onto the ferry. B: freeze the ferry and steer it across. A: switch the landing field UP to lift it into the dock.",
                new Vector3(91f, 1.5f, 0f), new Vector3(1.5f, 4f, 4f), 11f);

            // ---------- 7.6 Final Debris Fall ----------
            var cp116 = Checkpoint(ctx, null, new Vector3(115.6f, 0.4f, -1.5f), 5, "Debris Fall", new Vector3(3f, 4f, 3.6f), spawnX: 115.2f);
            var deck = ctx.Group("7.6 Debris");
            SolidBox(deck, "DeckA", new Vector3(118.35f, 0.15f, 0f), new Vector3(7.5f, 0.5f, 3.4f));
            SolidBox(deck, "DeckB", new Vector3(124.35f, -0.05f, 0f), new Vector3(4.5f, 0.5f, 3.4f));
            SolidBox(deck, "DeckC", new Vector3(130.35f, -0.25f, 0f), new Vector3(7.5f, 0.5f, 3.4f));
            SolidBox(deck, "DeckD", new Vector3(136.05f, -0.45f, 0f), new Vector3(3.9f, 0.5f, 3.4f));
            DebrisFall(ctx, ctx.Piece("7.6", "Station_Chunk_A"), new Vector3(119.5f, 0.6f, 0f), 7f, 0f);
            DebrisFall(ctx, ctx.Piece("7.6", "Station_Chunk_B", 1), new Vector3(128.5f, 0.4f, 0f), 7f, 3.5f);
            DebrisFall(ctx, ctx.Piece("7.6", "Station_Chunk_B", 0), new Vector3(135.4f, 0.2f, 0f), 8f, 5.5f);
            // a booster up to the high "safe" route over the debris — both must hit it together while carrying
            JumpPadAt(ctx, "7.6 Debris", new Vector3(116.0f, 0.4f, 1.0f), 14f, new Vector3(1.5f, 0f, 0.4f));
            Hint(ctx, "7.6  Final Debris Fall",
                "The station is coming apart! Red markers show where debris will hit — run between impacts.",
                new Vector3(113.5f, 3f, 0f), new Vector3(1.5f, 4f, 4f), 9f);

            // ---------- 7.7 Ship Docking Platform ----------
            Spawn("Platform_Metal_4x4", new Vector3(140f, -0.26f, 0f), Vector3.zero, ctx.Group("7.7 Docking"), null, "DockConnector");
            var cp140 = Checkpoint(ctx, ctx.Piece("7.7", "Checkpoint_Beacon"), new Vector3(139f, -0.2f, -1.6f), 6, "Docking Platform", new Vector3(3.6f, 4f, 3.6f), spawnX: 138.6f);
            var dockPlat = Adopt(ctx, ctx.Piece("7.7", "Docking_Platform"), "7.7 Docking", "DetachedDock");
            dockPlat.transform.SetPositionAndRotation(new Vector3(147f, -0.2f, 0f), Quaternion.identity);
            DeckCollider(dockPlat);
            var bob = dockPlat.AddComponent<BobbingPlatform>();
            bob.section = 6;
            bob.amplitude = new Vector3(0f, 1.8f, 0f);
            bob.tiltAmplitude = new Vector3(0f, 0f, 5f);
            bob.period = 4.5f;
            bob.alignedMessage = "Cable reconnected — the platform is stable!";
            var plug = Adopt(ctx, ctx.Piece("7.7", "Docking_Plug"), "7.7 Docking", "CablePlug");
            plug.transform.SetPositionAndRotation(new Vector3(140.6f, 1.68f, -1.2f), Quaternion.identity);
            MakeDynamic(plug, 2f);
            Gravity(plug, ZeroGravityResponse.Hover);
            Nullify(plug, "Cable Plug", 8f, false, 3f);
            Respawn(plug, 6);
            plug.AddComponent<SocketKey>().key = "plug";
            var ringVis = Spawn("Pressure_Plate", new Vector3(143.8f, -0.2f, -1.8f), Vector3.zero, dockPlat.transform, null, "PlugSocketRing");
            NoColliders(ringVis);
            var plugTrig = TriggerBox(dockPlat.transform, "PlugSocket", new Vector3(143.8f, 0.9f, -1.8f), new Vector3(1.6f, 2.4f, 1.6f));
            var plugSocket = plugTrig.AddComponent<Socket>();
            plugSocket.section = 6;
            plugSocket.key = "plug";
            plugSocket.maxSpeed = 4f;
            plugSocket.parentToSnap = true;
            plugSocket.indicator = ringVis.GetComponent<Renderer>();
            var plugSnap = new GameObject("Snap").transform;
            plugSnap.SetParent(plugTrig.transform, false);
            plugSnap.position = new Vector3(143.8f, 1.72f, -1.8f);
            plugSocket.snapPoint = plugSnap;
            bob.alignSignal = plugSocket;

            var walk = Adopt(ctx, ctx.Piece("7.7", "Platform_Metal_6x3"), "7.7 Docking", "MisalignedWalkway");
            var walkMover = walk.AddComponent<SignalMover>();
            walkMover.section = 6;
            walkMover.offPosition = new Vector3(155.5f, -2.4f, 0.4f);
            walkMover.onPosition = new Vector3(155.5f, -0.26f, 0f);
            walkMover.rotate = true;
            walkMover.offEuler = new Vector3(4f, 6f, 6f);
            walkMover.onEuler = Vector3.zero;
            walkMover.speed = 1.4f;
            walkMover.latch = true;
            walkMover.arrivedMessage = "Walkway aligned!";
            var alignZone = Zone(ctx, "Walkway Field", 6, new Vector3(155.5f, 0f, 0f), new Vector3(7f, 6f, 5f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Up }, players: false,
                arrows: new[] { new Vector3(152.8f, 2.6f, -1.9f), new Vector3(158.2f, 2.6f, -1.9f) });
            var alignSig = walk.AddComponent<ZoneModeSignal>();
            alignSig.zone = alignZone;
            alignSig.modes = new[] { GravityMode.Up };
            walkMover.source = alignSig;
            Hint(ctx, "7.7  Ship Docking Platform",
                "The docking platform broke loose! B: steer the cable plug onto the platform's socket to reconnect it. A: switch the far walkway's field UP to lift it into line.",
                new Vector3(138.6f, 1.5f, 0f), new Vector3(1.5f, 4f, 4f), 11f);

            // ---------- 7.8 Repair Under Pressure ----------
            var cp159 = Checkpoint(ctx, null, new Vector3(160f, -0.2f, -2f), 7, "Ship", new Vector3(3f, 4f, 3.6f), spawnX: 160.6f);
            var shipDock = ctx.Piece("7.8", "Docking_Platform");
            shipDock.transform.position = new Vector3(164f, -0.2f, 0f);
            DeckCollider(shipDock);
            var ship = ctx.Piece("7.8", "Spaceship");
            ship.transform.position = new Vector3(165f, 2.4f, 4.8f);
            NoColliders(ship);
            var bayPad = Spawn("GravityZone_Pad", new Vector3(162f, -0.2f, 0.4f), Vector3.zero, ctx.Group("7.8 Ship"), null, "EngineBayPad");
            NoColliders(bayPad);
            var bayTrig = TriggerBox(ctx.Group("7.8 Ship"), "EngineBay", new Vector3(162f, 1.2f, 0.4f), new Vector3(2.2f, 2.4f, 2.2f));
            var bay = bayTrig.AddComponent<EngineBay>();
            bay.indicator = bayPad.GetComponent<Renderer>();
            bay.slots = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var slot = new GameObject(i == 0 ? "SlotA" : "SlotB").transform;
                slot.SetParent(bayTrig.transform, false);
                slot.position = new Vector3(i == 0 ? 161.5f : 162.5f, 0.1f, 0.4f);
                bay.slots[i] = slot;
            }
            var lockA = LockPad(ctx, new Vector3(159.9f, -0.2f, -1.7f), ctx.Piece("7.8", "Warning_Light", 0));
            var lockB = LockPad(ctx, new Vector3(168.1f, -0.2f, -1.7f), ctx.Piece("7.8", "Warning_Light", 1));
            var shipZone = Zone(ctx, "Ship Gravity Field", 7, new Vector3(164f, 2.5f, 0f), new Vector3(10f, 5f, 5f), GravityMode.Down,
                new[] { GravityMode.Down, GravityMode.Zero }, players: false,
                arrows: new[] { new Vector3(159.6f, 3.6f, -1.9f), new Vector3(168.4f, 3.6f, -1.9f) });
            var stable = shipZone.gameObject.AddComponent<ZoneModeSignal>();
            stable.zone = shipZone;
            stable.modes = new[] { GravityMode.Down };
            var final = new GameObject("RepairLogic").AddComponent<LogicGate>();
            final.transform.SetParent(ctx.Group("7.8 Ship"), false);
            final.section = 7;
            final.inputs = new SignalSource[] { bay, lockA, lockB, stable };
            var timerTrig = TriggerBox(ctx.Group("7.8 Ship"), "RepairCountdown", new Vector3(164f, 1.5f, 0f), new Vector3(10f, 3f, 5f));
            var timer = timerTrig.AddComponent<CountdownRoom>();
            timer.section = 7;
            timer.duration = 120f;
            timer.successSignal = final;
            timer.label = "STATION COLLAPSE";
            timer.startMessage = "The station is breaking apart! Install both engine halves, keep gravity DOWN and hit both locks!";
            timer.successMessage = "Engine locked in — ignition!";
            var glitch = shipZone.gameObject.AddComponent<ZoneGlitcher>();
            glitch.zone = shipZone;
            glitch.activeWhile = timer;
            var goal = TriggerBox(ctx.Group("7.8 Ship"), "ShipGoal", new Vector3(164f, 1.5f, -0.5f), new Vector3(10f, 3f, 5f));
            var gt = goal.AddComponent<GoalTrigger>();
            gt.extraCondition = final;
            gt.waitingForCondition = "Install both engine halves, keep gravity DOWN and stand on both locks!";
            Hint(ctx, "7.8  Repair Under Pressure",
                "Final repair! Each of you walk into the glowing bay to install your engine half. A: keep the ship's gravity field on DOWN. Then both stand on the lock pads!",
                new Vector3(159.2f, 1.5f, 0f), new Vector3(1.5f, 4f, 4f), 11f);

            // ---------- walls ----------
            Wall(ctx, -1f, 14f, -2.5f);
            Wall(ctx, -1f, 14f, 2.5f);
            Wall(ctx, 14f, 48f, -1.7f);
            Wall(ctx, 14f, 48f, 1.7f);
            Wall(ctx, 48f, 64.4f, -2.5f);
            Wall(ctx, 48f, 64.4f, 2.5f);
            Wall(ctx, 64.4f, 96f, -1.9f);
            Wall(ctx, 64.4f, 96f, 1.9f);
            Wall(ctx, 96f, 171f, -2.5f);
            Wall(ctx, 96f, 158.6f, 2.5f);
            Wall(ctx, 158.6f, 171f, 2.2f);
            EndWall(ctx, -0.6f);
            EndWall(ctx, 170f);

            Astronaut(ctx, 0, start.GetSpawn(0));
            Astronaut(ctx, 1, start.GetSpawn(1));
            Astronaut(ctx, 2, start.GetSpawn(2));
            Astronaut(ctx, 3, start.GetSpawn(3));
            CoreObjects(ctx, 3, "STAGE 3 — ENGINE RUN: THE COLLAPSING RETURN", HudObjective.ReturnToShip, "Return to the Ship",
                "The station is tearing itself apart. The new engine comes in two halves — each of you carries one in your hands, " +
                "all the way back to your ship: across collapsing bridges, falling cables and a gravity ring.\n\nBoth halves must be installed to fly home.",
                "ENGINE ONLINE",
                "The new engine roars to life. With the station collapsing behind you, the ship breaks free and turns for home.\n\nThanks for playing BORROWED GRAVITY!",
                -25f, final: true,
                times: new[] { TimeOfDay.At(TimeOfDay.Night(), 0f), TimeOfDay.At(TimeOfDay.Night(), 0.7f), TimeOfDay.At(TimeOfDay.Dawn(), 1f) },
                routeStartX: 0f, routeEndX: 165f);
            Save(ctx, ScenePath);
        }

        static Transform SpawnPoint(StageContext ctx, Vector3 at)
        {
            var t = new GameObject("CarrierSpawn").transform;
            t.SetParent(ctx.Group("Checkpoints"), false);
            t.position = at;
            return t;
        }

        static PressurePlate LockPad(StageContext ctx, Vector3 at, GameObject light)
        {
            var visual = Spawn("Pressure_Plate", at, Vector3.zero, ctx.Group("7.8 Ship"), null, "LockPad");
            NoColliders(visual);
            var trig = TriggerBox(ctx.Group("7.8 Ship"), "LockPadTrigger", at + Vector3.up, new Vector3(1.8f, 2f, 1.8f));
            var pp = trig.AddComponent<PressurePlate>();
            pp.section = 7;
            pp.plateTop = visual.transform;
            pp.indicator = visual.GetComponent<Renderer>();
            pp.label = "Engine Lock";
            var sl = light.AddComponent<SignalLight>();
            sl.source = pp;
            sl.renderers = new[] { light.GetComponent<Renderer>() };
            sl.lightSource = PointLight(light.transform, light.transform.position + Vector3.up * 2.6f, Color.red, 6f, 3f);
            return pp;
        }

        static void DebrisFall(StageContext ctx, GameObject chunk, Vector3 landing, float period, float offset)
        {
            var g = ctx.Group("7.6 Debris");
            var root = new GameObject("DebrisFall");
            root.transform.SetParent(g, false);
            root.transform.position = landing;
            chunk = Adopt(ctx, chunk, "7.6 Debris");
            NoColliders(chunk);
            var hz = HazardBox(chunk.transform, "ChunkHit", chunk.transform.position, new Vector3(3.6f, 3.6f, 3.6f), HazardEffect.Respawn, false);
            hz.carrierDamage = 40f;
            var fe = root.AddComponent<FallingEvent>();
            fe.section = 5;
            fe.faller = chunk.transform;
            fe.fallStart = landing + new Vector3(0f, 22f, 0f);
            fe.fallEnd = landing + new Vector3(0f, 1.2f, 0f);
            fe.endEuler = chunk.transform.eulerAngles + new Vector3(30f, 20f, 10f);
            fe.fallTime = 0.8f;
            fe.warnTime = 2.6f;
            fe.repeat = true;
            fe.repeatPeriod = period;
            fe.repeatOffset = offset;
            fe.shadowMarker = ShadowMarker(g, landing, 2.4f);
            fe.message = "Debris incoming!";
            fe.impactShake = 1.1f;
        }
    }
}
