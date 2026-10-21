using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// HUD trong trận theo phong cách manga (thay HUD IMGUI cũ và GasHUD):
//  - Máu, gas, độ bền kiếm (thanh trong khung truyện), tốc độ, số titan còn lại
//  - Băng nhiệm vụ ở giữa phía trên, thông báo dạng khung đỏ, chữ "HẠ GỤC!" khi diệt titan
//  - Đường tốc độ phủ viền màn hình khi bay nhanh, viền đỏ khi trúng đòn / máu thấp
//  - Chấm đánh dấu gáy titan, đánh dấu điểm tiếp tế / cổng
// Chỉ hiện khi GameManager ở trạng thái Playing. Tự dựng bằng code, không cần sửa scene.
public class MangaHud : MonoBehaviour
{
    static readonly Color Ink = MangaStyle.Ink;
    static readonly Color Paper = MangaStyle.Paper;
    static readonly Color Red = MangaStyle.Red;
    const string RedHex = "#D91221";

    class Mark
    {
        public RectTransform rt;
        public RectTransform dot;
        public Image img;
        public Text txt;
    }

    GameManager gm;
    ODMController odm;
    PlayerCombat combat;
    Health hp;
    Camera cam;
    TimeFocus focus;

    Canvas canvas;
    RectTransform root;

    RectTransform hpFill, gasFill, bladeFill;
    Image hpImg, gasImg, bladeImg;
    Text hpVal, gasVal, bladeVal, titanTxt, objTxt, toastTxt, hintTxt, speedNum, killTag;
    MangaPanel killTagPanel;
    GameObject focusObj;
    CanvasGroup toastGroup;
    RectTransform toastRt;
    MangaSpeedLines lines;
    Image flashImg;
    RawImage vignetteImg;
    RectTransform burstRt;
    CanvasGroup burstGroup;
    Text burstTxt;

    readonly List<Mark> napes = new List<Mark>();
    readonly List<Mark> marks = new List<Mark>();

    float toastTimer, toastAge, flash, burstTimer;

    static readonly string[] KillWords = { "HẠ GỤC!", "TRÚNG GÁY!", "XUYÊN GÁY!", "MỘT CON NỮA!" };

    public static MangaHud Create(GameManager manager, ODMController player, PlayerCombat pc, Health health, Camera camera)
    {
        var go = new GameObject("MangaHud");
        var h = go.AddComponent<MangaHud>();
        h.Init(manager, player, pc, health, camera);
        return h;
    }

    // ================= API =================
    public void Toast(string message)
    {
        if (toastTxt == null) return;
        toastTxt.text = message;
        toastTimer = 3.2f;
        toastAge = 0f;
    }

    // ================= DỰNG =================
    void Init(GameManager manager, ODMController player, PlayerCombat pc, Health health, Camera camera)
    {
        gm = manager; odm = player; combat = pc; hp = health; cam = camera;
        focus = odm != null ? odm.GetComponent<TimeFocus>() : null;

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvas.enabled = false;

        root = (RectTransform)transform;

        // --- lớp nền: đường tốc độ, viền đỏ ---
        var linesGo = new GameObject("SpeedLines", typeof(RectTransform));
        linesGo.transform.SetParent(root, false);
        Stretch((RectTransform)linesGo.transform);
        lines = linesGo.AddComponent<MangaSpeedLines>();
        lines.count = 70;
        lines.inner = 0.46f;
        lines.fps = 12f;
        lines.raycastTarget = false;
        lines.color = new Color(Ink.r, Ink.g, Ink.b, 0f);
        linesGo.SetActive(false);

        var flashGo = new GameObject("Flash", typeof(RectTransform));
        flashGo.transform.SetParent(root, false);
        Stretch((RectTransform)flashGo.transform);
        flashImg = flashGo.AddComponent<Image>();
        flashImg.raycastTarget = false;
        flashImg.color = new Color(Red.r, Red.g, Red.b, 0f);

        var vigGo = new GameObject("Vignette", typeof(RectTransform));
        vigGo.transform.SetParent(root, false);
        Stretch((RectTransform)vigGo.transform);
        vignetteImg = vigGo.AddComponent<RawImage>();
        vignetteImg.texture = MangaTextures.Vignette();
        vignetteImg.raycastTarget = false;
        vignetteImg.color = new Color(1f, 1f, 1f, 0f);

        // --- góc trên trái: máu, gas, kiếm ---
        hpImg = BuildBar("HpBar", "HP", new Vector2(40f, -36f), 480f, 44f, Red, out hpFill, out hpVal);
        gasImg = BuildBar("GasBar", "GAS", new Vector2(40f, -92f), 480f, 44f, Ink, out gasFill, out gasVal);
        bladeImg = BuildBar("BladeBar", "KIẾM", new Vector2(40f, -148f), 480f, 40f, MangaStyle.Gray, out bladeFill, out bladeVal);

        var fp = MakePanel("FocusTag", new Vector2(0f, 1f), new Vector2(46f, -204f), new Vector2(230f, 40f), Ink, 14f, 0f, 6f);
        var ft = MakeText(fp.transform, "Txt", 24, Paper, TextAnchor.MiddleCenter);
        ft.text = "TẬP TRUNG";
        focusObj = fp.gameObject;
        focusObj.SetActive(false);

        // --- góc trên phải: titan ---
        var tp = MakePanel("TitanPanel", new Vector2(1f, 1f), new Vector2(-46f, -38f), new Vector2(290f, 62f), Ink, 20f, 0f, 7f);
        titanTxt = MakeText(tp.transform, "Txt", 40, Paper, TextAnchor.MiddleCenter);

        // --- giữa trên: nhiệm vụ + thông báo ---
        var op = MakePanel("Objective", new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(1060f, 62f), Paper, 0f, 5f, 8f);
        objTxt = MakeText(op.transform, "Txt", 30, Ink, TextAnchor.MiddleCenter);

        var toast = MakePanel("Toast", new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(1000f, 70f), Red, 24f, 5f, 8f);
        toastRt = toast.rectTransform;
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
        toastTxt = MakeText(toast.transform, "Txt", 36, Paper, TextAnchor.MiddleCenter);
        AddOutline(toastTxt, Ink, 2f);

        // --- góc dưới phải: tốc độ ---
        var sp = MakePanel("Speed", new Vector2(1f, 0f), new Vector2(-46f, 78f), new Vector2(300f, 104f), Ink, -22f, 0f, 8f);
        speedNum = MakeText(sp.transform, "Txt", 76, Paper, TextAnchor.MiddleCenter, FontStyle.Italic);

        killTagPanel = MakePanel("KillTag", new Vector2(1f, 0f), new Vector2(-46f, 198f), new Vector2(300f, 48f), Paper, 18f, 4f, 6f);
        killTag = MakeText(killTagPanel.transform, "Txt", 24, Ink, TextAnchor.MiddleCenter);

        // --- dưới giữa: gợi ý phím ---
        hintTxt = MakeText(root, "Hint", 21, Paper, TextAnchor.MiddleCenter);
        Place(hintTxt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1700f, 34f));
        AddOutline(hintTxt, Ink, 2f);

        // --- chữ nổ khi diệt titan (lớp trên cùng) ---
        var bg = new GameObject("KillBurst", typeof(RectTransform));
        bg.transform.SetParent(root, false);
        burstRt = (RectTransform)bg.transform;
        Place(burstRt, new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(580f, 330f));
        var burst = bg.AddComponent<MangaBurst>();
        burst.color = Red;
        burst.points = 14;
        burst.raycastTarget = false;
        burstGroup = bg.AddComponent<CanvasGroup>();
        burstGroup.alpha = 0f;
        burstGroup.blocksRaycasts = false;
        burstRt.localRotation = Quaternion.Euler(0f, 0f, -6f);
        burstTxt = MakeText(burstRt, "Txt", 92, Paper, TextAnchor.MiddleCenter, FontStyle.Italic);
        AddOutline(burstTxt, Ink, 4f);

        // --- sự kiện ---
        if (hp != null) hp.Damaged += OnHurt;
        Titan.Killed += OnKilled;
    }

    void OnDestroy()
    {
        if (hp != null) hp.Damaged -= OnHurt;
        Titan.Killed -= OnKilled;
    }

    void OnHurt(float amount)
    {
        flash = Mathf.Clamp01(0.55f + amount / 70f);
        GameAudio.Play2D(Sfx.PlayerHurt, 0.9f);
    }

    void OnKilled(Titan t)
    {
        if (gm == null || gm.Current != GameManager.State.Playing) return;
        burstTxt.text = KillWords[Random.Range(0, KillWords.Length)];
        burstTimer = 0.9f;
    }

    // ================= CẬP NHẬT =================
    void Update()
    {
        bool visible = gm != null && gm.Current == GameManager.State.Playing && odm != null && hp != null && combat != null;
        if (canvas.enabled != visible) canvas.enabled = visible;
        if (!visible) return;

        float dt = Time.unscaledDeltaTime;
        UpdateBars();
        UpdateTexts();
        UpdateLines();
        UpdateFlash(dt);
        UpdateToast(dt);
        UpdateBurst(dt);
        UpdateMarkers();
    }

    void UpdateBars()
    {
        hpFill.anchorMax = new Vector2(Mathf.Clamp01(hp.Normalized), 1f);
        hpVal.text = Mathf.CeilToInt(hp.Current) + " / " + Mathf.RoundToInt(hp.maxHealth);

        float g = odm.GasNormalized;
        gasFill.anchorMax = new Vector2(Mathf.Clamp01(g), 1f);
        gasVal.text = Mathf.RoundToInt(g * 100f) + "%";
        bool blink = g < 0.2f && Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f;
        gasImg.color = blink ? Red : Ink;

        float d = combat.Durability / 100f;
        bladeFill.anchorMax = new Vector2(Mathf.Clamp01(d), 1f);
        bladeVal.text = combat.IsReloading ? "ĐANG THAY..." : Mathf.RoundToInt(combat.Durability) + "%   x" + combat.Spare;
        bladeImg.color = d < 0.25f ? Red : MangaStyle.Gray;
    }

    void UpdateTexts()
    {
        speedNum.text = odm.Speed.ToString("F0") + "<size=34> m/s</size>";

        bool kill = combat.CanKillNow;
        killTagPanel.color = kill ? Red : Paper;
        killTag.color = kill ? Paper : Ink;
        killTag.text = kill ? "CHÉM GÁY ĐƯỢC!" : "CẦN " + combat.killSpeed.ToString("F0") + "+ m/s";

        titanTxt.text = "TITAN  <color=" + RedHex + ">" + Titan.All.Count + "</color>";

        string obj = gm.Mission != null ? gm.Mission.ObjectiveText
                                        : "Đợt " + gm.Wave + "/" + gm.TotalWaves + "  -  hạ gục hết titan";
        if (objTxt.text != obj) objTxt.text = obj;

        string hint = gm.Hint;
        if (hintTxt.text != hint) hintTxt.text = hint;

        bool f = focus != null && focus.Active;
        if (focusObj.activeSelf != f) focusObj.SetActive(f);
    }

    void UpdateLines()
    {
        float a = Mathf.SmoothStep(0f, 0.5f, Mathf.InverseLerp(15f, 34f, odm.Speed));
        bool on = a > 0.01f;
        if (lines.gameObject.activeSelf != on) lines.gameObject.SetActive(on);
        if (on) lines.color = new Color(Ink.r, Ink.g, Ink.b, a);
    }

    void UpdateFlash(float dt)
    {
        flash = Mathf.MoveTowards(flash, 0f, dt * 1.6f);
        float low = 0f;
        if (hp.Normalized < 0.3f)
            low = (0.3f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f)) * (1f - hp.Normalized / 0.3f);
        float k = Mathf.Max(flash, low);
        flashImg.color = new Color(Red.r, Red.g, Red.b, k * 0.32f);
        vignetteImg.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 1.5f));
    }

    void UpdateToast(float dt)
    {
        if (toastTimer > 0f)
        {
            toastTimer -= dt;
            toastAge += dt;
            toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.5f);
            float pop = Mathf.Clamp01(toastAge / 0.18f);
            float s = Mathf.Lerp(0.6f, 1f, 1f - (1f - pop) * (1f - pop));
            toastRt.localScale = new Vector3(s, s, 1f);
        }
        else toastGroup.alpha = 0f;
    }

    void UpdateBurst(float dt)
    {
        if (burstTimer > 0f)
        {
            burstTimer -= dt;
            float k = 1f - Mathf.Clamp01(burstTimer / 0.9f);
            burstGroup.alpha = Mathf.Clamp01(burstTimer / 0.25f);
            float s = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(k / 0.12f)) * (1f + k * 0.18f);
            burstRt.localScale = new Vector3(s, s, 1f);
        }
        else burstGroup.alpha = 0f;
    }

    // ---- chấm đánh dấu ----
    void UpdateMarkers()
    {
        float sc = canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;

        int ni = 0;
        if (cam != null)
        {
            foreach (Titan t in Titan.All)
            {
                if (t == null || t.IsDead || t.Nape == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(t.Nape.position);
                if (sp.z <= 0f || sp.z > 120f || sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) continue;

                bool inRange = Vector3.Distance(cam.transform.position, t.Nape.position) <= combat.napeReach;
                Mark m = GetMark(napes, ni++, false);
                m.rt.anchoredPosition = new Vector2(sp.x, sp.y) / sc;
                float size = inRange ? 30f : 15f;
                m.dot.sizeDelta = new Vector2(size, size);
                m.img.color = inRange ? (combat.CanKillNow ? Paper : Red) : new Color(Red.r, Red.g, Red.b, 0.8f);
            }
        }
        for (int i = ni; i < napes.Count; i++) napes[i].rt.gameObject.SetActive(false);

        int mi = 0;
        if (cam != null && gm.Mission != null)
        {
            foreach (var mk in gm.Mission.Markers)
            {
                Vector3 sp = cam.WorldToScreenPoint(mk.pos + Vector3.up * 8f);
                string txt = mk.label + "  " + Mathf.RoundToInt(Vector3.Distance(cam.transform.position, mk.pos)) + " m";
                Vector2 p;
                if (sp.z > 0f)
                {
                    p = new Vector2(Mathf.Clamp(sp.x, 130f, Screen.width - 130f), Mathf.Clamp(sp.y, 130f, Screen.height - 130f));
                }
                else
                {
                    bool right = cam.transform.InverseTransformPoint(mk.pos).x >= 0f;
                    p = new Vector2(right ? Screen.width - 190f : 190f, Screen.height * 0.5f);
                    txt = right ? txt + "  >>" : "<<  " + txt;
                }

                Mark m = GetMark(marks, mi++, true);
                m.rt.anchoredPosition = p / sc;
                m.dot.sizeDelta = new Vector2(20f, 20f);
                m.img.color = mk.color;
                m.txt.text = txt;
                m.txt.color = mk.color;
            }
        }
        for (int i = mi; i < marks.Count; i++) marks[i].rt.gameObject.SetActive(false);
    }

    Mark GetMark(List<Mark> list, int index, bool withText)
    {
        while (list.Count <= index) list.Add(NewMark(withText));
        Mark m = list[index];
        if (!m.rt.gameObject.activeSelf) m.rt.gameObject.SetActive(true);
        return m;
    }

    Mark NewMark(bool withText)
    {
        var go = new GameObject("Mark", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;

        var dotGo = new GameObject("Dot", typeof(RectTransform));
        var dot = (RectTransform)dotGo.transform;
        dot.SetParent(rt, false);
        dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
        dot.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var img = dotGo.AddComponent<Image>();
        img.raycastTarget = false;
        AddOutline(img, Ink, 3f);

        Text txt = null;
        if (withText)
        {
            txt = MakeText(rt, "Txt", 26, Paper, TextAnchor.MiddleCenter);
            var tr = txt.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(480f, 34f);
            tr.anchoredPosition = new Vector2(0f, 38f);
            AddOutline(txt, Ink, 2f);
        }
        return new Mark { rt = rt, dot = dot, img = img, txt = txt };
    }

    // ================= HÀM DỰNG UI =================
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void AddOutline(Graphic g, Color c, float dist)
    {
        var ol = g.gameObject.AddComponent<Outline>();
        ol.effectColor = c;
        ol.effectDistance = new Vector2(dist, -dist);
    }

    MangaPanel MakePanel(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill, float skew, float border, float shadow)
    {
        return MakePanel(root, name, anchor, pos, size, fill, skew, border, shadow);
    }

    static MangaPanel MakePanel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color fill, float skew, float border, float shadow)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var p = go.AddComponent<MangaPanel>();
        p.color = fill;
        p.skew = skew;
        p.border = border;
        p.shadow = shadow > 0f ? new Vector2(shadow, -shadow) : Vector2.zero;
        p.raycastTarget = false;
        Place((RectTransform)go.transform, anchor, pos, size);
        return p;
    }

    // Text kéo giãn kín phần tử cha
    static Text MakeText(Transform parent, string name, int size, Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        var t = go.AddComponent<Text>();
        t.font = MangaStyle.Font;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.raycastTarget = false;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // Thanh trong khung truyện: nhãn bên trái, phần tô co giãn theo anchorMax.x
    Image BuildBar(string name, string tag, Vector2 pos, float w, float h, Color fillColor, out RectTransform fill, out Text value)
    {
        var frame = MakePanel(name, new Vector2(0f, 1f), pos, new Vector2(w, h), Paper, 0f, 4f, 5f);

        var tagTxt = MakeText(frame.transform, "Tag", 24, Ink, TextAnchor.MiddleLeft);
        tagTxt.text = tag;
        tagTxt.rectTransform.offsetMin = new Vector2(14f, 0f);

        var trackGo = new GameObject("Track", typeof(RectTransform));
        var track = (RectTransform)trackGo.transform;
        track.SetParent(frame.transform, false);
        track.anchorMin = Vector2.zero;
        track.anchorMax = Vector2.one;
        track.offsetMin = new Vector2(92f, 8f);
        track.offsetMax = new Vector2(-8f, -8f);
        var bgImg = trackGo.AddComponent<Image>();
        bgImg.color = new Color(0.78f, 0.76f, 0.70f, 1f);
        bgImg.raycastTarget = false;

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fill = (RectTransform)fillGo.transform;
        fill.SetParent(track, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        var img = fillGo.AddComponent<Image>();
        img.color = fillColor;
        img.raycastTarget = false;

        value = MakeText(track, "Val", 20, Paper, TextAnchor.MiddleRight);
        value.rectTransform.offsetMax = new Vector2(-8f, 0f);
        AddOutline(value, Ink, 2f);
        return img;
    }
}
