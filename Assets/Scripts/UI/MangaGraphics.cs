using UnityEngine;
using UnityEngine.UI;

// Bộ "đồ nghề" vẽ giao diện kiểu manga bằng code, không cần ảnh:
//  - MangaStyle: màu (mực, giấy, đỏ nhấn) và font
//  - MangaTextures: chấm screentone sinh lúc chạy
//  - MangaPanel: khung truyện nghiêng, viền mực, đổ bóng đặc
//  - MangaSpeedLines: đường tốc độ tỏa ra từ tâm
//  - MangaBurst: hình ngôi sao nổ (khung chữ tượng thanh)
//  - MangaSlideIn: hiệu ứng trượt / bật vào
//  - MangaSfx: âm thanh giao diện tự tổng hợp (có thể thay bằng file trong Resources/UI/Audio)

public static class MangaStyle
{
    // Bảng màu theo giao diện của nhánh codex (menu chính): nền tối xanh đen, chữ ngà, nhấn đồng.
    // Tên biến giữ nguyên để code menu không phải đổi: Ink = màu chữ/viền sáng, Paper = nền tối,
    // Red = màu nhấn (đồng), Gray = chữ phụ.
    public static readonly Color Ink = new Color(0.910f, 0.878f, 0.796f, 1f);   // E8E0CB
    public static readonly Color Paper = new Color(0.035f, 0.055f, 0.062f, 1f);
    public static readonly Color Red = new Color(0.753f, 0.541f, 0.243f, 1f);   // C08A3E
    public static readonly Color Gray = new Color(0.659f, 0.667f, 0.624f, 1f);  // A8AA9F
    public static readonly Color ButtonHover = new Color(0.18f, 0.13f, 0.075f, 0.96f);
    public const string AccentHex = "#C08A3E";

    static Font titleFont;
    static Texture2D background;
    // Font tiêu đề (Ditty) và ảnh nền dùng chung với menu của codex, nằm trong Resources/UI
    public static Font TitleFont { get { if (titleFont == null) titleFont = Resources.Load<Font>("UI/Ditty"); return titleFont != null ? titleFont : Font; } }
    public static Texture2D Background { get { if (background == null) background = Resources.Load<Texture2D>("UI/background"); return background; } }

    static Font font;

    // Ưu tiên font manga bỏ vào Assets/Resources/UI/MangaFont (ttf/otf). Không có thì dùng font hệ thống đậm nét
    // (chọn các font có đủ dấu tiếng Việt).
    public static Font Font
    {
        get
        {
            if (font == null) font = LoadFont();
            return font;
        }
    }

    static Font LoadFont()
    {
        Font f = null;
        try
        {
            f = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Black", "Segoe UI", "Arial Black", "Arial", "Helvetica Neue", "Helvetica" }, 48);
        }
        catch (System.Exception) { f = null; }
        if (f != null) return f;

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}

public static class MangaTextures
{
    static Texture2D vignette;
    static readonly System.Collections.Generic.Dictionary<int, Texture2D> dots = new System.Collections.Generic.Dictionary<int, Texture2D>();

    // Ô screentone lặp được: chấm xếp so le, coverage 0..1 (độ đậm). Kích thước 64 px, chu kỳ 8 px.
    public static Texture2D Dots(float coverage)
    {
        int key = Mathf.RoundToInt(Mathf.Clamp01(coverage) * 100f);
        Texture2D t;
        if (dots.TryGetValue(key, out t) && t != null) return t;

        const int size = 64, period = 8;
        var pix = new Color32[size * size];
        float radius = key / 100f * period * 0.72f;
        for (int y = 0; y < size; y++)
        {
            int row = y / period;
            int offset = (row % 2 == 1) ? period / 2 : 0;
            float ly = (y % period) + 0.5f - period / 2f;
            for (int x = 0; x < size; x++)
            {
                float lx = ((x + offset) % period) + 0.5f - period / 2f;
                float d = Mathf.Sqrt(lx * lx + ly * ly);
                float a = Mathf.Clamp01(radius - d + 0.5f);
                pix[y * size + x] = new Color32(13, 13, 15, (byte)(a * 255f));
            }
        }
        t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Bilinear;
        t.SetPixels32(pix);
        t.Apply();
        dots[key] = t;
        return t;
    }

    // Screentone chuyển sắc: chấm nhỏ ở giữa, to dần ra bốn góc (làm tối viền trang như trang truyện in)
    public static Texture2D Vignette()
    {
        if (vignette != null) return vignette;

        const int w = 640, h = 360, period = 6;
        var pix = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            int row = y / period;
            int offset = (row % 2 == 1) ? period / 2 : 0;
            float ly = (y % period) + 0.5f - period / 2f;
            float ny = (y / (float)h - 0.5f) * 2f;
            for (int x = 0; x < w; x++)
            {
                float nx = (x / (float)w - 0.5f) * 2f;
                float dist = Mathf.Sqrt(nx * nx * 0.8f + ny * ny) / 1.35f;
                float cov = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1.0f, dist)) * 0.9f;
                float lx = ((x + offset) % period) + 0.5f - period / 2f;
                float d = Mathf.Sqrt(lx * lx + ly * ly);
                float a = Mathf.Clamp01(cov * period * 0.72f - d + 0.5f);
                pix[y * w + x] = new Color32(13, 13, 15, (byte)(a * 255f));
            }
        }
        vignette = new Texture2D(w, h, TextureFormat.RGBA32, false);
        vignette.wrapMode = TextureWrapMode.Clamp;
        vignette.filterMode = FilterMode.Bilinear;
        vignette.SetPixels32(pix);
        vignette.Apply();
        return vignette;
    }
}

// Đường tốc độ tỏa ra từ tâm. Mỗi ~0.1 giây đổi hạt giống một lần cho cảm giác nét vẽ tay rung nhẹ.
[RequireComponent(typeof(CanvasRenderer))]
public class MangaSpeedLines : MaskableGraphic
{
    public int count = 90;
    public float inner = 0.30f;           // vùng trống ở giữa (tỉ lệ bán kính)
    public Vector2 focus = Vector2.zero;  // lệch tâm, theo tỉ lệ kích thước (-0.5..0.5)
    public int seed = 1;
    public bool boil = true;
    public float fps = 9f;

    float acc;

    void Update()
    {
        if (!boil) return;
        acc += Time.unscaledDeltaTime;
        if (acc >= 1f / fps)
        {
            acc = 0f;
            seed++;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        return; // không vẽ đường tốc độ trong giao diện mới
#pragma warning disable CS0162
        Rect r = GetPixelAdjustedRect();
        Vector2 c = r.center + new Vector2(focus.x * r.width, focus.y * r.height);
        float R = Mathf.Sqrt(r.width * r.width + r.height * r.height) * 0.8f;
        var rng = new System.Random(seed);

        for (int i = 0; i < count; i++)
        {
            float a = (i + (float)rng.NextDouble() * 0.9f) / count * Mathf.PI * 2f;
            float tipR = inner * R * (0.85f + (float)rng.NextDouble() * 0.8f);
            float half = 0.004f + (float)rng.NextDouble() * 0.014f;
            if (rng.NextDouble() < 0.14) half *= 2.2f;

            Vector2 tip = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * tipR;
            Vector2 p1 = c + new Vector2(Mathf.Cos(a - half), Mathf.Sin(a - half)) * R;
            Vector2 p2 = c + new Vector2(Mathf.Cos(a + half), Mathf.Sin(a + half)) * R;

            int n = vh.currentVertCount;
            vh.AddVert(tip, color, Vector2.zero);
            vh.AddVert(p1, color, Vector2.zero);
            vh.AddVert(p2, color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
        }
    }
}

// Ngôi sao nổ nhiều cạnh nhọn (khung chữ tượng thanh kiểu "ẦM!!")
[RequireComponent(typeof(CanvasRenderer))]
public class MangaBurst : MaskableGraphic
{
    public int points = 16;
    public float innerRatio = 0.68f;
    public float jitter = 0.14f;
    public int seed = 3;
    public float rotation = 0f;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        return; // không vẽ hình nổ trong giao diện mới
#pragma warning disable CS0162
        Rect r = GetPixelAdjustedRect();
        Vector2 c = r.center;
        float rx = r.width * 0.5f, ry = r.height * 0.5f;
        var rng = new System.Random(seed);

        int n = points * 2;
        vh.AddVert(c, color, Vector2.zero);
        for (int k = 0; k < n; k++)
        {
            float ang = rotation * Mathf.Deg2Rad + k * Mathf.PI / points;
            float ratio = (k % 2 == 0) ? 1f - jitter * (float)rng.NextDouble() : innerRatio;
            vh.AddVert(c + new Vector2(Mathf.Cos(ang) * rx * ratio, Mathf.Sin(ang) * ry * ratio), color, Vector2.zero);
        }
        for (int k = 0; k < n; k++)
            vh.AddTriangle(0, 1 + k, 1 + (k + 1) % n);
    }
}


// Âm thanh giao diện. Không có file thì tự tổng hợp vài tiếng ngắn; muốn đổi thì bỏ file vào
// Assets/Resources/UI/Audio/ với tên: hover, click, back, slam, menu_music
public static class MangaSfx
{
    static AudioSource sfx;
    static AudioSource music;
    static AudioClip hover, click, back, slam, musicClip;
    static bool built;
    public static bool ExternalMusic; // true: menu codex đã tự phát nhạc, không phát nhạc tổng hợp

    static void Ensure()
    {
        if (sfx != null && built) return;

        var go = new GameObject("MangaAudio");
        Object.DontDestroyOnLoad(go);
        sfx = go.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;
        music = go.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.spatialBlend = 0f;
        music.loop = true;

        hover = Resources.Load<AudioClip>("UI/Audio/hover") ?? Synth("hover", 0.07f, (t, p) => Mathf.Sin(6.2832f * 1800f * t) * Mathf.Exp(-t * 70f) * 0.5f);
        click = Resources.Load<AudioClip>("UI/Audio/click") ?? Synth("click", 0.22f, (t, p) =>
            Mathf.Sin(6.2832f * 160f * t) * Mathf.Exp(-t * 16f) * 0.8f + (Hash(t) - 0.5f) * Mathf.Exp(-t * 45f) * 0.7f);
        back = Resources.Load<AudioClip>("UI/Audio/back") ?? Synth("back", 0.12f, (t, p) => Mathf.Sin(6.2832f * 420f * t) * Mathf.Exp(-t * 28f) * 0.6f);
        slam = Resources.Load<AudioClip>("UI/Audio/slam") ?? Synth("slam", 0.7f, (t, p) =>
            Mathf.Sin(6.2832f * (110f - 70f * p) * t) * Mathf.Exp(-t * 6f) * 0.9f + (Hash(t) - 0.5f) * Mathf.Exp(-t * 18f) * 0.6f);
        musicClip = Resources.Load<AudioClip>("UI/Audio/menu_music");
        if (musicClip == null) musicClip = GameAudio.MenuMusic(); // không có file thì dùng nhạc tổng hợp
        built = true;
    }

    static float Hash(float t)
    {
        float x = Mathf.Sin(t * 12989.8f) * 43758.5453f;
        return x - Mathf.Floor(x);
    }

    static AudioClip Synth(string name, float seconds, System.Func<float, float, float> f)
    {
        const int rate = 44100;
        int n = Mathf.Max(1, (int)(seconds * rate));
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)rate, i / (float)n), -1f, 1f);
        var c = AudioClip.Create(name, n, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }

    static void Play(AudioClip c, float volume)
    {
        Ensure();
        if (c != null) sfx.PlayOneShot(c, volume * GameSettings.Sfx);
    }

    public static void Hover() { Play(Get(0), 0.45f); }
    public static void Click() { Play(Get(1), 0.8f); }
    public static void Back() { Play(Get(2), 0.7f); }
    public static void Slam() { Play(Get(3), 0.9f); }

    static AudioClip Get(int i)
    {
        Ensure();
        switch (i) { case 0: return hover; case 1: return click; case 2: return back; default: return slam; }
    }

    // Nhạc nền menu (chỉ phát nếu có file menu_music)
    public static void Music(bool on)
    {
        Ensure();
        if (musicClip == null || ExternalMusic) return;
        music.volume = Mathf.Clamp01(GameSettings.Music);
        if (on && !music.isPlaying) { music.clip = musicClip; music.Play(); }
        else if (!on && music.isPlaying) music.Stop();
    }

    public static void RefreshVolume()
    {
        if (music != null) music.volume = Mathf.Clamp01(GameSettings.Music);
    }
}
