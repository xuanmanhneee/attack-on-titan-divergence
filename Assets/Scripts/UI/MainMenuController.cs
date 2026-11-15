using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class MainMenuController : MonoBehaviour
{
    [Header("Supplied assets")]
    [SerializeField] Texture2D backgroundTexture = null;
    [SerializeField] Font titleFont = null;
    [Tooltip("Optional explicit playlist. If empty, every AudioClip in Resources/Audio/Music is loaded automatically.")]
    [SerializeField] AudioClip[] menuPlaylist = null;
    [SerializeField] bool shuffleMenuPlaylist = false;
    [Range(0f, 1f)] [SerializeField] float menuMusicBaseVolume = 0.65f;

    static readonly Color Ivory = Hex("E8E0CB");
    static readonly Color Muted = Hex("A8AA9F");
    static readonly Color Brass = Hex("C08A3E");
    static readonly Color Ink = Hex("0B1114");
    static readonly Color ButtonIdle = new Color(0.035f, 0.055f, 0.062f, 0.88f);
    static readonly Color ButtonHover = new Color(0.18f, 0.13f, 0.075f, 0.96f);

    Font bodyFont;
    [SerializeField] CanvasGroup rootGroup;
    [SerializeField] GameObject creditsOverlay;
    [SerializeField] GameObject settingsOverlay;
    [SerializeField] GameObject startNavigation;
    [SerializeField] GameObject chapterNavigation;
    [SerializeField] Button startButton;
    [SerializeField] Button firstButton;
    [SerializeField] Button secondButton;
    [SerializeField] Button backButton;
    AudioSource menuAudio;
    AudioSource uiAudio;
    AudioClip hoverSound;
    AudioClip clickSound;
    int menuTrackIndex = -1;
    bool menuPlaybackStarted;

    void Awake()
    {
        Time.timeScale = 1f;
        GameVolumeSettings.ApplyMasterVolume();
        EnsureEventSystem();
        EnsureMenuCamera();
        CacheAuthoredUI();
        if (!enabled) return;
        WireAuthoredUI();
        SetupTitleOutline();
        // Lần đầu mở game: prologue chạy trước, chưa có nhạc/âm menu cho tới khi vào menu.
        if (Cutscene.PrologueSeen) StartMenu();
        else Cutscene.PlayPrologue(StartMenu);
    }

    void StartMenu()
    {
        if (this == null) return;
        SetupMenuMusic();
        SetupUISounds();
        StartCoroutine(Intro());
        AnimateIn();
        GameSettings.Changed += RefreshMenuMusicVolume;
    }

    void OnDestroy()
    {
        GameSettings.Changed -= RefreshMenuMusicVolume;
    }

    // Hiệu ứng trượt vào kiểu MangaMenu: title từ trái, thanh trên từ trên, nút từ phải lần lượt.
    // MangaSlideIn tự chạy lại mỗi khi nhóm nút được bật (START <-> CHAPTER).
    void AnimateIn()
    {
        var canvas = rootGroup.transform;
        Slide(canvas.Find("Title Block"), new Vector2(-1000f, 0f), 0.05f);
        Slide(canvas.Find("Top Bar"), new Vector2(0f, 80f), 0f);
        SlideChildren(startNavigation, 0.12f);
        SlideChildren(chapterNavigation, 0.05f);
        MangaSfx.Slam();
    }

    static void Slide(Transform t, Vector2 from, float delay)
    {
        if (t != null) t.gameObject.AddComponent<MangaSlideIn>().Init(from, delay);
    }

    static void SlideChildren(GameObject nav, float firstDelay)
    {
        if (nav == null) return;
        for (int i = 0; i < nav.transform.childCount; i++)
            Slide(nav.transform.GetChild(i), new Vector2(260f, 0f), firstDelay + 0.08f * i);
    }

    void Update()
    {
        if (menuPlaybackStarted && menuAudio != null && !menuAudio.isPlaying)
            PlayNextMenuTrack();

        if (GameInput.GetKeyDown(KeyCode.Escape))
        {
            if (creditsOverlay != null && creditsOverlay.activeSelf) ShowCredits(false);
            else if (settingsOverlay != null && settingsOverlay.activeSelf && !SettingsEscConsumed()) ShowSettings(false);
        }
    }

    void CacheAuthoredUI()
    {
        var canvas = transform.Find("Main Menu Canvas");
        if (canvas == null)
        {
            Debug.LogError("[MainMenu] Scene UI is missing. Rebuild it from Attack on Titan > Build Main Menu Scene.", this);
            enabled = false;
            return;
        }

        rootGroup = canvas.GetComponent<CanvasGroup>();
        creditsOverlay = canvas.Find("Credits Overlay")?.gameObject;
        settingsOverlay = canvas.Find("Settings Overlay")?.gameObject;
        chapterNavigation = canvas.Find("Primary Navigation")?.gameObject;
        startNavigation = canvas.Find("Start Navigation")?.gameObject;
        if (startNavigation != null)
            startButton = canvas.Find("Start Navigation/START")?.GetComponent<Button>();
        else
            startButton = canvas.Find("Primary Navigation/START")?.GetComponent<Button>();
        firstButton = canvas.Find("Primary Navigation/CHAPTER 0")?.GetComponent<Button>();
        secondButton = canvas.Find("Primary Navigation/CHAPTER 1")?.GetComponent<Button>();
        backButton = canvas.Find("Primary Navigation/BACK")?.GetComponent<Button>();
    }

    void WireAuthoredUI()
    {
        if (!enabled || rootGroup == null) return;
        var canvas = rootGroup.transform;
        BindButton(canvas, startNavigation != null ? "Start Navigation/START" : "Primary Navigation/START", () => ShowChapterMenu(true));
        BindButton(canvas, "Start Navigation/ARCHIVES", () => ShowCredits(true));
        BindButton(canvas, "Start Navigation/SETTINGS", () => ShowSettings(true));
        BindButton(canvas, "Start Navigation/QUIT GAME", Quit);
        BindButton(canvas, "Primary Navigation/CHAPTER 0", () => Load("Chapter0_TrainingCamp"));
        BindButton(canvas, "Primary Navigation/CHAPTER 1", () => Load("Chapter1_BattleOfTrost"));
        BindButton(canvas, "Primary Navigation/BACK", () => ShowChapterMenu(false));
        BindButton(canvas, "Credits Overlay/Credits Card/CLOSE ARCHIVES", () => ShowCredits(false));
        BindButton(canvas, "Settings Overlay/Settings Card/APPLY & CLOSE", () => ShowSettings(false));

        if (creditsOverlay != null) creditsOverlay.SetActive(false);
        if (settingsOverlay != null) settingsOverlay.SetActive(false);
        if (startNavigation != null) startNavigation.SetActive(true);
        if (chapterNavigation != null) chapterNavigation.SetActive(startNavigation != null ? false : true);
        if (startNavigation == null) ShowChapterMenu(false);
        if (startButton != null) EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    void BindButton(Transform root, string path, UnityEngine.Events.UnityAction action)
    {
        var button = root.Find(path)?.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(action);
            button.onClick.AddListener(PlayClickSound);
        }
    }

    void SetupUISounds()
    {
        uiAudio = gameObject.AddComponent<AudioSource>();
        uiAudio.playOnAwake = false;
        uiAudio.loop = false;
        uiAudio.spatialBlend = 0f;
        hoverSound = CreateUiTone("Menu Hover", 720f, 0.055f, 0.055f);
        clickSound = CreateUiTone("Menu Click", 260f, 0.11f, 0.08f);

        foreach (var visual in rootGroup.GetComponentsInChildren<MenuButtonVisual>(true))
            visual.SetSound(uiAudio, hoverSound);
    }

    void SetupTitleOutline()
    {
        var title = rootGroup.transform.Find("Title Block/Main Title")?.GetComponent<Text>();
        if (title == null) return;
        var outline = title.GetComponent<Outline>();
        if (outline == null) outline = title.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(4f, -4f);
        outline.useGraphicAlpha = false;
    }

    void PlayClickSound()
    {
        if (uiAudio != null && clickSound != null)
            uiAudio.PlayOneShot(clickSound, 0.55f * GameVolumeSettings.SfxVolume);
    }

    static AudioClip CreateUiTone(string name, float frequency, float duration, float volume)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 28f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
        }
        clip.SetData(samples, 0);
        return clip;
    }

    public void BuildSceneUI()
    {
        bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasObject = new GameObject("Main Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        rootGroup = canvasObject.GetComponent<CanvasGroup>();

        var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
        backgroundObject.transform.SetParent(canvas.transform, false);
        var background = backgroundObject.GetComponent<RawImage>();
        background.texture = backgroundTexture;
        background.color = Color.white;
        background.raycastTarget = false;
        Stretch(background.rectTransform);

        var grade = Image("Global Color Grade", canvas.transform, null, new Color(0.01f, 0.025f, 0.035f, 0.24f));
        Stretch(grade.rectTransform);

        var gradientObject = new GameObject("Left Readability Gradient", typeof(RectTransform), typeof(HorizontalGradientGraphic));
        gradientObject.transform.SetParent(canvas.transform, false);
        var leftGradient = gradientObject.GetComponent<HorizontalGradientGraphic>();
        leftGradient.LeftColor = new Color(0.015f, 0.025f, 0.03f, 0.96f);
        leftGradient.RightColor = new Color(0.015f, 0.025f, 0.03f, 0.02f);
        SetRect(leftGradient.rectTransform, 0f, 0f, 0.68f, 1f, 0f, 0f, 0f, 0f);

        BuildTopBar(canvas.transform);
        BuildTitle(canvas.transform);
        BuildStartNavigation(canvas.transform);
        BuildNavigation(canvas.transform);
        BuildMissionBrief(canvas.transform);
        BuildCredits(canvas.transform);
        BuildSettings(canvas.transform);

        if (startNavigation != null) startNavigation.SetActive(true);
        if (chapterNavigation != null) chapterNavigation.SetActive(false);

    }

    void BuildTopBar(Transform parent)
    {
        var bar = Image("Top Bar", parent, null, new Color(0.025f, 0.04f, 0.045f, 0.82f));
        SetRect(bar.rectTransform, 0f, 1f, 1f, 1f, 0f, -66f, 0f, 0f);
        var line = Image("Top Accent", bar.transform, null, Brass);
        SetRect(line.rectTransform, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 2f);

        var left = Text("Context", bar.transform, "SCOUT REGIMENT   /   OPERATIONS TERMINAL", 17, Muted, TextAnchor.MiddleLeft, false);
        SetRect(left.rectTransform, 0f, 0f, 0.6f, 1f, 72f, 0f, 0f, 0f);
        var right = Text("Build", bar.transform, "DIVERGENCE  //  BUILD 0.1", 15, Muted, TextAnchor.MiddleRight, false);
        SetRect(right.rectTransform, 0.6f, 0f, 1f, 1f, 0f, 0f, -64f, 0f);
    }

    void BuildTitle(Transform parent)
    {
        var block = RectObject("Title Block", parent);
        SetRect(block, 0f, 1f, 0f, 1f, 112f, -330f, 1810f, -106f);

        var title = Text("Main Title", block, "ATTACK ON TITAN: DIVERGENCE", 120, Ivory, TextAnchor.MiddleLeft, true);
        SetRect(title.rectTransform, 0f, 1f, 1f, 1f, 0f, -160f, 0f, -32f);
        title.fontStyle = FontStyle.Bold;
        var titleOutline = title.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        titleOutline.effectDistance = new Vector2(4f, -4f);
        titleOutline.useGraphicAlpha = false;

    }

    void BuildStartNavigation(Transform parent)
    {
        var nav = RectObject("Start Navigation", parent);
        startNavigation = nav.gameObject;
        SetRect(nav, 1f, 0f, 1f, 0f, -650f, 92f, -92f, 430f);
        startButton = MenuButton(nav, "", "START", "SELECT OPERATION", () => ShowChapterMenu(true), 0);
        MenuButton(nav, "", "ARCHIVES", "CREDITS & FIELD NOTES", () => ShowCredits(true), 1);
        MenuButton(nav, "", "SETTINGS", "AUDIO & DISPLAY", () => ShowSettings(true), 2);
        MenuButton(nav, "", "QUIT GAME", "RETURN TO DESKTOP", Quit, 3);
    }

    void BuildNavigation(Transform parent)
    {
        var nav = RectObject("Primary Navigation", parent);
        chapterNavigation = nav.gameObject;
        SetRect(nav, 1f, 0f, 1f, 0f, -650f, 92f, -92f, 350f);

        firstButton = MenuButton(nav, "01", "CHAPTER 0", "TRAINING CAMP", () => Load("Chapter0_TrainingCamp"), 0);
        MenuButton(nav, "02", "CHAPTER 1", "BATTLE OF TROST", () => Load("Chapter1_BattleOfTrost"), 1);
        MenuButton(nav, "", "BACK", "RETURN TO MAIN MENU", () => ShowChapterMenu(false), 2);
    }

    Button MenuButton(Transform parent, string number, string label, string detail, UnityEngine.Events.UnityAction action, int index)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonVisual));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), 0f, 1f, 1f, 1f, 0f, -index * 86f - 74f, 0f, -index * 86f);

        var image = go.GetComponent<Image>();
        image.color = ButtonIdle;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = ButtonHover;
        colors.selectedColor = ButtonHover;
        colors.pressedColor = new Color(0.42f, 0.27f, 0.11f, 1f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        button.onClick.AddListener(action);

        var frame = go.AddComponent<Outline>();
        frame.effectColor = new Color(Brass.r, Brass.g, Brass.b, 0.28f);
        frame.effectDistance = new Vector2(1.5f, -1.5f);
        frame.useGraphicAlpha = false;

        var accent = Image("Accent", go.transform, null, Brass);
        SetRect(accent.rectTransform, 0f, 0f, 0f, 1f, 0f, 0f, 4f, 0f);
        var numberText = Text("Number", go.transform, number, 14, Brass, TextAnchor.MiddleCenter, false);
        SetRect(numberText.rectTransform, 0f, 0f, 0f, 1f, 14f, 0f, 58f, 0f);
        var labelText = Text("Label", go.transform, label, 23, Ivory, TextAnchor.LowerLeft, false);
        SetRect(labelText.rectTransform, 0f, 0f, 1f, 1f, 70f, 25f, -55f, 0f);
        var detailText = Text("Detail", go.transform, detail, 13, Muted, TextAnchor.UpperLeft, false);
        SetRect(detailText.rectTransform, 0f, 0f, 1f, 1f, 71f, 0f, -55f, -43f);
        var arrow = Text("Arrow", go.transform, "›", 34, Brass, TextAnchor.MiddleCenter, false);
        SetRect(arrow.rectTransform, 1f, 0f, 1f, 1f, -52f, 0f, -14f, 0f);

        go.GetComponent<MenuButtonVisual>().Configure(image, accent, labelText, arrow, ButtonIdle, ButtonHover, Brass);
        go.GetComponent<MenuButtonVisual>().SetExtras(frame, detailText, numberText, new[] { labelText.rectTransform, detailText.rectTransform, numberText.rectTransform });
        return button;
    }

    void BuildMissionBrief(Transform parent)
    {
        var panel = Image("Mission Brief", parent, null, new Color(0.025f, 0.04f, 0.045f, 0.78f));
        SetRect(panel.rectTransform, 0f, 0f, 0f, 0f, 112f, 104f, 610f, 286f);
        var accent = Image("Mission Accent", panel.transform, null, Brass);
        SetRect(accent.rectTransform, 0f, 0f, 0f, 1f, 0f, 0f, 4f, 0f);
        var heading = Text("Heading", panel.transform, "CURRENT DIRECTIVE", 14, Brass, TextAnchor.MiddleLeft, false);
        SetRect(heading.rectTransform, 0f, 1f, 1f, 1f, 28f, -42f, -18f, -12f);
        var title = Text("Directive", panel.transform, "DEFEND HUMANITY", 24, Ivory, TextAnchor.MiddleLeft, false);
        SetRect(title.rectTransform, 0f, 1f, 1f, 1f, 28f, -82f, -18f, -43f);
        var body = Text("Body", panel.transform, "Master ODM gear, protect the district,\nand eliminate every Titan in your path.", 15, Muted, TextAnchor.UpperLeft, false);
        SetRect(body.rectTransform, 0f, 0f, 1f, 1f, 28f, 18f, -18f, -91f);
    }

    // Khung thẻ dùng chung cho Archives/Settings: viền đồng, góc nhấn, tiêu đề, trượt vào khi mở.
    RectTransform BuildCard(Transform overlay, string name, float width, float height, string heading, string subheading, string tag)
    {
        var dim = overlay.GetComponent<Image>();
        dim.color = new Color(0.005f, 0.012f, 0.016f, 0.86f);

        var card = Image(name, overlay, null, new Color(0.03f, 0.045f, 0.05f, 0.99f));
        SetRect(card.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, -width / 2f, -height / 2f, width / 2f, height / 2f);
        var frame = card.gameObject.AddComponent<Outline>();
        frame.effectColor = new Color(Brass.r, Brass.g, Brass.b, 0.55f);
        frame.effectDistance = new Vector2(1.5f, -1.5f);
        frame.useGraphicAlpha = false;
        card.gameObject.AddComponent<MangaSlideIn>().Init(new Vector2(0f, -70f), 0f, 0.94f, 0.32f);

        var top = Image("Top Rule", card.transform, null, Brass);
        SetRect(top.rectTransform, 0f, 1f, 1f, 1f, 0f, -5f, 0f, 0f);
        // Góc nhấn kiểu khung ngắm
        Corner(card.transform, 0f, 0f, 1f, 1f);
        Corner(card.transform, 1f, 0f, -1f, 1f);
        Corner(card.transform, 0f, 1f, 1f, -1f);
        Corner(card.transform, 1f, 1f, -1f, -1f);

        var tagText = Text("Tag", card.transform, tag, 13, Brass, TextAnchor.MiddleLeft, false);
        SetRect(tagText.rectTransform, 0f, 1f, 1f, 1f, 56f, -52f, -56f, -30f);
        var head = Text("Heading", card.transform, heading, 50, Ivory, TextAnchor.MiddleLeft, true);
        SetRect(head.rectTransform, 0f, 1f, 1f, 1f, 54f, -112f, -54f, -48f);
        var headOutline = head.gameObject.AddComponent<Outline>();
        headOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        headOutline.effectDistance = new Vector2(3f, -3f);
        var sub = Text("Subheading", card.transform, subheading, 14, Muted, TextAnchor.MiddleLeft, false);
        SetRect(sub.rectTransform, 0f, 1f, 1f, 1f, 56f, -142f, -56f, -116f);
        var rule = Image("Header Rule", card.transform, null, new Color(Brass.r, Brass.g, Brass.b, 0.5f));
        SetRect(rule.rectTransform, 0f, 1f, 1f, 1f, 56f, -152f, -56f, -150f);
        return card.rectTransform;
    }

    void Corner(Transform parent, float ax, float ay, float dx, float dy)
    {
        const float len = 22f, thick = 3f, inset = 10f;
        var h = Image("Corner H", parent, null, Brass);
        SetRect(h.rectTransform, ax, ay, ax, ay, dx > 0 ? inset : -inset - len, dy > 0 ? inset : -inset - thick, dx > 0 ? inset + len : -inset, dy > 0 ? inset + thick : -inset);
        var v = Image("Corner V", parent, null, Brass);
        SetRect(v.rectTransform, ax, ay, ax, ay, dx > 0 ? inset : -inset - thick, dy > 0 ? inset : -inset - len, dx > 0 ? inset + thick : -inset, dy > 0 ? inset + len : -inset);
    }

    void BuildCredits(Transform parent)
    {
        creditsOverlay = RectObject("Credits Overlay", parent).gameObject;
        Stretch(creditsOverlay.GetComponent<RectTransform>());
        creditsOverlay.AddComponent<Image>();

        var card = BuildCard(creditsOverlay.transform, "Credits Card", 1060f, 740f, "FIELD ARCHIVES",
            "FAN-MADE UNITY PROTOTYPE  /  NON-COMMERCIAL  /  STUDENT PROJECT", "ARCHIVE 01  /  ASSET CREDITS");

        var credits = new[]
        {
            new[] { "ODM Gear", "aap75  ·  Sketchfab", "CC BY 4.0" },
            new[] { "Gear Sword – Low Poly", "Cem Alagozlu  ·  Sketchfab", "CC BY 4.0" },
            new[] { "WRAD Arms", "wriks  ·  GitHub", "CC0 1.0" },
            new[] { "Low Poly Arms (Rigged)", "DevMops  ·  OpenGameArt", "CC0 1.0" },
            new[] { "UI Pack: Sci-Fi", "Kenney", "CC0 1.0" },
            new[] { "Interface Sounds", "Kenney", "CC0 1.0" },
        };
        for (int i = 0; i < credits.Length; i++)
        {
            float top = 176f + i * 70f;
            var row = RectObject("Credit " + (i + 1), card);
            SetRect(row, 0f, 1f, 1f, 1f, 56f, -top - 60f, -56f, -top);
            var bg = row.gameObject.AddComponent<Image>();
            bg.color = new Color(0.045f, 0.065f, 0.072f, 0.9f);
            bg.raycastTarget = false;
            row.gameObject.AddComponent<MangaSlideIn>().Init(new Vector2(120f, 0f), 0.08f + i * 0.05f);
            var bar = Image("Accent", row, null, Brass);
            SetRect(bar.rectTransform, 0f, 0f, 0f, 1f, 0f, 0f, 4f, 0f);
            var num = Text("Number", row, (i + 1).ToString("00"), 14, Brass, TextAnchor.MiddleCenter, false);
            SetRect(num.rectTransform, 0f, 0f, 0f, 1f, 14f, 0f, 62f, 0f);
            var title = Text("Title", row, credits[i][0], 21, Ivory, TextAnchor.LowerLeft, false);
            SetRect(title.rectTransform, 0f, 0f, 0.7f, 1f, 76f, 28f, 0f, 0f);
            var src = Text("Source", row, credits[i][1], 13, Muted, TextAnchor.UpperLeft, false);
            SetRect(src.rectTransform, 0f, 0f, 0.7f, 1f, 77f, 0f, 0f, -34f);
            var lic = Text("License", row, credits[i][2], 15, Brass, TextAnchor.MiddleRight, false);
            SetRect(lic.rectTransform, 0.6f, 0f, 1f, 1f, 0f, 0f, -22f, 0f);
        }

        var thanks = Text("Thanks", card, "THANK YOU FOR PLAYING", 14, Muted, TextAnchor.MiddleCenter, false);
        SetRect(thanks.rectTransform, 0f, 0f, 1f, 0f, 40f, 104f, -40f, 130f);
        var close = SimpleButton(card, "CLOSE ARCHIVES", () => ShowCredits(false));
        SetRect(close.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, -170f, 32f, 170f, 94f);
        creditsOverlay.SetActive(false);
    }

    Button SimpleButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MenuButtonVisual));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = ButtonIdle;
        var button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        var colors = button.colors;
        colors.highlightedColor = ButtonHover; colors.selectedColor = ButtonHover;
        colors.pressedColor = new Color(0.42f, 0.27f, 0.11f, 1f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        var frame = go.AddComponent<Outline>();
        frame.effectColor = new Color(Brass.r, Brass.g, Brass.b, 0.28f);
        frame.effectDistance = new Vector2(1.5f, -1.5f);
        frame.useGraphicAlpha = false;

        var accent = Image("Accent", go.transform, null, Brass);
        SetRect(accent.rectTransform, 0f, 0f, 0f, 1f, 0f, 0f, 4f, 0f);
        var text = Text("Label", go.transform, label, 19, Ivory, TextAnchor.MiddleCenter, false);
        Stretch(text.rectTransform);
        var arrow = Text("Arrow", go.transform, "›", 30, Brass, TextAnchor.MiddleCenter, false);
        SetRect(arrow.rectTransform, 1f, 0f, 1f, 1f, -52f, 0f, -14f, 0f);
        var visual = go.GetComponent<MenuButtonVisual>();
        visual.Configure(image, accent, text, arrow, ButtonIdle, ButtonHover, Brass);
        visual.SetExtras(frame, null, null, new RectTransform[0]);
        return button;
    }

    void ShowCredits(bool show)
    {
        if (show && settingsOverlay != null) settingsOverlay.SetActive(false);
        creditsOverlay.SetActive(show);
        EventSystem.current.SetSelectedGameObject(show ? creditsOverlay.transform.Find("Credits Card/CLOSE ARCHIVES")?.gameObject : firstButton.gameObject);
    }

    void ShowChapterMenu(bool show)
    {
        if (startNavigation != null)
        {
            startNavigation.SetActive(!show);
            if (chapterNavigation != null) chapterNavigation.SetActive(show);
        }
        else
        {
            startButton?.gameObject.SetActive(!show);
            firstButton?.gameObject.SetActive(show);
            secondButton?.gameObject.SetActive(show);
            backButton?.gameObject.SetActive(show);
        }
        EventSystem.current.SetSelectedGameObject(show ? firstButton?.gameObject : startButton?.gameObject);
    }

    bool SettingsEscConsumed()
    {
        var menu = settingsOverlay.GetComponent<SettingsMenu>();
        return menu != null && menu.EscConsumedThisFrame;
    }

    void ShowSettings(bool show)
    {
        if (show && creditsOverlay != null) creditsOverlay.SetActive(false);
        settingsOverlay.SetActive(show);
        EventSystem.current.SetSelectedGameObject(show ? settingsOverlay.transform.Find("Settings Card/APPLY & CLOSE")?.gameObject : firstButton.gameObject);
    }

    void SetupMenuMusic()
    {
        // Resource clips are the portable source of truth. This lets a future
        // MP3/OGG playlist replace an unsupported container without editing the scene.
        var resourceTracks = Resources.LoadAll<AudioClip>("Audio/Music");
        if (resourceTracks != null && resourceTracks.Length > 0)
            menuPlaylist = resourceTracks;
        if (menuPlaylist == null || menuPlaylist.Length == 0) return;

        System.Array.Sort(menuPlaylist, (a, b) => string.CompareOrdinal(a.name, b.name));

        menuAudio = gameObject.AddComponent<AudioSource>();
        menuAudio.playOnAwake = false;
        menuAudio.loop = false;
        menuAudio.spatialBlend = 0f;
        menuAudio.ignoreListenerPause = true;
        RefreshMenuMusicVolume();
        menuPlaybackStarted = true;
        PlayNextMenuTrack();
    }

    void PlayNextMenuTrack()
    {
        if (menuPlaylist == null || menuPlaylist.Length == 0 || menuAudio == null) return;

        if (shuffleMenuPlaylist && menuPlaylist.Length > 1)
        {
            int next;
            do next = Random.Range(0, menuPlaylist.Length); while (next == menuTrackIndex);
            menuTrackIndex = next;
        }
        else
        {
            menuTrackIndex = (menuTrackIndex + 1) % menuPlaylist.Length;
        }

        menuAudio.clip = menuPlaylist[menuTrackIndex];
        menuAudio.Play();
    }

    void RefreshMenuMusicVolume()
    {
        if (menuAudio != null)
            menuAudio.volume = menuMusicBaseVolume * GameVolumeSettings.MusicVolume;
    }

    void Load(string sceneName) { SceneManager.LoadScene(sceneName); }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator Intro()
    {
        rootGroup.alpha = 0f;
        float time = 0f;
        while (time < 0.55f)
        {
            time += Time.unscaledDeltaTime;
            rootGroup.alpha = Mathf.SmoothStep(0f, 1f, time / 0.55f);
            yield return null;
        }
        rootGroup.alpha = 1f;
    }

    Image Image(string name, Transform parent, Texture2D texture, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        if (texture != null)
            image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text Text(string name, Transform parent, string value, int size, Color color, TextAnchor alignment, bool displayFont)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.text = value;
        text.font = displayFont && titleFont != null ? titleFont : bodyFont;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    RectTransform RectObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void EnsureMenuCamera()
    {
        var camera = FindAnyObjectByType<Camera>();
        if (camera == null) camera = new GameObject("Menu Camera").AddComponent<Camera>();
        if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
        camera.enabled = true;
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Ink;
        camera.cullingMask = ~0;
        camera.depth = -100f;
        camera.targetDisplay = 0;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static void Stretch(RectTransform rect) => SetRect(rect, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);

    static void SetRect(RectTransform rect, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color);
        return color;
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public class HorizontalGradientGraphic : MaskableGraphic
{
    [SerializeField] Color leftColor = Color.black;
    [SerializeField] Color rightColor = Color.clear;
    [SerializeField] bool vertical;

    // Mặc định chuyển màu trái -> phải. Khi vertical = true: LeftColor ở đáy, RightColor ở đỉnh.
    public Color LeftColor { get => leftColor; set { leftColor = value; SetVerticesDirty(); } }
    public Color RightColor { get => rightColor; set { rightColor = value; SetVerticesDirty(); } }
    public bool Vertical { get => vertical; set { vertical = value; SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = GetPixelAdjustedRect();
        Color a = leftColor, b = vertical ? leftColor : leftColor, c = rightColor, d = rightColor;
        if (vertical) { a = leftColor; b = rightColor; c = rightColor; d = leftColor; }
        helper.AddVert(new Vector3(rect.xMin, rect.yMin), a, Vector2.zero);
        helper.AddVert(new Vector3(rect.xMin, rect.yMax), b, Vector2.up);
        helper.AddVert(new Vector3(rect.xMax, rect.yMax), c, Vector2.one);
        helper.AddVert(new Vector3(rect.xMax, rect.yMin), d, Vector2.right);
        helper.AddTriangle(0, 1, 2);
        helper.AddTriangle(2, 3, 0);
    }
}
public class MenuButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] Image background;
    [SerializeField] Image accent;
    [SerializeField] Text label;
    [SerializeField] Text arrow;
    [SerializeField] Color idle;
    [SerializeField] Color hover;
    [SerializeField] Color highlight;
    AudioSource soundSource;
    AudioClip hoverSound;

    public void Configure(Image bg, Image accentBar, Text labelText, Text arrowText, Color idleColor, Color hoverColor, Color highlightColor)
    {
        background = bg; accent = accentBar; label = labelText; arrow = arrowText;
        idle = idleColor; hover = hoverColor; highlight = highlightColor;
    }

    public void SetSound(AudioSource source, AudioClip clip)
    {
        soundSource = source;
        hoverSound = clip;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EventSystem.current.SetSelectedGameObject(gameObject);
        SetActive(true);
        PlayHover();
    }
    public void OnPointerExit(PointerEventData eventData) { if (EventSystem.current.currentSelectedGameObject != gameObject) SetActive(false); }
    public void OnSelect(BaseEventData eventData) => SetActive(true);
    public void OnDeselect(BaseEventData eventData) => SetActive(false);

    Outline frame;
    Text detail;
    Text number;
    RectTransform[] slideTargets;
    Vector2[] slideOrigins;
    Vector2 arrowOrigin;
    bool active;
    float t;

    public void SetExtras(Outline frameOutline, Text detailText, Text numberText, RectTransform[] slide)
    {
        frame = frameOutline; detail = detailText; number = numberText; slideTargets = slide;
        slideOrigins = new Vector2[slide.Length];
        arrowOrigin = arrow != null ? arrow.rectTransform.anchoredPosition : Vector2.zero;
        for (int i = 0; i < slide.Length; i++) slideOrigins[i] = slide[i].anchoredPosition;
    }

    void SetActive(bool value)
    {
        active = value;
        if (background == null) return;
        background.color = active ? hover : idle;
        accent.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, active ? 10f : 4f);
        label.color = active ? Color.white : new Color(0.91f, 0.88f, 0.8f, 1f);
        arrow.color = active ? Color.white : highlight;
        if (frame != null) frame.effectColor = new Color(highlight.r, highlight.g, highlight.b, active ? 0.95f : 0.28f);
        if (detail != null) detail.color = active ? new Color(0.91f, 0.88f, 0.8f, 1f) : new Color(0.659f, 0.667f, 0.624f, 1f);
    }

    // Trượt nhẹ chữ sang phải và mũi tên nhích theo khi được chọn
    void Update()
    {
        if (slideTargets == null) return;
        t = Mathf.MoveTowards(t, active ? 1f : 0f, Time.unscaledDeltaTime * 9f);
        float e = Mathf.SmoothStep(0f, 1f, t);
        for (int i = 0; i < slideTargets.Length; i++)
            slideTargets[i].anchoredPosition = slideOrigins[i] + new Vector2(10f * e, 0f);
        if (arrow != null) arrow.rectTransform.anchoredPosition = arrowOrigin + new Vector2(-6f * e, 0f);
    }

    void PlayHover()
    {
        if (soundSource != null && hoverSound != null)
            soundSource.PlayOneShot(hoverSound, 0.35f * GameVolumeSettings.SfxVolume);
    }
}

public static class GameVolumeSettings
{
    const string MasterKey = "audio.master";
    const string MusicKey = "audio.music";
    const string SfxKey = "audio.sfx";

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(MasterKey, 1f);
        set { PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, 0.75f);
        set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxKey, 1f);
        set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static void ApplyMasterVolume() => AudioListener.volume = MasterVolume;
}
