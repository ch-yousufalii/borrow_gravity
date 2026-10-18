using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Completes the stage when both players (and optionally a condition, e.g. the engine docked) are inside.</summary>
    [RequireComponent(typeof(Collider))]
    public class GoalTrigger : MonoBehaviour
    {
        public int requiredPlayers = 2;
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
            bool players = inside.Count >= Mathf.Max(1, Mathf.Min(requiredPlayers <= 2 ? Players.All.Count : requiredPlayers, Players.All.Count));
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
