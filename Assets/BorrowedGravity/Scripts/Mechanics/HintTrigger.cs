using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Shows a short instruction (and updates the objective line) the first time a player walks in.</summary>
    [RequireComponent(typeof(Collider))]
    public class HintTrigger : MonoBehaviour
    {
        [TextArea] public string message;
        public float duration = 5f;
        public string sectionTitle;
        public bool once = true;

        bool fired;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if ((once && fired) || Players.From(other) == null) return;
            fired = true;
            if (!string.IsNullOrEmpty(sectionTitle)) GameUI.SetSection(sectionTitle);
            if (!string.IsNullOrEmpty(message)) GameUI.ShowHint(message, duration);
        }
    }
}
