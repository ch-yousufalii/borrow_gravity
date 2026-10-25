using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Completes the stage when every player in the game is inside, and the optional condition is on.</summary>
    [RequireComponent(typeof(Collider))]
    public class GoalTrigger : MonoBehaviour
    {
        [Tooltip("Ignored: everyone in the game has to reach the goal.")]
        public int requiredPlayers = 1;
        public SignalSource extraCondition;
        public string waitingForPartner = "Waiting for your partner...";
        public string waitingForCondition = "Finish the job first!";

        readonly HashSet<PlayerController> inside = new HashSet<PlayerController>();
        bool done;
        float lastHint;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var p = Players.From(other);
            if (p != null) inside.Add(p);
        }

        void OnTriggerExit(Collider other)
        {
            var p = Players.From(other);
            if (p != null) inside.Remove(p);
        }

        void Update()
        {
            if (done) return;
            inside.RemoveWhere(p => p == null);
            if (inside.Count == 0) return;
            bool players = inside.Count >= Mathf.Max(1, Players.All.Count);
            bool condition = extraCondition == null || extraCondition.IsOn;
            if (players && condition)
            {
                done = true;
                if (StageManager.Instance != null) StageManager.Instance.CompleteStage();
                return;
            }
            if (Time.time - lastHint > 4f)
            {
                lastHint = Time.time;
                GameUI.ShowMessage(!players ? waitingForPartner : waitingForCondition, 2f);
            }
        }
    }
}
