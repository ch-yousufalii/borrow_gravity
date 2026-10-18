using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Magnetic cargo lane: clones of the template loads slide from <see cref="start"/> to <see cref="end"/> and shove
    /// anyone in the way back down the lane.
    /// </summary>
    public class Conveyor : MonoBehaviour, IResettable
    {
        public int section;
        public GameObject[] loadTemplates;
        public Vector3 start;
        public Vector3 end;
        public float speed = 3.2f;
        public float spawnInterval = 3.4f;
        public float firstSpawnDelay;

        class Load { public GameObject go; public Rigidbody rb; public float t; public float length; }
        readonly List<Load> loads = new List<Load>();
        float timer;
        int next;

        public int Section => section;

        void Start()
        {
            foreach (var t in loadTemplates) if (t != null) t.SetActive(false);
            timer = firstSpawnDelay;
            // pre-fill the lane so it is busy from the first frame
            float length = Vector3.Distance(start, end);
            for (float d = length * 0.15f; d < length; d += speed * spawnInterval) Spawn(d / length);
        }

        void FixedUpdate()
        {
            timer -= Time.fixedDeltaTime;
            if (timer <= 0f)
            {
                Spawn(0f);
                timer = spawnInterval;
            }
            float length = Mathf.Max(0.1f, Vector3.Distance(start, end));
            for (int i = loads.Count - 1; i >= 0; i--)
            {
                var l = loads[i];
                l.t += speed * Time.fixedDeltaTime / length;
                if (l.t >= 1f)
                {
                    Destroy(l.go);
                    loads.RemoveAt(i);
                    continue;
                }
                l.rb.MovePosition(Vector3.Lerp(start, end, l.t));
            }
        }

        void Spawn(float t)
        {
            if (loadTemplates == null || loadTemplates.Length == 0) return;
            var template = loadTemplates[next % loadTemplates.Length];
            next++;
            if (template == null) return;
            var go = Instantiate(template, Vector3.Lerp(start, end, t), template.transform.rotation, transform);
            go.SetActive(true);
            NetWorld.Spawned(template, go);
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            loads.Add(new Load { go = go, rb = rb, t = t });
        }

        public void ResetState() { }
    }
}
