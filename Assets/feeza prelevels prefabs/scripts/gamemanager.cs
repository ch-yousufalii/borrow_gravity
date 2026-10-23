using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    bool over;
    GUIStyle style;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    public void GameOver()
    {
        if (over) return;
        over = true;
        Time.timeScale = 0f;
    }

    // RestartButton isay call karta hai
    public void ResetState()
    {
        over = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (!over) return;

        bool keyR = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
        bool tap = Touchscreen.current != null &&
                   Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        if (keyR || tap || click)
        {
            ResetState();
            if (LevelManager.Instance != null)
                LevelManager.Instance.Restart();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    void OnGUI()
    {
        if (!over) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label);
            style.fontSize = 48;
            style.alignment = TextAlignment.Center == TextAlignment.Center ? TextAnchor.MiddleCenter : TextAnchor.MiddleCenter;
            style.normal.textColor = Color.red;
        }
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height),
                  "GAME OVER\nTap to Restart", style);
    }
}