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

    public enum LoopId { Ambience, EngineHum, Alarm, Drone }

    /// <summary>
    /// Procedurally synthesised sound effects (the jam has no audio assets). Clips are generated once on demand.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        static readonly Dictionary<LoopId, AudioSource> loops = new Dictionary<LoopId, AudioSource>();
        static GameObject host;
        static AudioSource[] voices;
        static int nextVoice;
        static readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();
        static System.Random rng = new System.Random(7);

        public static float MasterVolume = 0.8f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            host = null;
            voices = null;
            loops.Clear();
            lastPlayed.Clear();
        }

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
            src.PlayOneShot(Get(id), volume * MasterVolume);
        }

        public static void SetLoop(LoopId id, bool on, float volume = 0.5f)
        {
            if (!Application.isPlaying) return;
            NetSession.ForwardLoop(id, on, volume);
            EnsureHost();
            if (!loops.TryGetValue(id, out var src) || src == null)
            {
                if (!on) return;
                src = host.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                src.clip = MakeLoop(id);
                loops[id] = src;
            }
            src.volume = volume * MasterVolume;
            if (on && !src.isPlaying) src.Play();
            if (!on && src.isPlaying) src.Stop();
        }

        public static void StopAllLoops()
        {
            NetSession.ForwardStopLoops();
            foreach (var s in loops.Values) if (s != null) s.Stop();
        }

        static AudioClip Get(SfxId id)
        {
            if (clips.TryGetValue(id, out var c) && c != null) return c;
            c = Make(id);
            clips[id] = c;
            return c;
        }

        // ---------- synthesis ----------

        static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        static void Tone(float[] d, float start, float dur, float f0, float f1, float amp, float decay = 6f, int wave = 0)
        {
            int s = (int)(start * Rate), n = (int)(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n && s + i < d.Length; i++)
            {
                float k = (float)i / n;
                float f = Mathf.Lerp(f0, f1, k);
                phase += 2 * Mathf.PI * f / Rate;
                float w = wave == 1 ? Mathf.Sign(Mathf.Sin((float)phase)) * 0.6f
                        : wave == 2 ? (float)(phase / System.Math.PI % 2.0 - 1.0) * 0.7f
                        : Mathf.Sin((float)phase);
                float env = Mathf.Exp(-decay * k) * Mathf.Clamp01(i / (Rate * 0.004f));
                d[s + i] += w * amp * env;
            }
        }

        static void NoiseBurst(float[] d, float start, float dur, float amp, float decay, float smooth)
        {
            int s = (int)(start * Rate), n = (int)(dur * Rate);
            float lp = 0f;
            for (int i = 0; i < n && s + i < d.Length; i++)
            {
                float k = (float)i / n;
                lp += (Noise() - lp) * smooth;
                d[s + i] += lp * amp * Mathf.Exp(-decay * k);
            }
        }

        static AudioClip Make(SfxId id)
        {
            float[] d;
            switch (id)
            {
                case SfxId.Jump:
                    d = Buf(0.12f); Tone(d, 0, 0.12f, 320, 640, 0.35f, 5f, 1); break;
                case SfxId.Hit:
                    d = Buf(0.3f); NoiseBurst(d, 0, 0.25f, 0.8f, 8f, 0.25f); Tone(d, 0, 0.3f, 140, 60, 0.7f, 7f); break;
                case SfxId.Respawn:
                    d = Buf(0.5f); Tone(d, 0, 0.25f, 900, 300, 0.3f, 3f); Tone(d, 0.2f, 0.3f, 300, 1100, 0.3f, 3f); break;
                case SfxId.Checkpoint:
                    d = Buf(0.6f); Tone(d, 0, 0.2f, 523, 523, 0.35f, 4f); Tone(d, 0.12f, 0.2f, 659, 659, 0.35f, 4f); Tone(d, 0.24f, 0.35f, 784, 784, 0.4f, 3f); break;
                case SfxId.GravityShift:
                    d = Buf(0.6f); Tone(d, 0, 0.6f, 180, 900, 0.4f, 2.5f, 2); NoiseBurst(d, 0, 0.5f, 0.25f, 3f, 0.08f); break;
                case SfxId.Nullify:
                    d = Buf(0.6f);
                    Tone(d, 0, 0.6f, 1400, 420, 0.3f, 2.5f);
                    Tone(d, 0.03f, 0.55f, 1800, 600, 0.18f, 3f);
                    break;
                case SfxId.Release:
                    d = Buf(0.15f); Tone(d, 0, 0.15f, 700, 300, 0.3f, 6f); break;
                case SfxId.Denied:
                    d = Buf(0.3f); Tone(d, 0, 0.1f, 150, 150, 0.35f, 2f, 1); Tone(d, 0.15f, 0.1f, 130, 130, 0.35f, 2f, 1); break;
                case SfxId.Warning:
                    d = Buf(0.35f); Tone(d, 0, 0.12f, 880, 880, 0.35f, 2f); Tone(d, 0.18f, 0.12f, 880, 880, 0.35f, 2f); break;
                case SfxId.Alarm:
                    d = Buf(0.8f); for (int i = 0; i < 4; i++) Tone(d, i * 0.2f, 0.2f, i % 2 == 0 ? 620 : 820, i % 2 == 0 ? 620 : 820, 0.3f, 1f, 1); break;
                case SfxId.Crack:
                    d = Buf(0.5f); for (int i = 0; i < 6; i++) NoiseBurst(d, i * 0.07f + (float)rng.NextDouble() * 0.03f, 0.06f, 0.7f, 10f, 0.6f); break;
                case SfxId.Impact:
                    d = Buf(1f); NoiseBurst(d, 0, 0.9f, 1f, 5f, 0.05f); Tone(d, 0, 0.8f, 70, 35, 0.8f, 4f); break;
                case SfxId.Door:
                    d = Buf(1f); NoiseBurst(d, 0, 1f, 0.4f, 2f, 0.03f); Tone(d, 0, 1f, 90, 140, 0.35f, 2f, 2); break;
                case SfxId.Plate:
                    d = Buf(0.15f); NoiseBurst(d, 0, 0.03f, 0.5f, 10f, 0.7f); Tone(d, 0, 0.15f, 300, 420, 0.3f, 5f); break;
                case SfxId.Success:
                    d = Buf(1.4f);
                    Tone(d, 0, 0.3f, 523, 523, 0.3f, 3f); Tone(d, 0.18f, 0.3f, 659, 659, 0.3f, 3f);
                    Tone(d, 0.36f, 0.3f, 784, 784, 0.3f, 3f); Tone(d, 0.54f, 0.8f, 1046, 1046, 0.35f, 2f);
                    break;
                case SfxId.Tick:
                    d = Buf(0.05f); Tone(d, 0, 0.05f, 1300, 1300, 0.25f, 12f); break;
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
                    // rising tension: detuned saws gliding up + noise swell, cut off at the top
                    d = Buf(3f);
                    float lp = 0f, p1 = 0f, p2 = 0f;
                    for (int i = 0; i < d.Length; i++)
                    {
                        float t = (float)i / Rate, k = t / 3f;
                        float f = Mathf.Lerp(110f, 660f, k * k);
                        p1 += f / Rate; p2 += f * 1.01f / Rate;
                        float saw = (p1 % 1f) * 2f - 1f + (p2 % 1f) * 2f - 1f;
                        lp += (Noise() - lp) * Mathf.Lerp(0.02f, 0.4f, k);
                        float env = k * k * (k < 0.97f ? 1f : (1f - k) / 0.03f);
                        d[i] = (saw * 0.25f + lp * 0.8f) * env;
                    }
                    break;
                }
                case SfxId.Comms:
                    // radio chirp before someone talks
                    d = Buf(0.22f); Tone(d, 0, 0.06f, 1600, 1600, 0.25f, 3f, 1); Tone(d, 0.08f, 0.06f, 2100, 2100, 0.22f, 3f, 1);
                    NoiseBurst(d, 0.0f, 0.2f, 0.12f, 6f, 0.9f);
                    break;
                case SfxId.Creak:
                    d = Buf(0.7f); Tone(d, 0, 0.7f, 95, 70, 0.3f, 2f, 2); NoiseBurst(d, 0, 0.6f, 0.15f, 2f, 0.02f); break;
                default: // Spark
                    d = Buf(0.35f); for (int i = 0; i < 8; i++) NoiseBurst(d, i * 0.04f, 0.03f, 0.5f, 12f, 0.9f); break;
            }
            Normalize(d, 0.9f);
            return Clip(id.ToString(), d);
        }

        static AudioClip MakeLoop(LoopId id)
        {
            float seconds = 4f;
            var d = Buf(seconds);
            int n = d.Length;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float s = 0f;
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
                    default:
                        float ph = t % 1f;
                        float f = ph < 0.5f ? 640f : 860f;
                        s = (ph % 0.5f) < 0.35f ? 0.3f * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * f * t)) : 0f;
                        break;
                }
                d[i] = s;
            }
            // crossfade the ends so the loop is seamless
            int fade = Rate / 10;
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                d[i] = d[i] * k + d[n - fade + i] * (1f - k);
            }
            System.Array.Resize(ref d, n - fade);
            Normalize(d, 0.7f);
            return Clip("Loop_" + id, d);
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
