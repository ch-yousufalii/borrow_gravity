using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Level 7's second meeting with the Warden, on the ship deck: a gravity zone, a force field in front of the ship, a
    /// checkpoint, and the goal moved behind the field. Works on the level's prefab (Game.unity's Level_Stage7 is an
    /// instance of it), so it can be run again safely.
    /// </summary>
    public static class Level7LastStand
    {
        const string PrefabPath = "Assets/feeza prelevels prefabs/Level_Stage7.prefab";
        const string AltPrefabPath = "Assets/feeza prelevels prefabs/Level 7.prefab";
        static readonly Vector3 Center = new Vector3(96.5f, 3f, 0f);

        [MenuItem("BorrowGravity/Level 7/Add Last Stand", priority = 64)]
        public static void Apply()
        {
            string path = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null ? PrefabPath : AltPrefabPath;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var all = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in all.Where(t => t.name == "LastStandBarrier" || t.name == "Zone_Last Stand" || t.name == "CheckpointTrigger_Last_Stand" || t.name == "Checkpoint_Last_Stand"))
                    Object.DestroyImmediate(t.gameObject);

                var boss = root.GetComponentInChildren<WardenBoss>(true);
                var oldBarrier = boss.exitBarrier;

                // gravity zone over the fighting part of the deck
                var zoneGo = new GameObject("Zone_Last Stand");
                zoneGo.transform.SetParent(boss.arenaZone.transform.parent, false);
                zoneGo.transform.position = Center + new Vector3(0f, 12f, 0f);
                var zone = zoneGo.AddComponent<GravityZone>();
                zone.zoneName = "Last Stand";
                zone.section = 2;
                zone.size = new Vector3(19f, 34f, 14f);
                BuilderKit.SetMode(zone, GravityMode.Down);
                zone.cycle = new[] { GravityMode.Down, GravityMode.Zero };
                zone.controllable = true;
                zone.affectsPlayers = true;
                zone.affectsObjects = true;
                zone.drawOutline = false;
                zone.arrows = new Transform[0];
                zone.indicatorRenderers = new Renderer[0];

                // force field across the deck, in front of the ship
                var barrier = Object.Instantiate(oldBarrier, oldBarrier.transform.parent);
                barrier.name = "LastStandBarrier";
                barrier.transform.position = new Vector3(106f, 8f, 0f);
                barrier.transform.localScale = new Vector3(0.5f, 10f, 12f);

                // the goal sits behind the field
                var goal = root.GetComponentInChildren<GoalTrigger>(true);
                goal.transform.position = new Vector3(109f, goal.transform.position.y, 0f);
                var beacon = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Finish_The_Ship");
                if (beacon != null) beacon.position = new Vector3(109f, beacon.position.y, beacon.position.z);

                // checkpoint at the start of the deck
                var bridge = root.GetComponentsInChildren<Checkpoint>(true).First(c => c.label == "Bridge");
                var cpGo = new GameObject("CheckpointTrigger_Last_Stand");
                cpGo.transform.SetParent(bridge.transform.parent, false);
                cpGo.transform.position = new Vector3(90f, 3.03f, 0f);
                var box = cpGo.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, 2f, 0f);
                box.size = new Vector3(3f, 4f, 11f);
                var cp = cpGo.AddComponent<Checkpoint>();
                cp.section = 2;
                cp.label = "Last Stand";
                cp.spawnOffsets = new[] { new Vector3(-1f, 1.05f, -1f), new Vector3(-1f, 1.05f, 1f) };

                boss.lastStandZone = zone;
                boss.lastStandBarrier = barrier;
                boss.lastStandCenter = Center;
                boss.lastStandStartX = 91f;
                boss.lastStandHits = 2;

                var sm = root.GetComponentInChildren<StageManager>(true);
                if (sm != null && sm.introText.EndsWith("Three hits. Then run."))
                    sm.introText = sm.introText.Replace("Three hits. Then run.", "Three hits, then run. It won't stay down: it will meet you again at the ship.");

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Level7] Last stand added to " + path);
        }
    }
}
