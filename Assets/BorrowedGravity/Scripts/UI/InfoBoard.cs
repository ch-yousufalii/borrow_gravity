using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// An instruction board floating in the level next to a puzzle: section title + what to do. It sizes its panel to
    /// the text, turns (upright) to face this device's camera, and only shows while a player is close by (so boards
    /// never pile up on screen or over the HUD).
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
        [Tooltip("Shown only while one of this device's players is within this distance.")]
        public float nearPlayer = 9f;
        [Tooltip("Bigger text overall (1 = the builder's size).")]
        public float textScale = 1.35f;
        [Tooltip("Font atlas resolution multiplier: higher = sharper text from a distance.")]
        public float sharpness = 2.2f;
        [Tooltip("Hidden when the camera is closer than this (so a board never fills the screen).")]
        public float hideCloserThan = 4f;

        Renderer[] renderers;
        bool shown = true;

        void Start()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            transform.position += Vector3.up * lift;
            if (GameFlow.Solo && body != null) body.text = ForSolo(body.text);
            // render the glyphs at a higher font size and shrink them back: same layout, much crisper text
            foreach (var tm in new[] { title, body })
            {
                if (tm == null) continue;
                tm.fontSize = Mathf.RoundToInt(tm.fontSize * sharpness);
                tm.characterSize = textSize * textScale / sharpness;
            }
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
            bool near = playing && Nearest() == this && d2 > hideCloserThan * hideCloserThan;
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

        // solo: one astronaut does both jobs — E bends gravity (A's power), Q freezes (B's)
        static readonly (string from, string to)[] SoloWords =
        {
            ("(or B steers it — hold Right Shift)", "(or hold Q to steer it)"), ("A: ", "[E] "), ("B: ", "[Q] "),
            ("A zeroes", "[E] zero"), ("A restores", "[E] restore"), ("A drops", "[E] drop"),
            ("B steers", "[Q] steer"), ("B freezes", "[Q] freeze"), ("A carries the orange one, B the cyan one", "you carry one"),
            ("B: aim at", "[Q] aim at"), ("hold Right Shift", "hold Q"), ("Right Shift", "Q"), ("Stand on both console pads", "Stand on the console pads"), ("BOTH stand", "stand"), ("you're both", "you're"), ("Run across together", "Run across"),
            ("then run together", "then run"), ("move together", "move on"),
        };

        static string ForSolo(string text)
        {
            foreach (var (from, to) in SoloWords) text = text.Replace(from, to);
            return text;
        }

        // only one board at a time (the closest to this device's players), so two boards never overlap
        static readonly System.Collections.Generic.List<InfoBoard> boards = new System.Collections.Generic.List<InfoBoard>();
        static InfoBoard nearest;
        static int nearestFrame = -1;

        void OnEnable() { if (!boards.Contains(this)) boards.Add(this); }
        void OnDisable() => boards.Remove(this);

        /// <summary>Where the board that is showing sits on screen (GUI units), so HUD markers can keep clear of it.</summary>
        public static bool ShownRect(Camera cam, out Rect rect)
        {
            rect = default;
            var b = nearestFrame == Time.frameCount ? nearest : Nearest();
            if (b == null || !b.shown || b.panel == null || cam == null) return false;
            var r = b.panel.GetComponent<Renderer>();
            if (r == null) return false;
            var bounds = r.bounds;
            float s = UIKit.Scale;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var sp = cam.WorldToScreenPoint(corner);
                if (sp.z <= 0f) return false;
                var g = new Vector2(sp.x / s, (Screen.height - sp.y) / s);
                min = Vector2.Min(min, g);
                max = Vector2.Max(max, g);
            }
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        static InfoBoard Nearest()
        {
            if (nearestFrame == Time.frameCount) return nearest;
            nearestFrame = Time.frameCount;
            nearest = null;
            float best = float.MaxValue;
            foreach (var b in boards)
            {
                if (b == null) continue;
                float d = b.PlayerDistanceSqr();
                if (d < b.nearPlayer * b.nearPlayer && d < best) { best = d; nearest = b; }
            }
            return nearest;
        }

        /// <summary>Squared distance to the closest of this device's players (its own online, A/B on a shared screen).</summary>
        float PlayerDistanceSqr()
        {
            float best = float.MaxValue;
            foreach (var p in Players.All)
            {
                if (p == null || (NetSession.Online && p.playerIndex != NetSession.LocalPlayer)) continue;
                best = Mathf.Min(best, (p.transform.position - transform.position).sqrMagnitude);
            }
            return best;
        }
    }
}
