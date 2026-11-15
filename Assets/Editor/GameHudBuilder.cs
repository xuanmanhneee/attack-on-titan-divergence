using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dựng prefab HUD trong trận: Assets/Resources/UI/GameHud.prefab (menu Attack on Titan > Build Game HUD Prefab).
/// Phong cách theo menu chính: nền xanh đen, chữ ngà, nhấn đồng, khung viền mảnh.
/// Ba canvas tách lớp: FX (10) &lt; Markers (15) &lt; HUD (20); menu pause/kết quả (500) luôn nằm trên.
/// </summary>
public static class GameHudBuilder
{
    const string PrefabPath = "Assets/Resources/UI/GameHud.prefab";

    static readonly Color Ivory = new Color(0.91f, 0.878f, 0.796f, 1f);
    static readonly Color Brass = new Color(0.753f, 0.541f, 0.243f, 1f);
    static readonly Color Muted = new Color(0.659f, 0.667f, 0.624f, 1f);
    static readonly Color Plate = new Color(0.035f, 0.055f, 0.062f, 0.88f);
    static readonly Color Track = new Color(0.13f, 0.155f, 0.16f, 1f);
    static readonly Color Danger = new Color(0.86f, 0.27f, 0.2f, 1f);
    static readonly Color Good = new Color(0.36f, 0.72f, 0.58f, 1f);

    static Font font;

    [MenuItem("Attack on Titan/Build Game HUD Prefab")]
    public static void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var root = new GameObject("GameHud", typeof(RectTransform));
        var hud = root.AddComponent<GameHud>();

        var hudLayer = Layer(root.transform, "HUD Layer", 20);
        var markerLayer = Layer(root.transform, "Marker Layer", 15);
        var fxLayer = Layer(root.transform, "FX Layer", 10);

        // ---------- FX: viền đỏ khi trúng đòn ----------
        var hurt = Rect("Hurt", fxLayer);
        Stretch(hurt);
        var hurtGroup = hurt.gameObject.AddComponent<CanvasGroup>();
        hurtGroup.alpha = 0f; hurtGroup.blocksRaycasts = false; hurtGroup.interactable = false;
        var red = new Color(0.8f, 0.05f, 0.05f, 0.55f);
        var clear = new Color(0.8f, 0.05f, 0.05f, 0f);
        Edge(hurt, "Left", 0f, 0f, 0f, 1f, 0f, 0f, 300f, 0f, red, clear, false);
        Edge(hurt, "Right", 1f, 0f, 1f, 1f, -300f, 0f, 0f, 0f, clear, red, false);
        Edge(hurt, "Bottom", 0f, 0f, 1f, 0f, 0f, 0f, 0f, 200f, red, clear, true);
        Edge(hurt, "Top", 0f, 1f, 1f, 1f, 0f, -200f, 0f, 0f, clear, red, true);

        // ---------- HUD: góc dưới trái (máu, gas, thể lực) ----------
        var vitals = Panel(hudLayer, "Vitals", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(540f, 206f));
        var hp = BarRow(vitals, "HP", 0, Danger);
        var gas = BarRow(vitals, "GAS", 1, Good);
        var stamina = BarRow(vitals, "STAMINA", 2, Ivory);
        var status = Label(vitals, "Status", "", 15, Muted, TextAnchor.MiddleLeft);
        SetRect(status.rectTransform, 0f, 0f, 1f, 0f, 30f, 12f, -20f, 40f);
        var lowGas = Label(hudLayer, "Low Gas", "GAS SẮP CẠN", 22, Danger, TextAnchor.MiddleCenter);
        SetAnchored(lowGas.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 252f), new Vector2(540f, 34f));
        lowGas.enabled = false;

        // ---------- HUD: góc dưới phải (tốc độ) ----------
        var speed = Panel(hudLayer, "Speed", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(210f, 100f));
        var speedValue = Label(speed, "Value", "0", 54, Ivory, TextAnchor.MiddleCenter);
        SetRect(speedValue.rectTransform, 0f, 0f, 1f, 1f, 0f, 26f, 0f, -4f);
        var speedUnit = Label(speed, "Unit", "M/S", 14, Brass, TextAnchor.MiddleCenter);
        SetRect(speedUnit.rectTransform, 0f, 0f, 1f, 0f, 0f, 6f, 0f, 30f);

        // ---------- HUD: giữa trên (mục tiêu) ----------
        var objective = Panel(hudLayer, "Objective", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(920f, 92f));
        var objText = Label(objective, "Text", "", 24, Ivory, TextAnchor.MiddleCenter);
        SetRect(objText.rectTransform, 0f, 0f, 1f, 1f, 24f, 34f, -24f, -8f);
        var subText = Label(objective, "Sub", "", 15, Muted, TextAnchor.MiddleCenter);
        SetRect(subText.rectTransform, 0f, 0f, 1f, 0f, 24f, 10f, -24f, 34f);
        subText.supportRichText = true;
        var underline = Img("Underline", objective, new Color(Brass.r, Brass.g, Brass.b, 0.8f));
        SetRect(underline.rectTransform, 0f, 0f, 1f, 0f, 24f, 0f, -24f, 2f);

        // Thông báo
        var toast = Panel(hudLayer, "Toast", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(820f, 52f));
        var toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        var toastText = Label(toast, "Text", "", 22, Ivory, TextAnchor.MiddleCenter);
        SetRect(toastText.rectTransform, 0f, 0f, 1f, 1f, 24f, 0f, -24f, 0f);

        // Gợi ý phím
        var hint = Label(hudLayer, "Hint", "", 15, new Color(Muted.r, Muted.g, Muted.b, 0.8f), TextAnchor.MiddleCenter);
        SetAnchored(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(1400f, 28f));

        // ---------- Markers: dấu gáy, máu titan, điểm nhiệm vụ ----------
        var markers = Rect("Markers", markerLayer);
        Stretch(markers);

        var nape = Rect("Nape Template", markers);
        SetAnchored(nape, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
        Bracket(nape, 0f, 0f, 1f, 1f); Bracket(nape, 1f, 0f, -1f, 1f);
        Bracket(nape, 0f, 1f, 1f, -1f); Bracket(nape, 1f, 1f, -1f, -1f);
        nape.gameObject.SetActive(false);

        var bar = Img("Titan Bar Template", markers, Plate);
        SetAnchored(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 14f));
        Frame(bar.gameObject, 0.5f);
        var barTrack = Img("Track", bar.transform, Track);
        SetRect(barTrack.rectTransform, 0f, 0f, 1f, 1f, 2f, 2f, -2f, -2f);
        var barFill = Img("Fill", bar.transform, Danger);
        SetRect(barFill.rectTransform, 0f, 0f, 1f, 1f, 2f, 2f, -2f, -2f);
        bar.gameObject.SetActive(false);

        var mission = Rect("Mission Template", markers);
        SetAnchored(mission, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
        var plate = Img("Plate", mission, Plate);
        SetAnchored(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 34f), new Vector2(380f, 40f));
        Frame(plate.gameObject, 0.5f);
        var label = Label(plate.transform, "Label", "", 17, Ivory, TextAnchor.MiddleCenter);
        Stretch(label.rectTransform);
        var icon = Img("Icon", mission, Ivory);
        SetAnchored(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
        icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        mission.gameObject.SetActive(false);

        // ---------- Nối tham chiếu cho GameHud ----------
        var so = new SerializedObject(hud);
        var layers = so.FindProperty("layers");
        layers.arraySize = 3;
        layers.GetArrayElementAtIndex(0).objectReferenceValue = hudLayer.gameObject;
        layers.GetArrayElementAtIndex(1).objectReferenceValue = markerLayer.gameObject;
        layers.GetArrayElementAtIndex(2).objectReferenceValue = fxLayer.gameObject;
        Set(so, "hpFill", hp.fill); Set(so, "hpValue", hp.value);
        Set(so, "gasFill", gas.fill); Set(so, "gasFillImage", gas.fill.GetComponent<Image>()); Set(so, "gasValue", gas.value);
        Set(so, "staminaFill", stamina.fill); Set(so, "staminaFillImage", stamina.fill.GetComponent<Image>()); Set(so, "staminaValue", stamina.value);
        Set(so, "statusText", status); Set(so, "lowGasText", lowGas);
        Set(so, "speedValue", speedValue);
        Set(so, "objectiveText", objText); Set(so, "subText", subText);
        Set(so, "toastGroup", toastGroup); Set(so, "toastText", toastText);
        Set(so, "hintText", hint);
        Set(so, "hurtGroup", hurtGroup);
        Set(so, "napeTemplate", nape); Set(so, "titanBarTemplate", bar.rectTransform); Set(so, "missionTemplate", mission);
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[GameHud] Prefab saved to " + PrefabPath);
    }

    static void Set(SerializedObject so, string name, Object value)
    {
        var p = so.FindProperty(name);
        if (p == null) { Debug.LogError("[GameHud] Missing field " + name); return; }
        p.objectReferenceValue = value;
    }

    // ---------- Thành phần ----------
    struct BarParts { public RectTransform fill; public Text value; }

    static BarParts BarRow(Transform parent, string label, int index, Color fillColor)
    {
        float top = 20f + index * 50f;
        var name = Label(parent, label + " Label", label, 17, Brass, TextAnchor.MiddleLeft);
        SetRect(name.rectTransform, 0f, 1f, 0f, 1f, 30f, -top - 36f, 150f, -top);
        var track = Img(label + " Track", parent, Track);
        SetRect(track.rectTransform, 0f, 1f, 1f, 1f, 160f, -top - 25f, -96f, -top - 11f);
        var fill = Img("Fill", track.transform, fillColor);
        Stretch(fill.rectTransform);
        var value = Label(parent, label + " Value", "", 19, Ivory, TextAnchor.MiddleRight);
        SetRect(value.rectTransform, 1f, 1f, 1f, 1f, -90f, -top - 36f, -20f, -top);
        return new BarParts { fill = fill.rectTransform, value = value };
    }

    // Khung nền + thanh nhấn đồng bên trái + viền mảnh, giống nút menu chính
    static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var img = Img(name, parent, Plate);
        SetAnchored(img.rectTransform, anchor, pivot, pos, size);
        Frame(img.gameObject, 0.28f);
        var accent = Img("Accent", img.transform, Brass);
        SetRect(accent.rectTransform, 0f, 0f, 0f, 1f, 0f, 0f, 4f, 0f);
        return img.rectTransform;
    }

    static void Frame(GameObject go, float alpha)
    {
        var o = go.AddComponent<Outline>();
        o.effectColor = new Color(Brass.r, Brass.g, Brass.b, alpha);
        o.effectDistance = new Vector2(1.5f, -1.5f);
        o.useGraphicAlpha = false;
    }

    static void Bracket(Transform parent, float ax, float ay, float dx, float dy)
    {
        const float len = 14f, thick = 3f;
        var h = Img("Bracket H", parent, Ivory);
        SetRect(h.rectTransform, ax, ay, ax, ay, dx > 0 ? 0f : -len, dy > 0 ? 0f : -thick, dx > 0 ? len : 0f, dy > 0 ? thick : 0f);
        var v = Img("Bracket V", parent, Ivory);
        SetRect(v.rectTransform, ax, ay, ax, ay, dx > 0 ? 0f : -thick, dy > 0 ? 0f : -len, dx > 0 ? thick : 0f, dy > 0 ? len : 0f);
    }

    static void Edge(Transform parent, string name, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top, Color a, Color b, bool vertical)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalGradientGraphic));
        go.transform.SetParent(parent, false);
        var g = go.GetComponent<HorizontalGradientGraphic>();
        g.LeftColor = a; g.RightColor = b; g.Vertical = vertical;
        g.raycastTarget = false;
        SetRect(g.rectTransform, minX, minY, maxX, maxY, left, bottom, right, top);
    }

    static Transform Layer(Transform parent, string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // khớp theo chiều cao: GameHud quy đổi pixel -> canvas theo 1080 / Screen.height
        return go.transform;
    }

    // ---------- Tiện ích dựng UI ----------
    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image Img(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    static Text Label(Transform parent, string name, string value, int size, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.text = value; t.fontSize = size; t.color = color; t.alignment = anchor;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        return t;
    }

    static void Stretch(RectTransform r) { SetRect(r, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f); }

    // anchor + offset: (left, bottom) = offsetMin, (right, top) = offsetMax
    static void SetRect(RectTransform r, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
    {
        r.anchorMin = new Vector2(minX, minY);
        r.anchorMax = new Vector2(maxX, maxY);
        r.offsetMin = new Vector2(left, bottom);
        r.offsetMax = new Vector2(right, top);
    }

    static void SetAnchored(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }
}
