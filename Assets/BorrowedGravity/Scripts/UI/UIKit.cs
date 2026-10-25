using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// The art kit's HUD look in one place (colours, fonts, panels, buttons, power rings, meter, gauge) for IMGUI.
    /// Palette from the kit: void #140a1f, panel #1f1230, line #3a2650, ink #f4e9f1, dim #b49ec4,
    /// sun #ff9d5c (Player A / accents), glow #5ce1ff (Player B / focus).
    /// All rects are in "reference" units: call <see cref="Begin"/> first so 900 units = screen height.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Void = Hex(0x140a1f), PanelCol = Hex(0x1f1230), Line = Hex(0x3a2650);
        public static readonly Color Ink = Hex(0xf4e9f1), Dim = Hex(0xb49ec4), Sun = Hex(0xff9d5c), Glow = Hex(0x5ce1ff);
        public static readonly Color Danger = Hex(0xff6a7a), Good = Hex(0x7dffb0), Selected = Hex(0x2a1940);
        public const float ReferenceHeight = 780f; // lower = bigger UI everywhere (was 900)

        public static float Scale { get; private set; } = 1f;
        public static float Width => Screen.width / Scale;
        public static float Height => ReferenceHeight;

        static Font display, body, heavy;
        static Texture2D whiteTex, dotTex, pillTex;
        static Material radial;
        static readonly System.Collections.Generic.Dictionary<long, GUIStyle> styles = new System.Collections.Generic.Dictionary<long, GUIStyle>();

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        public static void Begin()
        {
            Scale = Screen.height / ReferenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
            Load();
        }

        static void Load()
        {
            if (whiteTex != null) return;
            display = Resources.Load<Font>("Fonts/Lato-Black");
            heavy = Resources.Load<Font>("Fonts/Lato-Bold");
            body = Resources.Load<Font>("Fonts/Lato-Regular");
            whiteTex = Texture2D.whiteTexture;
            pillTex = RoundRect(24, 12, new Color(0.04f, 0.03f, 0.06f, 0.95f), new Color(Ink.r, Ink.g, Ink.b, 0.55f));
            dotTex = Circle(32);
            var sh = Resources.Load<Shader>("Shaders/UIRadialFill");
            if (sh != null) radial = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        }

        // ---------- text ----------

        public enum Face { Display, Heavy, Body }

        public static GUIStyle Style(Face face, int size, Color color, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = false)
        {
            long key = ((long)face << 40) ^ ((long)size << 32) ^ ((long)anchor << 24) ^ (wrap ? 1L << 20 : 0) ^ color.GetHashCode();
            if (styles.TryGetValue(key, out var st)) return st;
            st = new GUIStyle
            {
                font = face == Face.Display ? display : face == Face.Heavy ? heavy : body,
                fontSize = size,
                alignment = anchor,
                wordWrap = wrap,
                richText = true,
                clipping = TextClipping.Overflow,
                normal = { textColor = color }
            };
            styles[key] = st;
            return st;
        }

        /// <summary>Text with a soft drop shadow so it reads over any sky.</summary>
        /// <summary>
        /// GUI.Label in reference units, but the glyphs are rendered at the screen's real pixel size: the GUI matrix
        /// scales the layout, and scaling text that way stretches a small font bitmap (blurry on HD phones).
        /// </summary>
        public static void Label(Rect r, string text, GUIStyle style)
        {
            if (Mathf.Abs(Scale - 1f) < 0.02f || style.font == null) { GUI.Label(r, text, style); return; }
            var m = GUI.matrix;
            int size = style.fontSize;
            style.fontSize = Mathf.Max(1, Mathf.RoundToInt(size * Scale));
            GUI.matrix = m * Matrix4x4.Scale(new Vector3(1f / Scale, 1f / Scale, 1f)); // keeps rotations (arrow glyphs)
            GUI.Label(new Rect(r.x * Scale, r.y * Scale, r.width * Scale, r.height * Scale), text, style);
            GUI.matrix = m;
            style.fontSize = size;
        }

        public static void Text(Rect r, string text, GUIStyle style)
        {
            var c = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.65f * c.a);
            Label(new Rect(r.x + 1.5f, r.y + 2f, r.width, r.height), Strip(text), style);
            style.normal.textColor = c;
            Label(r, text, style);
        }

        static string Strip(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<.*?>", "");

        /// <summary>Uppercase label with letter spacing (the kit's 0.12–0.14em display captions).</summary>
        public static string Caps(string s, int spaces = 1)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var gap = new string(' ', spaces);
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s.ToUpperInvariant()) { sb.Append(ch); sb.Append(gap); }
            return sb.ToString().TrimEnd();
        }

        // ---------- shapes ----------

        /// <summary>Translucent panel with a hairline frame and corner brackets (the menu reference look).</summary>
        public static void Panel(Rect r, bool soft = false)
        {
            Fill(r, new Color(Void.r, Void.g, Void.b, soft ? 0.5f : 0.72f));
            Frame(r, new Color(Ink.r, Ink.g, Ink.b, 0.22f), 1f);
            Brackets(r, 12f, 2f, new Color(Ink.r, Ink.g, Ink.b, 0.85f));
        }

        public static void Frame(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        /// <summary>L-shaped corner brackets.</summary>
        public static void Brackets(Rect r, float len, float t, Color c)
        {
            Fill(new Rect(r.x, r.y, len, t), c); Fill(new Rect(r.x, r.y, t, len), c);
            Fill(new Rect(r.xMax - len, r.y, len, t), c); Fill(new Rect(r.xMax - t, r.y, t, len), c);
            Fill(new Rect(r.x, r.yMax - t, len, t), c); Fill(new Rect(r.x, r.yMax - len, t, len), c);
            Fill(new Rect(r.xMax - len, r.yMax - t, len, t), c); Fill(new Rect(r.xMax - t, r.yMax - len, t, len), c);
        }

        /// <summary>A thin divider with small end ticks.</summary>
        public static void Divider(float x0, float x1, float y, float alpha = 0.35f)
        {
            var c = new Color(Ink.r, Ink.g, Ink.b, alpha);
            Fill(new Rect(x0, y, x1 - x0, 1f), c);
            Fill(new Rect(x0, y - 3f, 1f, 7f), c);
            Fill(new Rect(x1 - 1f, y - 3f, 1f, 7f), c);
        }

        /// <summary>The small dark pill label sitting on a divider ("NEW GAME").</summary>
        public static void TagPill(Vector2 center, string text, Color accent)
        {
            var st = Style(Face.Display, 12, Ink, TextAnchor.MiddleCenter);
            float w = st.CalcSize(new GUIContent(Caps(text))).x + 30f;
            var r = new Rect(center.x - w * 0.5f, center.y - 12f, w, 24f);
            var tex = new GUIStyle { normal = { background = pillTex }, border = new RectOffset(12, 12, 12, 12) };
            if (Event.current.type == EventType.Repaint) tex.Draw(r, false, false, false, false);
            Fill(new Rect(r.x + 8f, r.center.y - 1f, 4f, 2f), accent);
            Fill(new Rect(r.xMax - 12f, r.center.y - 1f, 4f, 2f), accent);
            Text(r, Caps(text), st);
        }

        /// <summary>Square arrow button ("&lt;" / "&gt;"). Returns true when clicked.</summary>
        public static bool ArrowBox(Rect r, bool right, bool lit)
        {
            Fill(r, new Color(Void.r, Void.g, Void.b, 0.55f));
            Frame(r, lit ? Glow : new Color(Ink.r, Ink.g, Ink.b, 0.6f), lit ? 2f : 1.5f);
            var inner = new Rect(r.x + 4, r.y + 4, r.width - 8, r.height - 8);
            Fill(inner, lit ? new Color(Glow.r, Glow.g, Glow.b, 0.25f) : new Color(Ink.r, Ink.g, Ink.b, 0.12f));
            Text(r, right ? "\u203A" : "\u2039", Style(Face.Display, 26, lit ? Glow : Ink, TextAnchor.MiddleCenter));
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>
        /// A selector row: caption + value between two arrow boxes, on hairlines. Returns -1/+1 when an arrow is
        /// clicked, 0 otherwise.
        /// </summary>
        public static int Selector(Rect r, string caption, string value, bool selected)
        {
            Divider(r.x + 46f, r.xMax - 46f, r.y, selected ? 0.6f : 0.3f);
            Divider(r.x + 46f, r.xMax - 46f, r.yMax, selected ? 0.6f : 0.3f);
            int result = 0;
            float box = Mathf.Min(40f, r.height - 8f);
            if (ArrowBox(new Rect(r.x, r.center.y - box * 0.5f, box, box), false, selected)) result = -1;
            if (ArrowBox(new Rect(r.xMax - box, r.center.y - box * 0.5f, box, box), true, selected)) result = 1;
            Text(new Rect(r.x, r.y + 6f, r.width, 16f), Caps(caption), Style(Face.Display, 11, selected ? Glow : Dim, TextAnchor.MiddleCenter));
            Text(new Rect(r.x, r.y + 20f, r.width, r.height - 24f), Caps(value), Style(Face.Display, 20, Ink, TextAnchor.MiddleCenter));
            return result;
        }

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, whiteTex);
            GUI.color = old;
        }

        public static void Tex(Rect r, Texture t, Color? tint = null)
        {
            if (t == null) return;
            var old = GUI.color;
            GUI.color = tint ?? Color.white;
            GUI.DrawTexture(r, t, ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        public static void Dot(Vector2 c, float size, Color col) => Tex(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), dotTex, col);

        /// <summary>Bracket button (menu reference look); selected = glow frame and brackets. Returns true when clicked.</summary>
        public static bool Button(Rect r, string label, bool selected, int size = 18)
        {
            Fill(r, selected ? new Color(Glow.r, Glow.g, Glow.b, 0.16f) : new Color(Void.r, Void.g, Void.b, 0.45f));
            Frame(r, selected ? new Color(Glow.r, Glow.g, Glow.b, 0.8f) : new Color(Ink.r, Ink.g, Ink.b, 0.28f), 1f);
            Brackets(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), 14f, 2f, selected ? Glow : new Color(Ink.r, Ink.g, Ink.b, 0.7f));
            Text(r, Caps(label, 2), Style(Face.Display, size, selected ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.8f), TextAnchor.MiddleCenter));
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Kit power widget: Ring_Track, Ring_Cooldown clipped radially and tinted, badge inset 14%.</summary>
        public static void PowerRing(Rect r, Texture track, Texture ring, Texture badge, float fill, Color tint)
        {
            Tex(r, track);
            if (ring != null && radial != null && Event.current.type == EventType.Repaint)
            {
                radial.SetFloat("_Fill", Mathf.Clamp01(fill));
                radial.SetColor("_Color", tint);
                var screen = new Rect(r.x * Scale, r.y * Scale, r.width * Scale, r.height * Scale);
                var m = GUI.matrix;
                GUI.matrix = Matrix4x4.identity;
                Graphics.DrawTexture(screen, ring, radial);
                GUI.matrix = m;
            }
            else if (ring != null && radial == null) Tex(r, ring, new Color(tint.r, tint.g, tint.b, fill));
            float inset = r.width * 0.14f;
            Tex(new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f), badge);
        }

        /// <summary>Kit engine meter: frame, fill at left 19.7% top 30% w 76.2% h 40%, clipped on the right.</summary>
        public static void Meter(Rect r, Texture frame, Texture fill, float value, Color tint)
        {
            value = Mathf.Clamp01(value);
            var well = new Rect(r.x + r.width * 0.197f, r.y + r.height * 0.30f, r.width * 0.762f, r.height * 0.40f);
            if (fill != null)
            {
                var old = GUI.color;
                GUI.color = tint;
                GUI.DrawTextureWithTexCoords(new Rect(well.x, well.y, well.width * value, well.height), fill, new Rect(0, 0, value, 1));
                GUI.color = old;
            }
            else Fill(new Rect(well.x, well.y, well.width * value, well.height), tint);
            Tex(r, frame);
        }

        /// <summary>Kit tilt gauge: dial plus needle (w 9.4%, left 45.3%, bottom 4%, pivot at 91% height) rotated ±75°.</summary>
        public static void Gauge(Rect r, Texture dial, Texture needle, float degrees, Color needleTint)
        {
            Tex(r, dial);
            if (needle == null) return;
            float w = r.width * 0.094f;
            float h = w * (220f / 48f);
            var nr = new Rect(r.x + r.width * 0.453f, r.yMax - r.height * 0.04f - h, w, h);
            var pivot = new Vector2(nr.x + w * 0.5f, nr.y + h * 0.91f);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Clamp(degrees, -75f, 75f), pivot * Scale);
            var old = GUI.color;
            GUI.color = needleTint;
            GUI.DrawTexture(nr, needle);
            GUI.color = old;
            GUI.matrix = m;
        }

        /// <summary>Full-screen dim used behind menus and story screens.</summary>
        public static void Shade(float alpha) => Fill(new Rect(0, 0, Width, Height), new Color(Void.r, Void.g, Void.b, alpha));

        /// <summary>Cinematic letterbox bars (k: 0 = none, 1 = full).</summary>
        public static void Letterbox(float k)
        {
            if (k <= 0f) return;
            float h = Height * 0.12f * k;
            Fill(new Rect(0, 0, Width, h), Color.black);
            Fill(new Rect(0, Height - h, Width, h), Color.black);
        }

        // ---------- textures ----------

        static Texture2D RoundRect(int size, int radius, Color fill, Color border)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                    float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float outer = Mathf.Clamp01(radius - d + 0.5f);
                    float inner = Mathf.Clamp01(radius - 1.2f - d + 0.5f);
                    var c = Color.Lerp(border, fill, inner);
                    c.a *= outer;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        static Texture2D Circle(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)))));
            t.Apply();
            return t;
        }
    }
}
