using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Level 7's goal: the players no longer start with the engine. Ten parts are spread over the arena, the escape route
    /// and the ship deck, with the last two behind the Warden's force field; each one shows up in the finder's hands, and
    /// the tenth completes the level. No countdown: the Warden is the pressure, and the results screen shows the time.
    /// Works on the level's prefab and can be run again.
    /// </summary>
    public static class Level7PartsHunt
    {
        const string PrefabPath = "Assets/feeza prelevels prefabs/Level 7.prefab";

        // where the ten parts wait, in the order the route reaches them
        static readonly Vector3[] Spots =
        {
            new Vector3(-12f, 1.3f, 12f), new Vector3(12f, 1.3f, -12f), new Vector3(6f, 1.3f, 18f),   // arena, under fire
            new Vector3(42f, 1.3f, 2f), new Vector3(58f, 4.3f, -2f), new Vector3(76f, 4.3f, 0f),       // pad deck, high deck, on the shuttle's way
            new Vector3(93f, 4.3f, -4f), new Vector3(100f, 4.3f, 4f),                                  // ship deck, during the last stand
            new Vector3(108.5f, 4.3f, -3f), new Vector3(108.5f, 4.3f, 3f)                              // behind the force field
        };

        [MenuItem("BorrowGravity/Level 7/Add Engine Parts Hunt", priority = 66)]
        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "EngineStack" || t.name == "EngineParts").ToArray())
                    Object.DestroyImmediate(t.gameObject);

                // the players don't arrive with the engine any more
                foreach (var half in root.GetComponentsInChildren<EnginePart>(true).ToArray())
                    Object.DestroyImmediate(half.gameObject);

                var boss = root.GetComponentInChildren<WardenBoss>(true);
                var mats = boss.model.GetComponentsInChildren<Renderer>(true);
                var hull = mats.First(r => r.name == "Body").sharedMaterial;
                var ember = mats.First(r => r.name == "HealthLight").sharedMaterial;
                var safe = root.GetComponentsInChildren<Renderer>(true).First(r => r.name == "ShipDeckLight").sharedMaterial;

                var gameplay = root.transform.Find("Gameplay");
                var group = new GameObject("EngineParts").transform;
                group.SetParent(gameplay, false);
                var hunt = group.gameObject.AddComponent<EnginePartHunt>();
                hunt.pickups = Spots.Select((s, i) => Pickup(group, i, s, hull, ember, safe)).ToArray();
                foreach (var p in root.GetComponentsInChildren<PlayerController>(true))
                {
                    var pieces = HandPieces(p, hull, ember, safe);
                    if (p.playerIndex == 0) hunt.handPiecesA = pieces;
                    else if (p.playerIndex == 1) hunt.handPiecesB = pieces;
                }

                // the goal is the tenth part now, and nothing times the run
                var goal = root.GetComponentInChildren<GoalTrigger>(true);
                if (goal != null) goal.gameObject.SetActive(false);
                var beacon = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Finish_The_Ship");
                if (beacon != null) beacon.gameObject.SetActive(false);
                boss.useEscapeTimer = false;

                var sm = root.GetComponentInChildren<StageManager>(true);
                sm.objectiveText = "Find the 10 engine parts";
                sm.introText = "Your ship is just past this platform, but its engine lies in pieces, scattered all over the graveyard. Find all ten and it can fly.\n\n" +
                               "The old security machine won't let you. B freezes it. A changes gravity while it's frozen, and the shift tears at its frame.\n\n" +
                               "Three hits open the way out. It will hunt you along the route and meet you again at the ship, with the last parts behind its force field.";
                sm.completeText = "All ten parts, and the Warden is scrap. Fit the engine, and it's time to go home.";

                foreach (var b in root.GetComponentsInChildren<InfoBoard>(true))
                    if (b.title != null && b.title.text.Contains("WARDEN"))
                        b.body.text = "Find 10 engine parts: look for the glowing\npillars. B: freeze the Warden (get close and\nface it). A: change gravity while it's frozen.";

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Level7] engine parts hunt added");
        }

        /// <summary>A glowing engine piece with a tall pillar of light, so it can be spotted from across the level.</summary>
        static GameObject Pickup(Transform group, int index, Vector3 at, Material hull, Material ember, Material safe)
        {
            var root = new GameObject("EnginePart_" + (index + 1)).transform;
            root.SetParent(group, false);
            root.position = at;
            LevelArt.Shape(PrimitiveType.Cylinder, root, "Core", at, new Vector3(0.62f, 0.26f, 0.62f), hull).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            LevelArt.Shape(PrimitiveType.Cylinder, root, "RingL", at + Vector3.left * 0.2f, new Vector3(0.74f, 0.05f, 0.74f), ember).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            LevelArt.Shape(PrimitiveType.Cylinder, root, "RingR", at + Vector3.right * 0.2f, new Vector3(0.74f, 0.05f, 0.74f), ember).transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            LevelArt.Shape(PrimitiveType.Cylinder, root, "Pillar", at + Vector3.up * 8f, new Vector3(0.14f, 8f, 0.14f), safe);
            BuilderKit.PointLight(root, at + Vector3.up * 0.6f, new Color(1f, 0.55f, 0.2f), 6f, 2.5f);
            return root.gameObject;
        }

        /// <summary>Ten pieces that build a small engine in the hands, hidden until they're found.</summary>
        static GameObject[] HandPieces(PlayerController player, Material hull, Material ember, Material safe)
        {
            var stack = new GameObject("EngineStack").transform;
            stack.SetParent(player.transform, false);
            stack.localPosition = new Vector3(0f, 0.05f, 0.62f);
            var at = stack.position;
            var pieces = new List<GameObject>();
            var side = Quaternion.Euler(0f, 0f, 90f);
            for (int i = 0; i < 7; i++)
            {
                bool thick = i % 2 == 0;
                var ring = LevelArt.Shape(PrimitiveType.Cylinder, stack, "Ring" + i, at + player.transform.right * (-0.36f + i * 0.12f),
                    thick ? new Vector3(0.34f, 0.06f, 0.34f) : new Vector3(0.26f, 0.06f, 0.26f), thick ? hull : ember);
                ring.transform.localRotation = side;
                pieces.Add(ring);
            }
            pieces.Add(LevelArt.Shape(PrimitiveType.Sphere, stack, "NozzleR", at + player.transform.right * 0.47f, Vector3.one * 0.2f, ember));
            pieces.Add(LevelArt.Shape(PrimitiveType.Sphere, stack, "NozzleL", at - player.transform.right * 0.47f, Vector3.one * 0.2f, ember));
            pieces.Add(LevelArt.Box(stack, "Glow", at + player.transform.up * 0.2f, new Vector3(0.5f, 0.05f, 0.1f), safe));
            foreach (var piece in pieces)
            {
                foreach (var c in piece.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                piece.SetActive(false);
            }
            // big enough to stick out past the body, so the camera behind the player can see it grow
            stack.localPosition = new Vector3(0f, 0.2f, 0.55f);
            stack.localScale = Vector3.one * 1.7f;
            return pieces.ToArray();
        }
    }
}
