using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static BorrowGravity.EditorTools.BuilderKit;
using static BorrowGravity.EditorTools.IntroBuilder;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Builds the ending cinematic (Assets/Scenes/Outro.unity) on the Stage 3 layout: A and B carry the new engine to
    /// the ship, slide it into the bay, the engine comes alive, they climb aboard and the ship takes off.
    /// </summary>
    public static class OutroBuilder
    {
        public const string ScenePath = "Assets/Scenes/Outro.unity";

        const float HandY = 0.62f;

        static readonly Color ColA = new Color(1f, 0.62f, 0.36f);
        static readonly Color ColB = new Color(0.36f, 0.88f, 1f);
        static readonly Color Narrator = new Color(0.8f, 0.75f, 0.9f);

        public static void Build()
        {
            var ctx = BeginStage("Stage3");
            var root = new GameObject("Cinematic").transform;

            // the layout's parked ship is swapped for one that can fly away
            var parked = ctx.Piece("7.8", "Spaceship");
            Vector3 shipEuler = parked != null ? parked.transform.eulerAngles : new Vector3(0f, 90f, 0f);
            Vector3 shipScale = parked != null ? parked.transform.localScale : Vector3.one;
            if (parked != null) Remove(parked);
            var dock = ctx.Piece("7.8", "Docking_Platform");
            if (dock != null) dock.transform.position = new Vector3(164f, -0.2f, 0f);
            var shipPos = new Vector3(165f, 2.4f, 4.8f);
            // the model's nose is its local -z (the engine sits at +z): turn it round so it flies away nose first
            var heroEuler = shipEuler + new Vector3(0f, 180f, 0f);
            var awayTo = new Vector3(330f, 62f, 60f);
            var away = awayTo - (shipPos + new Vector3(0f, 4.6f, 0f));
            var awayEuler = new Vector3(Mathf.Atan2(away.y, new Vector2(away.x, away.z).magnitude) * Mathf.Rad2Deg * 0.8f,
                                        Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg + 180f, 0f);
            var ship = Spawn("Spaceship", shipPos, heroEuler, root, shipScale, "HeroShip");
            NoColliders(ship);
            var trail = Particles(ship.transform, "EngineTrail", new Vector3(0f, 0.6f, 6.2f), Kind.Trail);
            var blast = Particles(ship.transform, "EngineBlast", new Vector3(0f, 0.8f, 5.6f), Kind.Explosion);
            var dust = Particles(root, "LiftOffDust", new Vector3(165f, -0.1f, 3f), Kind.Dust);
            var engineGlow = PointLight(ship.transform, shipPos + new Vector3(0f, 1.5f, 0f), new Color(0.35f, 0.9f, 1f), 12f, 5f);
            engineGlow.gameObject.name = "EngineGlow";

            // the new engine in two halves, one in each astronaut's hands
            var halfA = EngineHalf(root, "EngineHalf_A", new Color(1f, 0.62f, 0.36f));
            var halfB = EngineHalf(root, "EngineHalf_B", new Color(0.36f, 0.88f, 1f));
            var bayPos = new Vector3(162f, -0.2f, 0.4f);
            var baySparks = Particles(root, "BaySparks", bayPos + new Vector3(0f, 1.2f, 0f), Kind.Explosion);

            const float deckY = 0.72f;
            var a = Actor(root, 0, new Vector3(152f, deckY, -0.6f));
            var b = Actor(root, 1, new Vector3(152.4f, deckY, 0.6f));
            foreach (var actor in new[] { a, b })
            {
                var anim = actor.GetComponent<SuitAnimator>();
                if (anim != null) anim.holdArms = true;
            }

            // ---------- camera ----------
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 2000f;
            cam.fieldOfView = 40f;
            camGo.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var vol = new GameObject("Cinematic Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 5f;
            vol.sharedProfile = CinematicProfile();

            var cs = new GameObject("Cutscene").AddComponent<Cutscene>();
            cs.cam = cam;
            cs.isEnding = true;
            cs.length = 34f;
            cs.titleTime = 27f;
            cs.title = "BORROWED GRAVITY";
            cs.subtitle = "Mission complete  ·  Thanks for playing";
            cs.fades = new List<Vector2> { new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(32.6f, 0f), new Vector2(34f, 1f) };

            // ---------- shots ----------
            Key(cs, 0f, new Vector3(149f, 4f, -10f), new Vector3(155f, 1f, 0f), 40f);         // 1  the carry
            Key(cs, 6.5f, new Vector3(156f, 3.2f, -9f), new Vector3(160f, 0.8f, 0f), 40f);
            Key(cs, 7.2f, new Vector3(159.5f, 2.2f, -5f), new Vector3(162f, 0.4f, 0.6f), 38f);  // 2  into the bay
            Key(cs, 11f, new Vector3(161f, 3f, -6f), new Vector3(164.5f, 1.6f, 3f), 40f);
            Key(cs, 12f, new Vector3(156f, 5f, -12f), new Vector3(165f, 2f, 3f), 42f);          // 3  boarding
            Key(cs, 16f, new Vector3(154f, 4f, -14f), new Vector3(165f, 4f, 4.8f), 42f);       // 4  lift-off
            Key(cs, 19f, new Vector3(150f, 6f, -16f), new Vector3(172f, 9f, 10f), 40f);
            Key(cs, 25f, new Vector3(148f, 8f, -18f), new Vector3(260f, 40f, 45f), 30f);       // 5  gone
            Key(cs, 34f, new Vector3(148f, 9f, -18f), new Vector3(300f, 52f, 55f), 26f);

            // ---------- action ----------
            // the carry: the engine between them, both walking in step
            Move(cs, a, 0f, 7f, new Vector3(152f, deckY, -0.6f), new Vector3(160f, deckY, -0.6f), new Vector3(0, 90, 0), new Vector3(0, 90, 0), true);
            Move(cs, b, 0f, 7f, new Vector3(152.4f, deckY, 0.6f), new Vector3(160.4f, deckY, 0.6f), new Vector3(0, 90, 0), new Vector3(0, 90, 0), true);
            Move(cs, halfA, 0f, 7f, new Vector3(152.65f, HandY, -0.6f), new Vector3(160.65f, HandY, -0.6f), new Vector3(0, 90, 0), new Vector3(0, 90, 0));
            Move(cs, halfB, 0f, 7f, new Vector3(153.05f, HandY, 0.6f), new Vector3(161.05f, HandY, 0.6f), new Vector3(0, 90, 0), new Vector3(0, 90, 0));
            // each sets their half into the bay, then the halves slide up into the ship
            Move(cs, a, 7.3f, 1.2f, new Vector3(160f, deckY, -0.6f), new Vector3(160.8f, deckY, -0.1f), new Vector3(0, 90, 0), new Vector3(0, 70, 0), true);
            Move(cs, b, 7.3f, 1.2f, new Vector3(160.4f, deckY, 0.6f), new Vector3(161.6f, deckY, 1.3f), new Vector3(0, 90, 0), new Vector3(0, 140, 0), true);
            Move(cs, halfA, 7.3f, 1.2f, new Vector3(160.65f, HandY, -0.6f), new Vector3(161.5f, HandY, -0.1f), new Vector3(0, 90, 0), new Vector3(0, 70, 0));
            Move(cs, halfB, 7.3f, 1.2f, new Vector3(161.05f, HandY, 0.6f), new Vector3(162.2f, HandY, 1.0f), new Vector3(0, 90, 0), new Vector3(0, 140, 0));
            Move(cs, halfA, 8.6f, 0.8f, new Vector3(161.5f, HandY, -0.1f), new Vector3(161.5f, 0.15f, 0.4f), new Vector3(0, 70, 0), new Vector3(0, 90, 0));
            Move(cs, halfB, 8.8f, 0.8f, new Vector3(162.2f, HandY, 1.0f), new Vector3(162.5f, 0.15f, 0.4f), new Vector3(0, 140, 0), new Vector3(0, 90, 0));
            Move(cs, halfA, 9.7f, 1.3f, new Vector3(161.5f, 0.15f, 0.4f), shipPos + new Vector3(-0.5f, -0.6f, -1.2f), new Vector3(0, 90, 0), new Vector3(0, 90, 0));
            Move(cs, halfB, 9.7f, 1.3f, new Vector3(162.5f, 0.15f, 0.4f), shipPos + new Vector3(0.5f, -0.6f, -1.2f), new Vector3(0, 90, 0), new Vector3(0, 90, 0));
            // boarding
            Move(cs, a, 12f, 2.6f, new Vector3(160.8f, deckY, -0.1f), new Vector3(164.2f, deckY, 2.8f), new Vector3(0, 70, 0), new Vector3(0, 20, 0), true);
            Move(cs, b, 12.3f, 2.4f, new Vector3(161.6f, deckY, 1.3f), new Vector3(165.8f, deckY, 2.8f), new Vector3(0, 140, 0), new Vector3(0, 340, 0), true);
            // lift-off and away
            Move(cs, ship.transform, 16f, 3f, shipPos, shipPos + new Vector3(0f, 4.6f, 0f), heroEuler, heroEuler + new Vector3(6f, 0f, 0f));
            Move(cs, ship.transform, 19f, 6f, shipPos + new Vector3(0f, 4.6f, 0f), awayTo, heroEuler + new Vector3(6f, 0f, 0f), awayEuler);

            Cue(cs, 0f, CueType.Deactivate, trail);
            Cue(cs, 0f, CueType.Deactivate, engineGlow.gameObject);
            Cue(cs, 9.4f, CueType.Burst, baySparks, 1f);
            Cue(cs, 9.4f, CueType.Sound, null, 0.8f, SfxId.Plate);
            Cue(cs, 11f, CueType.Deactivate, halfA.gameObject);
            Cue(cs, 11f, CueType.Deactivate, halfB.gameObject);
            Cue(cs, 11f, CueType.Sound, null, 1f, SfxId.Success);
            Cue(cs, 11f, CueType.Activate, engineGlow.gameObject);
            Cue(cs, 11f, CueType.Activate, trail);
            Cue(cs, 11f, CueType.Shake, null, 0.5f);
            Cue(cs, 14.6f, CueType.Sound, null, 0.6f, SfxId.Door);
            Cue(cs, 14.7f, CueType.Deactivate, a.gameObject);
            Cue(cs, 14.8f, CueType.Deactivate, b.gameObject);
            Cue(cs, 16f, CueType.Burst, dust, 1f);
            Cue(cs, 16f, CueType.Shake, null, 1.2f);
            Cue(cs, 16f, CueType.Sound, null, 0.9f, SfxId.GravityShift);
            Cue(cs, 19f, CueType.Burst, blast, 1f);
            Cue(cs, 19f, CueType.Flash, null);
            Cue(cs, 19f, CueType.Shake, null, 1.6f);
            Cue(cs, 19f, CueType.Sound, null, 1f, SfxId.Impact);

            Crew(cs, root, a, b, new Vector3(-1.4f, 0f, -0.6f), new Vector3(-1.4f, 0f, 0.6f), (1, halfB.gameObject));

            // ---------- dialogue ----------
            Line(cs, 1.6f, 2.9f, "", Narrator, "Back at the docking platform, with a brand-new engine...");
            Line(cs, 4.7f, 2.1f, "A", ColA, "Got your half? Don't drop it now...");
            Line(cs, 7.0f, 2.4f, "B", ColB, "Wouldn't dream of it. Bay's open — both halves in!");
            Line(cs, 9.6f, 1.5f, "A", ColA, "Locking mine in... now!");
            Line(cs, 11.3f, 2.1f, "B", ColB, "Engine's online! Listen to that hum.");
            Line(cs, 13.6f, 2.2f, "A", ColA, "Then let's go home. Together.");
            Line(cs, 16.3f, 2.0f, "B", ColB, "Station's coming apart — punch it!");
            Line(cs, 20.5f, 3.8f, "", Narrator, "Two travellers, one borrowed gravity — and a ship that flies again.");
            Line(cs, 24.6f, 2.0f, "A", ColA, "Next time, we bring a spare.");

            // ---------- cinematic sound ----------
            Cue(cs, 11.0f, CueType.Sound, null, 0.6f, SfxId.Rumble);
            Cue(cs, 16.0f, CueType.Sound, null, 0.8f, SfxId.Rumble);
            Cue(cs, 16.2f, CueType.Sound, null, 0.5f, SfxId.Whoosh);
            Cue(cs, 19.0f, CueType.Sound, null, 1f, SfxId.Boom);
            Cue(cs, 19.1f, CueType.Sound, null, 0.9f, SfxId.Whoosh);
            Cue(cs, 24.2f, CueType.Sound, null, 0.7f, SfxId.Riser);
            Cue(cs, 27.0f, CueType.Sound, null, 0.6f, SfxId.Boom);

            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) Object.DestroyImmediate(c);
            Save(ctx, ScenePath);
        }

        /// <summary>A hand-held engine half (scaled-down core with a coloured glow) for the cutscene.</summary>
        internal static Transform EngineHalf(Transform parent, string name, Color glow)
        {
            var half = new GameObject(name).transform;
            half.SetParent(parent, false);
            var core = Spawn("Engine_Core", Vector3.zero, Vector3.zero, half, Vector3.one * 0.45f, "Core");
            core.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            NoColliders(core);
            PointLight(half, half.position + Vector3.up * 0.6f, glow, 3.5f, 2.2f);
            return half;
        }
    }
}
