using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Cutscene mở đầu game: phát Resources/Vfx/cutscene_open_game.mp4 toàn màn hình (bật/tắt trong Cài đặt).
// Khi video còn khoảng 2 giây thì nhạc menu bắt đầu (onMusic), khi còn khoảng 1,5 giây thì video mờ dần để lộ menu
// phía sau và menu trượt vào (onTransition). Space / Esc / Enter / click = bỏ qua (cũng chuyển tiếp mượt vào menu).
// (Cutscene hoá titan là video transform_vfx trong TitanForm, không qua lớp này.)
public class Cutscene : MonoBehaviour
{
    public const string ResourcePath = "Vfx/cutscene_open_game";
    public const float MusicLead = 2.4f;       // giây còn lại của video lúc nhạc menu bắt đầu
    public const float TransitionLead = 1.5f;  // giây còn lại của video lúc bắt đầu chuyển vào menu
    const float SkipFade = 0.4f;

    public static bool IsPlaying { get; private set; }

    // onMusic: tới lúc cho nhạc menu phát. onTransition: tới lúc hiện menu. Không phát (tắt trong Cài đặt / thiếu video) thì gọi ngay cả hai.
    public static void PlayPrologue(Action onMusic, Action onTransition)
    {
        VideoClip clip = null;
        if (GameSettings.WatchPrologue && !IsPlaying) clip = Resources.Load<VideoClip>(ResourcePath);
        if (clip == null)
        {
            if (onMusic != null) onMusic();
            if (onTransition != null) onTransition();
            return;
        }
        var go = new GameObject("Cutscene");
        go.AddComponent<Cutscene>().Begin(clip, onMusic, onTransition);
    }

    CanvasGroup group;
    RawImage videoImage;
    VideoPlayer video;
    RenderTexture videoTex;
    float length;
    bool started, musicFired, transitionFired, done;
    float skipTimer = -1f; // >= 0: đang bỏ qua (mờ nhanh rồi kết thúc)
    Action onMusic, onTransition;

    void Awake()
    {
        IsPlaying = true;
        BuildUI();
    }

    void Begin(VideoClip clip, Action music, Action transition)
    {
        onMusic = music;
        onTransition = transition;
        length = (float)clip.length;

        videoTex = new RenderTexture((int)Mathf.Max(16, clip.width), (int)Mathf.Max(16, clip.height), 0);
        videoImage.texture = videoTex;
        var fit = videoImage.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; // giữ tỉ lệ, viền đen nếu khác màn hình
        fit.aspectRatio = (float)clip.width / Mathf.Max(1, (float)clip.height);

        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.clip = clip;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = videoTex;
        video.audioOutputMode = VideoAudioOutputMode.Direct;
        video.controlledAudioTrackCount = 1;
        video.isLooping = false;
        video.prepareCompleted += _ => { Debug.Log("[Cutscene] Prepare xong, phát video " + length.ToString("0.0") + "s"); SetVolume(1f); video.Play(); started = true; };
        video.loopPointReached += _ => { Debug.Log("[Cutscene] Video hết"); Finish(); };
        video.errorReceived += (_, msg) => { Debug.LogWarning("[Cutscene] Lỗi video: " + msg); Skip(); };
        video.Prepare();
    }

    // Audio Direct không đi qua AudioListener: tự nhân âm lượng Master × Nhạc.
    void SetVolume(float fade)
    {
        if (video == null || video.audioTrackCount <= 0) return;
        video.SetDirectAudioVolume(0, GameVolumeSettings.MasterVolume * GameVolumeSettings.MusicVolume * fade);
    }

    void Update()
    {
        if (done) return;

        if (skipTimer >= 0f)
        {
            skipTimer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(1f - skipTimer / SkipFade);
            group.alpha = k;
            SetVolume(k);
            if (skipTimer >= SkipFade) Finish();
            return;
        }

        if (GameInput.GetKeyDownRaw(KeyCode.Escape) || GameInput.GetKeyDownRaw(KeyCode.Space)
            || GameInput.GetKeyDownRaw(KeyCode.Return) || GameInput.GetMouseButtonDown(0))
        {
            Debug.Log("[Cutscene] Người chơi bỏ qua (started=" + started + ", t=" + (video != null ? video.time : 0) + ")");
            Skip();
            return;
        }
        if (!started) return;

        if (videoImage.color.a < 1f && video.frame > 0) videoImage.color = Color.white; // chỉ hiện khi đã có khung hình

        float remaining = length - (float)video.time;
        if (!musicFired && remaining <= MusicLead) FireMusic();
        if (remaining <= TransitionLead)
        {
            FireTransition();
            float k = Mathf.SmoothStep(1f, 0f, 1f - Mathf.Clamp01(remaining / TransitionLead));
            group.alpha = k;
            SetVolume(k);
        }
    }

    void FireMusic()
    {
        if (musicFired) return;
        musicFired = true;
        if (onMusic != null) onMusic();
    }

    void FireTransition()
    {
        if (transitionFired) return;
        transitionFired = true;
        if (onTransition != null) onTransition();
    }

    // Bỏ qua / lỗi: nhạc và menu vào ngay, cutscene mờ nhanh rồi tắt.
    void Skip()
    {
        if (skipTimer >= 0f || done) return;
        FireMusic();
        FireTransition();
        skipTimer = 0f;
    }

    void Finish()
    {
        if (done) return;
        done = true;
        FireMusic();
        FireTransition();
        if (video != null) video.Stop();
        IsPlaying = false;
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        IsPlaying = false;
        if (videoTex != null) { videoTex.Release(); Destroy(videoTex); }
    }

    // ---------- UI ----------
    void BuildUI()
    {
        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        group = canvasGo.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false; // click bỏ qua đọc qua GameInput, không chặn menu phía sau khi đã mờ đi

        var bg = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        bg.transform.SetParent(canvasGo.transform, false);
        bg.color = Color.black;
        bg.raycastTarget = false;
        Stretch(bg.rectTransform);

        videoImage = new GameObject("Video", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        videoImage.transform.SetParent(canvasGo.transform, false);
        videoImage.raycastTarget = false;
        videoImage.color = Color.clear; // ẩn tới khi có khung hình đầu
        Stretch(videoImage.rectTransform);

        var hint = new GameObject("Hint", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        hint.transform.SetParent(canvasGo.transform, false);
        hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontSize = 22;
        hint.color = new Color(1f, 1f, 1f, 0.45f);
        hint.alignment = TextAnchor.LowerRight;
        hint.raycastTarget = false;
        hint.text = "SPACE / ESC: bỏ qua";
        var r = hint.rectTransform;
        r.anchorMin = new Vector2(0.5f, 0f);
        r.anchorMax = new Vector2(0.98f, 0.05f);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }
}
