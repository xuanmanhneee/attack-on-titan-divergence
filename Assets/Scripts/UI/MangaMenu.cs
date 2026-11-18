using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Toàn bộ màn hình giao diện kiểu manga (đen trắng + đỏ nhấn), dựng bằng code lúc chạy:
//   Briefing (màn hình chờ + giới thiệu nhiệm vụ) - Pause - Result (thắng/thua)
// Tham khảo bố cục: Persona 5 (khối nghiêng, đen/trắng/đỏ), Hades (menu dọc trượt vào), trang truyện in (screentone, đường tốc độ).
//
// Không dùng EventSystem: tự đọc chuột/bàn phím qua GameInput nên chạy được ở cả hai hệ input
// và khi Time.timeScale = 0 (mọi hiệu ứng dùng thời gian thật).
// Điều khiển: chuột (di chuột, bấm, kéo thanh trượt) hoặc Lên/Xuống + Trái/Phải + Enter, Esc = quay lại.
public class MangaMenu : MonoBehaviour
{
    public enum Page { None, Briefing, Pause, Result }

    public event Action Resume, Restart, MainMenu, Quit;

    public Page Current { get; private set; }
    public bool InSettings { get { return SettingsMenu.IsOpen; } }
    public bool BriefingConfirmed { get; private set; }
    public bool EscConsumedThisFrame { get { return SettingsMenu.EscConsumedThisFrame; } }

    // ---------------- Mục có thể chọn ----------------
    class MenuItem
    {
        public RectTransform rt;               // vùng nhận chuột (cả hàng)
        public Action<Vector2> onClick;        // tham số = vị trí chuột; NaN nếu bấm bằng bàn phím
        public Action<bool> onFocus;           // đổi hình ảnh khi được chọn / bỏ chọn
        public bool disabled;
    }

    RectTransform canvasRt;
    readonly Dictionary<Page, GameObject> pages = new Dictionary<Page, GameObject>();
    readonly Dictionary<Page, List<MenuItem>> pageItems = new Dictionary<Page, List<MenuItem>>();
    List<MenuItem> items = new List<MenuItem>();
    int sel;
    Vector2 lastMouse;
    int ignoreFrame = -1; // khung hình vừa đóng Cài đặt: bỏ qua input để cú bấm không lọt xuống nút phía dưới

    // Briefing
    Text briefStatus, briefTip, briefPrompt;
    RectTransform progressFillRt;
    float progress, progressTarget, tipTimer;
    int tipIndex;
    bool briefReady;

    // Pause / Result
    Text pauseKeys, resultTitle, resultStats;
    Outline resultStroke;
    MangaBurst resultBurst;

    static readonly Color Ink = MangaStyle.Ink;
    static readonly Color Paper = MangaStyle.Paper;
    static readonly Color Red = MangaStyle.Red;
    static readonly Color Gray = MangaStyle.Gray;

    const string RedHex = MangaStyle.AccentHex;

    public static MangaMenu Create()
    {
        var go = new GameObject("MangaMenu");
        return go.AddComponent<MangaMenu>();
    }

    // =====================================================================
    //  API cho GameManager
    // =====================================================================
    public void ShowBriefing()
    {
        BriefingConfirmed = false;
        briefReady = false;
        progress = 0f;
        progressTarget = 0.05f;
        tipTimer = 0f;
        tipIndex = UnityEngine.Random.Range(0, 100);
        briefPrompt.gameObject.SetActive(false);
        briefStatus.text = "";
        ShowTip();
        ShowPage(Page.Briefing);
    }

    public void SetProgress(float p, string status)
    {
        progressTarget = Mathf.Clamp01(p);
        briefStatus.text = status;
    }

    public void SetBriefingReady()
    {
        briefReady = true;
        progressTarget = 1f;
        briefStatus.text = "Sẵn sàng!";
    }

    float inputLockUntil;

    public void ShowPause()
    {
        RefreshPauseKeys();
        ShowPage(Page.Pause);
    }

    public void ShowResult(bool won, int kills, float seconds)
    {
        resultBurst.color = won ? Red : Ink;
        resultBurst.SetVerticesDirty();
        resultTitle.text = won ? "CHIẾN THẮNG!" : "BẠN ĐÃ GỤC";
        resultTitle.color = won ? Paper : Red;
        resultStroke.effectColor = won ? Ink : Paper;
        int s = Mathf.RoundToInt(seconds);
        resultStats.text = "Titan hạ gục:  <color=" + RedHex + ">" + kills + "</color>\n" +
                           "Thời gian:  <color=" + RedHex + ">" + (s / 60).ToString("00") + ":" + (s % 60).ToString("00") + "</color>";
        inputLockUntil = Time.unscaledTime + 0.7f; // chống bấm nhầm khi vừa chết/thắng
        ShowPage(Page.Result);
        MangaSfx.Slam();
    }

    public void Hide()
    {
        foreach (var kv in pages) kv.Value.SetActive(false);
        Current = Page.None;
        MangaSfx.Music(false);
    }

    // =====================================================================
    //  Khởi tạo
    // =====================================================================
    void Awake()
    {
        var cgo = new GameObject("MangaCanvas");
        cgo.transform.SetParent(transform, false);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasRt = (RectTransform)cgo.transform;

        BuildBriefing();
        BuildPause();
        BuildResult();

        GameSettings.Changed += OnSettingsChanged;
    }

    void OnDestroy()
    {
        GameSettings.Changed -= OnSettingsChanged;
        GameSettings.Save();
    }

    void OnSettingsChanged() { MangaSfx.RefreshVolume(); }

    // =====================================================================
    //  Điều khiển
    // =====================================================================
    void ShowPage(Page p)
    {
        foreach (var kv in pages) if (kv.Key != p) kv.Value.SetActive(false);
        pages[p].SetActive(false);
        pages[p].SetActive(true); // tắt rồi bật để chạy lại hiệu ứng trượt vào

        Current = p;
        items = pageItems[p];
        sel = FirstSelectable();
        lastMouse = GameInput.MousePosition;
        ApplyFocus();

        bool music = p == Page.Briefing;
        MangaSfx.Music(music);
    }

    int FirstSelectable()
    {
        for (int i = 0; i < items.Count; i++) if (!items[i].disabled) return i;
        return 0;
    }

    void ApplyFocus()
    {
        for (int i = 0; i < items.Count; i++)
            if (items[i].onFocus != null) items[i].onFocus(i == sel);
    }

    void SetSel(int i)
    {
        if (i == sel || i < 0 || i >= items.Count) return;
        sel = i;
        ApplyFocus();
        MangaSfx.Hover();
    }

    int HitTest(Vector2 mp)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            if (it.rt == null || it.disabled || !it.rt.gameObject.activeInHierarchy) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(it.rt, mp, null)) return i;
        }
        return -1;
    }

    void Activate(MenuItem it, Vector2 mp)
    {
        if (it == null || it.disabled) return;
        if (it.onClick != null)
        {
            MangaSfx.Click();
            it.onClick(mp);
        }
    }

    void Move(int dir)
    {
        if (items.Count == 0) return;
        int i = sel;
        for (int n = 0; n < items.Count; n++)
        {
            i = (i + dir + items.Count) % items.Count;
            if (!items[i].disabled) break;
        }
        SetSel(i);
    }

    void Update()
    {
        if (Current == Page.None || SettingsMenu.IsOpen || Time.frameCount <= ignoreFrame) return;
        float dt = Time.unscaledDeltaTime;
        if (Current == Page.Result && Time.unscaledTime < inputLockUntil) return;

        if (Current == Page.Briefing) { UpdateBriefing(dt); return; }

        // ----- chuột -----
        Vector2 mp = GameInput.MousePosition;
        bool moved = (mp - lastMouse).sqrMagnitude > 1f;
        lastMouse = mp;

        if (moved)
        {
            int h = HitTest(mp);
            if (h >= 0) SetSel(h);
        }
        if (GameInput.GetMouseButtonDown(0))
        {
            int h = HitTest(mp);
            if (h >= 0)
            {
                SetSel(h);
                Activate(items[h], mp);
            }
        }

        // ----- bàn phím -----
        if (GameInput.GetKeyDownRaw(KeyCode.UpArrow) || GameInput.GetKeyDownRaw(KeyCode.W)) Move(-1);
        if (GameInput.GetKeyDownRaw(KeyCode.DownArrow) || GameInput.GetKeyDownRaw(KeyCode.S)) Move(1);

        if (items.Count > 0 && sel < items.Count)
        {
            var cur = items[sel];
            if (GameInput.GetKeyDownRaw(KeyCode.Return) || GameInput.GetKeyDownRaw(KeyCode.Space))
                Activate(cur, new Vector2(float.NaN, float.NaN));
        }
    }

    void OpenSettings()
    {
        pages[Page.Pause].SetActive(false);
        SettingsMenu.Open(() =>
        {
            ignoreFrame = Time.frameCount;
            RefreshPauseKeys(); // phím vừa đổi phải hiện đúng
            ShowPage(Page.Pause);
        });
    }

    // =====================================================================
    //  Helper dựng UI
    // =====================================================================
    static RectTransform NewRT(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Image Fill(Transform parent, Color c)
    {
        var rt = Stretch(NewRT("Fill", parent));
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    static Image Bar(Transform parent, string name, Vector2 pos, Vector2 size, Color c)
    {
        var rt = Place(NewRT(name, parent), pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        return img;
    }

    static MangaPanel Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color fill, float skew, float border = 5f, float shadow = 10f)
    {
        var rt = Place(NewRT(name, parent), pos, size);
        var p = rt.gameObject.AddComponent<MangaPanel>();
        p.color = fill;
        p.skew = skew;
        p.border = border;
        p.shadow = shadow > 0f ? new Vector2(shadow, -shadow) : Vector2.zero;
        p.raycastTarget = false;
        return p;
    }

    static MangaBurst Burst(Transform parent, string name, Vector2 pos, Vector2 size, Color c, int points, float inner, int seed, float delay)
    {
        var rt = Place(NewRT(name, parent), pos, size);
        var b = rt.gameObject.AddComponent<MangaBurst>();
        b.color = c;
        b.points = points;
        b.innerRatio = inner;
        b.seed = seed;
        b.raycastTarget = false;
        rt.gameObject.AddComponent<MangaSlideIn>().Init(Vector2.zero, delay, 0f, 0.45f);
        return b;
    }

    static Text AddText(GameObject go, string s, int size, Color c, TextAnchor a, bool wrap, FontStyle style)
    {
        var t = go.AddComponent<Text>();
        t.font = MangaStyle.Font;
        t.text = s;
        t.fontSize = size;
        t.color = c;
        t.alignment = a;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    // Chữ phủ kín một RectTransform
    static Text Txt(Transform parent, string s, int size, Color c, TextAnchor a = TextAnchor.MiddleCenter, bool wrap = false, FontStyle style = FontStyle.Bold)
    {
        var rt = Stretch(NewRT("Text", parent));
        return AddText(rt.gameObject, s, size, c, a, wrap, style);
    }

    // Chữ trong một ô đặt ở vị trí cho trước
    static Text TextAt(Transform parent, string s, int size, Color c, TextAnchor a, Vector2 pos, Vector2 box, bool wrap = false, FontStyle style = FontStyle.Bold)
    {
        var rt = Place(NewRT("Text", parent), pos, box);
        return AddText(rt.gameObject, s, size, c, a, wrap, style);
    }

    static Outline Stroke(Text t, Color c, float d)
    {
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = c;
        o.effectDistance = new Vector2(d, -d);
        return o;
    }

    RectTransform NewPage(Page p)
    {
        var rt = Stretch(NewRT("Page_" + p, canvasRt));
        rt.gameObject.SetActive(false);
        pages[p] = rt.gameObject;
        pageItems[p] = new List<MenuItem>();
        return rt;
    }

    // Nền trang: giấy (hoặc nền tối mờ), screentone tối dần ra rìa, đường tốc độ
    static void Backdrop(Transform page, bool opaque, Color lines, float lineInner)
    {
        Fill(page, opaque ? Paper : new Color(0.015f, 0.025f, 0.03f, 0.92f));

    }

    // Nút kiểu codex: nền xanh đen, thanh nhấn đồng bên trái, mũi tên bên phải. Được chọn thì nền ấm lên.
    MenuItem MakeButton(Transform parent, string text, Vector2 pos, Vector2 size, float skew, Action click, float delay, int fontSize, Func<bool> active = null)
    {
        var root = Place(NewRT("Btn_" + text, parent), pos, size);
        root.gameObject.AddComponent<MangaSlideIn>().Init(new Vector2(220f, 0f), delay);

        var face = Stretch(NewRT("Face", root));
        var panel = face.gameObject.AddComponent<MangaPanel>();
        panel.color = new Color(0.035f, 0.055f, 0.062f, 0.88f);
        panel.border = 0f;
        panel.raycastTarget = false;
        Bar(face, "Accent", new Vector2(-size.x / 2f + 2.5f, 0f), new Vector2(5f, size.y), Red);
        var label = Txt(face, text, Mathf.Min(fontSize, Mathf.RoundToInt(size.y * 0.42f)), Ink, TextAnchor.MiddleLeft, false, FontStyle.Normal);
        label.rectTransform.offsetMin = new Vector2(34f, 0f);
        label.rectTransform.offsetMax = new Vector2(-50f, 0f);
        var arrow = Txt(face, "›", Mathf.RoundToInt(size.y * 0.5f), Red, TextAnchor.MiddleRight, false, FontStyle.Normal);
        arrow.rectTransform.offsetMax = new Vector2(-18f, 0f);

        var item = new MenuItem { rt = root };
        item.onClick = p => { if (click != null) click(); };
        item.onFocus = f =>
        {
            bool on = f || (active != null && active());
            panel.color = on ? MangaStyle.ButtonHover : new Color(0.035f, 0.055f, 0.062f, 0.88f);
            label.color = Ink;
            face.anchoredPosition = f ? new Vector2(-8f, 0f) : Vector2.zero;
        };
        item.onFocus(false);
        return item;
    }

    // =====================================================================
    //  BRIEFING (màn hình chờ + giới thiệu nhiệm vụ)
    // =====================================================================
    void BuildBriefing()
    {
        var page = NewPage(Page.Briefing);
        Backdrop(page, true, new Color(Ink.r, Ink.g, Ink.b, 0.3f), 0.42f);

        var head = Panel(page, "Head", new Vector2(-470f, 470f), new Vector2(840f, 100f), Ink, 40f, 0f, 10f);
        head.gameObject.AddComponent<MangaSlideIn>().Init(new Vector2(-1000f, 0f), 0.05f);
        Txt(head.transform, "CHƯƠNG 1 · TRẬN TROST", 54, Paper, TextAnchor.MiddleCenter, false, FontStyle.BoldAndItalic);

        // Khung truyện lớn: lời dẫn
        var story = Panel(page, "Story", new Vector2(-370f, 120f), new Vector2(1040f, 520f), Paper, 0f, 8f, 14f);
        story.gameObject.AddComponent<MangaSlideIn>().Init(new Vector2(0f, 800f), 0.15f);
        var inner = Stretch(NewRT("Inner", story.transform));
        inner.offsetMin = new Vector2(12f, 12f);
        inner.offsetMax = new Vector2(-12f, -12f);
        inner.gameObject.AddComponent<RectMask2D>();
        var lines = Stretch(NewRT("Lines", inner)).gameObject.AddComponent<MangaSpeedLines>();
        lines.color = new Color(Ink.r, Ink.g, Ink.b, 0.16f);
        lines.inner = 0.5f;
        lines.focus = new Vector2(0.3f, -0.15f);
        lines.raycastTarget = false;
        var tone = Place(NewRT("Tone", inner), new Vector2(380f, -200f), new Vector2(420f, 260f)).gameObject.AddComponent<RawImage>();
        tone.texture = MangaTextures.Dots(0.5f);
        tone.uvRect = new Rect(0f, 0f, 420f / 64f, 260f / 64f);
        tone.color = new Color(1f, 1f, 1f, 0.55f);
        tone.raycastTarget = false;

        var story1 = TextAt(inner, "Năm 850. Titan Siêu Đại xuất hiện trở lại và phá thủng cổng khu Trost.\n\n" +
                                   "Titan tràn vào phố. Quân đoàn được lệnh chặn chúng lại, bằng thiết bị cơ động ba chiều và hai lưỡi kiếm.\n\n" +
                                   "Bạn là một tân binh. Phía sau bạn là dân thường.",
                            34, Ink, TextAnchor.UpperLeft, Vector2.zero, new Vector2(930f, 450f), true);
        story1.lineSpacing = 1.1f;

        // Khung mục tiêu
        var obj = Panel(page, "Objectives", new Vector2(480f, 200f), new Vector2(680f, 360f), Paper, -14f, 8f, 14f);
        obj.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -1.5f);
        obj.gameObject.AddComponent<MangaSlideIn>().Init(new Vector2(900f, 0f), 0.25f);
        var tag1 = Panel(obj.transform, "Tag", new Vector2(-170f, 150f), new Vector2(250f, 60f), Red, 14f, 4f, 5f);
        Txt(tag1.transform, "MỤC TIÊU", 32, Paper);
        var objText = TextAt(obj.transform,
            "<color=" + RedHex + ">1</color>  Dọn sạch titan trong khu phố\n\n" +
            "<color=" + RedHex + ">2</color>  Hạ đợt titan tràn qua cổng\n\n" +
            "<color=" + RedHex + ">3</color>  Hoá Titan, bê đá tới lấp cổng",
            30, Ink, TextAnchor.UpperLeft, new Vector2(0f, -35f), new Vector2(600f, 250f), true);
        objText.lineSpacing = 1.0f;

        // Khung mẹo (nền mực, chữ trắng)
        var tip = Panel(page, "Tips", new Vector2(480f, -145f), new Vector2(680f, 250f), Ink, 14f, 0f, 12f);
        tip.gameObject.AddComponent<MangaSlideIn>().Init(new Vector2(900f, 0f), 0.35f);
        var tag2 = Panel(tip.transform, "Tag", new Vector2(-190f, 100f), new Vector2(170f, 56f), Red, 14f, 0f, 0f);
        Txt(tag2.transform, "MẸO", 30, Paper);
        briefTip = TextAt(tip.transform, "", 27, Paper, TextAnchor.UpperLeft, new Vector2(0f, -22f), new Vector2(590f, 150f), true, FontStyle.Normal);

        // Thanh tiến độ
        briefStatus = TextAt(page, "", 30, Ink, TextAnchor.MiddleLeft, new Vector2(-470f, -385f), new Vector2(900f, 44f));
        var bar = Panel(page, "Bar", new Vector2(0f, -435f), new Vector2(1500f, 46f), Paper, 24f, 5f, 8f);
        bar.gameObject.AddComponent<MangaSlideIn>().Init(new Vector2(0f, -200f), 0.4f);
        progressFillRt = NewRT("Fill", bar.transform);
        progressFillRt.anchorMin = new Vector2(0f, 0f);
        progressFillRt.anchorMax = new Vector2(0f, 1f);
        progressFillRt.pivot = new Vector2(0f, 0.5f);
        progressFillRt.anchoredPosition = new Vector2(8f, 0f);
        progressFillRt.sizeDelta = new Vector2(0f, -16f);
        var fill = progressFillRt.gameObject.AddComponent<MangaPanel>();
        fill.color = Red;
        fill.skew = 18f;
        fill.border = 0f;
        fill.shadow = Vector2.zero;
        fill.raycastTarget = false;

        briefPrompt = TextAt(page, "NHẤN PHÍM BẤT KỲ ĐỂ BẮT ĐẦU", 46, Red, TextAnchor.MiddleCenter, new Vector2(0f, -497f), new Vector2(1400f, 60f), false, FontStyle.BoldAndItalic);
        Stroke(briefPrompt, Paper, 3f);
        briefPrompt.gameObject.SetActive(false);
    }

    static string K(int action) { return GameSettings.KeyName(GameSettings.KeyOf(action)); }

    void ShowTip()
    {
        string[] tips =
        {
            "Giữ " + K(0) + " / " + K(1) + " để bắn móc trái / phải; thả phím để nhả dây. " + K(2) + " thu dây khi đang tiến về phía điểm neo.",
            "Chém gáy cần đủ tốc độ: đu dây thật nhanh rồi lao vào gáy titan và nhấn " + K(4) + ".",
            "Hết gas? Chạy tới cột sáng xanh để tiếp tế gas. Gas hồi chậm khi đứng yên.",
            "Móc vào mái nhà để đu cao; thả dây ngay khi vượt qua điểm neo để giữ đà.",
            "Va mạnh vào tường sẽ bị nảy ra. Giảm tốc trước khi chạm, hoặc giữ " + K(2) + " để bám nhẹ vào tường.",
            "Titan chỉ đập được tới tầm tay. Bay cao hơn tầm tay là an toàn.",
            "Ở trên không thời gian chậm lại để bạn kịp ngắm. Nhấn " + K(5) + " để tắt hiệu ứng này.",
        };
        briefTip.text = tips[((tipIndex % tips.Length) + tips.Length) % tips.Length];
    }

    void UpdateBriefing(float dt)
    {
        progress = Mathf.MoveTowards(progress, progressTarget, dt * 0.9f);
        progressFillRt.sizeDelta = new Vector2(1484f * progress, -16f);

        tipTimer += dt;
        if (tipTimer > 4.5f) { tipTimer = 0f; tipIndex++; ShowTip(); }

        if (!briefReady) return;

        if (progress >= 0.99f)
        {
            briefPrompt.gameObject.SetActive(true);
            var c = briefPrompt.color;
            c.a = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 6f);
            briefPrompt.color = c;

            KeyCode any;
            if (!BriefingConfirmed && (GameInput.GetMouseButtonDown(0) || GameInput.AnyKeyDown(out any) || GameInput.GetKeyDownRaw(KeyCode.Return)))
            {
                BriefingConfirmed = true;
                MangaSfx.Slam();
            }
        }
    }

    // =====================================================================
    //  PAUSE
    // =====================================================================
    void BuildPause()
    {
        var page = NewPage(Page.Pause);
        var list = pageItems[Page.Pause];
        Backdrop(page, false, new Color(1f, 1f, 1f, 0.1f), 0.3f);

        // Tiêu đề phẳng kiểu codex: font Ditty, thanh nhấn đồng bên dưới
        var title = TextAt(page, "TẠM DỪNG", 110, Ink, TextAnchor.MiddleLeft, new Vector2(-420f, 330f), new Vector2(900f, 150f), false, FontStyle.Bold);
        title.font = MangaStyle.TitleFont;
        Stroke(title, new Color(0f, 0f, 0f, 0.9f), 4f).useGraphicAlpha = false;
        Bar(page, "TitleRule", new Vector2(-420f, 250f), new Vector2(900f, 4f), Red);

        float bx = -560f, bw = 600f, bh = 70f, y0 = 120f, step = 82f;
        list.Add(MakeButton(page, "TIẾP TỤC", new Vector2(bx, y0), new Vector2(bw, bh), 0f, () => { if (Resume != null) Resume(); }, 0.05f, 30));
        list.Add(MakeButton(page, "CHƠI LẠI", new Vector2(bx, y0 - step), new Vector2(bw, bh), 0f, () => { if (Restart != null) Restart(); }, 0.10f, 30));
        list.Add(MakeButton(page, "CÀI ĐẶT", new Vector2(bx, y0 - 2 * step), new Vector2(bw, bh), 0f, OpenSettings, 0.15f, 30));
        list.Add(MakeButton(page, "VỀ MENU CHÍNH", new Vector2(bx, y0 - 3 * step), new Vector2(bw, bh), 0f, () => { if (MainMenu != null) MainMenu(); }, 0.20f, 30));
        list.Add(MakeButton(page, "THOÁT GAME", new Vector2(bx, y0 - 4 * step), new Vector2(bw, bh), 0f, () => { if (Quit != null) Quit(); }, 0.25f, 30));

        var keys = Panel(page, "Keys", new Vector2(510f, 30f), new Vector2(720f, 700f), Paper, -10f, 8f, 14f);
        keys.gameObject.AddComponent<MangaSlideIn>().Init(new Vector2(900f, 0f), 0.2f);
        var tag = Panel(keys.transform, "Tag", new Vector2(-190f, 300f), new Vector2(300f, 62f), Red, 14f, 4f, 5f);
        Txt(tag.transform, "ĐIỀU KHIỂN", 32, Paper);
        pauseKeys = TextAt(keys.transform, "", 28, Ink, TextAnchor.UpperLeft, new Vector2(0f, -40f), new Vector2(620f, 560f), true, FontStyle.Normal);
        pauseKeys.lineSpacing = 1.15f;
    }

    void RefreshPauseKeys()
    {
        pauseKeys.text =
            "<color=" + RedHex + ">W A S D</color>  Di chuyển\n" +
            "<color=" + RedHex + ">Chuột</color>  Nhìn / ngắm\n" +
            "<color=" + RedHex + ">" + K(0) + " / " + K(1) + "</color>  Bắn móc trái / phải (giữ)\n" +
            "<color=" + RedHex + ">" + K(2) + "</color>  Thu dây mạnh\n" +
            "<color=" + RedHex + ">" + K(3) + "</color>  Gas: tăng tốc, lái theo hướng nhìn\n" +
            "<color=" + RedHex + ">" + K(4) + "</color>  Chém gáy\n" +
            "<color=" + RedHex + ">" + K(5) + "</color>  Bật/tắt làm chậm thời gian\n" +
            "<color=" + RedHex + ">Esc</color>  Tạm dừng / tiếp tục";
    }

    // =====================================================================
    //  RESULT
    // =====================================================================
    void BuildResult()
    {
        var page = NewPage(Page.Result);
        var list = pageItems[Page.Result];
        Backdrop(page, false, new Color(1f, 1f, 1f, 0.12f), 0.25f);

        resultBurst = Burst(page, "Burst", new Vector2(0f, 170f), new Vector2(1300f, 560f), Red, 22, 0.72f, 5, 0.05f);
        resultTitle = TextAt(page, "CHIẾN THẮNG!", 156, Paper, TextAnchor.MiddleCenter, new Vector2(0f, 170f), new Vector2(1300f, 230f), false, FontStyle.BoldAndItalic);
        resultTitle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f);
        resultStroke = Stroke(resultTitle, Ink, 8f);
        resultTitle.gameObject.AddComponent<MangaSlideIn>().Init(Vector2.zero, 0.2f, 0f, 0.5f);

        var stats = Panel(page, "Stats", new Vector2(0f, -100f), new Vector2(780f, 170f), Paper, 0f, 6f, 10f);
        stats.gameObject.AddComponent<MangaSlideIn>().Init(new Vector2(0f, -400f), 0.3f);
        resultStats = Txt(stats.transform, "", 38, Ink);
        resultStats.lineSpacing = 1.15f;

        list.Add(MakeButton(page, "CHƠI LẠI (R)", new Vector2(-430f, -310f), new Vector2(410f, 96f), 28f, () => { if (Restart != null) Restart(); }, 0.4f, 38));
        list.Add(MakeButton(page, "MENU CHÍNH", new Vector2(0f, -310f), new Vector2(410f, 96f), 28f, () => { if (MainMenu != null) MainMenu(); }, 0.46f, 38));
        list.Add(MakeButton(page, "THOÁT", new Vector2(430f, -310f), new Vector2(410f, 96f), 28f, () => { if (Quit != null) Quit(); }, 0.52f, 38));
    }
}
