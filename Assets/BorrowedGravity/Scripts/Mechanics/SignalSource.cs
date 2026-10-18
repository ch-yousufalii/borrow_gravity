using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Anything that can be on or off: plates, sockets, gates, puzzle results.</summary>
    public abstract class SignalSource : MonoBehaviour
    {
        public abstract bool IsOn { get; }
    }
}
