using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
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
        SetupMenuMusic();
        SetupUISounds();
        StartCoroutine(Intro());
    }

    void Update()
    {
        if (menuPlaybackStarted && menuAudio != null && !menuAudio.isPlaying)
            PlayNextMenuTrack();

        if (GameInput.GetKeyDown(KeyCode.Escape))
        {
            if (creditsOverlay != null && creditsOverlay.activeSelf) ShowCredits(false);
            else if (settingsOverlay != null && settingsOverlay.activeSelf) ShowSettings(false);
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

        BindSlider(canvas, "Settings Overlay/Settings Card/MASTER VOLUME", GameVolumeSettings.MasterVolume, value =>
        {
            GameVolumeSettings.MasterVolume = value;
            GameVolumeSettings.ApplyMasterVolume();
        });
        BindSlider(canvas, "Settings Overlay/Settings Card/MUSIC VOLUME", GameVolumeSettings.MusicVolume, value =>
        {
            GameVolumeSettings.MusicVolume = value;
            RefreshMenuMusicVolume();
        });
        BindSlider(canvas, "Settings Overlay/Settings Card/SFX VOLUME", GameVolumeSettings.SfxVolume,
            value => GameVolumeSettings.SfxVolume = value);

        var fullscreen = canvas.Find("Settings Overlay/Settings Card/FULLSCREEN/Toggle")?.GetComponent<Toggle>();
        if (fullscreen != null)
        {
            fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
            fullscreen.onValueChanged.AddListener(value => Screen.fullScreen = value);
        }

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

    static void BindSlider(Transform root, string path, float value, UnityEngine.Events.UnityAction<float> action)
    {
        var row = root.Find(path);
        var slider = row?.Find("Slider")?.GetComponent<Slider>();
        var valueText = row?.Find("Value")?.GetComponent<Text>();
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);
        if (valueText != null) valueText.text = Mathf.RoundToInt(value * 100f) + "%";
        slider.onValueChanged.AddListener(newValue =>
        {
            if (valueText != null) valueText.text = Mathf.RoundToInt(newValue * 100f) + "%";
            action.Invoke(newValue);
        });
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
        SetRect(go.GetComponent<RectTransform>(), 0f, 1f, 1f, 1f, 0f, -index * 82f - 70f, 0f, -index * 82f);

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

        var accent = Image("Accent", go.transform, null, Brass);
        SetRect(accent.rectTransform, 0f, 0f, 0f, 1f, 0f, 0f, 5f, 0f);
        var numberText = Text("Number", go.transform, number, 14, Brass, TextAnchor.MiddleCenter, false);
        SetRect(numberText.rectTransform, 0f, 0f, 0f, 1f, 14f, 0f, 58f, 0f);
        var labelText = Text("Label", go.transform, label, 23, Ivory, TextAnchor.LowerLeft, false);
        SetRect(labelText.rectTransform, 0f, 0f, 1f, 1f, 70f, 25f, -55f, 0f);
        var detailText = Text("Detail", go.transform, detail, 13, Muted, TextAnchor.UpperLeft, false);
        SetRect(detailText.rectTransform, 0f, 0f, 1f, 1f, 71f, 0f, -55f, -43f);
        var arrow = Text("Arrow", go.transform, "›", 34, Brass, TextAnchor.MiddleCenter, false);
        SetRect(arrow.rectTransform, 1f, 0f, 1f, 1f, -52f, 0f, -10f, 0f);

        go.GetComponent<MenuButtonVisual>().Configure(image, accent, labelText, arrow, ButtonIdle, ButtonHover, Brass);
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

    void BuildCredits(Transform parent)
    {
        creditsOverlay = RectObject("Credits Overlay", parent).gameObject;
        Stretch(creditsOverlay.GetComponent<RectTransform>());
        creditsOverlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        var modal = Image("Credits Card", creditsOverlay.transform, null, new Color(0.035f, 0.05f, 0.055f, 0.99f));
        SetRect(modal.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, -390f, -255f, 390f, 255f);
        var top = Image("Top Rule", modal.transform, null, Brass);
        SetRect(top.rectTransform, 0f, 1f, 1f, 1f, 0f, -5f, 0f, 0f);
        var heading = Text("Heading", modal.transform, "FIELD ARCHIVES", 42, Ivory, TextAnchor.MiddleCenter, true);
        SetRect(heading.rectTransform, 0f, 1f, 1f, 1f, 40f, -98f, -40f, -30f);
        var sub = Text("Subheading", modal.transform, "ATTACK ON TITAN: DIVERGENCE", 16, Brass, TextAnchor.MiddleCenter, false);
        SetRect(sub.rectTransform, 0f, 1f, 1f, 1f, 40f, -132f, -40f, -100f);
        var body = Text("Body", modal.transform,
            "A FAN-MADE UNITY PROTOTYPE\n\nDesign & Development  /  Divergence Team\nBackground artwork and type assets supplied with the project.\n\nThank you for playing.",
            18, Muted, TextAnchor.MiddleCenter, false);
        SetRect(body.rectTransform, 0f, 0f, 1f, 1f, 55f, 105f, -55f, -150f);

        var close = SimpleButton(modal.transform, "CLOSE ARCHIVES", () => ShowCredits(false));
        SetRect(close.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, -150f, 34f, 150f, 92f);
        creditsOverlay.SetActive(false);
    }

    void BuildSettings(Transform parent)
    {
        settingsOverlay = RectObject("Settings Overlay", parent).gameObject;
        Stretch(settingsOverlay.GetComponent<RectTransform>());
        settingsOverlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

        var modal = Image("Settings Card", settingsOverlay.transform, null, new Color(0.035f, 0.05f, 0.055f, 0.99f));
        SetRect(modal.rectTransform, 0.5f, 0.5f, 0.5f, 0.5f, -420f, -330f, 420f, 330f);
        var top = Image("Top Rule", modal.transform, null, Brass);
        SetRect(top.rectTransform, 0f, 1f, 1f, 1f, 0f, -5f, 0f, 0f);

        var heading = Text("Heading", modal.transform, "SETTINGS", 46, Ivory, TextAnchor.MiddleLeft, true);
        SetRect(heading.rectTransform, 0f, 1f, 1f, 1f, 54f, -92f, -54f, -27f);
        var sub = Text("Subheading", modal.transform, "SYSTEM CONFIGURATION  /  CHANGES SAVE AUTOMATICALLY", 14, Brass, TextAnchor.MiddleLeft, false);
        SetRect(sub.rectTransform, 0f, 1f, 1f, 1f, 56f, -126f, -54f, -96f);

        var section = Text("Audio Section", modal.transform, "AUDIO", 15, Muted, TextAnchor.MiddleLeft, false);
        SetRect(section.rectTransform, 0f, 1f, 1f, 1f, 56f, -176f, -54f, -145f);
        var sectionRule = Image("Audio Rule", modal.transform, null, new Color(Brass.r, Brass.g, Brass.b, 0.45f));
        SetRect(sectionRule.rectTransform, 0f, 1f, 1f, 1f, 56f, -179f, -54f, -177f);

        SettingSlider(modal.transform, "MASTER VOLUME", GameVolumeSettings.MasterVolume, 212f, value =>
        {
            GameVolumeSettings.MasterVolume = value;
            GameVolumeSettings.ApplyMasterVolume();
        });
        SettingSlider(modal.transform, "MUSIC VOLUME", GameVolumeSettings.MusicVolume, 292f, value =>
        {
            GameVolumeSettings.MusicVolume = value;
            RefreshMenuMusicVolume();
        });
        SettingSlider(modal.transform, "SFX VOLUME", GameVolumeSettings.SfxVolume, 372f, value =>
            GameVolumeSettings.SfxVolume = value);

        var displaySection = Text("Display Section", modal.transform, "DISPLAY", 15, Muted, TextAnchor.MiddleLeft, false);
        SetRect(displaySection.rectTransform, 0f, 1f, 1f, 1f, 56f, -456f, -54f, -425f);
        var displayRule = Image("Display Rule", modal.transform, null, new Color(Brass.r, Brass.g, Brass.b, 0.45f));
        SetRect(displayRule.rectTransform, 0f, 1f, 1f, 1f, 56f, -459f, -54f, -457f);
        SettingToggle(modal.transform, "FULLSCREEN", Screen.fullScreen, 488f, value => Screen.fullScreen = value);

        var close = SimpleButton(modal.transform, "APPLY & CLOSE", () => ShowSettings(false));
        SetRect(close.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, -170f, 34f, 170f, 92f);
        settingsOverlay.SetActive(false);
    }

    void SettingSlider(Transform parent, string label, float initialValue, float topOffset, UnityEngine.Events.UnityAction<float> changed)
    {
        var row = RectObject(label, parent);
        SetRect(row, 0f, 1f, 1f, 1f, 56f, -topOffset - 56f, -56f, -topOffset);
        var labelText = Text("Label", row, label, 17, Ivory, TextAnchor.UpperLeft, false);
        SetRect(labelText.rectTransform, 0f, 0f, 0.45f, 1f, 0f, 0f, 0f, 0f);
        var valueText = Text("Value", row, Mathf.RoundToInt(initialValue * 100f) + "%", 16, Brass, TextAnchor.UpperRight, false);
        SetRect(valueText.rectTransform, 0.8f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);

        var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(row, false);
        SetRect(sliderObject.GetComponent<RectTransform>(), 0f, 0f, 1f, 0f, 0f, 2f, 0f, 16f);
        var slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = initialValue;

        var track = Image("Track", sliderObject.transform, null, new Color(0.18f, 0.2f, 0.2f, 1f));
        Stretch(track.rectTransform);
        var fillArea = RectObject("Fill Area", sliderObject.transform);
        Stretch(fillArea);
        var fill = Image("Fill", fillArea, null, Brass);
        Stretch(fill.rectTransform);
        var handleArea = RectObject("Handle Slide Area", sliderObject.transform);
        Stretch(handleArea);
        var handle = Image("Handle", handleArea, null, Ivory);
        SetRect(handle.rectTransform, 0f, 0.5f, 0f, 0.5f, -7f, -11f, 7f, 11f);
        handle.raycastTarget = true;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.onValueChanged.AddListener(value =>
        {
            valueText.text = Mathf.RoundToInt(value * 100f) + "%";
            changed.Invoke(value);
        });
    }

    void SettingToggle(Transform parent, string label, bool initialValue, float topOffset, UnityEngine.Events.UnityAction<bool> changed)
    {
        var row = RectObject(label, parent);
        SetRect(row, 0f, 1f, 1f, 1f, 56f, -topOffset - 54f, -56f, -topOffset);
        var labelText = Text("Label", row, label, 17, Ivory, TextAnchor.MiddleLeft, false);
        SetRect(labelText.rectTransform, 0f, 0f, 0.7f, 1f, 0f, 0f, 0f, 0f);

        var toggleObject = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
        toggleObject.transform.SetParent(row, false);
        SetRect(toggleObject.GetComponent<RectTransform>(), 1f, 0.5f, 1f, 0.5f, -54f, -18f, 0f, 18f);
        var background = toggleObject.GetComponent<Image>(); background.color = ButtonIdle;
        var check = Image("Checkmark", toggleObject.transform, null, Brass);
        SetRect(check.rectTransform, 0f, 0f, 1f, 1f, 7f, 7f, -7f, -7f);
        var toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = background; toggle.graphic = check; toggle.isOn = initialValue;
        toggle.onValueChanged.AddListener(changed);
    }

    Button SimpleButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.color = ButtonIdle;
        var button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        var text = Text("Label", go.transform, label, 17, Ivory, TextAnchor.MiddleCenter, false);
        Stretch(text.rectTransform);
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

public class HorizontalGradientGraphic : MaskableGraphic
{
    [SerializeField] Color leftColor = Color.black;
    [SerializeField] Color rightColor = Color.clear;

    public Color LeftColor { get => leftColor; set { leftColor = value; SetVerticesDirty(); } }
    public Color RightColor { get => rightColor; set { rightColor = value; SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = GetPixelAdjustedRect();
        helper.AddVert(new Vector3(rect.xMin, rect.yMin), leftColor, Vector2.zero);
        helper.AddVert(new Vector3(rect.xMin, rect.yMax), leftColor, Vector2.up);
        helper.AddVert(new Vector3(rect.xMax, rect.yMax), rightColor, Vector2.one);
        helper.AddVert(new Vector3(rect.xMax, rect.yMin), rightColor, Vector2.right);
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

    void SetActive(bool active)
    {
        if (background == null) return;
        background.color = active ? hover : idle;
        accent.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, active ? 9f : 5f);
        label.color = active ? Color.white : new Color(0.91f, 0.88f, 0.8f, 1f);
        arrow.color = active ? Color.white : highlight;
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
