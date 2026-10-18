using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Remembers which split-screen camera shows this player and hands it to the PlayerController on load.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCameraLink : MonoBehaviour
    {
        public Camera view;

        void Awake() => GetComponent<PlayerController>().ViewCamera = view;
    }
}
