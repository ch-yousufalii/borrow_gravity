using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlipObject : MonoBehaviour
{
    public static bool Flipped;
    public static float CenterY = 5f;

    public float speed = 4f;

    public bool IsFrozen { get; private set; }

    Rigidbody rb;
    Renderer rend;
    Color baseColor;
    bool highlighted;
    float startY;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Flipped = false; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        startY = transform.position.y;

        rend = GetComponentInChildren<Renderer>();
        if (rend) baseColor = rend.material.color;
    }

    void FixedUpdate()
    {
        if (IsFrozen) return;

        float target = Flipped ? 2f * CenterY - startY : startY;
        Vector3 p = rb.position;
        p.y = Mathf.MoveTowards(p.y, target, speed * Time.fixedDeltaTime);
        rb.MovePosition(p);
    }

    public void SetFrozen(bool frozen)
    {
        IsFrozen = frozen;
        UpdateColor();
    }

    public void SetHighlight(bool on)
    {
        highlighted = on;
        UpdateColor();
    }

    void UpdateColor()
    {
        if (!rend) return;
        if (IsFrozen) rend.material.color = Color.cyan;
        else if (highlighted) rend.material.color = Color.yellow;
        else rend.material.color = baseColor;
    }
}