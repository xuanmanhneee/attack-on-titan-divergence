using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Tooltip("Optional explicit playlist. If empty, every AudioClip in Resources/Audio/Music is loaded automatically.")]
    [SerializeField] AudioClip[] menuPlaylist = null;
    [SerializeField] bool shuffleMenuPlaylist = false;
    [Range(0f, 1f)] [SerializeField] float menuMusicBaseVolume = 0.65f;

    static readonly Color Ink = new Color(0.043f, 0.067f, 0.078f, 1f);

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
        MangaSfx.Slam();
        GameSettings.Changed += RefreshMenuMusicVolume;
    }

    void OnDestroy()
    {
        GameSettings.Changed -= RefreshMenuMusicVolume;
    }

    // Hiệu ứng trượt vào kiểu MangaMenu: title từ trái, thanh trên từ trên, nút từ phải lần lượt.
    // MangaSlideIn tự chạy lại mỗi khi nhóm nút được bật (START <-> CHAPTER).
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
            Debug.LogError("[MainMenu] Thiếu \"Main Menu Canvas\" trong scene MainMenu.", this);
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
}
