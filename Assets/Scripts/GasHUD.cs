using UnityEngine;
using UnityEngine.UI;

// HUD gas + tốc độ. Tự tạo UI lúc chạy game, KHÔNG cần sửa scene:
// chỉ cần trong scene có ODMController là thanh gas tự xuất hiện.
public class GasHUD : MonoBehaviour
{
    ODMController player;
    Image fill;
    Text label;
    Text speedText;

    static readonly Color Full = new Color(0.3f, 0.9f, 1f, 0.95f);
    static readonly Color Low = new Color(1f, 0.35f, 0.2f, 0.95f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindFirstObjectByType<GasHUD>() != null) return;
        if (FindFirstObjectByType<ODMController>() == null) return;
        new GameObject("GasHUD").AddComponent<GasHUD>();
    }

    void Start()
    {
        player = FindFirstObjectByType<ODMController>();

        var canvasObj = new GameObject("GasHUD_Canvas");
        canvasObj.transform.SetParent(transform);
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Khung nền thanh gas (góc dưới trái)
        var bg = MakeImage("GasBG", canvasObj.transform, new Color(0, 0, 0, 0.5f));
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0, 0);
        bgRt.anchoredPosition = new Vector2(40, 40);
        bgRt.sizeDelta = new Vector2(360, 24);

        fill = MakeImage("GasFill", bg.transform, Full);
        var fRt = fill.rectTransform;
        fRt.anchorMin = new Vector2(0, 0); fRt.anchorMax = new Vector2(1, 1);
        fRt.offsetMin = new Vector2(3, 3); fRt.offsetMax = new Vector2(-3, -3);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;

        label = MakeText("GasLabel", bg.transform, 20, TextAnchor.LowerLeft);
        label.rectTransform.anchoredPosition = new Vector2(0, 30);
        label.rectTransform.sizeDelta = new Vector2(360, 28);

        speedText = MakeText("Speed", canvasObj.transform, 26, TextAnchor.LowerRight);
        var sRt = speedText.rectTransform;
        sRt.anchorMin = sRt.anchorMax = sRt.pivot = new Vector2(1, 0);
        sRt.anchoredPosition = new Vector2(-40, 40);
        sRt.sizeDelta = new Vector2(300, 36);
    }

    void Update()
    {
        if (player == null) return;
        float g = player.GasNormalized;
        fill.fillAmount = g;
        fill.color = Color.Lerp(Low, Full, Mathf.InverseLerp(0.1f, 0.4f, g));
        label.text = "GAS " + Mathf.RoundToInt(g * 100f) + "%";
        speedText.text = player.Speed.ToString("F1") + " m/s";
    }

    static Image MakeImage(string n, Transform parent, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = c; img.raycastTarget = false;
        return img;
    }

    static Text MakeText(string n, Transform parent, int size, TextAnchor anchor)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size; t.alignment = anchor; t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }
}
