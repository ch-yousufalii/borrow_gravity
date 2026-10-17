using UnityEngine;
using UnityEngine.InputSystem;

public class FreezePower : MonoBehaviour
{
    public float range = 7f;

    public GravityObject Target { get; private set; }   // peela
    public GravityObject Frozen { get; private set; }   // cyan

    void Update()
    {
        var kb = Keyboard.current;

        FindNearest();

        bool pressed = (kb != null && kb.enterKey.wasPressedThisFrame)
                       || TouchButton.Consume(TouchButton.Action.P2Power);

        if (!pressed) return;

        if (Frozen != null)
        {
            var old = Frozen;
            old.SetFrozen(false);
            Frozen = null;
            if (Target == old) return;   // sirf release
        }

        if (Target != null)
        {
            Target.SetFrozen(true);
            Frozen = Target;
        }
    }

    void FindNearest()
    {
        GravityObject best = null;
        float bestDist = range;

        foreach (var g in FindObjectsByType<GravityObject>(FindObjectsSortMode.None))
        {
            var col = g.GetComponent<Collider>();
            Vector3 p = col ? col.ClosestPoint(transform.position) : g.transform.position;
            float d = Vector3.Distance(transform.position, p);
            if (d < bestDist) { bestDist = d; best = g; }
        }

        if (best != Target)
        {
            if (Target != null) Target.SetHighlight(false);
            Target = best;
            if (Target != null) Target.SetHighlight(true);
        }
    }
}