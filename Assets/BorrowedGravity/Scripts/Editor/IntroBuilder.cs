using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static BorrowGravity.EditorTools.BuilderKit;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Builds the opening cinematic (Assets/Scenes/Intro.unity) on the Stage 1 layout, so it ends exactly where
    /// Stage 1 begins: the crashed ship on the broken launch platform.
    /// </summary>
    public static class IntroBuilder
    {
        public const string ScenePath = "Assets/Scenes/Intro.unity";

        static readonly Color ColA = new Color(1f, 0.62f, 0.36f);
        static readonly Color ColB = new Color(0.36f, 0.88f, 1f);
        static readonly Color Narrator = new Color(0.8f, 0.75f, 0.9f);

        public static void Build()
        {
            var ctx = BeginStage("Stage1");
            var root = new GameObject("Cinematic").transform;

            // the layout's parked ship is replaced by an animated one that lands in the same spot
            Remove(ctx.Piece("5.1", "Spaceship"));
            var shipFinalPos = new Vector3(4f, 2.6f, 4.4f);
            var shipFinalRot = new Vector3(0f, 90f, 0f);
            var ship = Spawn("Spaceship", new Vector3(-70f, 16f, 40f), new Vector3(0f, 70f, 6f), root, Vector3.one * 0.75f, "HeroShip");
            NoColliders(ship);

            var trail = Particles(ship.transform, "EngineTrail", new Vector3(0f, 0.6f, 6.2f), Kind.Trail);
            var sparks = Particles(ship.transform, "EngineSparks", new Vector3(0f, 0.8f, 5.6f), Kind.Sparks);
            var smoke = Particles(ship.transform, "EngineSmoke", new Vector3(0f, 1f, 5.6f), Kind.Smoke);
            var blast = Particles(ship.transform, "EngineBlast", new Vector3(0f, 0.8f, 5.6f), Kind.Explosion);
            var dust = Particles(root, "LandingDust", new Vector3(4f, 0.1f, 2.4f), Kind.Dust);
            var alarmLight = PointLight(ship.transform, ship.transform.position + new Vector3(0f, 3f, 0f), new Color(1f, 0.15f, 0.1f), 14f, 6f);
            alarmLight.gameObject.name = "AlarmLight";

            // the astronauts and the broken engine core
            var a = Actor(root, 0, new Vector3(5.4f, 0.92f, 2.2f));
            var b = Actor(root, 1, new Vector3(5.4f, 0.92f, 2.2f));
            var core = Spawn("Engine_Core", new Vector3(6.2f, 1.6f, 3.4f), new Vector3(0f, 90f, 0f), root, null, "BrokenEngineCore");
            NoColliders(core);
            var coreSmoke = Particles(core.transform, "CoreSmoke", new Vector3(0f, 1.6f, 0f), Kind.Smoke);
            var coreSparks = Particles(core.transform, "CoreSparks", new Vector3(0f, 1.2f, 0f), Kind.Sparks);

            // ---------- camera ----------
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.farClipPlane = 2000f;
            cam.fieldOfView = 40f;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            var vol = new GameObject("Cinematic Volume").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 5f;
            vol.sharedProfile = CinematicProfile();

            var cs = new GameObject("Cutscene").AddComponent<Cutscene>();
            cs.cam = cam;
            cs.length = 41f;
            cs.titleTime = 36f;
            cs.subtitle = "Stage 1  ·  The Shattered Orbit";
            cs.fades = new List<Vector2> { new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(39.6f, 0f), new Vector2(41f, 1f) };

            // ---------- shots ----------
            Key(cs, 0f, new Vector3(-58f, 24f, -14f), new Vector3(-62f, 15f, 36f), 34f);          // 1  wide: ship in the debris field
            Key(cs, 6.5f, new Vector3(-40f, 17f, -6f), new Vector3(-30f, 11f, 18f), 38f);
            Key(cs, 7.5f, new Vector3(-36f, 13.5f, 6f), new Vector3(-26f, 10.5f, 15f), 46f);     // 2  close on the engine
            Key(cs, 12.5f, new Vector3(-18f, 10f, -4f), new Vector3(-6f, 7f, 9f), 42f);
            Key(cs, 13.5f, new Vector3(-16f, 9f, -16f), new Vector3(0f, 4f, 5f), 40f);             // 3  crash landing
            Key(cs, 18.5f, new Vector3(-2f, 6f, -15f), new Vector3(4f, 2.2f, 3.5f), 38f);
            Key(cs, 19.5f, new Vector3(1.5f, 2.8f, -6.5f), new Vector3(4.5f, 1.4f, 1.5f), 36f);   // 4  the crew steps out
            Key(cs, 24f, new Vector3(4.5f, 2.4f, -5.2f), new Vector3(6.2f, 1f, 1.2f), 34f);
            Key(cs, 27.5f, new Vector3(8.5f, 3.2f, -7f), new Vector3(9f, -1f, 0.5f), 40f);
            Key(cs, 28.5f, new Vector3(2.5f, 3.6f, -8.5f), new Vector3(3f, 1.4f, 0f), 38f);       // 5  the station far away
            Key(cs, 31.5f, new Vector3(6f, 7f, -15f), new Vector3(70f, 6f, 2f), 34f);
            Key(cs, 35f, new Vector3(16f, 11f, -24f), new Vector3(163f, 8f, 3f), 26f);
            Key(cs, 41f, new Vector3(22f, 13f, -28f), new Vector3(163f, 8f, 3f), 24f);

            // ---------- action ----------
            Move(cs, ship.transform, 0f, 7.5f, new Vector3(-70f, 16f, 40f), new Vector3(-28f, 11f, 16f), new Vector3(0f, 70f, 6f), new Vector3(0f, 80f, 3f));
            Move(cs, ship.transform, 7.5f, 5.5f, new Vector3(-28f, 11f, 16f), new Vector3(-6f, 7.5f, 9f), new Vector3(0f, 80f, 3f), new Vector3(8f, 95f, -14f));
            Move(cs, ship.transform, 13f, 5f, new Vector3(-6f, 7.5f, 9f), shipFinalPos, new Vector3(8f, 95f, -14f), shipFinalRot);
            Cue(cs, 0f, CueType.Activate, trail);
            Cue(cs, 0f, CueType.Deactivate, alarmLight.gameObject);
            Cue(cs, 8f, CueType.Burst, blast, 1f);
            Cue(cs, 8f, CueType.Flash, null);
            Cue(cs, 8f, CueType.Shake, null, 1.8f);
            Cue(cs, 8f, CueType.Sound, null, 1f, SfxId.Impact);
            Cue(cs, 8f, CueType.Deactivate, trail);
            Cue(cs, 8.1f, CueType.Activate, sparks);
            Cue(cs, 8.1f, CueType.Activate, smoke);
            Cue(cs, 8.3f, CueType.AlarmOn, null, 1.5f);
            Cue(cs, 8.3f, CueType.Activate, alarmLight.gameObject);
            Cue(cs, 10.5f, CueType.Shake, null, 0.8f);
            Cue(cs, 10.5f, CueType.Sound, null, 0.7f, SfxId.Crack);
            Cue(cs, 18f, CueType.Shake, null, 1.6f);
            Cue(cs, 18f, CueType.Sound, null, 1f, SfxId.Impact);
            Cue(cs, 18f, CueType.Burst, dust, 1f);
            Cue(cs, 19.2f, CueType.AlarmOff, null);
            Cue(cs, 19.2f, CueType.Deactivate, sparks);
            Cue(cs, 19.4f, CueType.Sound, null, 0.6f, SfxId.Door);
            Cue(cs, 19.5f, CueType.Activate, a.gameObject);
            Cue(cs, 20f, CueType.Activate, b.gameObject);
            Cue(cs, 23f, CueType.Activate, core);
            Cue(cs, 23f, CueType.Activate, coreSmoke);
            Cue(cs, 23f, CueType.Activate, coreSparks);
            Cue(cs, 23.4f, CueType.Sound, null, 0.6f, SfxId.Crack);
            Cue(cs, 26.2f, CueType.Sound, null, 0.6f, SfxId.Hit);
            Cue(cs, 33f, CueType.Deactivate, smoke);

            // A and B climb out, haul the smoking core onto the deck and shove it into the void
            Move(cs, a, 19.5f, 3f, new Vector3(5.4f, 0.92f, 2.2f), new Vector3(2.2f, 0.92f, -0.6f), new Vector3(0, 200, 0), new Vector3(0, 250, 0), true);
            Move(cs, b, 20f, 3f, new Vector3(5.4f, 0.92f, 2.2f), new Vector3(4.0f, 0.92f, -0.8f), new Vector3(0, 200, 0), new Vector3(0, 160, 0), true);
            Move(cs, core.transform, 23f, 2f, new Vector3(6.2f, 1.6f, 3.4f), new Vector3(6.6f, 0.02f, 0.6f), new Vector3(0, 90, 0), new Vector3(8, 70, -6));
            Move(cs, a, 23.2f, 1.6f, new Vector3(2.2f, 0.92f, -0.6f), new Vector3(4.6f, 0.92f, 0.4f), new Vector3(0, 250, 0), new Vector3(0, 90, 0), true);
            Move(cs, b, 23.4f, 1.5f, new Vector3(4.0f, 0.92f, -0.8f), new Vector3(5.0f, 0.92f, -0.6f), new Vector3(0, 160, 0), new Vector3(0, 70, 0), true);
            Move(cs, core.transform, 26f, 2.2f, new Vector3(6.6f, 0.02f, 0.6f), new Vector3(12f, -14f, 1.5f), new Vector3(8, 70, -6), new Vector3(70, 160, 40));
            Move(cs, a, 26f, 0.8f, new Vector3(4.6f, 0.92f, 0.4f), new Vector3(5.3f, 0.92f, 0.4f), new Vector3(0, 90, 0), new Vector3(0, 90, 0), true);
            Move(cs, b, 26f, 0.8f, new Vector3(5.0f, 0.92f, -0.6f), new Vector3(5.6f, 0.92f, -0.6f), new Vector3(0, 70, 0), new Vector3(0, 70, 0), true);
            Move(cs, a, 28.5f, 2f, new Vector3(5.3f, 0.92f, 0.4f), new Vector3(1.9f, 0.92f, 0f), new Vector3(0, 90, 0), new Vector3(0, 80, 0), true);
            Move(cs, b, 28.7f, 2f, new Vector3(5.6f, 0.92f, -0.6f), new Vector3(3.7f, 0.92f, 0f), new Vector3(0, 70, 0), new Vector3(0, 90, 0), true);

            // ---------- dialogue ----------
            Line(cs, 1.5f, 4.2f, "", Narrator, "Somewhere in the Shattered Orbit, a small cargo ship limps home...");
            Line(cs, 8.6f, 2.2f, "A", ColA, "What was that?!");
            Line(cs, 10.9f, 2.6f, "B", ColB, "The engine's gone critical — hold on, we're going down!");
            Line(cs, 14.2f, 3.6f, "", Narrator, "Emergency landing on a broken launch platform.");
            Line(cs, 20.2f, 2.4f, "A", ColA, "Everyone in one piece? ...Good.");
            Line(cs, 22.8f, 2.8f, "B", ColB, "The engine core is fried. Help me pull it out.");
            Line(cs, 25.7f, 2.2f, "A", ColA, "On three — push!");
            Line(cs, 28.4f, 2.8f, "B", ColB, "No spare on board... but the scanner found one.");
            Line(cs, 31.3f, 2.6f, "A", ColA, "In that abandoned station. Across all of that wreckage.");
            Line(cs, 33.9f, 2.4f, "B", ColB, "You bend the gravity, I'll hold things still. Let's go.");

            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)) Object.DestroyImmediate(c);
            Save(ctx, ScenePath);
        }

        // ---------- helpers ----------

        internal static void Key(Cutscene cs, float t, Vector3 pos, Vector3 look, float fov) =>
            cs.cameraKeys.Add(new CutsceneCamKey { time = t, position = pos, lookAt = look, fov = fov });

        internal static void Line(Cutscene cs, float t, float d, string speaker, Color c, string text) =>
            cs.lines.Add(new CutsceneLine { time = t, duration = d, speaker = speaker, color = c, text = text });

        internal static void Cue(Cutscene cs, float t, CueType type, GameObject target, float amount = 1f, SfxId sound = SfxId.Impact) =>
            cs.cues.Add(new CutsceneCue { time = t, type = type, target = target, amount = amount, sound = sound });

        internal static void Move(Cutscene cs, Transform actor, float t, float d, Vector3 from, Vector3 to, Vector3 fromE, Vector3 toE, bool walk = false) =>
            cs.moves.Add(new CutsceneMove { actor = actor, time = t, duration = d, fromPos = from, toPos = to, fromEuler = fromE, toEuler = toE, walk = walk });

        internal static Transform Actor(Transform parent, int index, Vector3 pos)
        {
            var go = new GameObject(index == 0 ? "Astronaut_A" : "Astronaut_B");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            SuitModel(go.transform, index);
            return go.transform;
        }

        internal enum Kind { Trail, Sparks, Smoke, Explosion, Dust }

        internal static GameObject Particles(Transform parent, string name, Vector3 localPos, Kind kind)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            var em = ps.emission;
            var shape = ps.shape;
            var col = ps.colorOverLifetime;
            var size = ps.sizeOverLifetime;
            var r = go.GetComponent<ParticleSystemRenderer>();
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            col.enabled = true;
            size.enabled = true;
            switch (kind)
            {
                case Kind.Trail:
                    main.startLifetime = 0.8f; main.startSpeed = 2f; main.startSize = 1.2f; main.startColor = new Color(0.4f, 0.9f, 1f);
                    em.rateOverTime = 60f; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.3f;
                    col.color = Grad(new Color(0.6f, 0.95f, 1f), new Color(0.2f, 0.5f, 1f), 1f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
                    r.sharedMaterial = ParticleMat("FX_Additive", true);
                    break;
                case Kind.Sparks:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f); main.startColor = new Color(1f, 0.75f, 0.3f); main.gravityModifier = 1.2f;
                    em.rateOverTime = 45f; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35f; shape.radius = 0.2f;
                    col.color = Grad(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.3f, 0.1f), 1f, 0f);
                    r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 3f;
                    r.sharedMaterial = ParticleMat("FX_Additive", true);
                    break;
                case Kind.Smoke:
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f); main.startColor = new Color(0.25f, 0.22f, 0.26f, 0.7f);
                    main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                    em.rateOverTime = 14f; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.4f;
                    col.color = Grad(new Color(0.35f, 0.3f, 0.35f), new Color(0.15f, 0.12f, 0.16f), 0.7f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.2f));
                    r.sharedMaterial = ParticleMat("FX_Smoke", false);
                    break;
                case Kind.Explosion:
                    main.loop = false; main.duration = 0.5f; main.playOnAwake = false;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f); main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 10f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3.2f); main.startColor = new Color(1f, 0.6f, 0.2f);
                    em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
                    shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.6f;
                    col.color = Grad(new Color(1f, 0.95f, 0.6f), new Color(0.6f, 0.1f, 0.05f), 1f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.4f, 1, 1.4f));
                    r.sharedMaterial = ParticleMat("FX_Additive", true);
                    break;
                default: // dust
                    main.loop = false; main.duration = 0.6f; main.playOnAwake = false;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f); main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2f); main.startColor = new Color(0.75f, 0.6f, 0.65f, 0.6f);
                    em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });
                    shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 3f; shape.rotation = new Vector3(-90f, 0f, 0f);
                    col.color = Grad(new Color(0.8f, 0.65f, 0.7f), new Color(0.5f, 0.4f, 0.45f), 0.6f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.8f));
                    r.sharedMaterial = ParticleMat("FX_Smoke", false);
                    break;
            }
            return go;
        }

        static ParticleSystem.MinMaxGradient Grad(Color a, Color b, float alphaA, float alphaB)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(alphaA, 0f), new GradientAlphaKey(alphaA * 0.8f, 0.4f), new GradientAlphaKey(alphaB, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static Material ParticleMat(string name, bool additive)
        {
            string path = $"Assets/Materials/Game/{name}.mat";
            var shader = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.mainTexture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            m.SetColor("_TintColor", additive ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : new Color(0.5f, 0.5f, 0.5f, 0.6f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Stronger cinematic grade for the cutscene: bloom, vignette, slight depth of field.</summary>
        internal static VolumeProfile CinematicProfile()
        {
            string path = "Assets/Materials/Game/BG_CinematicFX.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.3f);
            bloom.scatter.Override(0.7f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.38f);
            vig.color.Override(new Color(0.08f, 0.01f, 0.08f));
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(90f);
            dof.gaussianEnd.Override(400f);
            dof.gaussianMaxRadius.Override(0.5f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.contrast.Override(10f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
