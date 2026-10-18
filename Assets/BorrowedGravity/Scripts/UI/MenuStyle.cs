using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Drawing kit for the final menu design (borrowed-gravity-menu.html): only white at different opacities,
    /// hairline outlines, rounded pills, letter-spaced Lato and no fills. Works in UIKit's 900-high reference space.
    /// </summary>
    public static class MenuStyle
    {
        public const float W80 = 0.8f, W50 = 0.5f, W25 = 0.25f, W12 = 0.12f;

        /// <summary>Strength of the soft text shadow (raise it where text sits over the busy game view).</summary>
        public static float ShadowAlpha = 0.35f;

        static readonly Dictionary<long, float> charWidths = new Dictionary<long, float>();

        public static Color White(float a = 1f) => new Color(1f, 1f, 1f, a);

        /// <summary>Rectangle outline (radius 0) or rounded outline / pill (radius &gt; 0). stroke 0 = filled.</summary>
        public static void Box(Rect r, float alpha, float stroke = 1f, float radius = 0f)
        {
            if (alpha <= 0f || Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, White(alpha), stroke, radius);
        }

        /// <summary>Soft dark glass under HUD boxes so white text stays readable over bright scenery.</summary>
        public static void Glass(Rect r, float radius = 0f, float alpha = 0.4f)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.02f, 0.03f, 0.07f, alpha), 0f, radius);
        }

        public static void Line(float x, float y, float w, float alpha, float h = 1f) => UIKit.Fill(new Rect(x, y, w, h), White(alpha));

        public static void DashedLine(float x0, float x1, float y, float alpha)
        {
            for (float x = x0; x < x1; x += 7f) Line(x, y, Mathf.Min(4f, x1 - x), alpha);
        }

        /// <summary>Corner brackets top-left and bottom-right, like the active row in the design.</summary>
        public static void Brackets(Rect r, float len = 12f, float t = 2f, float alpha = 1f)
        {
            var c = White(alpha);
            UIKit.Fill(new Rect(r.x - 2f, r.y - 2f, len, t), c);
            UIKit.Fill(new Rect(r.x - 2f, r.y - 2f, t, len), c);
            UIKit.Fill(new Rect(r.xMax + 2f - len, r.yMax + 2f - t, len, t), c);
            UIKit.Fill(new Rect(r.xMax + 2f - t, r.yMax + 2f - len, t, len), c);
        }

        static GUIStyle Font(UIKit.Face face, int size, float alpha) =>
            UIKit.Style(face, size, White(alpha), TextAnchor.UpperLeft);

        /// <summary>Width of text with extra letter spacing (em = fraction of the font size).</summary>
        public static float Measure(string text, UIKit.Face face, int size, float spacingEm)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var st = Font(face, size, 1f);
            float w = 0f, gap = spacingEm * size;
            foreach (char ch in text)
            {
                long key = ((long)face << 40) | ((long)size << 24) | ch;
                if (!charWidths.TryGetValue(key, out float cw))
                {
                    cw = ch == ' ' ? st.CalcSize(new GUIContent("a a")).x - st.CalcSize(new GUIContent("aa")).x : st.CalcSize(new GUIContent(ch.ToString())).x;
                    charWidths[key] = cw;
                }
                w += cw + gap;
            }
            return w - gap;
        }

        /// <summary>Letter-spaced text inside r, aligned left / centre / right and vertically centred.</summary>
        public static void Text(Rect r, string text, UIKit.Face face, int size, float alpha, float spacingEm = 0f,
            TextAnchor align = TextAnchor.MiddleCenter, float yOffset = 0f)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0f) return;
            float total = Measure(text, face, size, spacingEm);
            float x = align == TextAnchor.MiddleLeft || align == TextAnchor.UpperLeft ? r.x
                    : align == TextAnchor.MiddleRight || align == TextAnchor.UpperRight ? r.xMax - total
                    : r.center.x - total * 0.5f;
            float lineH = size * 1.35f;
            float y = (align == TextAnchor.UpperLeft || align == TextAnchor.UpperCenter || align == TextAnchor.UpperRight ? r.y : r.center.y - lineH * 0.5f) + yOffset;
            var st = Font(face, size, alpha);
            var shadow = UIKit.Style(face, size, new Color(0f, 0f, 0f, ShadowAlpha * alpha), TextAnchor.UpperLeft);
            if (spacingEm <= 0f)
            {
                UIKit.Label(new Rect(x + 1f, y + 1.5f, total + 4f, lineH), text, shadow);
                UIKit.Label(new Rect(x, y, total + 4f, lineH), text, st);
                return;
            }
            float gap = spacingEm * size;
            foreach (char ch in text)
            {
                long key = ((long)face << 40) | ((long)size << 24) | ch;
                charWidths.TryGetValue(key, out float cw);
                string s = ch.ToString();
                UIKit.Label(new Rect(x + 1f, y + 1.5f, cw + 4f, lineH), s, shadow);
                UIKit.Label(new Rect(x, y, cw + 4f, lineH), s, st);
                x += cw + gap;
            }
        }

        /// <summary>Wrapped paragraph text (no letter spacing).</summary>
        public static void Paragraph(Rect r, string text, UIKit.Face face, int size, float alpha, TextAnchor align = TextAnchor.UpperCenter)
        {
            var st = UIKit.Style(face, size, White(alpha), align, true);
            UIKit.Label(r, text, st);
        }

        static void Shape(Rect r, Color c, float radius = 0f)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, 0f, radius);
        }

        /// <summary>
        /// The little astronaut mascot (white suit, dark visor, cyan details) waving a glowing light-stick.
        /// feet = bottom-centre point in reference pixels; about 250 px tall at scale 1.
        /// </summary>
        public static void Astronaut(Vector2 feet, float scale)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color white = new Color(0.95f, 0.95f, 0.96f), shade = new Color(0.82f, 0.83f, 0.86f), grey = new Color(0.36f, 0.37f, 0.4f);
            Color visor = new Color(0.1f, 0.1f, 0.12f), cyan = new Color(0.36f, 0.88f, 1f);
            Rect R(float x, float y, float w, float h) => new Rect(feet.x + x * scale, feet.y + y * scale, w * scale, h * scale);
            float t = Time.unscaledTime;

            // legs with cyan stripes
            Shape(R(-38, -64, 34, 64), white, 6f * scale);
            Shape(R(4, -64, 34, 64), shade, 6f * scale);
            Shape(R(-38, -14, 34, 5), cyan);
            Shape(R(4, -14, 34, 5), cyan);
            // backpack, body (lit left, shaded right), shoulder band, chest panel
            Shape(R(30, -178, 44, 96), grey, 10f * scale);
            Shape(R(-58, -156, 116, 100), white, 18f * scale);
            Shape(R(2, -156, 56, 100), shade, 18f * scale);
            Shape(R(-40, -152, 80, 9), new Color(0.72f, 0.73f, 0.76f), 4f * scale);
            Shape(R(-24, -116, 48, 24), visor, 6f * scale);
            Shape(R(-17, -108, 9, 9), cyan, 4.5f * scale);
            Shape(R(-4, -107, 20, 3), white);
            Shape(R(-4, -101, 14, 3), white);
            // right arm + glove
            Shape(R(50, -142, 20, 46), shade, 9f * scale);
            Shape(R(54, -104, 18, 18), cyan, 4f * scale);
            // helmet with visor and a glint
            Shape(R(-62, -270, 124, 124), white, 62f * scale);
            Shape(R(2, -268, 58, 118), new Color(shade.r, shade.g, shade.b, 0.55f), 50f * scale);
            Shape(R(-44, -238, 88, 54), visor, 24f * scale);
            Shape(R(-30, -230, 22, 8), new Color(1f, 1f, 1f, 0.18f), 4f * scale);

            // left arm raised with the light-stick, waving around the shoulder
            var keep = GUI.matrix;
            float wave = Mathf.Sin(t * 2.6f) * 9f;
            GUIUtility.RotateAroundPivot(wave, new Vector2(feet.x - 52f * scale, feet.y - 140f * scale) * UIKit.Scale);
            Shape(R(-74, -142, 22, 44), white, 9f * scale);
            float glowPulse = 0.25f + 0.1f * Mathf.Sin(t * 5f);
            Shape(R(-86, -262, 26, 134), new Color(cyan.r, cyan.g, cyan.b, glowPulse), 13f * scale);
            Shape(R(-78, -256, 10, 122), cyan, 5f * scale);
            Shape(R(-76, -252, 4, 110), new Color(1f, 1f, 1f, 0.7f), 2f * scale);
            Shape(R(-82, -132, 22, 20), cyan, 5f * scale);
            GUI.matrix = keep;
        }

        /// <summary>A ring of small dots, lit clockwise from the top for 'fraction' (cooldowns, timers) — white only.</summary>
        public static void Arc(Vector2 center, float radius, float fraction, float alpha = 1f, int segments = 44, float dot = 3.4f)
        {
            if (Event.current.type != EventType.Repaint) return;
            int lit = Mathf.RoundToInt(Mathf.Clamp01(fraction) * segments);
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                var c = center + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * radius;
                float d = i < lit ? dot : dot * 0.6f;
                GUI.DrawTexture(new Rect(c.x - d * 0.5f, c.y - d * 0.5f, d, d), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                    White(i < lit ? alpha : alpha * 0.25f), 0f, d * 0.5f);
            }
        }

        /// <summary>A keycap: rounded outline with the key's label (the &lt;kbd&gt; look).</summary>
        public static float Key(float x, float centerY, string label)
        {
            float w = Mathf.Max(24f, Measure(label, UIKit.Face.Heavy, 11, 0f) + 14f);
            var r = new Rect(x, centerY - 10f, w, 20f);
            Box(r, W50, 1f, 4f);
            Text(r, label, UIKit.Face.Heavy, 11, 1f);
            return w;
        }

        /// <summary>Outlined button (Back / Stay / Quit / Connect ...). Returns true when clicked.</summary>
        public static bool Button(Rect r, string label, bool focused = false)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover || focused) UIKit.Fill(r, White(W12));
            Box(r, hover || focused ? 1f : W50, 1f);
            Text(r, label.ToUpperInvariant(), UIKit.Face.Heavy, 12, 1f, 0.22f);
            if (Event.current.type == EventType.MouseDown && hover)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }
    }
}
