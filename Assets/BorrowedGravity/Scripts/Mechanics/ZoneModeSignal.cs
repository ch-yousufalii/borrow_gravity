using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>On while a zone is in one of the given modes.</summary>
    public class ZoneModeSignal : SignalSource
    {
        public GravityZone zone;
        public GravityMode[] modes = { GravityMode.Down };

        public override bool IsOn
        {
            get
            {
                if (zone == null) return false;
                foreach (var m in modes) if (zone.Mode == m) return true;
                return false;
            }
        }
    }
}
