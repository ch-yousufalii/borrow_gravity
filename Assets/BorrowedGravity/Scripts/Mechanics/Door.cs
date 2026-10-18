using System.Collections.Generic;
using UnityEngine;

namespace BorrowGravity
{
    /// <summary>Slides parts between closed and open poses following a signal.</summary>
    public class Door : MonoBehaviour, IResettable
    {
        public int section;
        public SignalSource source;
        public Transform[] parts;
        public Vector3[] openOffsets;
        public float speed = 2.5f;
        [Tooltip("Stay open once opened.")]
        public bool latch = true;
        public bool invert;
        public string openMessage;

        Vector3[] closedPos;
        bool open, everOpened;

        public bool IsOpen => open;
        public int Section => section;

        void Awake()
        {
            closedPos = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++) closedPos[i] = parts[i].localPosition;
        }

        void Update()
        {
            bool want = source != null && source.IsOn;
            if (invert) want = !want;
            if (latch && everOpened) want = true;
            if (want != open)
            {
                open = want;
                if (open)
                {
                    everOpened = true;
                    if (!string.IsNullOrEmpty(openMessage)) GameUI.ShowMessage(openMessage, 3f);
                }
                Sfx.Play(SfxId.Door, 0.7f, open ? 1f : 0.85f);
            }
            for (int i = 0; i < parts.Length; i++)
            {
                Vector3 target = closedPos[i] + (open ? openOffsets[Mathf.Min(i, openOffsets.Length - 1)] : Vector3.zero);
                var rb = parts[i].GetComponent<Rigidbody>();
                Vector3 next = Vector3.MoveTowards(parts[i].localPosition, target, speed * Time.deltaTime);
                if (rb != null && rb.isKinematic && parts[i].parent != null)
                    rb.MovePosition(parts[i].parent.TransformPoint(next));
                else
                    parts[i].localPosition = next;
            }
        }

        public void ResetState()
        {
            open = false;
            everOpened = false;
            for (int i = 0; i < parts.Length; i++) parts[i].localPosition = closedPos[i];
        }
    }
}
