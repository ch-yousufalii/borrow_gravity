using UnityEngine;
using System.Collections.Generic;

public class ExitZone : MonoBehaviour
{
    public int requiredPlayers = 2;
    public float nextLevelDelay = 1.5f;

    HashSet<Collider> inside = new HashSet<Collider>();
    bool done;
    float timer;
    GUIStyle big, small;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void Start()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
            inside.Add(other);
    }

    void OnTriggerExit(Collider other)
    {
        inside.Remove(other);
    }

    int CountPlayers()
    {
        var set = new HashSet<PlayerController>();
        foreach (var c in inside)
        {
            if (c == null) continue;
            var p = c.GetComponentInParent<PlayerController>();
            if (p != null) set.Add(p);
        }
        return set.Count;
    }

    void Update()
    {
        if (done)
        {
            // Time.timeScale 0 nahi karte, taake timer chale
            timer += Time.deltaTime;
            if (timer >= nextLevelDelay)
            {
                if (LevelManager.Instance != null)
                {
                    if (LevelManager.Instance.IsLastLevel)
                        LevelManager.Instance.NextLevel();   // shuru se
                    else
                        LevelManager.Instance.NextLevel();
                }
            }
            return;
        }

        if (CountPlayers() >= requiredPlayers)
        {
            done = true;
            timer = 0f;
        }
    }

    void OnGUI()
    {
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label);
            big.fontSize = 56;
            big.alignment = TextAnchor.MiddleCenter;
            big.normal.textColor = Color.green;
            small = new GUIStyle(GUI.skin.label);
            small.fontSize = 24;
            small.alignment = TextAnchor.UpperCenter;
            small.normal.textColor = Color.white;
        }

        if (!done)
        {
            GUI.Label(new Rect(0, 40, Screen.width, 40),
                      "Exit: " + CountPlayers() + " / " + requiredPlayers + " players", small);
            return;
        }

        bool last = LevelManager.Instance != null && LevelManager.Instance.IsLastLevel;
        string msg = last ? "SHIP REPAIRED!\nYOU WIN" : "LEVEL COMPLETE!";
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), msg, big);
    }
}