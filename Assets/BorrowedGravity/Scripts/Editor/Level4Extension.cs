using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Level 4 extension, "7.9 Event Horizon Run": its own art (generated obsidian hex platforms with gold / violet
    /// glowing rims, crystal shards, spires, a sweeper beam and the escape gate — nothing from Levels 1-3) and four
    /// medium hurdles between the last deck and the ship: phase steps, an orbit gate, a sweeper beam and bobbing lifts.
    /// Works on the level 4 prefab (Game.unity's Level_Stage4 is an instance of it), so re-running rebuilds the section.
    /// </summary>
    public static class Level4Extension
    {
        const string PrefabPath = "Assets/feeza prelevels prefabs/level 4.prefab";
        const string ArtRoot = "Assets/BorrowedGravity/Art/Level4";
        const string GroupName = "7.9 Event Horizon Run";
        // the ship platform used to start at x 202; the new section fills the space and the ship moves on
        const float ShipShift = 74.5f;
        const float Top = 0f; // walking height of the new platforms

        static readonly HashSet<string> OwnNames = new HashSet<string>
        {
            "CheckpointTrigger_Event_Horizon", "CheckpointTrigger_Orbit_Gate", "CheckpointTrigger_Sweeper", "CheckpointTrigger_Lifts"
        };

        static Material obsidian, gold, violet, hazard;
        static Mesh hexMesh, hexRimMesh, shardMesh, spireMesh, beamMesh, slabMesh, gateMesh;

        [MenuItem("BorrowGravity/Level 4/Build Event Horizon Run", priority = 60)]
        public static void Build()
        {
            MakeArt();
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var gameplay = root.transform.Find("Gameplay");
                var old = gameplay.Find(GroupName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
                // checkpoints and hints of an earlier build live in the shared groups
                foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => OwnNames.Contains(t.name) || t.name.StartsWith("Hint_7.9")).ToArray())
                    Object.DestroyImmediate(t.gameObject);
                ShiftShip(root);
                var group = new GameObject(GroupName).transform;
                group.SetParent(gameplay, false);
                BuildSection(root, group);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("[Level4Extension] Event Horizon Run built into " + PrefabPath);
        }

        // ---------- the section ----------

        static void BuildSection(GameObject root, Transform g)
        {
            // rest spot after the last deck + checkpoint
            Hex(g, "EH_Rest0", new Vector3(202.5f, Top, 0f), 2.2f, false);
            Checkpoint(root, g, "Event Horizon", 8, new Vector3(202.5f, Top, 0f));
            Hint(root, "7.9  PHASE STEPS — cross while they glow", new Vector3(202.5f, Top, 0f));
            Spire(g, new Vector3(201.5f, Top, 4.6f), 7f);

            // 1. phase steps: a slow wave, each solid 3.5 s, blinking 1 s, gone 1 s
            for (int i = 0; i < 5; i++)
            {
                var p = Hex(g, "EH_PhaseStep" + i, new Vector3(207f + i * 4.3f, Top, 0f), 2f, true);
                var ph = p.AddComponent<PhasePlatform>();
                ph.section = 8;
                ph.solidTime = 3.5f; // medium: long solid window, short gap
                ph.goneTime = 1f;
                ph.offset = -i * 0.9f;
            }

            // 2. orbit gate: a wide platform with two slow shards circling it (a hit knocks you back, it doesn't kill)
            Hex(g, "EH_Rest1", new Vector3(228.6f, Top, 0f), 2.2f, false);
            Checkpoint(root, g, "Orbit Gate", 9, new Vector3(228.6f, Top, 0f));
            Hint(root, "7.9  ORBIT GATE — slip between the shards", new Vector3(228.6f, Top, 0f));
            Hex(g, "EH_OrbitDeck", new Vector3(236.2f, Top, 0f), 5f, false);
            var arms = new GameObject("EH_OrbitArms");
            arms.transform.SetParent(g, false);
            arms.transform.position = new Vector3(236.2f, Top + 1.1f, 0f);
            var rot = arms.AddComponent<Rotator>();
            rot.section = 9; rot.axis = Vector3.up; rot.degreesPerSecond = 40f;
            for (int i = 0; i < 2; i++)
            {
                float a = i * Mathf.PI;
                Shard(arms.transform, "EH_Shard" + i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.4f);
            }
            Spire(g, new Vector3(236.2f, Top, 7.5f), 9f);
            Spire(g, new Vector3(236.2f, Top, -7.5f), 6f);

            // 3. sweeper: a long walkway with a low beam turning over it — jump it
            Hex(g, "EH_Rest2", new Vector3(243.8f, Top, 0f), 2.2f, false);
            Checkpoint(root, g, "Sweeper", 10, new Vector3(243.8f, Top, 0f));
            Hint(root, "7.9  SWEEPER — jump the beam", new Vector3(243.8f, Top, 0f));
            Slab(g, "EH_Walkway", new Vector3(253.8f, Top, 0f), new Vector3(15f, 0.6f, 5f));
            var pivot = new GameObject("EH_SweeperPivot");
            pivot.transform.SetParent(g, false);
            pivot.transform.position = new Vector3(253.8f, Top + 0.45f, 0f);
            var sweep = pivot.AddComponent<Rotator>();
            sweep.section = 10; sweep.axis = Vector3.up; sweep.degreesPerSecond = 55f;
            Beam(pivot.transform);

            // 4. bobbing lifts to the ship
            Hex(g, "EH_Rest3", new Vector3(264f, Top, 0f), 2.2f, false);
            Checkpoint(root, g, "Lifts", 11, new Vector3(264f, Top, 0f));
            Hint(root, "7.9  LIFTS — hop across as they rise", new Vector3(264f, Top, 0f));
            for (int i = 0; i < 2; i++)
            {
                var lift = Hex(g, "EH_Lift" + i, new Vector3(268.8f + i * 4.6f, Top, 0f), 2f, false);
                var bob = lift.AddComponent<BobbingPlatform>();
                bob.section = 11;
                bob.amplitude = new Vector3(0f, 1.1f, 0f);
                bob.period = i == 0 ? 4f : 5f;
                bob.tiltAmplitude = Vector3.zero;
            }

            // finish: the escape gate on the ship platform (the old goal sat halfway through the level)
            var gate = Make("EH_EscapeGate", g, gateMesh, gold, false);
            gate.transform.position = new Vector3(205.9f + ShipShift, Top + 2.6f, 0f);
            gate.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            var goal = root.GetComponentsInChildren<GoalTrigger>(true).FirstOrDefault();
            if (goal != null) goal.transform.position = new Vector3(205.9f + ShipShift, Top + 1.3f, 0f);

            // invisible side walls so nobody walks off sideways
            foreach (float z in new[] { -6f, 6f })
            {
                var w = new GameObject("EH_SideWall_" + (z < 0 ? "L" : "R"));
                w.transform.SetParent(g, false);
                w.transform.position = new Vector3(242.5f, 10f, z);
                var bc = w.AddComponent<BoxCollider>();
                bc.size = new Vector3(87f, 100f, 1f);
            }

            // a debris drop from the old layout lands right on the new path: keep the rock, drop the event
            foreach (var fe in root.GetComponentsInChildren<FallingEvent>(true))
                if (fe.transform.position.x > 230f && fe.transform.position.x < 260f) fe.gameObject.SetActive(false);
        }

        /// <summary>The docking platform, ship, its checkpoint and the end wall move on by <see cref="ShipShift"/>.</summary>
        static void ShiftShip(GameObject root)
        {
            var dock = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Docking_Platform[0]");
            if (dock == null || dock.position.x > 240f) return; // already moved
            var move = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.parent == null) continue;
                string p = t.parent.name;
                if (p == "7.8 Repair Under Pressure" || (p == "7.8 Ship" && t.GetComponent<GoalTrigger>() == null)
                    || t.name == "Checkpoint_Ship" || t.name == "CheckpointTrigger_Ship" || t.name == "EndWall_170")
                    move.Add(t);
            }
            foreach (var t in move) t.position += new Vector3(ShipShift, 0f, 0f);
            foreach (var cp in root.GetComponentsInChildren<Checkpoint>(true))
                if (cp.name == "CheckpointTrigger_Ship") cp.section = 12;
        }

        // ---------- pieces ----------

        static GameObject Hex(Transform parent, string name, Vector3 topCentre, float radius, bool phase)
        {
            var go = Make(name, parent, hexMesh, obsidian, true);
            go.transform.position = topCentre - new Vector3(0f, 0.6f, 0f);
            go.transform.localScale = new Vector3(radius, 1f, radius);
            var rim = Make("Rim", go.transform, hexRimMesh, phase ? violet : gold, false);
            rim.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            return go;
        }

        static void Slab(Transform parent, string name, Vector3 topCentre, Vector3 size)
        {
            var go = Make(name, parent, slabMesh, obsidian, true);
            go.transform.position = topCentre - new Vector3(0f, size.y, 0f);
            go.transform.localScale = size;
            foreach (float z in new[] { -0.5f, 0.5f })
            {
                var edge = Make("Edge", go.transform, slabMesh, gold, false);
                edge.transform.localPosition = new Vector3(0f, 1f, z);
                edge.transform.localScale = new Vector3(1f, 0.06f / size.y, 0.03f);
            }
        }

        static void Shard(Transform parent, string name, Vector3 local)
        {
            var go = Make(name, parent, shardMesh, hazard, false);
            go.transform.localPosition = local;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(0.9f, 1.6f, 0.9f);
            var hz = go.AddComponent<Hazard>();
            hz.effect = HazardEffect.Knockback;
            hz.knockSpeed = 6f;
            hz.knockUp = 3f;
        }

        static void Beam(Transform pivot)
        {
            var go = Make("EH_SweeperBeam", pivot, beamMesh, hazard, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = new Vector3(2.4f, 1f, 1f);
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(2f, 0.4f, 0.4f);
            var hz = go.AddComponent<Hazard>();
            hz.effect = HazardEffect.Knockback;
            hz.knockSpeed = 5f;
            hz.knockUp = 4f;
            hz.useOwnVelocity = true;
        }

        static void Spire(Transform parent, Vector3 basePos, float height)
        {
            var go = Make("EH_Spire", parent, spireMesh, obsidian, false);
            go.transform.position = basePos - new Vector3(0f, 1.5f, 0f);
            go.transform.localScale = new Vector3(1f, height, 1f);
            var tip = Make("Tip", go.transform, shardMesh, violet, false);
            tip.transform.localPosition = new Vector3(0f, 1f, 0f);
            tip.transform.localScale = new Vector3(0.6f, 0.6f / height, 0.6f);
        }

        static void Checkpoint(GameObject root, Transform g, string label, int section, Vector3 at)
        {
            var parent = root.transform.Find("Gameplay/Checkpoints") ?? g;
            var go = new GameObject("CheckpointTrigger_" + label.Replace(' ', '_'));
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 2f, 0f);
            box.size = new Vector3(3f, 4f, 4f);
            var cp = go.AddComponent<BorrowGravity.Checkpoint>();
            cp.section = section;
            cp.label = label;
            cp.spawnOffsets = new[] { new Vector3(-0.8f, 1.05f, 0f), new Vector3(0.8f, 1.05f, 0f) };
        }

        static void Hint(GameObject root, string title, Vector3 at)
        {
            var parent = root.transform.Find("Gameplay/Hints");
            var go = new GameObject("Hint_" + title);
            go.transform.SetParent(parent, false);
            go.transform.position = at + new Vector3(0f, 1.5f, 0f);
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(2f, 4f, 6f);
            go.AddComponent<HintTrigger>().sectionTitle = title;
        }

        static GameObject Make(string name, Transform parent, Mesh mesh, Material mat, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (collider) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true; }
            return go;
        }

        // ---------- art: meshes and materials (saved as assets, so they are real project art) ----------

        static void MakeArt()
        {
            foreach (var d in new[] { ArtRoot + "/Meshes", ArtRoot + "/Materials" }) EnsureFolder(d);
            obsidian = Mat("EH_Obsidian", new Color(0.07f, 0.06f, 0.11f), Color.black, 0.8f, 0.35f);
            gold = Mat("EH_GoldRim", new Color(1f, 0.74f, 0.32f), new Color(1f, 0.62f, 0.2f) * 2.4f, 0.6f, 0.6f);
            violet = Mat("EH_VioletGlow", new Color(0.62f, 0.38f, 1f), new Color(0.55f, 0.3f, 1f) * 2.8f, 0.5f, 0f);
            hazard = Mat("EH_HazardShard", new Color(1f, 0.32f, 0.16f), new Color(1f, 0.3f, 0.1f) * 3f, 0.7f, 0.1f);
            hexMesh = SaveMesh("EH_HexPrism", HexPrism(1f, 0.6f));
            hexRimMesh = SaveMesh("EH_HexRim", HexRing(1.02f, 0.86f, 0.08f));
            shardMesh = SaveMesh("EH_Shard", Bipyramid(0.45f, 0.8f, 6));
            spireMesh = SaveMesh("EH_Spire", Pyramid(0.55f, 1f, 6));
            beamMesh = SaveMesh("EH_Beam", Box(new Vector3(2f, 0.3f, 0.3f)));
            slabMesh = SaveMesh("EH_Slab", Box(Vector3.one, centreY: 0.5f));
            gateMesh = SaveMesh("EH_EscapeGate", Torus(2.4f, 0.22f, 40, 10));
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int cut = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, cut));
            AssetDatabase.CreateFolder(path.Substring(0, cut), path.Substring(cut + 1));
        }

        static Material Mat(string name, Color baseCol, Color emission, float smooth, float metal)
        {
            string path = ArtRoot + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", baseCol);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = ArtRoot + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            mesh.name = name;
            mesh.RecalculateBounds();
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // flat-sided hexagon with edges facing ±x, top at y = height, bottom at 0
        static Vector3 HexCorner(int k, float r) { float a = (30f + 60f * k) * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); }

        static Mesh HexPrism(float r, float h)
        {
            var b = new MeshBuilder(new Vector3(0f, h * 0.5f, 0f));
            var top = new Vector3(0f, h, 0f);
            for (int k = 0; k < 6; k++)
            {
                Vector3 a = HexCorner(k, r), c = HexCorner(k + 1, r);
                b.Tri(top, a + top, c + top);                          // top
                b.Tri(Vector3.zero, c, a);                             // bottom
                b.Quad(a, c, c + top, a + top);                        // side
            }
            return b.Build();
        }

        static Mesh HexRing(float outer, float inner, float h)
        {
            var b = new MeshBuilder(new Vector3(0f, h * 0.5f, 0f));
            var up = new Vector3(0f, h, 0f);
            for (int k = 0; k < 6; k++)
            {
                Vector3 o0 = HexCorner(k, outer), o1 = HexCorner(k + 1, outer), i0 = HexCorner(k, inner), i1 = HexCorner(k + 1, inner);
                b.Quad(i0 + up, o0 + up, o1 + up, i1 + up); // top band... wound to face up
                b.Quad(o0, o1, o1 + up, o0 + up);           // outside
            }
            return b.Build();
        }

        static Mesh Bipyramid(float r, float halfH, int sides)
        {
            var b = new MeshBuilder(Vector3.zero);
            Vector3 top = new Vector3(0f, halfH, 0f), bot = new Vector3(0f, -halfH, 0f);
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
                var p1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
                b.Tri(top, p1, p0);
                b.Tri(bot, p0, p1);
            }
            return b.Build();
        }

        static Mesh Pyramid(float r, float h, int sides)
        {
            var b = new MeshBuilder(new Vector3(0f, h * 0.25f, 0f));
            var top = new Vector3(0f, h, 0f);
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * Mathf.PI * 2f / sides, a1 = (k + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r);
                var p1 = new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
                b.Tri(top, p1, p0);
                b.Tri(Vector3.zero, p0, p1);
            }
            return b.Build();
        }

        static Mesh Box(Vector3 size, float centreY = 0f)
        {
            Vector3 h = size * 0.5f, c = new Vector3(0f, centreY * size.y, 0f);
            var b = new MeshBuilder(c);
            Vector3 P(float x, float y, float z) => c + new Vector3(x * h.x, y * h.y, z * h.z);
            b.Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));   // top
            b.Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1)); // bottom
            b.Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1)); // -z
            b.Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1));     // +z
            b.Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1)); // -x
            b.Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));     // +x
            return b.Build();
        }

        static Mesh Torus(float R, float r, int seg, int side)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float u = i * Mathf.PI * 2f / seg;
                var centre = new Vector3(Mathf.Cos(u) * R, Mathf.Sin(u) * R, 0f);
                for (int j = 0; j <= side; j++)
                {
                    float v = j * Mathf.PI * 2f / side;
                    var n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(u) * Mathf.Cos(v), Mathf.Sin(v));
                    verts.Add(centre + n * r); norms.Add(n);
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    int a = i * (side + 1) + j, b2 = a + side + 1;
                    AddFacing(tris, verts, norms, a, b2, a + 1);
                    AddFacing(tris, verts, norms, a + 1, b2, b2 + 1);
                }
            var m = new Mesh();
            m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0);
            return m;
        }

        static void AddFacing(List<int> tris, List<Vector3> v, List<Vector3> n, int a, int b, int c)
        {
            var face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            if (Vector3.Dot(face, n[a] + n[b] + n[c]) < 0f) { int tmp = b; b = c; c = tmp; }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        /// <summary>Flat-shaded triangles (each face gets its own vertices and normal), turned to face away from
        /// <see cref="centre"/> so the solid is never inside out.</summary>
        class MeshBuilder
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            readonly List<int> t = new List<int>();
            readonly Vector3 centre;

            public MeshBuilder(Vector3 centre) => this.centre = centre;

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var normal = Vector3.Cross(b - a, c - a).normalized;
                if (Vector3.Dot(normal, (a + b + c) / 3f - centre) < 0f) { var tmp = b; b = c; c = tmp; normal = -normal; }
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                n.Add(normal); n.Add(normal); n.Add(normal);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(a, b, c); Tri(a, c, d); }

            public Mesh Build()
            {
                var m = new Mesh();
                m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
                return m;
            }
        }
    }
}
