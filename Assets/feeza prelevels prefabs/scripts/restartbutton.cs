using UnityEngine;
using UnityEngine.UI;

public class RestartButton : MonoBehaviour
{
    Font font;

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGO = new GameObject("RestartCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;   // touch buttons ke upar, StartScreen (500) ke neeche
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var go = new GameObject("RestartButton");
        go.transform.SetParent(canvasGO.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.9f, 0.2f, 0.2f, 0.75f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(240, 100);
        rt.anchoredPosition = new Vector2(-30, -30);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Restart);

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var t = textGO.AddComponent<Text>();
        t.font = font;
        t.text = "RESTART";
        t.fontSize = 44;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.raycastTarget = false;
        var trt = textGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
    }

    public void Restart()
    {
        // Start screen abhi khuli hai to kuch mat karo
        if (GameObject.Find("StartCanvas") != null) return;
        if (LevelManager.Instance == null) return;

        if (GameManager.Instance != null) GameManager.Instance.ResetState();
        LevelManager.Instance.Restart();
    }
}