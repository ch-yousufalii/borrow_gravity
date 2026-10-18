using UnityEngine;

namespace BorrowGravity
{
    /// <summary>
    /// Names the section in the HUD the first time a player walks in. The instruction itself is on the section's
    /// <see cref="InfoBoard"/> next to the puzzle (it used to be repeated in a HUD box, which doubled the text).
    /// </summary>
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
        }
    }
}
