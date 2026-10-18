using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// On-screen controls state per player (index 0 = A, 1 = B), written by <see cref="VirtualControls"/> and read by
    /// PlayerController and the powers alongside the keyboard.
    /// </summary>
    public static class VirtualInput
    {
        static readonly Vector2[] move = new Vector2[4];
        static readonly bool[] jumpHeld = new bool[4], powerHeld = new bool[4];
        static readonly bool[] jumpPrev = new bool[4], powerPrev = new bool[4];
        static readonly int[] frame = new int[4];
        static readonly bool[] jumpDown = new bool[4], powerDown = new bool[4], powerUp = new bool[4];
        static readonly Vector2[] look = new Vector2[4];
        static readonly bool[] power2Held = new bool[4], power2Prev = new bool[4], power2Down = new bool[4], power2Up = new bool[4];
        static readonly int[] frame2 = new int[4];

        /// <summary>Second power button (solo: nullify), written by the controls overlay each frame.</summary>
        public static void SetPower2(int player, bool held)
        {
            if (player < 0 || player > 3) return;
            power2Down[player] = held && !power2Prev[player];
            power2Up[player] = !held && power2Prev[player];
            power2Held[player] = held;
            power2Prev[player] = held;
            frame2[player] = Time.frameCount;
        }

        static bool Fresh2(int p) => p >= 0 && p < 4 && Time.frameCount - frame2[p] <= 1;
        public static bool Power2Pressed(int p) => Fresh2(p) && power2Down[p];
        public static bool Power2Held(int p) => Fresh2(p) && power2Held[p];
        public static bool Power2Released(int p) => Fresh2(p) && power2Up[p];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            for (int i = 0; i < 4; i++)
            {
                move[i] = Vector2.zero;
                jumpHeld[i] = powerHeld[i] = jumpPrev[i] = powerPrev[i] = false;
                jumpDown[i] = powerDown[i] = powerUp[i] = false;
                look[i] = Vector2.zero;
            }
        }

        /// <summary>Called once per frame by the controls overlay.</summary>
        public static void Set(int player, Vector2 stick, bool jump, bool power)
        {
            if (player < 0 || player > 3) return;
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

        /// <summary>Camera turn requested this frame, in degrees (x = yaw, y = pitch), from mouse or finger drags.</summary>
        public static void AddLook(int player, Vector2 degrees)
        {
            if (player >= 0 && player < 4) look[player] += degrees;
        }

        /// <summary>Take (and clear) the accumulated camera turn for a player.</summary>
        public static Vector2 ConsumeLook(int player)
        {
            if (player < 0 || player > 3) return Vector2.zero;
            var v = look[player];
            look[player] = Vector2.zero;
            return v;
        }

        static bool Fresh(int p) => p >= 0 && p < 4 && Time.frameCount - frame[p] <= 1;

        public static Vector2 Move(int p) => Fresh(p) ? move[p] : Vector2.zero;
        public static bool JumpPressed(int p) => Fresh(p) && jumpDown[p];
        public static bool JumpHeld(int p) => Fresh(p) && jumpHeld[p];
        public static bool PowerPressed(int p) => Fresh(p) && powerDown[p];
        public static bool PowerHeld(int p) => Fresh(p) && powerHeld[p];
        public static bool PowerReleased(int p) => Fresh(p) && powerUp[p];
    }
}
