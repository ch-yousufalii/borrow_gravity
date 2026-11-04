using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// A one-time moment in the background: when a player passes <see cref="triggerX"/>, the mover travels from A to B
    /// with a camera shake and a line on the HUD. Scenery only, so it never touches the route.
    /// </summary>
    public class FlybyEvent : MonoBehaviour
    {
        public Transform mover;
        public Vector3 from, to;
        public float triggerX;
        public float duration = 6f;
        public float shake = 0.4f;
        [TextArea(1, 3)] public string message;

        float started = -1f;
        bool done;

        void Awake()
        {
            if (mover != null) mover.gameObject.SetActive(false);
        }

        void Update()
        {
            if (done || mover == null) return;
            if (started < 0f)
            {
                foreach (var p in Players.All)
                {
                    if (p == null || p.transform.position.x < triggerX) continue;
                    started = Time.time;
                    mover.position = from;
                    mover.gameObject.SetActive(true);
                    if (!string.IsNullOrEmpty(message)) GameUI.ShowMessage(message, 4f);
                    if (shake > 0f) CoopCamera.Shake(shake, 1.2f);
                    Sfx.Play(SfxId.Whoosh, 0.8f);
                    break;
                }
                return;
            }
            float t = (Time.time - started) / Mathf.Max(0.1f, duration);
            mover.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f)
            {
                done = true;
                mover.gameObject.SetActive(false);
            }
        }
    }
}
