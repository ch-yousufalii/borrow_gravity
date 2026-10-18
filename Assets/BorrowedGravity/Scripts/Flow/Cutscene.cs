using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BorrowGravity
{
    public enum CueType { Activate, Deactivate, Shake, Sound, AlarmOn, AlarmOff, Burst, Flash }

    [Serializable]
    public class CutsceneCamKey
    {
        public float time;
        public Vector3 position;
        public Vector3 lookAt;
        public float fov = 42f;
    }

    [Serializable]
    public class CutsceneLine
    {
        public float time;
        public float duration = 3.5f;
        public string speaker;
        public Color color = Color.white;
        [TextArea] public string text;
    }

    [Serializable]
    public class CutsceneMove
    {
        public Transform actor;
        public float time;
        public float duration = 2f;
        public Vector3 fromPos, toPos;
        public Vector3 fromEuler, toEuler;
        [Tooltip("Bob up and down like walking.")]
        public bool walk;
    }

    [Serializable]
    public class CutsceneCue
    {
        public float time;
        public CueType type;
        public GameObject target;
        public float amount = 1f;
        public SfxId sound;
    }

    /// <summary>
    /// Plays the opening cinematic: camera keys (eased dolly with a little handheld sway), actor moves, cues
    /// (particles, shakes, sounds), subtitles and fades, then hands over to Stage 1. Hold Space / Enter / Esc to skip.
    /// </summary>
    public class Cutscene : MonoBehaviour
    {
        public Camera cam;
        public List<CutsceneCamKey> cameraKeys = new List<CutsceneCamKey>();
        public List<CutsceneLine> lines = new List<CutsceneLine>();
        public List<CutsceneMove> moves = new List<CutsceneMove>();
        public List<CutsceneCue> cues = new List<CutsceneCue>();
        [Tooltip("Black fade keys: (time, alpha).")]
        public List<Vector2> fades = new List<Vector2> { new Vector2(0f, 1f), new Vector2(1.5f, 0f) };
        public float length = 40f;
        public float titleTime = 36f;
        public string title = "BORROWED GRAVITY";
        public string subtitle = "Stage 1 · The Shattered Orbit";
        public float skipHold = 0.8f;
        [Tooltip("The ending: when it finishes, go back to the main menu instead of into Stage 1.")]
        public bool isEnding;
        public float handheld = 0.25f;

        float t, skip, shakeAmount, shakeUntil, flashUntil;
        int nextCue;
        bool done;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            cues.Sort((a, b) => a.time.CompareTo(b.time));
            foreach (var c in cues) if (c.type == CueType.Activate && c.target != null) c.target.SetActive(false);
            Sfx.SetLoop(LoopId.Ambience, true, 0.4f);
            Apply(0f);
        }

        void Update()
        {
            if (done) return;
            t += Time.deltaTime;
            bool holding = PlayerKeys.Held(Key.Space) || PlayerKeys.Held(Key.Enter) || PlayerKeys.Held(Key.Escape) || PlayerKeys.Held(Key.NumpadEnter)
                           || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                           || (Mouse.current != null && Mouse.current.leftButton.isPressed);
            skip = holding ? skip + Time.deltaTime : Mathf.Max(0f, skip - Time.deltaTime * 2f);
            if (NetSession.IsClient)
            {
                // the host's cutscene decides when the game starts: ask it to skip, and wait for its stage
                if (skip >= skipHold) { skip = 0f; NetSession.SendAction(NetAction.Skip); }
                if (t >= length) { done = true; return; }
            }
            else if (skip >= skipHold || t >= length) { Finish(); return; }
            while (nextCue < cues.Count && cues[nextCue].time <= t) Fire(cues[nextCue++]);
            Apply(t);
        }

        /// <summary>Skip to the end (the other phone held "skip").</summary>
        public void SkipNow()
        {
            if (!done) Finish();
        }

        void Finish()
        {
            done = true;
            Sfx.SetLoop(LoopId.Alarm, false);
            if (isEnding) GameFlow.AfterOutro();
            else GameFlow.AfterIntro();
        }

        void Fire(CutsceneCue c)
        {
            switch (c.type)
            {
                case CueType.Activate: if (c.target != null) c.target.SetActive(true); break;
                case CueType.Deactivate: if (c.target != null) c.target.SetActive(false); break;
                case CueType.Shake: shakeAmount = c.amount; shakeUntil = t + 0.6f + c.amount * 0.4f; break;
                case CueType.Sound: Sfx.Play(c.sound, Mathf.Clamp01(c.amount)); break;
                case CueType.AlarmOn: Sfx.SetLoop(LoopId.Alarm, true, 0.15f * c.amount); break;
                case CueType.AlarmOff: Sfx.SetLoop(LoopId.Alarm, false); break;
                case CueType.Flash: flashUntil = t + 0.25f; break;
                case CueType.Burst:
                    if (c.target != null)
                    {
                        c.target.SetActive(true);
                        foreach (var ps in c.target.GetComponentsInChildren<ParticleSystem>()) ps.Play(true);
                    }
                    break;
            }
        }

        static float Ease(float k) => k * k * (3f - 2f * k);

        void Apply(float time)
        {
            // actors
            foreach (var m in moves)
            {
                if (m.actor == null || time < m.time) continue;
                float k = m.duration <= 0f ? 1f : Mathf.Clamp01((time - m.time) / m.duration);
                float e = m.walk ? k : Ease(k);
                Vector3 p = Vector3.Lerp(m.fromPos, m.toPos, e);
                if (m.walk && k < 1f) p.y += Mathf.Abs(Mathf.Sin((time - m.time) * 9f)) * 0.12f;
                m.actor.position = p;
                m.actor.rotation = Quaternion.Slerp(Quaternion.Euler(m.fromEuler), Quaternion.Euler(m.toEuler), e);
            }

            // camera: eased segment between keys (Catmull-Rom on position so the dolly flows through keys)
            if (cam != null && cameraKeys.Count > 0)
            {
                int i = 0;
                while (i < cameraKeys.Count - 1 && cameraKeys[i + 1].time <= time) i++;
                var a = cameraKeys[i];
                var b = cameraKeys[Mathf.Min(i + 1, cameraKeys.Count - 1)];
                float span = Mathf.Max(0.01f, b.time - a.time);
                float k = a == b ? 1f : Ease(Mathf.Clamp01((time - a.time) / span));
                var p0 = cameraKeys[Mathf.Max(i - 1, 0)].position;
                var p3 = cameraKeys[Mathf.Min(i + 2, cameraKeys.Count - 1)].position;
                Vector3 pos = CatmullRom(p0, a.position, b.position, p3, k);
                Vector3 look = Vector3.Lerp(a.lookAt, b.lookAt, k);
                float sway = handheld;
                pos += new Vector3(Mathf.PerlinNoise(time * 0.35f, 1f) - 0.5f, Mathf.PerlinNoise(2f, time * 0.3f) - 0.5f, 0f) * sway;
                var rot = Quaternion.LookRotation(look - pos, Vector3.up);
                if (time < shakeUntil)
                {
                    float s = shakeAmount * Mathf.Clamp01((shakeUntil - time) * 1.5f);
                    rot *= Quaternion.Euler((Mathf.PerlinNoise(time * 30f, 0f) - 0.5f) * 4f * s, (Mathf.PerlinNoise(0f, time * 30f) - 0.5f) * 4f * s, 0f);
                }
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = Mathf.Lerp(a.fov, b.fov, k);
            }
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        float FadeAt(float time)
        {
            if (fades.Count == 0) return 0f;
            if (time <= fades[0].x) return fades[0].y;
            for (int i = 0; i < fades.Count - 1; i++)
                if (time < fades[i + 1].x)
                    return Mathf.Lerp(fades[i].y, fades[i + 1].y, Mathf.InverseLerp(fades[i].x, fades[i + 1].x, time));
            return fades[fades.Count - 1].y;
        }

        void OnGUI()
        {
            UIKit.Begin();
            float w = UIKit.Width, h = UIKit.Height;
            UIKit.Letterbox(Mathf.Clamp01(t / 1.2f));

            // dialogue: speaker name in its colour, typewriter text, small next-line marker (Genshin-style)
            foreach (var l in lines)
            {
                if (t < l.time || t > l.time + l.duration) continue;
                float local = t - l.time;
                float alpha = Mathf.Min(Mathf.Clamp01(local / 0.25f), Mathf.Clamp01((l.time + l.duration - t) / 0.3f));
                int chars = Mathf.Clamp(Mathf.FloorToInt(local * 48f), 0, l.text.Length);
                float y = h * 0.88f - 96f;
                if (!string.IsNullOrEmpty(l.speaker))
                {
                    var nameStyle = UIKit.Style(UIKit.Face.Display, 20, new Color(l.color.r, l.color.g, l.color.b, alpha), TextAnchor.MiddleCenter);
                    UIKit.Text(new Rect(0, y, w, 26), UIKit.Caps(l.speaker, 1), nameStyle);
                    float nw = nameStyle.CalcSize(new GUIContent(UIKit.Caps(l.speaker, 1))).x;
                    UIKit.Fill(new Rect(w * 0.5f - nw * 0.5f - 34, y + 13, 22, 1), new Color(1, 1, 1, 0.5f * alpha));
                    UIKit.Fill(new Rect(w * 0.5f + nw * 0.5f + 12, y + 13, 22, 1), new Color(1, 1, 1, 0.5f * alpha));
                }
                var body = UIKit.Style(string.IsNullOrEmpty(l.speaker) ? UIKit.Face.Heavy : UIKit.Face.Body, 22,
                    new Color(1, 1, 1, alpha), TextAnchor.UpperCenter, true);
                if (string.IsNullOrEmpty(l.speaker)) body = UIKit.Style(UIKit.Face.Heavy, 22, new Color(UIKit.Dim.r, UIKit.Dim.g, UIKit.Dim.b, alpha), TextAnchor.UpperCenter, true);
                UIKit.Text(new Rect(w * 0.5f - 520, y + 32, 1040, 70), l.text.Substring(0, chars), body);
                if (chars >= l.text.Length && Mathf.Repeat(t * 2f, 1f) > 0.4f)
                    UIKit.Text(new Rect(0, y + 84, w, 16), "▾", UIKit.Style(UIKit.Face.Display, 12, new Color(1, 1, 1, alpha * 0.7f), TextAnchor.MiddleCenter));
            }

            // title card
            if (t >= titleTime)
            {
                float a = Mathf.Clamp01((t - titleTime) / 1f) * Mathf.Clamp01((length - t) / 0.6f);
                UIKit.Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, a * 0.55f));
                UIKit.Text(new Rect(0, h * 0.40f, w, 80), title.Replace("GRAVITY", "<color=#5ce1ff>GRAVITY</color>"), UIKit.Style(UIKit.Face.Display, 70, new Color(1, 1, 1, a), TextAnchor.MiddleCenter));
                UIKit.Divider(w * 0.5f - 260, w * 0.5f + 260, h * 0.40f + 92, 0.5f * a);
                UIKit.Text(new Rect(0, h * 0.40f + 102, w, 26), UIKit.Caps(subtitle), UIKit.Style(UIKit.Face.Display, 16, new Color(UIKit.Dim.r, UIKit.Dim.g, UIKit.Dim.b, a), TextAnchor.MiddleCenter));
            }

            // skip hint (top right, inside the letterbox)
            var sr = new Rect(w - 330, 26, 300, 40);
            float fill = Mathf.Clamp01(skip / skipHold);
            UIKit.Dot(new Vector2(sr.x + 18, sr.center.y), 22f, new Color(1, 1, 1, 0.15f));
            if (fill > 0f) UIKit.Dot(new Vector2(sr.x + 18, sr.center.y), 22f * fill, UIKit.Glow);
            UIKit.Text(new Rect(sr.x + 38, sr.y, sr.width - 38, sr.height), UIKit.Caps("Hold Space or screen · Skip"), UIKit.Style(UIKit.Face.Display, 12, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft));

            float fade = FadeAt(t);
            if (t < flashUntil) fade = Mathf.Max(fade, 0f);
            if (fade > 0f) UIKit.Fill(new Rect(0, 0, w, h), new Color(0, 0, 0, fade));
            if (t < flashUntil) UIKit.Fill(new Rect(0, 0, w, h), new Color(1f, 0.8f, 0.6f, Mathf.Clamp01((flashUntil - t) * 3f)));
        }
    }
}
