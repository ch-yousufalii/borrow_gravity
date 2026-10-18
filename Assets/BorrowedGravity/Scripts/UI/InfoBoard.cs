using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// An instruction board floating in the level next to a puzzle: section title + what to do. It sizes its panel to
    /// the text, turns (upright) to face this device's camera, and fades out when the player is far away.
    /// Built by the stage builders from the same text as the HUD hints.
    /// </summary>
    public class InfoBoard : MonoBehaviour
    {
        public TextMesh title, body;
        public Transform panel, edge;
        public float padding = 0.18f;
        [Tooltip("Text size in the world (characterSize of the title and body).")]
        public float textSize = 0.026f;
        public float lineGap = 0.12f;
        [Tooltip("Extra height above where the builder placed it (keeps the text clear of props).")]
        public float lift = 1f;
        public float showWithin = 22f;
        [Tooltip("Hidden when the camera is closer than this (so a board never fills the screen).")]
        public float hideCloserThan = 4f;

        Renderer[] renderers;
        bool shown = true;

        void Start()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            transform.position += Vector3.up * lift;
            if (title != null) title.characterSize = textSize;
            if (body != null) body.characterSize = textSize;
            Layout();
        }

        void Layout()
        {
            if (title == null || body == null || panel == null) return;
            var keep = transform.rotation;
            transform.rotation = Quaternion.identity;
            var tr = title.GetComponent<Renderer>();
            // body starts just under the title
            float titleH = tr.bounds.size.y / Mathf.Max(0.0001f, transform.lossyScale.y);
            body.transform.localPosition = new Vector3(0f, title.transform.localPosition.y - titleH - lineGap, 0f);
            var br = body.GetComponent<Renderer>();
            // measure in local space with the board unrotated
            Bounds b = tr.bounds;
            b.Encapsulate(br.bounds);
            Vector3 c = transform.InverseTransformPoint(b.center);
            Vector3 size = new Vector3(b.size.x / transform.lossyScale.x, b.size.y / transform.lossyScale.y, 1f);
            panel.localPosition = new Vector3(c.x, c.y, 0.02f);
            panel.localScale = new Vector3(size.x + padding * 2f, size.y + padding * 2f, 1f);
            if (edge != null)
            {
                edge.localPosition = new Vector3(c.x, c.y + size.y * 0.5f + padding, 0.01f);
                edge.localScale = new Vector3(size.x + padding * 2f, 0.05f, 1f);
            }
            transform.rotation = keep;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 to = transform.position - cam.transform.position;
            float d2 = to.sqrMagnitude;
            var sm = StageManager.Instance;
            bool playing = sm == null || !sm.isActiveAndEnabled || sm.State == StageState.Playing; // not behind the intro card / pause
            bool near = playing && d2 < showWithin * showWithin && d2 > hideCloserThan * hideCloserThan;
            if (near != shown)
            {
                shown = near;
                foreach (var r in renderers) if (r != null) r.enabled = near;
            }
            if (!near) return;
            // stay upright, turn to face the camera (text reads from the camera's side)
            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
        }
    }
}
