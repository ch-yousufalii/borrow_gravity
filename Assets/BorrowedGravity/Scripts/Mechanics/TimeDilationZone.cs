using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Near the horizon time stretches: while a player is inside, the whole game eases into slow motion, and it eases
    /// back once nobody is. The host's clock only; a joining phone mirrors the result.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TimeDilationZone : MonoBehaviour
    {
        public float slowScale = 0.5f;
        public float easeSeconds = 0.8f;
        [TextArea(1, 3)] public string message;

        readonly System.Collections.Generic.HashSet<PlayerController> inside = new System.Collections.Generic.HashSet<PlayerController>();
        float scale = 1f;
        bool announced;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var p = Players.From(other);
            if (p == null) return;
            inside.Add(p);
            if (announced) return;
            announced = true;
            if (!string.IsNullOrEmpty(message)) GameUI.ShowMessage(message, 3f);
            Sfx.Play(SfxId.Rumble, 0.7f, 0.6f);
            CoopCamera.Shake(0.3f, 1f);
        }

        void OnTriggerExit(Collider other)
        {
            var p = Players.From(other);
            if (p != null) inside.Remove(p);
        }

        void Update()
        {
            var sm = StageManager.Instance;
            if (sm == null || sm.State != StageState.Playing) return; // pausing and finishing own the clock
            inside.RemoveWhere(p => p == null || !p.gameObject.activeInHierarchy);
            float target = inside.Count > 0 ? slowScale : 1f;
            if (Mathf.Approximately(scale, target) && Mathf.Approximately(Time.timeScale, target)) return;
            scale = Mathf.MoveTowards(scale, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, easeSeconds));
            Time.timeScale = scale;
        }

        void OnDisable()
        {
            if (!Mathf.Approximately(scale, 1f)) Time.timeScale = 1f;
            scale = 1f;
            inside.Clear();
        }
    }
}
