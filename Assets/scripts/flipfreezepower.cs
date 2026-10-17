using UnityEngine;
using UnityEngine.InputSystem;

public class FlipFreezePower : MonoBehaviour
{
    public float range = 7f;

    public FlipObject Target { get; private set; }   // peela
    public FlipObject Frozen { get; private set; }   // cyan

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
        FlipObject best = null;
        float bestDist = range;

        foreach (var f in FindObjectsByType<FlipObject>(FindObjectsSortMode.None))
        {
            var col = f.GetComponent<Collider>();
            Vector3 p = col ? col.ClosestPoint(transform.position) : f.transform.position;
            float d = Vector3.Distance(transform.position, p);
            if (d < bestDist) { bestDist = d; best = f; }
        }

        if (best != Target)
        {
            if (Target != null) Target.SetHighlight(false);
            Target = best;
            if (Target != null) Target.SetHighlight(true);
        }
    }
}