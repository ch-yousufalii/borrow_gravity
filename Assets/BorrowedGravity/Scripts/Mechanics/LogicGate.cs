using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    public enum GateMode { All, Any }

    /// <summary>Combines other signals. With <see cref="latch"/> it stays on once it has turned on (until reset).</summary>
    public class LogicGate : SignalSource, IResettable
    {
        public int section;
        public SignalSource[] inputs;
        public GateMode mode = GateMode.All;
        public bool latch;
        public bool invert;
        public string onMessage;

        bool latched, announced;

        public int Section => section;

        public override bool IsOn
        {
            get
            {
                if (latched) return true;
                bool v = Evaluate();
                if (invert) v = !v;
                if (v && latch) latched = true;
                return v;
            }
        }

        bool Evaluate()
        {
            if (inputs == null || inputs.Length == 0) return false;
            if (mode == GateMode.All)
            {
                foreach (var s in inputs) if (s == null || !s.IsOn) return false;
                return true;
            }
            foreach (var s in inputs) if (s != null && s.IsOn) return true;
            return false;
        }

        void Update()
        {
            if (!announced && !string.IsNullOrEmpty(onMessage) && IsOn)
            {
                announced = true;
                GameUI.ShowMessage(onMessage, 3f);
            }
        }

        public void ResetState()
        {
            latched = false;
            announced = false;
        }
    }
}
