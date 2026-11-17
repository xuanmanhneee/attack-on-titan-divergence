using UnityEngine;

// Nhạc nền trong trận, một bài dài chia hai đoạn:
//   Thường : lặp từ 0:00 tới loopEnd, đến gần cuối thì crossfade về 0:00 (không nhảy cứng).
//   Titan  : SetTitan(true) crossfade sang đoạn còn lại (từ loopEnd tới hết bài). Hết bài thì crossfade
//            về 0:00 và quay lại vòng lặp thường. Có thể cho quay về sớm khi hết titan (returnOnRevert).
// Dùng thời gian thật (unscaled) nên vẫn chạy mượt khi thế giới đứng hình lúc biến hình hoặc đang pause.
// Hai AudioSource luân phiên nhau để chồng tiếng lúc chuyển.
[DisallowMultipleComponent]
public class BackgroundMusicPlayer : MonoBehaviour
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.24f;
    public float loopEnd = 140f;          // giây: hết đoạn nhạc thường
    public float loopFade = 3f;           // crossfade khi lặp lại
    public bool returnOnRevert = false;   // true: hết titan là chuyển ngay về đoạn thường

    enum Mode { Normal, Titan }

    AudioSource[] src = new AudioSource[2];
    float[] weight = new float[2];
    int cur;
    Mode mode = Mode.Normal;
    bool fading;
    float fadeT, fadeDur = 1f;
    float duck = 1f, duckTarget = 1f;
    bool started;

    public bool IsTitanSection => mode == Mode.Titan;

    void Awake()
    {
        for (int i = 0; i < 2; i++)
        {
            src[i] = gameObject.AddComponent<AudioSource>();
            src[i].playOnAwake = false;
            src[i].loop = false;
            src[i].spatialBlend = 0f;
            src[i].volume = 0f;
            src[i].ignoreListenerPause = true; // pause game không làm nhạc im bất thường
        }
    }

    void Start() { StartPlaying(); }

    void StartPlaying()
    {
        if (started || clip == null) return;
        started = true;
        cur = 0;
        src[0].clip = clip;
        src[0].time = 0f;
        weight[0] = 1f; weight[1] = 0f;
        src[0].Play();
    }

    // fade: số giây crossfade. Chỉ chuyển khi đang ở đoạn thường.
    // restart: true = luôn nhảy về đầu đoạn titan (loopEnd) kể cả khi đang ở đoạn titan rồi (ghi đè); dùng cho lúc hoá titan.
    public void SetTitan(bool on, float fade = 1.5f, bool restart = false)
    {
        if (!started) StartPlaying();
        if (!started) return;
        if (on && (mode == Mode.Normal || restart))
        {
            mode = Mode.Titan;
            float start = Mathf.Min(loopEnd, clip.length - 1f);
            CrossfadeTo(start, fade);
        }
        else if (!on && mode == Mode.Titan && returnOnRevert)
        {
            mode = Mode.Normal;
            CrossfadeTo(0f, Mathf.Max(fade, 2f));
        }
    }

    // 1 = bình thường, nhỏ hơn = nhạc hạ xuống (vd lúc thời gian đứng).
    public void SetDuck(float level) { duckTarget = Mathf.Clamp01(level); }

    void CrossfadeTo(float time, float dur)
    {
        int next = 1 - cur;
        // Nếu đang crossfade dở thì cắt nguồn cũ để lấy chỗ.
        src[next].clip = clip;
        src[next].time = Mathf.Clamp(time, 0f, clip.length - 0.1f);
        src[next].Play();
        weight[next] = 0f;
        cur = next;
        fading = true;
        fadeT = 0f;
        fadeDur = Mathf.Max(0.05f, dur);
    }

    void Update()
    {
        if (!started) { StartPlaying(); return; }
        float dt = Time.unscaledDeltaTime;
        int old = 1 - cur;

        duck = Mathf.MoveTowards(duck, duckTarget, dt * 2f);

        if (fading)
        {
            fadeT += dt / fadeDur;
            float p = Mathf.Clamp01(fadeT);
            // Công suất đều: cos/sin thay vì tuyến tính nên chỗ giao không bị lõm âm lượng.
            weight[cur] = Mathf.Sin(p * Mathf.PI * 0.5f);
            weight[old] = Mathf.Cos(p * Mathf.PI * 0.5f);
            if (p >= 1f) { fading = false; weight[old] = 0f; src[old].Stop(); }
        }
        else
        {
            AudioSource s = src[cur];
            float t = s.time;
            if (mode == Mode.Normal)
            {
                // Sắp tới cuối đoạn thường: bắt đầu chồng đầu bài vào.
                if (t >= loopEnd - loopFade || !s.isPlaying) CrossfadeTo(0f, loopFade);
            }
            else
            {
                // Đoạn titan chạy tới hết bài rồi lặp về đầu và quay lại đoạn thường.
                if (t >= clip.length - loopFade - 0.05f || !s.isPlaying)
                {
                    mode = Mode.Normal;
                    CrossfadeTo(0f, loopFade);
                }
            }
        }

        float master = volume * GameSettings.Music * duck;
        for (int i = 0; i < 2; i++) src[i].volume = master * weight[i];
    }
}
