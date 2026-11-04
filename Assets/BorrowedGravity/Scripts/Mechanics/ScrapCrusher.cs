using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A wall of crushed wreckage that starts moving when a player passes <see cref="triggerX"/> and keeps pushing along
    /// +X until <see cref="stopX"/>. Anyone it catches sends the whole team back to the checkpoint.
    /// </summary>
    public class ScrapCrusher : MonoBehaviour, IResettable
    {
        public int section;
        public float triggerX;
        public float stopX;
        public float speed = 1.2f;
        [Tooltip("Distance from this object's centre to the wall's leading face.")]
        public float frontOffset = 3f;
        public string warning = "THE CRUSHER IS MOVING!";
        [Tooltip("Start only once every player has passed triggerX, so nobody is caught before the chase begins.")]
        public bool waitForEveryone;
        [Tooltip("Waits this far below its place, out of the way of the route, and rises into place as it starts moving.")]
        public float riseDistance;
        public float riseSpeed = 14f;

        Vector3 startPos;
        float homeY;
        bool running, stopped;

        public int Section => section;

        void Awake()
        {
            homeY = transform.position.y;
            transform.position += Vector3.down * riseDistance;
            startPos = transform.position;
        }

        bool ReadyToStart()
        {
            bool any = false;
            foreach (var p in Players.All)
            {
                if (p == null) continue;
                bool past = p.transform.position.x >= triggerX;
                if (past) any = true;
                else if (waitForEveryone) return false;
            }
            return any;
        }

        void Update()
        {
            if (stopped) return;
            var sm = StageManager.Instance;
            if (sm == null || sm.State != StageState.Playing) return;

            if (!running)
            {
                if (ReadyToStart())
                {
                    running = true;
                    GameUI.ShowWarning(HudHazard.General, warning, 0f, 3f);
                    Sfx.Play(SfxId.Rumble, 0.9f);
                    CoopCamera.Shake(0.5f, 0.7f);
                }
                return;
            }

            var pos = transform.position;
            pos.x = Mathf.MoveTowards(pos.x, stopX, speed * Time.deltaTime);
            pos.y = Mathf.MoveTowards(pos.y, homeY, riseSpeed * Time.deltaTime);
            transform.position = pos;
            CoopCamera.Shake(0.04f, 0.1f);

            if (pos.x >= stopX)
            {
                stopped = true;
                CoopCamera.Shake(0.6f, 0.8f);
                Sfx.Play(SfxId.Boom, 0.8f);
                return;
            }

            float front = pos.x + frontOffset;
            foreach (var p in Players.All)
            {
                if (p == null || !p.CanBeHurt || p.transform.position.x > front) continue;
                Sfx.Play(SfxId.Impact, 1f);
                GameUI.ShowMessage("Crushed! Back to the checkpoint", 2f);
                sm.ResetToCheckpoint();
                return;
            }
        }

        public void ResetState()
        {
            transform.position = startPos;
            running = false;
            stopped = false;
        }
    }
}
