using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class TouchUI : MonoBehaviour
{
    [Header("Kis player ke buttons dikhane hain")]
    public bool showPlayer1 = true;
    public bool showPlayer2 = true;

    public float size = 170f;
    public float gap = 20f;
    public float margin = 30f;

    Font font;

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SetupEventSystem();

        var canvasGO = new GameObject("TouchCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        Color blue = new Color(0.2f, 0.5f, 1f, 0.55f);
        Color orange = new Color(1f, 0.55f, 0.1f, 0.55f);

        if (showPlayer1)
        {
            // P1: neeche left
            Make(canvasGO.transform, "<", TouchButton.Action.P1Left, 0, 1, 0, blue);
            Make(canvasGO.transform, ">", TouchButton.Action.P1Right, 0, 0, 0, blue);
            Make(canvasGO.transform, "JUMP", TouchButton.Action.P1Jump, 0, 1, 1, blue);
            Make(canvasGO.transform, "GRAVITY", TouchButton.Action.P1Power, 0, 0, 1, blue);
        }
        if (showPlayer2)
        {
            // P2: neeche right
            Make(canvasGO.transform, "<", TouchButton.Action.P2Left, 1, 1, 0, orange);
            Make(canvasGO.transform, ">", TouchButton.Action.P2Right, 1, 0, 0, orange);
            Make(canvasGO.transform, "JUMP", TouchButton.Action.P2Jump, 1, 1, 1, orange);
            Make(canvasGO.transform, "FREEZE", TouchButton.Action.P2Power, 1, 0, 1, orange);
        }
    }

    void SetupEventSystem()
    {
        var es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
        }
        var old = es.GetComponent<StandaloneInputModule>();
        if (old) Destroy(old);
        if (es.GetComponent<InputSystemUIInputModule>() == null)
            es.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    // side: 0 = left, 1 = right | col: 0 kinare ke paas | row: 0 neeche
    void Make(Transform parent, string label, TouchButton.Action action,
              int side, int col, int row, Color color)
    {
        var go = new GameObject(action.ToString());
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;

        var rt = go.GetComponent<RectTransform>();
        float anchorX = side;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(anchorX, 0f);
        rt.sizeDelta = new Vector2(size, size);
        float dir = side == 0 ? 1f : -1f;
        rt.anchoredPosition = new Vector2(dir * (margin + col * (size + gap)),
                                          margin + row * (size + gap));

        var tb = go.AddComponent<TouchButton>();
        tb.action = action;

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var t = textGO.AddComponent<Text>();
        t.font = font;
        t.text = label;
        t.fontSize = label.Length > 2 ? 32 : 60;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
    }
}