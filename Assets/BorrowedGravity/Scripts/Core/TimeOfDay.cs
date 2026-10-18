using System;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Day / evening / night lighting driven by how far the players have travelled along the route (x).
    /// Blends the sun (angle, colour, intensity), the panoramic sky's tint and exposure, fog and ambient light.
    /// </summary>
    public class TimeOfDay : MonoBehaviour
    {
        [Serializable]
        public class Key
        {
            public string label = "Day";
            [Range(0f, 1f)] public float progress;
            public Color sunColor = new Color(1f, 0.92f, 0.82f);
            public float sunIntensity = 1.3f;
            public Vector3 sunEuler = new Vector3(50f, 28f, 0f);
            public Color skyTint = new Color(0.5f, 0.5f, 0.5f);
            public float skyExposure = 1f;
            public Color fogColor = new Color(0.42f, 0.14f, 0.31f);
            public float ambient = 1.15f;
        }

        public static string CurrentLabel { get; private set; } = "";

        public Light sun;
        public float routeStartX, routeEndX = 100f;
        public Key[] keys;
        public float smoothing = 0.6f;

        Material sky;
        float progress = -1f;

        void Start()
        {
            if (RenderSettings.skybox != null)
            {
                sky = new Material(RenderSettings.skybox);
                RenderSettings.skybox = sky;
            }
            if (sun == null) sun = RenderSettings.sun;
            Apply(Evaluate(Target()), true);
        }

        float Target()
        {
            float sum = 0f;
            int n = 0;
            foreach (var p in Players.All) if (p != null) { sum += p.transform.position.x; n++; }
            return n == 0 ? 0f : Mathf.InverseLerp(routeStartX, routeEndX, sum / n);
        }

        void Update()
        {
            float t = Target();
            progress = progress < 0f ? t : Mathf.Lerp(progress, t, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            Apply(Evaluate(progress), false);
        }

        Key Evaluate(float p)
        {
            if (keys == null || keys.Length == 0) return new Key();
            if (p <= keys[0].progress) return keys[0];
            for (int i = 0; i < keys.Length - 1; i++)
            {
                var a = keys[i];
                var b = keys[i + 1];
                if (p > b.progress) continue;
                float k = Mathf.InverseLerp(a.progress, b.progress, p);
                return new Key
                {
                    label = k < 0.5f ? a.label : b.label,
                    sunColor = Color.Lerp(a.sunColor, b.sunColor, k),
                    sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, k),
                    sunEuler = Vector3.Lerp(a.sunEuler, b.sunEuler, k),
                    skyTint = Color.Lerp(a.skyTint, b.skyTint, k),
                    skyExposure = Mathf.Lerp(a.skyExposure, b.skyExposure, k),
                    fogColor = Color.Lerp(a.fogColor, b.fogColor, k),
                    ambient = Mathf.Lerp(a.ambient, b.ambient, k)
                };
            }
            return keys[keys.Length - 1];
        }

        void Apply(Key k, bool force)
        {
            CurrentLabel = k.label;
            if (sun != null)
            {
                sun.color = k.sunColor;
                sun.intensity = k.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(k.sunEuler);
            }
            if (sky != null)
            {
                if (sky.HasProperty("_Tint")) sky.SetColor("_Tint", k.skyTint);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", k.skyExposure);
            }
            RenderSettings.fogColor = k.fogColor;
            RenderSettings.ambientIntensity = k.ambient;
        }

        // ---------- presets used by the stage builders ----------

        public static Key Day() => new Key { label = "Day", sunColor = new Color(1f, 0.93f, 0.85f), sunIntensity = 1.45f, sunEuler = new Vector3(55f, 30f, 0f),
            skyTint = new Color(0.56f, 0.56f, 0.58f), skyExposure = 1.15f, fogColor = new Color(0.55f, 0.32f, 0.42f), ambient = 1.25f };
        public static Key Evening() => new Key { label = "Evening", sunColor = new Color(1f, 0.6f, 0.38f), sunIntensity = 1.15f, sunEuler = new Vector3(14f, 40f, 0f),
            skyTint = new Color(0.55f, 0.42f, 0.42f), skyExposure = 0.9f, fogColor = new Color(0.45f, 0.16f, 0.26f), ambient = 1.0f };
        public static Key Dusk() => new Key { label = "Evening", sunColor = new Color(0.85f, 0.45f, 0.55f), sunIntensity = 0.75f, sunEuler = new Vector3(6f, 50f, 0f),
            skyTint = new Color(0.42f, 0.32f, 0.46f), skyExposure = 0.65f, fogColor = new Color(0.25f, 0.1f, 0.3f), ambient = 0.8f };
        public static Key Night() => new Key { label = "Night", sunColor = new Color(0.55f, 0.65f, 1f), sunIntensity = 0.45f, sunEuler = new Vector3(40f, 200f, 0f),
            skyTint = new Color(0.22f, 0.24f, 0.4f), skyExposure = 0.38f, fogColor = new Color(0.07f, 0.06f, 0.16f), ambient = 0.55f };
        public static Key Dawn() => new Key { label = "Dawn", sunColor = new Color(1f, 0.7f, 0.55f), sunIntensity = 0.95f, sunEuler = new Vector3(8f, 20f, 0f),
            skyTint = new Color(0.5f, 0.4f, 0.45f), skyExposure = 0.8f, fogColor = new Color(0.4f, 0.18f, 0.25f), ambient = 0.9f };

        public static Key At(Key k, float progress) { k.progress = progress; return k; }
    }
}
