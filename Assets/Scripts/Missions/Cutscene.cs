using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Nếu có VideoClip tại Resources/Cutscenes/Prologue hoặc Resources/Cutscenes/FirstTitan thì phát video đó;
// không có thì dùng slide placeholder bên dưới.
// Cutscene placeholder dựng hoàn toàn bằng code (không cần asset/scene).
// Mỗi slide = một khung hình minh hoạ giả (màu + chữ [PLACEHOLDER]) kèm lời thoại. Khi có art/video thật
// chỉ cần thay danh sách Slide (hoặc thay Image/Sprite trong Show()).
//   Prologue      : hỏi Xem/Bỏ qua, chỉ hiện 1 lần duy nhất (cờ lưu trong PlayerPrefs, xoá dữ liệu là hiện lại).
//   Titan lần đầu : hiện lần hoá titan đầu tiên, các lần sau chỉ còn VFX trong TitanForm.
// Điều khiển: Space/Enter/click = sang slide, Esc = bỏ qua cả cutscene. Cutscene chạy bằng thời gian không-scale
// và tạm dừng game (timeScale = 0) trong lúc phát.
public class Cutscene : MonoBehaviour
{
    public struct Slide
    {
        public string caption;
        public string speaker;
        public Color tint;
        public float duration;
        public Slide(string speaker, string caption, Color tint, float duration = 4f)
        { this.speaker = speaker; this.caption = caption; this.tint = tint; this.duration = duration; }
    }

    const string PrologueKey = "AOT_PrologueSeen";
    const string TitanKey = "AOT_FirstTitanSeen";

    public const string ForceKey = "AOT_ForceCutscenes"; // EditorPrefs, chỉ có trong Editor
    public const string PrologueFlag = PrologueKey, TitanFlag = TitanKey;

    static bool Forced
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(ForceKey, false);
#else
            return false;
#endif
        }
    }

    public static bool PrologueSeen => !Forced && PlayerPrefs.GetInt(PrologueKey, 0) == 1;
    public static bool FirstTitanSeen => !Forced && PlayerPrefs.GetInt(TitanKey, 0) == 1;
    public static bool IsPlaying { get; private set; }

    // ---------- Nội dung placeholder ----------
    static readonly Slide[] PrologueSlides =
    {
        new Slide("", "Một trăm năm trước, loài người suýt bị xoá sổ.", new Color(0.10f, 0.12f, 0.16f), 4f),
        new Slide("", "Những kẻ khổng lồ xuất hiện. Con người trốn sau ba lớp tường.", new Color(0.16f, 0.13f, 0.10f), 4.5f),
        new Slide("", "Cho đến ngày bức tường ngoài cùng bị phá vỡ.", new Color(0.22f, 0.09f, 0.08f), 4f),
        new Slide("", "Bạn là một tân binh vừa tốt nghiệp. Trận chiến đầu tiên đang chờ.", new Color(0.09f, 0.14f, 0.12f), 4.5f),
    };

    static readonly Slide[] FirstTitanSlides =
    {
        new Slide("", "Cơn đau xé toạc cơ thể. Bạn cắn vào tay mình.", new Color(0.25f, 0.08f, 0.06f), 3.5f),
        new Slide("", "Tia sét đỏ loé lên - hơi nóng bốc ra từ khắp người.", new Color(0.30f, 0.14f, 0.05f), 3.5f),
        new Slide("", "Bạn không còn là con người nữa.", new Color(0.12f, 0.12f, 0.12f), 3.5f),
    };

    public static void PlayPrologue(Action onDone = null)
    {
        Spawn(PrologueSlides, "Cutscenes/Prologue", true, () =>
        {
            PlayerPrefs.SetInt(PrologueKey, 1);
            PlayerPrefs.Save();
            onDone?.Invoke();
        });
    }

    // Trả về true nếu cutscene được phát (onDone sẽ chạy sau); false nếu đã xem rồi -> gọi onDone ngay.
    public static bool PlayFirstTitan(Action onDone)
    {
        if (FirstTitanSeen || IsPlaying) { onDone?.Invoke(); return false; }
        Spawn(FirstTitanSlides, "Cutscenes/FirstTitan", false, () =>
        {
            PlayerPrefs.SetInt(TitanKey, 1);
            PlayerPrefs.Save();
            onDone?.Invoke();
        });
        return true;
    }

    // ---------- Triển khai ----------
    Slide[] slides;
    bool askFirst;
    string videoPath;
    VideoPlayer video;
    RenderTexture videoTex;
    RawImage videoImage;
    Action onDone;

    CanvasGroup group;
    Image panel;
    Text speakerText, captionText, hintText, placeholderText, promptTitle;
    Text yesText, noText;
    RectTransform watchRect, skipRect;
    GameObject promptRoot, slideRoot;

    enum Phase { Prompt, Slides, Video, Done }
    Phase phase;
    int index;
    float t;
    int choice = 1;             // 0 = Yes (bỏ qua), 1 = No (xem)
    float prevScale;
    CursorLockMode prevLock;
    bool prevVisible;

    static void Spawn(Slide[] slides, string videoPath, bool ask, Action done)
    {
        if (IsPlaying) return;
        var go = new GameObject("Cutscene");
        var c = go.AddComponent<Cutscene>();
        c.slides = slides; c.videoPath = videoPath; c.askFirst = ask; c.onDone = done;
    }

    void Awake()
    {
        IsPlaying = true;
        prevScale = Time.timeScale;
        prevLock = Cursor.lockState;
        prevVisible = Cursor.visible;
        Time.timeScale = 0f;
        BuildUI();
    }

    void Start()
    {
        if (askFirst)
        {
            phase = Phase.Prompt;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            promptRoot.SetActive(true);
            slideRoot.SetActive(false);
        }
        else StartSlides();
    }

    void StartSlides()
    {
        promptRoot.SetActive(false);
        var clip = Resources.Load<VideoClip>(videoPath);
        if (clip != null) { StartVideo(clip); return; }
        phase = Phase.Slides;
        slideRoot.SetActive(true);
        index = 0; t = 0f;
        ApplySlide();
    }

    void StartVideo(VideoClip clip)
    {
        phase = Phase.Video;
        slideRoot.SetActive(false);
        videoTex = new RenderTexture((int)Mathf.Max(16, clip.width), (int)Mathf.Max(16, clip.height), 0);
        videoImage.texture = videoTex;
        var fit = videoImage.GetComponent<AspectRatioFitter>();
        if (fit == null) fit = videoImage.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; // giữ tỉ lệ, viền đen nếu khác màn hình
        fit.aspectRatio = (float)clip.width / Mathf.Max(1, (float)clip.height);
        videoImage.gameObject.SetActive(true);
        hintText.transform.SetParent(videoImage.transform.parent, false);
        hintText.gameObject.SetActive(true);
        hintText.text = "SPACE / ESC: bỏ qua";
        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.clip = clip;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = videoTex;
        video.audioOutputMode = VideoAudioOutputMode.Direct;
        video.isLooping = false;
        video.loopPointReached += _ => Finish();
        video.errorReceived += (_, __) => Finish();
        video.Play();
    }

    void ApplySlide()
    {
        var s = slides[index];
        panel.color = s.tint;
        speakerText.text = s.speaker;
        captionText.text = s.caption;
        placeholderText.text = "[ PLACEHOLDER " + (index + 1) + "/" + slides.Length + " ]";
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (phase == Phase.Prompt) UpdatePrompt();
        else if (phase == Phase.Slides) UpdateSlides(dt);
        else if (phase == Phase.Video && (GameInput.GetKeyDownRaw(KeyCode.Escape) || GameInput.GetKeyDownRaw(KeyCode.Space)))
            Finish();
    }

    void UpdatePrompt()
    {
        // choice 0 = Yes (bỏ qua prologue), 1 = No (xem)
        if (GameInput.GetKeyDownRaw(KeyCode.UpArrow) || GameInput.GetKeyDownRaw(KeyCode.W)) choice = 0;
        if (GameInput.GetKeyDownRaw(KeyCode.DownArrow) || GameInput.GetKeyDownRaw(KeyCode.S)) choice = 1;

        Vector2 m = GameInput.MousePosition;
        bool overYes = RectTransformUtility.RectangleContainsScreenPoint(watchRect, m, null);
        bool overNo = RectTransformUtility.RectangleContainsScreenPoint(skipRect, m, null);
        if (overYes) choice = 0; else if (overNo) choice = 1;

        var lit = Color.white; var dim = new Color(1f, 1f, 1f, 0.4f);
        yesText.color = choice == 0 ? lit : dim; yesText.text = choice == 0 ? "> Yes" : "Yes";
        noText.color = choice == 1 ? lit : dim; noText.text = choice == 1 ? "> No" : "No";

        bool skip = GameInput.GetKeyDownRaw(KeyCode.Escape);
        bool confirm = GameInput.GetKeyDownRaw(KeyCode.Return) || GameInput.GetKeyDownRaw(KeyCode.KeypadEnter)
                       || GameInput.GetKeyDownRaw(KeyCode.Space)
                       || (GameInput.GetMouseButtonDown(0) && (overYes || overNo));
        if (!skip && !confirm) return;
        if (skip || choice == 0) Finish(); else StartSlides();
    }

    void UpdateSlides(float dt)
    {
        t += dt;
        var s = slides[index];
        float fade = Mathf.Clamp01(Mathf.Min(t / 0.5f, (s.duration - t) / 0.5f));
        group.alpha = 1f;
        panel.canvasRenderer.SetAlpha(fade);
        captionText.canvasRenderer.SetAlpha(fade);
        speakerText.canvasRenderer.SetAlpha(fade);
        placeholderText.canvasRenderer.SetAlpha(fade);

        if (GameInput.GetKeyDownRaw(KeyCode.Escape)) { Finish(); return; }
        bool next = t >= s.duration
                    || (t > 0.3f && (GameInput.GetKeyDownRaw(KeyCode.Space) || GameInput.GetKeyDownRaw(KeyCode.Return)
                                     || GameInput.GetMouseButtonDown(0)));
        if (!next) return;
        index++;
        t = 0f;
        if (index >= slides.Length) Finish(); else ApplySlide();
    }

    void Finish()
    {
        if (phase == Phase.Done) return;
        phase = Phase.Done;
        if (video != null) video.Stop();
        if (videoTex != null) videoTex.Release();
        Time.timeScale = prevScale <= 0.0001f ? 1f : prevScale;
        Cursor.lockState = prevLock;
        Cursor.visible = prevVisible;
        IsPlaying = false;
        var cb = onDone;
        Destroy(gameObject);
        cb?.Invoke();
    }

    void OnDestroy()
    {
        if (phase != Phase.Done) { IsPlaying = false; Time.timeScale = prevScale <= 0.0001f ? 1f : prevScale; }
    }

    // ---------- UI ----------
    void BuildUI()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasGo.GetComponent<CanvasGroup>();

        var bg = NewImage("Black", canvasGo.transform, Color.black);
        Stretch(bg.rectTransform);

        videoImage = new GameObject("Video", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        videoImage.transform.SetParent(canvasGo.transform, false);
        Stretch(videoImage.rectTransform);
        videoImage.raycastTarget = false;
        videoImage.gameObject.SetActive(false);

        // ---- slide ----
        slideRoot = new GameObject("Slides", typeof(RectTransform));
        slideRoot.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)slideRoot.transform);

        panel = NewImage("Panel", slideRoot.transform, Color.gray);
        Anchor(panel.rectTransform, 0.08f, 0.22f, 0.92f, 0.94f);
        placeholderText = NewText("Placeholder", slideRoot.transform, font, 38, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleCenter);
        Anchor(placeholderText.rectTransform, 0.08f, 0.22f, 0.92f, 0.94f);
        speakerText = NewText("Speaker", slideRoot.transform, font, 30, new Color(0.85f, 0.65f, 0.3f), TextAnchor.LowerLeft);
        Anchor(speakerText.rectTransform, 0.1f, 0.15f, 0.9f, 0.21f);
        captionText = NewText("Caption", slideRoot.transform, font, 36, new Color(0.91f, 0.88f, 0.8f), TextAnchor.UpperLeft);
        Anchor(captionText.rectTransform, 0.1f, 0.04f, 0.9f, 0.16f);
        hintText = NewText("Hint", slideRoot.transform, font, 22, new Color(1, 1, 1, 0.45f), TextAnchor.LowerRight);
        Anchor(hintText.rectTransform, 0.5f, 0.0f, 0.98f, 0.04f);
        hintText.text = "SPACE / CLICK: tiếp   ·   ESC: bỏ qua";

        // ---- prompt: tối giản, chỉ chữ ----
        promptRoot = new GameObject("Prompt", typeof(RectTransform));
        promptRoot.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)promptRoot.transform);
        promptTitle = NewText("Title", promptRoot.transform, font, 26, new Color(0.62f, 0.63f, 0.6f), TextAnchor.MiddleCenter);
        Anchor(promptTitle.rectTransform, 0.3f, 0.52f, 0.7f, 0.58f);
        promptTitle.text = "Skip Prologue?";
        yesText = NewOption("Yes", promptRoot.transform, font, "Yes", 0.46f, out watchRect);
        noText = NewOption("No", promptRoot.transform, font, "No", 0.40f, out skipRect);
    }

    static Text NewOption(string name, Transform parent, Font font, string label, float yMin, out RectTransform rect)
    {
        var t = NewText(name, parent, font, 24, Color.white, TextAnchor.MiddleCenter);
        Anchor(t.rectTransform, 0.4f, yMin, 0.6f, yMin + 0.05f);
        t.text = label;
        rect = t.rectTransform;
        return t;
    }

    static Image NewImage(string name, Transform parent, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = c;
        return img;
    }

    static Text NewText(string name, Transform parent, Font font, int size, Color c, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.fontSize = size; t.color = c; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform r) => Anchor(r, 0, 0, 1, 1);

    static void Anchor(RectTransform r, float x0, float y0, float x1, float y1)
    {
        r.anchorMin = new Vector2(x0, y0);
        r.anchorMax = new Vector2(x1, y1);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
