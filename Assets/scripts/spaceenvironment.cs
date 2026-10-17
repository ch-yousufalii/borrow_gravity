using UnityEngine;

public class SpaceEnvironment : MonoBehaviour
{
    public Material starMaterial;
    public Material planetMaterial;
    public Material hazardMaterial;
    public int starCount = 250;

    void Start()
    {
        Random.InitState(7);

        // Taare
        var stars = new GameObject("Stars").transform;
        stars.SetParent(transform, false);
        for (int i = 0; i < starCount; i++)
        {
            var s = Make(PrimitiveType.Sphere, starMaterial, stars);
            s.transform.position = new Vector3(
                Random.Range(-60f, 60f), Random.Range(-20f, 30f), Random.Range(20f, 60f));
            s.transform.localScale = Vector3.one * Random.Range(0.1f, 0.35f);
        }

        // Planet
        var planet = Make(PrimitiveType.Sphere, planetMaterial, transform);
        planet.name = "Planet";
        planet.transform.position = new Vector3(10f, 10f, 35f);
        planet.transform.localScale = Vector3.one * 16f;

        // Laal hazard strips (sirf sajawat)
        for (int i = 0; i < 2; i++)
        {
            var h = Make(PrimitiveType.Cube, hazardMaterial, transform);
            h.name = "HazardStrip" + i;
            h.transform.position = new Vector3(i == 0 ? -10f : 12f, 7f + i * 1.2f, 10f);
            h.transform.localScale = new Vector3(40f, 0.15f, 6f);
        }
    }

    GameObject Make(PrimitiveType type, Material mat, Transform parent)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }
}