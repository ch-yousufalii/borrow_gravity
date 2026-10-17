using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class StartScreen : MonoBehaviour
{
    public string gameTitle = "BORROW GRAVITY";
    public string subtitle = "Do astronaut. Do gravity powers.";

    GameObject canvasGO;
    Font font;

    void Start()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Time.timeScale = 0f;   // PLAY tak game ruka rahe
        Build();
    }

    void Update()
    {
        if (canvasGO == null) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
            Play();
    }

    void Build()
    {
        canvasGO = new GameObject("StartCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;   // TouchUI ke upar
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Poori screen ka parda (neeche ke buttons ko bhi block karta hai)
        var bg = NewImage("Background", canvasGO.transform, new Color(0.02f, 0.03f, 0.08f, 1f));
        Stretch(bg.rectTransform);

        MakeText("Title", canvasGO.transform, gameTitle, 110, new Vector2(0, 220), new Vector2(1600, 160), Color.white);
        MakeText("Subtitle", canvasGO.transform, subtitle, 44, new Vector2(0, 110), new Vector2(1600, 80), new Color(0.7f, 0.85f, 1f));

        var btnImg = NewImage("PlayButton", canvasGO.transform, new Color(0.2f, 0.6f, 1f, 1f));
        var rt = btnImg.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(480, 150);
        rt.anchoredPosition = new Vector2(0, -90);
        var btn = btnImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(Play);
        MakeText("PlayLabel", btnImg.transform, "PLAY", 80, Vector2.zero, new Vector2(480, 150), Color.white);
    }

    public void Play()
    {
        if (canvasGO == null) return;
        Destroy(canvasGO);
        canvasGO = null;
        if (LevelManager.Instance != null)
            LevelManager.Instance.Load(LevelManager.Instance.startLevel);
        else
            Time.timeScale = 1f;
    }

    Image NewImage(string name, Transform parent, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = c;
        return img;
    }

    void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    void MakeText(string name, Transform parent, string text, int size, Vector2 pos, Vector2 box, Color c)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = c;
        t.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = box;
        rt.anchoredPosition = pos;
    }
}