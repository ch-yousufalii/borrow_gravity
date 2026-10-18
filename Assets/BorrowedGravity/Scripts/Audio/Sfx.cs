using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    public enum SfxId
    {
        Jump, Hit, Respawn, Checkpoint, GravityShift, Nullify, Release, Denied, Warning, Alarm,
        Crack, Impact, Door, Plate, Success, Tick, Creak, Spark,
        // cinematic (cutscenes)
        Whoosh, Boom, Rumble, Riser, Comms
    }

    public enum LoopId { Ambience, EngineHum, Alarm, Drone, Music }

    /// <summary>
    /// Procedurally synthesised sound effects and music (the jam has no audio assets). Clips are generated once on
    /// demand: soft sine / FM-bell tones, filtered noise and a small reverb, so nothing sounds like a raw beep.
    /// Volumes follow <see cref="SoundSettings"/> (master × effects, master × music).
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        static readonly Dictionary<LoopId, AudioSource> loops = new Dictionary<LoopId, AudioSource>();
        static readonly Dictionary<LoopId, float> loopBase = new Dictionary<LoopId, float>();
        static GameObject host;
        static AudioSource[] voices;
        static int nextVoice;
        static readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();
        static System.Random rng = new System.Random(7);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            host = null;
            voices = null;
            loops.Clear();
            loopBase.Clear();
            lastPlayed.Clear();
        }

        static float EffectsVolume => SoundSettings.Master * SoundSettings.Effects;
        static float LoopVolume(LoopId id) => SoundSettings.Master * (id == LoopId.Music ? SoundSettings.Music : SoundSettings.Effects);

        static void EnsureHost()
        {
            if (host != null) return;
            host = new GameObject("SfxHost");
            Object.DontDestroyOnLoad(host);
            voices = new AudioSource[12];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = host.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
        }

        public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            if (!Application.isPlaying) return;
            NetSession.ForwardSfx(id, volume, pitch);
            // avoid machine-gunning the same sound
            if (lastPlayed.TryGetValue(id, out float t) && Time.unscaledTime - t < 0.05f) return;
            lastPlayed[id] = Time.unscaledTime;
            EnsureHost();
            var src = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            src.pitch = pitch;
            src.PlayOneShot(Get(id), volume * EffectsVolume);
        }

        public static void SetLoop(LoopId id, bool on, float volume = 0.5f)
        {
            if (!Application.isPlaying) return;
            if (id != LoopId.Music) NetSession.ForwardLoop(id, on, volume); // each device plays its own music
            EnsureHost();
            if (!loops.TryGetValue(id, out var src) || src == null)
            {
                if (!on) return;
                src = host.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                src.clip = id == LoopId.Music ? MakeMusic() : MakeLoop(id);
                loops[id] = src;
            }
            loopBase[id] = volume;
            src.volume = volume * LoopVolume(id);
            if (on && !src.isPlaying) src.Play();
            if (!on && src.isPlaying) src.Stop();
        }

        /// <summary>Settings changed: running loops follow the new levels at once.</summary>
        public static void ApplyVolumes()
        {
            foreach (var kv in loops)
                if (kv.Value != null && loopBase.TryGetValue(kv.Key, out float v)) kv.Value.volume = v * LoopVolume(kv.Key);
        }

        /// <summary>Stops the sound loops; the music keeps going across levels (see <see cref="StopMusic"/>).</summary>
        public static void StopAllLoops()
        {
            NetSession.ForwardStopLoops();
            foreach (var kv in loops) if (kv.Value != null && kv.Key != LoopId.Music) kv.Value.Stop();
        }

        /// <summary>Cutscenes have their own sound bed: no music under them.</summary>
        public static void StopMusic()
        {
            if (loops.TryGetValue(LoopId.Music, out var src) && src != null) src.Stop();
        }

        static AudioClip Get(SfxId id)
        {
            if (clips.TryGetValue(id, out var c) && c != null) return c;
            c = Make(id);
            clips[id] = c;
            return c;
        }

        // ---------- synthesis helpers ----------

        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        static float Note(float semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);

        /// <summary>Soft sine (plus a little 2nd harmonic) gliding f0 → f1, short attack, exponential decay.</summary>
        static void Sine(float[] d, float start, float dur, float f0, float f1, float amp, float decay = 6f, float attack = 0.006f, float warmth = 0.15f)
        {
            int s = (int)(start * Rate), n = (int)(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n && s + i < d.Length; i++)
            {
                float k = (float)i / n;
                float f = f0 * Mathf.Pow(f1 / f0, k); // exponential glide sounds natural
                phase += 2 * System.Math.PI * f / Rate;
                float w = Mathf.Sin((float)phase) + warmth * Mathf.Sin((float)(2 * phase));
                float env = Mathf.Exp(-decay * k) * Mathf.Clamp01(i / (Rate * attack)) * Mathf.Clamp01((n - i) / (Rate * 0.01f));
                d[s + i] += w * amp * env;
            }
        }

        /// <summary>FM bell / chime: bright at the start, mellowing as it rings.</summary>
        static void Bell(float[] d, float start, float dur, float f, float amp, float decay = 4f, float ratio = 2.0f, float index = 2.5f)
        {
            int s = (int)(start * Rate), n = (int)(dur * Rate);
            for (int i = 0; i < n && s + i < d.Length; i++)
            {
                float t = (float)i / Rate, k = (float)i / n;
                float mod = index * Mathf.Exp(-t * 6f) * Mathf.Sin(2 * Mathf.PI * f * ratio * t);
                float env = Mathf.Exp(-decay * k) * Mathf.Clamp01(i / (Rate * 0.003f)) * Mathf.Clamp01((n - i) / (Rate * 0.01f));
                d[s + i] += Mathf.Sin(2 * Mathf.PI * f * t + mod) * amp * env;
            }
        }

        /// <summary>Noise through a band (one-pole low-pass "lo", high-pass "hi"), with an attack and decay.</summary>
        static void Hiss(float[] d, float start, float dur, float amp, float lo, float hi, float attack, float decay)
        {
            int s = (int)(start * Rate), n = (int)(dur * Rate);
            float lp = 0f, hp = 0f, prev = 0f;
            for (int i = 0; i < n && s + i < d.Length; i++)
            {
                float k = (float)i / n;
                lp += (Noise() - lp) * lo;
                hp = (hp + lp - prev) * (1f - hi);
                prev = lp;
                float env = Mathf.Clamp01(k / Mathf.Max(0.001f, attack)) * Mathf.Exp(-decay * k);
                d[s + i] += hp * amp * env;
            }
        }

        /// <summary>Small room: four feedback delays mixed back in (gives every sound a soft tail).</summary>
        static void Reverb(float[] d, float mix, float feedback = 0.62f)
        {
            int[] taps = { 1307, 1637, 1811, 1931 };
            var wet = new float[d.Length];
            var line = new float[d.Length];
            foreach (int tap in taps)
            {
                System.Array.Clear(line, 0, line.Length);
                for (int i = 0; i < d.Length; i++)
                {
                    float back = i >= tap ? line[i - tap] : 0f;
                    line[i] = d[i] + back * feedback;
                    wet[i] += back;
                }
            }
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                lp += (wet[i] - lp) * 0.35f; // darker tail
                d[i] += lp * mix / taps.Length;
            }
        }

        // ---------- the sounds ----------

        static AudioClip Make(SfxId id)
        {
            float[] d;
            switch (id)
            {
                case SfxId.Jump: // light "hup": quick rising sine + a puff of air
                    d = Buf(0.25f);
                    Sine(d, 0, 0.16f, 300, 560, 0.45f, 5f);
                    Hiss(d, 0, 0.12f, 0.25f, 0.4f, 0.6f, 0.1f, 6f);
                    Reverb(d, 0.25f);
                    break;
                case SfxId.Hit: // soft body thud
                    d = Buf(0.45f);
                    Sine(d, 0, 0.35f, 150, 55, 0.8f, 7f, 0.002f, 0.3f);
                    Hiss(d, 0, 0.2f, 0.5f, 0.12f, 0.1f, 0.02f, 9f);
                    break;
                case SfxId.Respawn: // shimmering rise
                    d = Buf(1.1f);
                    for (int i = 0; i < 4; i++) Bell(d, i * 0.07f, 0.6f, Note(3 + i * 4), 0.22f, 5f, 3f, 1.4f);
                    Hiss(d, 0, 0.4f, 0.12f, 0.6f, 0.9f, 0.8f, 2f);
                    Reverb(d, 0.5f);
                    break;
                case SfxId.Checkpoint: // two-note chime, G5 → C6
                    d = Buf(1.4f);
                    Bell(d, 0, 0.9f, Note(10), 0.35f, 4f);
                    Bell(d, 0.13f, 1.1f, Note(15), 0.4f, 3.5f);
                    Reverb(d, 0.55f);
                    break;
                case SfxId.GravityShift: // deep swoosh that bends upward
                    d = Buf(0.9f);
                    Hiss(d, 0, 0.7f, 0.6f, 0.08f, 0.02f, 0.45f, 3f);
                    Sine(d, 0, 0.7f, 110, 330, 0.45f, 3f, 0.08f, 0.3f);
                    Sine(d, 0.05f, 0.6f, 220, 660, 0.15f, 4f, 0.08f, 0f);
                    Reverb(d, 0.4f);
                    break;
                case SfxId.Nullify: // crystalline freeze: a high, glassy cluster that settles
                    d = Buf(1.1f);
                    Bell(d, 0, 0.9f, Note(22), 0.22f, 4f, 3.5f, 1.8f);
                    Bell(d, 0.02f, 0.85f, Note(26), 0.16f, 4.5f, 3.5f, 1.8f);
                    Bell(d, 0.04f, 0.8f, Note(29), 0.12f, 5f, 3.5f, 1.8f);
                    Sine(d, 0, 0.5f, 900, 450, 0.12f, 4f);
                    Reverb(d, 0.6f);
                    break;
                case SfxId.Release: // soft pop down
                    d = Buf(0.3f);
                    Sine(d, 0, 0.18f, 620, 280, 0.4f, 6f);
                    Reverb(d, 0.3f);
                    break;
                case SfxId.Denied: // two low, muted taps
                    d = Buf(0.4f);
                    Sine(d, 0, 0.12f, 220, 200, 0.4f, 5f, 0.004f, 0.4f);
                    Sine(d, 0.14f, 0.14f, 185, 165, 0.4f, 5f, 0.004f, 0.4f);
                    break;
                case SfxId.Warning: // soft double tone
                    d = Buf(0.6f);
                    Bell(d, 0, 0.25f, Note(12), 0.35f, 5f, 1f, 0.6f);
                    Bell(d, 0.2f, 0.3f, Note(12), 0.35f, 5f, 1f, 0.6f);
                    Reverb(d, 0.3f);
                    break;
                case SfxId.Alarm: // smooth two-tone siren
                    d = Buf(0.8f);
                    for (int i = 0; i < 2; i++) Sine(d, i * 0.4f, 0.4f, 600, 820, 0.35f, 0.5f, 0.03f, 0.25f);
                    break;
                case SfxId.Crack: // dull cracking
                    d = Buf(0.6f);
                    for (int i = 0; i < 6; i++) Hiss(d, i * 0.07f + (float)rng.NextDouble() * 0.03f, 0.07f, 0.8f, 0.3f, 0.05f, 0.05f, 9f);
                    Sine(d, 0, 0.3f, 90, 60, 0.3f, 5f);
                    break;
                case SfxId.Impact:
                    d = Buf(1.2f);
                    Hiss(d, 0, 0.9f, 0.9f, 0.06f, 0.01f, 0.01f, 5f);
                    Sine(d, 0, 0.8f, 75, 35, 0.8f, 4f, 0.002f, 0.3f);
                    Reverb(d, 0.3f);
                    break;
                case SfxId.Door: // pneumatic slide
                    d = Buf(1.1f);
                    Hiss(d, 0, 0.9f, 0.45f, 0.25f, 0.2f, 0.15f, 2.5f);
                    Sine(d, 0, 0.9f, 95, 150, 0.25f, 2f, 0.05f, 0.4f);
                    Sine(d, 0.85f, 0.2f, 120, 80, 0.35f, 6f);
                    break;
                case SfxId.Plate: // click + soft tone
                    d = Buf(0.3f);
                    Hiss(d, 0, 0.025f, 0.5f, 0.6f, 0.3f, 0.05f, 8f);
                    Sine(d, 0, 0.2f, 360, 420, 0.3f, 6f);
                    Reverb(d, 0.2f);
                    break;
                case SfxId.Success: // rising major arpeggio with a bright last note
                {
                    d = Buf(2.2f);
                    int[] steps = { 3, 7, 10, 15 };
                    for (int i = 0; i < steps.Length; i++)
                        Bell(d, i * 0.14f, i == 3 ? 1.5f : 0.7f, Note(steps[i]), i == 3 ? 0.4f : 0.3f, i == 3 ? 3f : 4.5f);
                    Sine(d, 0.42f, 1.4f, Note(-9), Note(-9), 0.18f, 2.5f, 0.05f, 0.2f);
                    Reverb(d, 0.55f);
                    break;
                }
                case SfxId.Tick: // menu tick: tiny wooden click
                    d = Buf(0.06f);
                    Sine(d, 0, 0.05f, 1500, 1100, 0.25f, 10f, 0.001f, 0f);
                    break;
                case SfxId.Whoosh:
                {
                    // filtered noise sweeping up then away, with a doppler-ish tone underneath
                    d = Buf(1.2f);
                    float lp = 0f;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = (float)i / Rate, k = t / 1.2f;
                        float env = Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f));
                        float cut = Mathf.Lerp(0.02f, 0.35f, Mathf.Sin(Mathf.PI * k));
                        lp += (Noise() - lp) * cut;
                        d[i] = lp * env * 1.6f + 0.12f * env * Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(220f, 90f, k) * t);
                    }
                    break;
                }
                case SfxId.Boom:
                {
                    // deep cinematic hit: sub drop + noise body + long tail
                    d = Buf(2.6f);
                    float lp = 0f;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = (float)i / Rate;
                        float sub = Mathf.Sin(2 * Mathf.PI * (55f * t - 18f * t * t)) * Mathf.Exp(-t * 1.6f);
                        lp += (Noise() - lp) * Mathf.Lerp(0.25f, 0.02f, Mathf.Clamp01(t * 2f));
                        d[i] = sub * 0.9f + lp * Mathf.Exp(-t * 2.2f) * 1.4f;
                    }
                    break;
                }
                case SfxId.Rumble:
                {
                    d = Buf(2.2f);
                    float lp = 0f;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = (float)i / Rate, k = t / 2.2f;
                        lp += (Noise() - lp) * 0.015f;
                        float env = Mathf.Sin(Mathf.PI * k);
                        d[i] = (lp * 6f + 0.3f * Mathf.Sin(2 * Mathf.PI * 38f * t)) * env * (0.7f + 0.3f * Mathf.Sin(2 * Mathf.PI * 7f * t));
                    }
                    break;
                }
                case SfxId.Riser:
                {
                    // rising tension: soft detuned tones gliding up + noise swell, cut off at the top
                    d = Buf(3f);
                    float lp = 0f, p1 = 0f, p2 = 0f;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = (float)i / Rate, k = t / 3f;
                        float f = Mathf.Lerp(110f, 660f, k * k);
                        p1 += f / Rate; p2 += f * 1.01f / Rate;
                        float tone = Mathf.Sin(2 * Mathf.PI * p1) + Mathf.Sin(2 * Mathf.PI * p2) * 0.8f;
                        lp += (Noise() - lp) * Mathf.Lerp(0.02f, 0.4f, k);
                        float env = k * k * (k < 0.97f ? 1f : (1f - k) / 0.03f);
                        d[i] = (tone * 0.3f + lp * 0.8f) * env;
                    }
                    break;
                }
                case SfxId.Comms: // radio chirp before someone talks
                    d = Buf(0.25f);
                    Sine(d, 0, 0.06f, 1600, 1700, 0.22f, 3f, 0.002f, 0f);
                    Sine(d, 0.08f, 0.07f, 2100, 2000, 0.2f, 3f, 0.002f, 0f);
                    Hiss(d, 0, 0.2f, 0.12f, 0.8f, 0.5f, 0.05f, 5f);
                    break;
                case SfxId.Creak:
                    d = Buf(0.8f);
                    Sine(d, 0, 0.7f, 95, 72, 0.3f, 2f, 0.05f, 0.6f);
                    Hiss(d, 0, 0.6f, 0.15f, 0.03f, 0.01f, 0.2f, 2f);
                    break;
                default: // Spark: little electric crackles
                    d = Buf(0.4f);
                    for (int i = 0; i < 7; i++) Hiss(d, i * 0.045f, 0.03f, 0.45f, 0.9f, 0.5f, 0.05f, 10f);
                    break;
            }
            Normalize(d, 0.85f);
            return Clip(id.ToString(), d);
        }

        static AudioClip MakeLoop(LoopId id)
        {
            var d = Buf(4f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float s;
                switch (id)
                {
                    case LoopId.Ambience:
                        lp += (Noise() - lp) * 0.01f;
                        s = lp * 2.5f + 0.25f * Mathf.Sin(2 * Mathf.PI * 55f * t) * (0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * 0.25f * t))
                          + 0.12f * Mathf.Sin(2 * Mathf.PI * 82.5f * t);
                        break;
                    case LoopId.Drone:
                        // low cinematic bed: two slow-beating fifths and a soft filtered air
                        lp += (Noise() - lp) * 0.004f;
                        s = 0.35f * Mathf.Sin(2 * Mathf.PI * 49f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 49.5f * t)
                          + 0.18f * Mathf.Sin(2 * Mathf.PI * 73.5f * t) * (0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * 0.25f * t)) + lp * 3f;
                        break;
                    case LoopId.EngineHum:
                        s = 0.4f * Mathf.Sin(2 * Mathf.PI * 70f * t) + 0.25f * Mathf.Sin(2 * Mathf.PI * 140f * t) * (0.7f + 0.3f * Mathf.Sin(2 * Mathf.PI * 2f * t))
                          + 0.1f * Mathf.Sin(2 * Mathf.PI * 210f * t);
                        break;
                    default: // Alarm: a smooth wailing siren (lp holds the phase here)
                    {
                        float sweep = 0.5f - 0.5f * Mathf.Cos(2 * Mathf.PI * t / 2f);
                        lp += 2 * Mathf.PI * Mathf.Lerp(560f, 820f, sweep) / Rate;
                        s = 0.3f * (Mathf.Sin(lp) + 0.2f * Mathf.Sin(2f * lp));
                        break;
                    }
                }
                d[i] = s;
            }
            return Seamless(d, 0.7f, "Loop_" + id);
        }

        /// <summary>
        /// Calm space music, a 32 s loop: a slow pad over Am – F – C – G, a soft bass and a gentle bell arpeggio.
        /// </summary>
        static AudioClip MakeMusic()
        {
            const float bar = 8f, length = bar * 4f;
            var d = Buf(length + 1.5f);
            // chord tones in semitones from A4; the bells play them an octave up
            int[][] chords =
            {
                new[] { -12, -9, -5, 0 },   // Am
                new[] { -16, -12, -9, -4 }, // F
                new[] { -21, -14, -9, -5 }, // C
                new[] { -14, -10, -7, -2 }, // G
            };
            int[] roots = { -24, -28, -21, -26 };
            int[] order = { 0, 1, 2, 3, 2, 1, 2, 3 };
            for (int c = 0; c < 4; c++)
            {
                float start = c * bar;
                // pad: two slightly detuned soft tones per note, swelling in and out (overlapping the next chord)
                foreach (int semi in chords[c])
                {
                    Pad(d, start, bar + 1.2f, Note(semi), 0.08f);
                    Pad(d, start, bar + 1.2f, Note(semi) * 1.004f, 0.06f);
                }
                Pad(d, start, bar + 1.2f, Note(roots[c]), 0.14f);
                // arpeggio: one note a second up and down the chord, a quieter echo an octave higher between
                for (int i = 0; i < 8; i++)
                {
                    float t = start + i;
                    Bell(d, t, 1.6f, Note(chords[c][order[i]] + 12), i == 0 ? 0.07f : 0.05f, 4f, 2f, 1.2f);
                    if (i % 2 == 0) Bell(d, t + 0.5f, 1.2f, Note(chords[c][order[(i + 3) % 8]] + 24), 0.025f, 5f, 2f, 1f);
                }
            }
            // the last chord's tail wraps onto the start, so the loop has no seam
            int n = (int)(length * Rate);
            for (int i = n; i < d.Length; i++) d[i - n] += d[i];
            System.Array.Resize(ref d, n);
            Reverb(d, 0.5f, 0.7f);
            Normalize(d, 0.6f);
            return Clip("Loop_Music", d);
        }

        static void Pad(float[] d, float start, float dur, float f, float amp)
        {
            int s = (int)(start * Rate), n = (int)(dur * Rate);
            for (int i = 0; i < n && s + i < d.Length; i++)
            {
                float t = (float)i / Rate, k = (float)i / n;
                float env = Mathf.Sin(Mathf.PI * k); // swell in, fade out
                float w = Mathf.Sin(2 * Mathf.PI * f * t) + 0.25f * Mathf.Sin(2 * Mathf.PI * 2f * f * t) * (0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 0.2f * t));
                d[s + i] += w * amp * env;
            }
        }

        /// <summary>Crossfade the ends of a loop so it repeats without a click.</summary>
        static AudioClip Seamless(float[] d, float peak, string name)
        {
            int n = d.Length, fade = Rate / 10;
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                d[i] = d[i] * k + d[n - fade + i] * (1f - k);
            }
            System.Array.Resize(ref d, n - fade);
            Normalize(d, peak);
            return Clip(name, d);
        }

        static void Normalize(float[] d, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }
    }
}
