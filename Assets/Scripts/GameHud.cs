using System.Collections.Generic;
using UnityEngine;

// HUD trong trận, vẽ bằng IMGUI nhưng dùng sprite của Kenney "UI Pack: Sci-Fi" (CC0)
// trong Resources/UI/Kenney: khung kính 9-slice, thanh máu/gas/kiếm bóng, tâm ngắm.
// Bảng màu theo menu: nền xanh đen, chữ ngà, nhấn đồng.
public class GameHud : MonoBehaviour
{
    static readonly Color Ivory = new Color(0.91f, 0.878f, 0.796f, 1f);
    static readonly Color Brass = new Color(0.753f, 0.541f, 0.243f, 1f);
    static readonly Color Muted = new Color(0.659f, 0.667f, 0.624f, 1f);
    static readonly Color Plate = new Color(0.03f, 0.05f, 0.058f, 0.82f);

    GameManager gm;
    ODMController odm;
    PlayerCombat combat;
    Health health;
    Camera cam;
    TimeFocus focus;

    string toast = "";
    float toastTimer;
    float shownGas = 1f, shownHp = 1f, shownBlade = 1f;
    float hurtFlash;
    float lastHp = -1f;

    Texture2D panel, panelNotch, panelRect, track, crosshair;
    Texture2D barGreen, barYellow, barRed, barBlue;
    Font numFont;
    GUIStyle plateStyle, notchStyle, rectStyle, trackStyle;
    GUIStyle barGreenStyle, barYellowStyle, barRedStyle, barBlueStyle;
    GUIStyle text, num, small, centered;
    float styleScale = -1f;

    public static GameHud Create(GameManager manager, ODMController player, PlayerCombat pc, Health hp, Camera camera)
    {
        var go = new GameObject("GameHud");
        var h = go.AddComponent<GameHud>();
        h.gm = manager; h.odm = player; h.combat = pc; h.health = hp; h.cam = camera;
        h.focus = player.GetComponent<TimeFocus>();
        return h;
    }

    public void Toast(string m)
    {
        toast = m;
        toastTimer = 3f;
    }

    void Awake()
    {
        panel = Load("panel_glass");
        panelNotch = Load("panel_notches");
        panelRect = Load("panel_rect");
        track = Load("bar_track");
        crosshair = Load("crosshair");
        barGreen = Load("bar_green");
        barYellow = Load("bar_yellow");
        barRed = Load("bar_red");
        barBlue = Load("bar_blue");
        numFont = Resources.Load<Font>("UI/Kenney/KenneyFutureNarrow");
    }

    static Texture2D Load(string n) { return Resources.Load<Texture2D>("UI/Kenney/" + n); }

    void Update()
    {
        toastTimer -= Time.unscaledDeltaTime;
        float dt = Time.unscaledDeltaTime;
        if (odm != null) shownGas = Mathf.MoveTowards(shownGas, odm.GasNormalized, 2.5f * dt);
        if (health != null)
        {
            shownHp = Mathf.MoveTowards(shownHp, health.Normalized, 2f * dt);
            if (lastHp >= 0f && health.Current < lastHp - 0.01f) hurtFlash = 1f;
            lastHp = health.Current;
        }
        if (combat != null) shownBlade = Mathf.MoveTowards(shownBlade, combat.Durability / 100f, 2f * dt);
        hurtFlash = Mathf.MoveTowards(hurtFlash, 0f, 2.2f * dt);
    }

    // ---------- kiểu chữ / khung ----------
    static GUIStyle Slice(Texture2D t, int border)
    {
        var st = new GUIStyle();
        st.normal.background = t;
        st.border = new RectOffset(border, border, border, border);
        return st;
    }

    void EnsureStyles(float s)
    {
        if (Mathf.Approximately(styleScale, s) && text != null) return;
        styleScale = s;

        plateStyle = Slice(panel, 14);
        notchStyle = Slice(panelNotch, 24);
        rectStyle = Slice(panelRect, 12);
        trackStyle = Slice(track, 8);
        barGreenStyle = Slice(barGreen, 8);
        barYellowStyle = Slice(barYellow, 8);
        barRedStyle = Slice(barRed, 8);
        barBlueStyle = Slice(barBlue, 8);

        text = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * s), alignment = TextAnchor.MiddleLeft, richText = true };
        text.normal.textColor = Ivory;
        small = new GUIStyle(text) { fontSize = Mathf.RoundToInt(18 * s) };
        small.normal.textColor = Muted;
        centered = new GUIStyle(text) { alignment = TextAnchor.MiddleCenter };
        num = new GUIStyle(text) { alignment = TextAnchor.MiddleRight };
    }

    static void Tinted(GUIStyle st, Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        if (st.normal.background != null) st.Draw(r, GUIContent.none, false, false, false, false);
        else GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    void Text(Rect r, string t, GUIStyle st, Color c)
    {
        Color old = st.normal.textColor;
        // bóng chữ để dễ đọc trên nền sáng
        st.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), t, st);
        st.normal.textColor = c;
        GUI.Label(r, t, st);
        st.normal.textColor = old;
    }

    // Thanh có đầu bo: nền rãnh tối + phần đầy theo tỉ lệ
    void Bar(Rect r, float v, GUIStyle fillStyle)
    {
        Tinted(trackStyle, r, Color.white);
        v = Mathf.Clamp01(v);
        float minW = 16f * styleScale;
        if (v > 0.001f)
        {
            float w = Mathf.Max(minW, r.width * v);
            Tinted(fillStyle, new Rect(r.x, r.y, Mathf.Min(w, r.width), r.height), Color.white);
        }
    }

    GUIStyle FillFor(float v)
    {
        if (v >= 0.5f) return barGreenStyle;
        if (v >= 0.2f) return barYellowStyle;
        return barRedStyle;
    }

    void OnGUI()
    {
        if (odm == null || gm == null || gm.Current != GameManager.State.Playing) return;
        if (Event.current.type != EventType.Repaint) return; // chỉ vẽ ở sự kiện Repaint (GUIStyle.Draw không chạy ở Layout)
        float s = Screen.height / 1080f;
        EnsureStyles(s);

        if (hurtFlash > 0.01f) DrawHurt();
        DrawVitals(s);
        DrawSpeed(s);
        DrawObjective(s);
        DrawNapeMarks(s);
        DrawMissionMarks(s);
        DrawToast(s);
        DrawHint(s);
    }

    void DrawHurt()
    {
        Color old = GUI.color;
        float a = hurtFlash * 0.35f;
        float t = 90f;
        GUI.color = new Color(0.8f, 0.05f, 0.05f, a);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, Screen.height - t, Screen.width, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0, 0, t, Screen.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(Screen.width - t, 0, t, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    // ----- góc dưới trái: máu, gas, kiếm -----
    void DrawVitals(float s)
    {
        float w = 560 * s, h = 214 * s;
        var box = new Rect(32 * s, Screen.height - h - 32 * s, w, h);
        Tinted(plateStyle, box, Plate);
        // vạch nhấn đồng ở cạnh trái khung
        GUI.color = Brass;
        GUI.DrawTexture(new Rect(box.x + 8 * s, box.y + 14 * s, 4 * s, box.height - 28 * s), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float x = box.x + 30 * s, rowH = 54 * s, y = box.y + 18 * s;
        float labelW = 96 * s, barW = w - labelW - 150 * s;

        Row("HP", shownHp, health != null ? Mathf.CeilToInt(health.Current).ToString() : "", barRedStyle, x, y, labelW, barW, rowH, s);
        float g = odm.GasNormalized;
        Row("GAS", shownGas, Mathf.RoundToInt(g * 100f) + "%", FillFor(shownGas), x, y + rowH, labelW, barW, rowH, s);
        string blades = combat.IsReloading ? "..." : Mathf.RoundToInt(combat.Durability) + "%";
        Row("KIẾM", shownBlade, blades, barBlueStyle, x, y + 2 * rowH, labelW, barW, rowH, s);

        Text(new Rect(x, y + 3 * rowH - 6 * s, w - 60 * s, 30 * s), "Dự phòng: " + combat.Spare + (combat.IsReloading ? "   ·   đang thay kiếm…" : ""), small, Muted);

        // cảnh báo gas thấp nhấp nháy
        if (g < 0.2f && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.4f)
            Text(new Rect(box.x, box.y - 40 * s, box.width, 34 * s), "GAS SẮP CẠN", centered, new Color(1f, 0.35f, 0.25f));
    }

    void Row(string label, float v, string value, GUIStyle fill, float x, float y, float labelW, float barW, float rowH, float s)
    {
        var lbl = new GUIStyle(text);
        if (numFont != null && label != "KIẾM") lbl.font = numFont;
        Text(new Rect(x, y, labelW, rowH * 0.9f), label, lbl, Brass);
        Bar(new Rect(x + labelW, y + 7 * s, barW, rowH * 0.62f), v, fill);
        Text(new Rect(x + labelW + barW + 6 * s, y, 100 * s, rowH * 0.9f), value, num, Ivory);
    }

    // ----- góc dưới phải: tốc độ -----
    void DrawSpeed(float s)
    {
        float w = 250 * s, h = 112 * s;
        var box = new Rect(Screen.width - w - 32 * s, Screen.height - h - 32 * s, w, h);
        Tinted(plateStyle, box, Plate);
        var big = new GUIStyle(num) { fontSize = Mathf.RoundToInt(64 * s), alignment = TextAnchor.MiddleCenter };
        if (numFont != null) big.font = numFont;
        Text(new Rect(box.x, box.y + 4 * s, box.width, box.height * 0.7f), odm.Speed.ToString("F0"), big, Ivory);
        var unit = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
        if (numFont != null) unit.font = numFont;
        Text(new Rect(box.x, box.y + box.height - 36 * s, box.width, 30 * s), "M/S", unit, Brass);
    }

    // ----- giữa trên: mục tiêu -----
    void DrawObjective(float s)
    {
        string obj = gm.Mission != null ? gm.Mission.ObjectiveText
                                        : "Đợt " + gm.Wave + "/" + gm.TotalWaves + "  -  hạ gục hết titan";
        float w = 980 * s, h = 82 * s;
        var box = new Rect((Screen.width - w) / 2f, 22 * s, w, h);
        Tinted(notchStyle, box, Plate);
        GUI.color = Brass;
        GUI.DrawTexture(new Rect(box.x + 22 * s, box.yMax - 14 * s, box.width - 44 * s, 2 * s), Texture2D.whiteTexture);
        GUI.color = Color.white;
        Text(new Rect(box.x, box.y + 6 * s, box.width, 46 * s), obj, centered, Ivory);

        string sub = "TITAN CÒN  <color=#C08A3E>" + Titan.All.Count + "</color>";
        if (focus != null && focus.Active) sub += "      <color=#C08A3E>TẬP TRUNG · THỜI GIAN CHẬM</color>";
        var subStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
        Text(new Rect(box.x, box.y + 44 * s, box.width, 28 * s), sub, subStyle, Muted);
    }

    // ----- chấm đánh dấu gáy titan -----
    void DrawNapeMarks(float s)
    {
        if (cam == null) return;
        foreach (Titan t in Titan.All)
        {
            if (t == null || t.IsDead || t.Nape == null) continue;
            Vector3 sp = cam.WorldToScreenPoint(t.Nape.position);
            if (sp.z <= 0f || sp.z > 140f) continue;
            bool inRange = Vector3.Distance(cam.transform.position, t.Nape.position) <= combat.napeReach;
            float size = (inRange ? 56f : 30f) * s;
            Color c = inRange ? new Color(1f, 0.86f, 0.25f, 1f) : new Color(1f, 0.3f, 0.2f, 0.75f);
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(sp.x - size / 2f, Screen.height - sp.y - size / 2f, size, size), crosshair != null ? crosshair : Texture2D.whiteTexture);
            GUI.color = old;
        }
    }

    // ----- điểm tiếp tế / cổng -----
    void DrawMissionMarks(float s)
    {
        if (cam == null || gm.Mission == null) return;
        foreach (var mk in gm.Mission.Markers)
        {
            float dist = Vector3.Distance(cam.transform.position, mk.pos);
            string txt = mk.label + "  " + Mathf.RoundToInt(dist) + " m";
            Vector3 sp = cam.WorldToScreenPoint(mk.pos + Vector3.up * 8f);
            float bw = 300 * s, bh = 40 * s;
            Rect r;
            if (sp.z > 0f)
            {
                float x = Mathf.Clamp(sp.x, bw / 2f + 20 * s, Screen.width - bw / 2f - 20 * s);
                float y = Mathf.Clamp(Screen.height - sp.y, 130 * s, Screen.height - 280 * s);
                r = new Rect(x - bw / 2f, y - bh, bw, bh);
                Color old = GUI.color;
                GUI.color = mk.color;
                GUI.DrawTexture(new Rect(x - 7 * s, y + 4 * s, 14 * s, 14 * s), Texture2D.whiteTexture);
                GUI.color = old;
            }
            else
            {
                bool right = cam.transform.InverseTransformPoint(mk.pos).x >= 0f;
                txt = right ? txt + "  ›››" : "‹‹‹  " + txt;
                r = new Rect(right ? Screen.width - bw - 24 * s : 24 * s, Screen.height * 0.5f, bw, bh);
            }
            Tinted(rectStyle, r, new Color(0.03f, 0.05f, 0.058f, 0.75f));
            var st = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            Text(r, txt, st, mk.color);
        }
    }

    void DrawToast(float s)
    {
        if (toastTimer <= 0f || string.IsNullOrEmpty(toast)) return;
        float a = Mathf.Clamp01(toastTimer);
        float w = 900 * s, h = 54 * s;
        var r = new Rect((Screen.width - w) / 2f, 128 * s, w, h);
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, a);
        Tinted(rectStyle, r, new Color(0.03f, 0.05f, 0.058f, 0.85f * a));
        Text(r, toast, centered, new Color(Ivory.r, Ivory.g, Ivory.b, a));
        GUI.color = old;
    }

    void DrawHint(float s)
    {
        string h = gm.Hint;
        if (string.IsNullOrEmpty(h)) return;
        var st = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(16 * s) };
        Text(new Rect(Screen.width / 2f - 700 * s, Screen.height - 40 * s, 1400 * s, 30 * s), h, st, new Color(Muted.r, Muted.g, Muted.b, 0.75f));
    }
}
