using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GravityObject : MonoBehaviour
{
    // GRAVITY: ON = boards apni asli jagah, OFF = boards ulte (flip)
    public static bool RoomGravityOn = true;

    [Header("Settings")]
    public float centerY = 5f;      // level ka beech (floor aur ceiling ka average)
    public float speed = 4f;        // board kitni tezi se jaye

    public bool IsFrozen { get; private set; }

    Rigidbody rb;
    Renderer rend;
    Color baseColor;
    bool highlighted;
    float startY;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { RoomGravityOn = true; }

    void Awake()
    {
        RoomGravityOn = true;   // naya level hamesha ON se shuru

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

        float target = RoomGravityOn ? startY : 2f * centerY - startY;
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