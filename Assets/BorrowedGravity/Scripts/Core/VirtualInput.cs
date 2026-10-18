using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// On-screen controls state per player (index 0 = A, 1 = B), written by <see cref="VirtualControls"/> and read by
    /// PlayerController and the powers alongside the keyboard.
    /// </summary>
    public static class VirtualInput
    {
        static readonly Vector2[] move = new Vector2[2];
        static readonly bool[] jumpHeld = new bool[2], powerHeld = new bool[2];
        static readonly bool[] jumpPrev = new bool[2], powerPrev = new bool[2];
        static readonly int[] frame = new int[2];
        static readonly bool[] jumpDown = new bool[2], powerDown = new bool[2], powerUp = new bool[2];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int i = 0; i < 2; i++)
            {
                move[i] = Vector2.zero;
                jumpHeld[i] = powerHeld[i] = jumpPrev[i] = powerPrev[i] = false;
                jumpDown[i] = powerDown[i] = powerUp[i] = false;
            }
        }

        /// <summary>Called once per frame by the controls overlay.</summary>
        public static void Set(int player, Vector2 stick, bool jump, bool power)
        {
            if (player < 0 || player > 1) return;
            move[player] = Vector2.ClampMagnitude(stick, 1f);
            jumpDown[player] = jump && !jumpPrev[player];
            powerDown[player] = power && !powerPrev[player];
            powerUp[player] = !power && powerPrev[player];
            jumpHeld[player] = jump;
            powerHeld[player] = power;
            jumpPrev[player] = jump;
            powerPrev[player] = power;
            frame[player] = Time.frameCount;
        }

        static bool Fresh(int p) => p >= 0 && p < 2 && Time.frameCount - frame[p] <= 1;

        public static Vector2 Move(int p) => Fresh(p) ? move[p] : Vector2.zero;
        public static bool JumpPressed(int p) => Fresh(p) && jumpDown[p];
        public static bool JumpHeld(int p) => Fresh(p) && jumpHeld[p];
        public static bool PowerPressed(int p) => Fresh(p) && powerDown[p];
        public static bool PowerHeld(int p) => Fresh(p) && powerHeld[p];
        public static bool PowerReleased(int p) => Fresh(p) && powerUp[p];
    }
}
