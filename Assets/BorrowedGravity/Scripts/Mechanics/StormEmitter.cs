using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Debris storm: bursts of wreckage fly down a hall. Warning lights pulse before each burst, and the bursts come
    /// faster the further the players get. Shields, pillars and walls stop the debris.
    /// </summary>
    public class StormEmitter : MonoBehaviour
    {
        public GameObject[] projectileTemplates;
        public Vector3 spawnMin;
        public Vector3 spawnMax;
        public Vector3 velocity = new Vector3(-15f, 0f, 0f);
        public int burstCount = 5;
        public float burstSpread = 0.18f;
        public float warnTime = 1.3f;
        [Tooltip("Gap between bursts at the start / end of the hall.")]
        public float intervalStart = 3.4f, intervalEnd = 2.0f;
        public float hallStartX, hallEndX;
        public Light[] warnLights;
        public Renderer[] warnRenderers;

        float timer;
        int toSpawn;
        float spawnTimer;
        bool warned;
        Material[] warnMats;

        void Awake()
        {
            foreach (var t in projectileTemplates) if (t != null) t.SetActive(false);
            warnMats = new Material[warnRenderers != null ? warnRenderers.Length : 0];
            for (int i = 0; i < warnMats.Length; i++) { warnMats[i] = warnRenderers[i].material; warnMats[i].EnableKeyword("_EMISSION"); }
            timer = intervalStart;
        }

        bool PlayersInHall(out float progress)
        {
            progress = 0f;
            bool any = false;
            foreach (var p in Players.All)
            {
                if (p == null) continue;
                float x = p.transform.position.x;
                if (x > hallStartX - 6f && x < hallEndX + 2f)
                {
                    any = true;
                    progress = Mathf.Max(progress, Mathf.InverseLerp(hallStartX, hallEndX, x));
                }
            }
            return any;
        }

        void Update()
        {
            bool active = PlayersInHall(out float progress);
            float interval = Mathf.Lerp(intervalStart, intervalEnd, progress);
            float warn = warnTime * Mathf.Lerp(1f, 0.65f, progress);
            if (active) timer -= Time.deltaTime;

            bool warning = active && timer <= warn && timer > 0f;
            if (warning && !warned) { warned = true; Sfx.Play(SfxId.Warning, 0.5f, 1.2f); }
            float blink = warning ? (Mathf.Repeat(timer * 8f, 1f) > 0.5f ? 1f : 0.1f) : 0f;
            if (warnLights != null) foreach (var l in warnLights) if (l != null) { l.enabled = blink > 0f; l.intensity = 6f * blink; }
            foreach (var m in warnMats) m.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.1f) * 4f * blink);

            if (active && timer <= 0f)
            {
                toSpawn = burstCount;
                spawnTimer = 0f;
                timer = interval;
                warned = false;
                CoopCamera.Shake(0.35f, 0.4f);
            }
            if (toSpawn > 0)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0f)
                {
                    SpawnOne();
                    toSpawn--;
                    spawnTimer = burstSpread;
                }
            }
        }

        void SpawnOne()
        {
            var template = projectileTemplates[Random.Range(0, projectileTemplates.Length)];
            Vector3 p = new Vector3(Random.Range(spawnMin.x, spawnMax.x), Random.Range(spawnMin.y, spawnMax.y), Random.Range(spawnMin.z, spawnMax.z));
            var go = Instantiate(template, p, Random.rotation, transform);
            go.SetActive(true);
            NetWorld.Spawned(template, go);
            var proj = go.GetComponent<StormProjectile>();
            if (proj == null) proj = go.AddComponent<StormProjectile>();
            proj.velocity = velocity * Random.Range(0.85f, 1.15f);
        }
    }
}
