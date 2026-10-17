using UnityEngine;

public class GameHUD : MonoBehaviour
{
    public string stageTitle = "STAGE 1 — THE GAP";
    public string stageHint = "Get BOTH astronauts across the gap to the bridge";
    public FreezePower freezePower;

    GUIStyle left, center, hint, right;

    void Build()
    {
        if (left != null) return;
        int big = Mathf.RoundToInt(Screen.height * 0.035f);
        int small = Mathf.RoundToInt(Screen.height * 0.022f);

        left = new GUIStyle(GUI.skin.label) { fontSize = big, fontStyle = FontStyle.Bold };
        center = new GUIStyle(GUI.skin.label) { fontSize = big, alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
        center.normal.textColor = Color.white;
        hint = new GUIStyle(GUI.skin.label) { fontSize = small, alignment = TextAnchor.UpperCenter };
        hint.normal.textColor = new Color(0.8f, 0.85f, 1f);
        right = new GUIStyle(GUI.skin.label) { fontSize = small, alignment = TextAnchor.UpperRight };
        right.normal.textColor = new Color(0.2f, 1f, 0.9f);
    }

    void OnGUI()
    {
        Build();

        bool on = GravityObject.RoomGravityOn;
        left.normal.textColor = on ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.3f, 0.9f);
        GUI.Label(new Rect(20, 15, 500, 60), "GRAVITY: " + (on ? "ON" : "OFF"), left);

        GUI.Label(new Rect(0, 12, Screen.width, 60), stageTitle, center);
        GUI.Label(new Rect(0, 12 + Screen.height * 0.045f, Screen.width, 40), stageHint, hint);

        string target = "none", anchored = "none";
        if (freezePower != null)
        {
            if (freezePower.Target != null) target = freezePower.Target.name;
            if (freezePower.Frozen != null) anchored = freezePower.Frozen.name;
        }
        GUI.Label(new Rect(Screen.width - 520, 15, 500, 80),
                  "ANCHOR\nTarget: " + target + "\nAnchored: " + anchored, right);
    }
}