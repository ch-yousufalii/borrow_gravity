using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ExitZone : MonoBehaviour
{
    public int requiredPlayers = 2;

    readonly HashSet<PlayerController> inside = new HashSet<PlayerController>();
    bool done;
    GUIStyle big, small;

    void OnTriggerEnter(Collider other) { Add(other); }
    void OnTriggerStay(Collider other) { Add(other); }

    void OnTriggerExit(Collider other)
    {
        var p = other.GetComponentInParent<PlayerController>();
        if (p != null) inside.Remove(p);
    }

    void Add(Collider other)
    {
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;
        inside.Add(p);

        if (!done && inside.Count >= requiredPlayers)
        {
            done = true;
            Time.timeScale = 0f;
            Debug.Log("STAGE COMPLETE");
        }
    }

    void Update()
    {
        if (done && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    void OnGUI()
    {
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label);
            big.fontSize = 48;
            big.alignment = TextAnchor.MiddleCenter;
            big.normal.textColor = Color.green;

            small = new GUIStyle(GUI.skin.label);
            small.fontSize = 26;
            small.alignment = TextAnchor.LowerCenter;
            small.normal.textColor = Color.yellow;
        }

        if (done)
        {
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height),
                      "STAGE 1 COMPLETE!\nPress R to Play Again", big);
        }
        else if (inside.Count > 0)
        {
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height - 30),
                      "Exit: " + inside.Count + " / " + requiredPlayers + " players", small);
        }
    }
}
