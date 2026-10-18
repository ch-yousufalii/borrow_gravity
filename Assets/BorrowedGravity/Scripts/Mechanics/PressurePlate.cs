using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Pressed while enough players (and/or heavy objects) stand on it.</summary>
    [RequireComponent(typeof(Collider))]
    public class PressurePlate : SignalSource, IResettable
    {
        public int section;
        public int requiredCount = 1;
        public bool acceptObjects;
        public bool acceptPlayers = true;
        public string label = "Plate";
        public Transform plateTop;
        public float pressDepth = 0.12f;
        public Renderer indicator;
        public Light indicatorLight;

        readonly HashSet<Collider> inside = new HashSet<Collider>();
        readonly HashSet<Object> counted = new HashSet<Object>();
        Vector3 topRest;
        Material indicatorMat;
        bool on;

        public override bool IsOn => on;
        public int Count => counted.Count;
        public int Section => section;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (plateTop != null) topRest = plateTop.localPosition;
            if (indicator != null) { indicatorMat = indicator.material; indicatorMat.EnableKeyword("_EMISSION"); }
            Refresh(true);
        }

        void OnTriggerEnter(Collider c) => inside.Add(c);
        void OnTriggerExit(Collider c) => inside.Remove(c);

        void FixedUpdate()
        {
            counted.Clear();
            inside.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            foreach (var c in inside)
            {
                var p = Players.From(c);
                if (p != null) { if (acceptPlayers) counted.Add(p); continue; }
                if (!acceptObjects) continue;
                var w = c.GetComponentInParent<PlateWeight>();
                if (w != null) counted.Add(w);
            }
            bool now = counted.Count >= requiredCount;
            if (now != on)
            {
                on = now;
                Sfx.Play(SfxId.Plate, 0.6f, on ? 1.1f : 0.8f);
            }
            Refresh(false);
        }

        void Refresh(bool force)
        {
            if (plateTop != null)
                plateTop.localPosition = Vector3.Lerp(plateTop.localPosition, topRest + Vector3.down * (on ? pressDepth : 0f), force ? 1f : 0.3f);
            Color c = on ? new Color(0.3f, 1f, 0.5f) : (counted.Count > 0 ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.3f, 0.25f));
            if (indicatorMat != null) indicatorMat.SetColor("_EmissionColor", c * 2.5f);
            if (indicatorLight != null) indicatorLight.color = c;
        }

        public void ResetState()
        {
            inside.Clear();
            counted.Clear();
            on = false;
            Refresh(true);
        }
    }
}
