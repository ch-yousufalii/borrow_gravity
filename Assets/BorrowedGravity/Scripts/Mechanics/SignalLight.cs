using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Colours renderers / lights green when the signal is on, red when off.</summary>
    public class SignalLight : MonoBehaviour
    {
        public SignalSource source;
        public Renderer[] renderers;
        public Light lightSource;
        public Color onColor = new Color(0.3f, 1f, 0.5f);
        public Color offColor = new Color(1f, 0.3f, 0.25f);

        Material[] mats;
        bool? last;

        void Awake()
        {
            var list = new List<Material>();
            if (renderers != null)
                foreach (var r in renderers) if (r != null) { var m = r.material; m.EnableKeyword("_EMISSION"); list.Add(m); }
            mats = list.ToArray();
        }

        void Update()
        {
            bool on = source != null && source.IsOn;
            if (last == on) return;
            last = on;
            var c = on ? onColor : offColor;
            foreach (var m in mats) m.SetColor("_EmissionColor", c * 3f);
            if (lightSource != null) lightSource.color = c;
        }
    }
}
