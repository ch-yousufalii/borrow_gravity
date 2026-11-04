using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// An optional pickup placed off the safe route. The siblings under one parent make up a set, so the count needs no
    /// global state.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class SalvageCore : MonoBehaviour
    {
        public float spinSpeed = 90f;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void Update() => transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        void OnTriggerEnter(Collider other)
        {
            var p = Players.From(other);
            if (p == null) return;
            gameObject.SetActive(false);
            int total = 0, found = 0;
            foreach (Transform t in transform.parent)
            {
                if (t.GetComponent<SalvageCore>() == null) continue;
                total++;
                if (!t.gameObject.activeSelf) found++;
            }
            Sfx.Play(SfxId.Success, 0.5f, 1.4f);
            GameUI.Toast(p, $"Salvage core {found}/{total}");
            if (found == total) GameUI.ShowMessage("Every salvage core found!", 3f);
        }
    }
}
