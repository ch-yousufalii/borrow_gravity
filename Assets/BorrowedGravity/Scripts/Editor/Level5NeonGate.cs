using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowGravity.EditorTools
{
    /// <summary>
    /// Level 5's pressure gate (the wall that lifts when a player steps on the plate) and its floor: generated neon panel
    /// textures, glowing strips and coloured lights in the style of Stage 1. Works on the Game scene and can be run again.
    /// </summary>
    public static class Level5NeonGate
    {
        const string Dir = "Assets/BorrowedGravity/Art/Level5";
        static readonly Color Teal = new Color(0.2f, 1f, 0.85f), Magenta = new Color(1f, 0.2f, 0.75f);

        [MenuItem("BorrowGravity/Level 5/Neon Gate", priority = 67)]
        public static void Apply()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var l5 = scene.GetRootGameObjects().First(r => r.name == "Level_Stage5");
            var gate = l5.GetComponentInChildren<PressureDoor>(true);
            var door = gate.door;
            var floor = gate.transform.parent;
            var plate = gate.GetComponentInChildren<MeshRenderer>();
            foreach (var t in l5.GetComponentsInChildren<Transform>(true).Where(t => t != null && t.name.StartsWith("Neon_")).ToArray())
                if (t != null) Object.DestroyImmediate(t.gameObject);

            var doorMat = PanelMaterial("L5_NeonDoor", true, new Vector2(door.lossyScale.z / 4f, 1f));
            var floorMat = PanelMaterial("L5_NeonFloor", false, new Vector2(floor.lossyScale.x / 3f, floor.lossyScale.z / 3f));
            door.GetComponent<MeshRenderer>().sharedMaterial = doorMat;
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMat;
            plate.sharedMaterial = Glow("L5_NeonPlate", Teal, 2.2f);
            var tealGlow = Glow("L5_NeonTeal", Teal, 4f);
            var pinkGlow = Glow("L5_NeonPink", Magenta, 4f);

            // strips on the door ride up with it; strips along the floor's long sides stay put
            Strip(door, "Neon_DoorTop", new Vector3(0f, 0.5f, 0f), new Vector3(1.04f, 0.03f, 1f), pinkGlow);
            Strip(door, "Neon_DoorBottom", new Vector3(0f, -0.5f, 0f), new Vector3(1.04f, 0.03f, 1f), tealGlow);
            Strip(floor, "Neon_FloorNear", new Vector3(0f, 0.51f, -0.49f), new Vector3(1f, 0.04f, 0.02f), tealGlow);
            Strip(floor, "Neon_FloorFar", new Vector3(0f, 0.51f, 0.49f), new Vector3(1f, 0.04f, 0.02f), tealGlow);

            Light(door, "Neon_DoorLight", door.position + Vector3.left * 2.5f, Magenta, 11f, 5f);
            Light(floor, "Neon_FloorLightA", floor.position + new Vector3(-4.5f, 2.5f, 0f), Teal, 10f, 4f);
            Light(floor, "Neon_FloorLightB", floor.position + new Vector3(4.5f, 2.5f, 0f), Teal, 10f, 4f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Level5] neon gate done");
        }

        static void Strip(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void Light(Transform parent, string name, Vector3 at, Color color, float range, float intensity)
        {
            var l = BuilderKit.PointLight(parent, at, color, range, intensity);
            l.name = name;
            l.transform.position = at;
        }

        static Material Glow(string name, Color color, float strength)
        {
            var m = Load(name);
            m.SetColor("_BaseColor", color);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * strength);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material PanelMaterial(string name, bool chevrons, Vector2 tiling)
        {
            var m = Load(name);
            m.SetTexture("_BaseMap", Texture(name + "_Base", chevrons, false));
            m.SetTexture("_EmissionMap", Texture(name + "_Glow", chevrons, true));
            m.SetTextureScale("_BaseMap", tiling);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.65f);
            m.SetFloat("_Metallic", 0.4f);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.white * 2.6f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Load(string name)
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>A dark metal panel with seams (base), or just its neon lines on black (glow).</summary>
        static Texture2D Texture(string name, bool chevrons, bool glow)
        {
            const int S = 512;
            var tex = new Texture2D(S, S, TextureFormat.RGB24, false);
            var dark = new Color(0.075f, 0.085f, 0.15f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    bool edge = x < 6 || x >= S - 6 || y < 6 || y >= S - 6;
                    bool inner = x % (S / 2) < 3 || y % (S / 2) < 3;
                    bool chev = chevrons && Mathf.Abs(y - S / 2) < 120 && ((x + Mathf.Abs(y - S / 2)) % 110) < 16;
                    Color c;
                    if (glow) c = edge ? Teal : chev ? Magenta : Color.black;
                    else
                    {
                        float shade = 1f + ((x / 64 + y / 64) % 2) * 0.12f;
                        c = dark * shade;
                        if (edge) c = Teal * 0.8f;
                        else if (inner) c = dark * 0.45f;
                        else if (chev) c = Magenta * 0.75f;
                        c.a = 1f;
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            string path = Dir + "/" + name + ".png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
