using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Anything a checkpoint reset must put back (puzzle objects, timed hazards, doors...).
    /// <see cref="Section"/> is the stage section index (0-based); a reset from checkpoint N resets sections >= N.
    /// </summary>
    public interface IResettable
    {
        int Section { get; }
        void ResetState();
    }
    /// <summary>Shared helpers for finding players in triggers and the like.</summary>
    public static class Players
    {
        static readonly List<PlayerController> all = new List<PlayerController>();
        public static IReadOnlyList<PlayerController> All => all;

        public static void Register(PlayerController p) { if (!all.Contains(p)) all.Add(p); }
        public static void Unregister(PlayerController p) => all.Remove(p);

        public static PlayerController From(Collider c) => c == null ? null : c.GetComponentInParent<PlayerController>();

        public static PlayerController Get(int index)
        {
            foreach (var p in all) if (p.playerIndex == index) return p;
            return null;
        }
    }
}
