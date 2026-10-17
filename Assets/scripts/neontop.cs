using UnityEngine;

public class NeonTop : MonoBehaviour
{
    public Material glowMaterial;

    void Start()
    {
        var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(strip.GetComponent<Collider>());
        strip.transform.SetParent(transform, false);
        strip.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        strip.transform.localScale = new Vector3(1.002f, 0.08f, 1.002f);

        var r = strip.GetComponent<Renderer>();
        r.sharedMaterial = glowMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}