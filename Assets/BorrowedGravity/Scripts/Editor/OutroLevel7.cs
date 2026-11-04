using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Moves the ending (Level_Outro in the Game scene) from the old Stage 3 layout to where the game now ends: Level 7's
    /// ship deck, its sky and backdrop, with the Warden's wreck on the deck. The crew's choreography is kept and shifted
    /// to the deck; the ship uses its Level 7 pose. Run once after building the outro (a second run does nothing).
    /// </summary>
    public static class OutroLevel7
    {
        // old docking platform -> Level 7 ship deck
        static readonly Vector3 Shift = new Vector3(-63f, 3.18f, 0f);
        static readonly Vector3 ShipPos = new Vector3(102f, 5.5f, 10f);
        static readonly Vector3 ShipEuler = new Vector3(0f, 270f, 0f); // the model's nose is local -z: faces +x

        [MenuItem("BorrowGravity/Level 7/Move Ending To Ship Deck", priority = 65)]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            var roots = scene.GetRootGameObjects();
            var outro = roots.First(r => r.name == "Level_Outro");
            var l7 = roots.First(r => r.name == "Level_Stage7");
            var cs = outro.GetComponentInChildren<Cutscene>(true);
            var ship = outro.GetComponentsInChildren<Transform>(true).First(t => t.name == "HeroShip");
            if (ship.position.x < 150f) { Debug.Log("[Outro] already on the ship deck"); return; }

            Environment(outro, l7);
            Lighting(outro, l7);
            Choreography(cs, outro, ship);
            Wreck(cs, outro, l7);
            Story(cs);
            Level7Text();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Outro] ending moved to Level 7's ship deck");
        }

        static void Environment(GameObject outro, GameObject l7)
        {
            var env = outro.transform.Find("Environment");
            for (int i = env.childCount - 1; i >= 0; i--) Object.DestroyImmediate(env.GetChild(i).gameObject);

            var gameplay = l7.transform.Find("Gameplay");
            var backdrop = Object.Instantiate(gameplay.Find("Backdrop").gameObject, env);
            backdrop.name = "Backdrop";

            // the route and the deck, as scenery
            var escape = Object.Instantiate(gameplay.Find("Escape").gameObject, env);
            escape.name = "Ship Deck";
            foreach (var t in escape.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t == null || t == escape.transform) continue;
                if (t.name == "HomeShip" || t.name.StartsWith("Finish") || t.name == "EscapeWall" || t.name == "EscapeEnd")
                    Object.DestroyImmediate(t.gameObject);
            }
            Strip(escape);
        }

        /// <summary>Scenery only: no gameplay scripts, physics or colliders.</summary>
        static void Strip(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        static void Lighting(GameObject outro, GameObject l7)
        {
            var from = l7.GetComponent<StageRoot>();
            var to = outro.GetComponent<StageRoot>();
            to.skybox = from.skybox;
            to.ambientMode = from.ambientMode;
            to.ambientIntensity = from.ambientIntensity;
            to.ambientLight = from.ambientLight;
            to.fog = from.fog;
            to.fogMode = from.fogMode;
            to.fogDensity = from.fogDensity;
            to.fogColor = from.fogColor;
            EditorUtility.SetDirty(to);
            foreach (var name in new[] { "Sun", "Rim Light" })
            {
                var a = l7.transform.Find("Lighting/" + name);
                var b = outro.transform.Find("Lighting/" + name);
                if (a == null || b == null) continue;
                b.rotation = a.rotation;
                var la = a.GetComponent<Light>();
                var lb = b.GetComponent<Light>();
                lb.color = la.color;
                lb.intensity = la.intensity;
                EditorUtility.SetDirty(lb);
            }
        }

        static void Choreography(Cutscene cs, GameObject outro, Transform ship)
        {
            foreach (var t in outro.transform.Find("Cinematic").Cast<Transform>())
                if (t != ship) t.position += Shift;
            var dust = outro.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "LiftOffDust");
            if (dust != null) dust.position = new Vector3(ShipPos.x, 3.1f, 6f);

            foreach (var m in cs.moves.Where(m => m.actor != null && m.actor != ship))
            {
                m.fromPos += Shift;
                m.toPos += Shift;
            }

            // the ship: Level 7's pose, then up and away along the flight path, nose first
            var hover = ShipPos + new Vector3(0f, 5f, 0f);
            var away = hover + new Vector3(165f, 55f, 55f);
            var d = away - hover;
            var climb = ShipEuler + new Vector3(6f, 0f, 0f);
            var leave = new Vector3(Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg * 0.8f, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 180f, 0f);
            ship.SetPositionAndRotation(ShipPos, Quaternion.Euler(ShipEuler));
            ship.localScale = Vector3.one * 1.4f;
            foreach (var m in cs.moves.Where(m => m.actor == ship))
            {
                bool lift = m.time < 17f;
                m.fromPos = lift ? ShipPos : hover;
                m.toPos = lift ? hover : away;
                m.fromEuler = lift ? ShipEuler : climb;
                m.toEuler = lift ? climb : leave;
            }

            // camera: same shots on the deck; from boarding on, framed on the ship at its new spot
            foreach (var k in cs.cameraKeys)
            {
                k.position += Shift;
                k.lookAt += Shift;
                if (k.time >= 11f) { k.position += new Vector3(-2f, 2f, -3f); k.lookAt.z += 4f; }
            }
            EditorUtility.SetDirty(ship);
            EditorUtility.SetDirty(cs);
        }

        /// <summary>What's left of the Warden, smoking on the deck: the fight really did just end here.</summary>
        static void Wreck(Cutscene cs, GameObject outro, GameObject l7)
        {
            var boss = l7.GetComponentInChildren<WardenBoss>(true);
            var cinematic = outro.transform.Find("Cinematic");
            var wreck = Object.Instantiate(boss.model, cinematic);
            wreck.name = "WardenWreck";
            wreck.SetActive(true);
            Strip(wreck);
            foreach (var t in wreck.GetComponentsInChildren<Transform>(true).ToArray())
                if (t != null && t.name == "HealthLight") Object.DestroyImmediate(t.gameObject);
            foreach (var l in wreck.GetComponentsInChildren<Light>(true)) l.intensity *= 0.35f;
            wreck.transform.SetPositionAndRotation(new Vector3(90.5f, 3.9f, 4.2f), Quaternion.Euler(22f, 215f, 14f)); // behind the crew, clear of every camera shot
            wreck.transform.localScale = Vector3.one * 0.85f;

            var sparks = outro.GetComponentsInChildren<Transform>(true).First(t => t.name == "BaySparks");
            var wreckSparks = Object.Instantiate(sparks.gameObject, cinematic);
            wreckSparks.name = "WreckSparks";
            wreckSparks.transform.position = wreck.transform.position + Vector3.up * 1.6f;
            foreach (float time in new[] { 0.8f, 3.6f, 6.4f, 12.5f })
                cs.cues.Add(new CutsceneCue { time = time, type = CueType.Burst, target = wreckSparks, amount = 0.7f });
            cs.cues.Sort((a, b) => a.time.CompareTo(b.time));
        }

        static void Story(Cutscene cs)
        {
            void Say(string old, string now)
            {
                var line = cs.lines.FirstOrDefault(l => l.text.StartsWith(old));
                if (line != null) line.text = now;
            }
            Say("Back at the docking platform", "The Warden is scrap. Nothing stands between them and the ship now...");
            Say("Station's coming apart", "That star won't wait for us. Punch it!");
            cs.subtitle = "Chapter 1 complete  ·  Thanks for playing";
        }

        static void Level7Text()
        {
            const string path = "Assets/feeza prelevels prefabs/Level 7.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var sm = root.GetComponentInChildren<StageManager>(true);
                sm.completeText = "The Warden is scrap and the ship is right there. Fit the engine, and it's time to go home.";
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
