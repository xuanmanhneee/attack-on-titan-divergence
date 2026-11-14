using System;
using System.Collections.Generic;
using UnityEngine;

// Toàn bộ cài đặt của người chơi (lưu bằng PlayerPrefs).
// Giao diện chỉnh các biến public rồi gọi Commit(); các hệ thống khác lắng nghe sự kiện Changed
// (hoặc đọc thẳng giá trị) để áp dụng.
public static class GameSettings
{
    public static event Action Changed;

    // ---------- Điều khiển ----------
    public static float MouseSens = 1f;     // hệ số nhân với độ nhạy gốc của ODMController
    public static bool InvertY = false;
    public static float Fov = 75f;
    public static bool SlowMo = true;       // làm chậm thời gian khi ở trên không
    public static bool SmartAim = true;     // ngắm hỗ trợ
    public static bool AutoReel = true;     // tự thu dây nhẹ khi nhìn về phía móc
    public static bool AutoRelease = true;  // tự nhả dây khi vượt điểm neo
    public static bool AnchorLift = true;   // nâng điểm neo lên mái nhà

    // ---------- Âm thanh ----------
    // Dùng chung với menu codex (GameVolumeSettings) để một thanh âm lượng duy nhất cho cả game
    public static float Master { get { return GameVolumeSettings.MasterVolume; } set { GameVolumeSettings.MasterVolume = value; } }
    public static float Music { get { return GameVolumeSettings.MusicVolume; } set { GameVolumeSettings.MusicVolume = value; } }
    public static float Sfx { get { return GameVolumeSettings.SfxVolume; } set { GameVolumeSettings.SfxVolume = value; } }

    // ---------- Đồ họa ----------
    public static int Quality = -1;         // -1 = giữ nguyên mức hiện tại của dự án
    public static bool Fullscreen = true;
    public static int ResW, ResH;           // 0 = giữ nguyên độ phân giải hiện tại
    public static bool VSync = true;
    public static int FpsLimit = 0;         // 0 = không giới hạn
    public static readonly int[] FpsOptions = { 0, 30, 60, 90, 120, 144, 240 };

    // ---------- Phím tắt ----------
    // Code game luôn hỏi theo phím MẶC ĐỊNH (DefaultKeys); GameInput đổi sang phím người chơi đã gán.
    public static readonly string[] ActionLabels =
    {
        "Bắn móc trái", "Bắn móc phải", "Thu dây mạnh", "Gas / lướt", "Chém", "Thay kiếm", "Làm chậm thời gian"
    };
    public static readonly KeyCode[] DefaultKeys =
    {
        KeyCode.Q, KeyCode.E, KeyCode.Space, KeyCode.LeftShift, KeyCode.F, KeyCode.R, KeyCode.T
    };
    static readonly KeyCode[] keys = (KeyCode[])DefaultKeys.Clone();

    public static KeyCode KeyOf(int action) { return keys[action]; }

    public static KeyCode Remap(KeyCode defaultKey)
    {
        for (int i = 0; i < DefaultKeys.Length; i++)
            if (DefaultKeys[i] == defaultKey) return keys[i];
        return defaultKey;
    }

    // Gán phím mới cho một hành động. Phím đã có hành động khác dùng thì hai hành động đổi chỗ cho nhau.
    public static void Rebind(int action, KeyCode newKey)
    {
        for (int j = 0; j < keys.Length; j++)
            if (j != action && keys[j] == newKey) keys[j] = keys[action];
        keys[action] = newKey;
        Commit();
    }

    // ---------- Lưu / tải ----------
    const string P = "aot.";
    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        Load();
        ApplySystem();
        if (ResW > 0) ApplyScreen();
    }

    public static void Load()
    {
        if (loaded) return;
        loaded = true;

        MouseSens = PlayerPrefs.GetFloat(P + "sens", MouseSens);
        InvertY = PlayerPrefs.GetInt(P + "invertY", InvertY ? 1 : 0) == 1;
        Fov = PlayerPrefs.GetFloat(P + "fov", Fov);
        SlowMo = PlayerPrefs.GetInt(P + "slowmo", SlowMo ? 1 : 0) == 1;
        SmartAim = PlayerPrefs.GetInt(P + "smartAim", SmartAim ? 1 : 0) == 1;
        AutoReel = PlayerPrefs.GetInt(P + "autoReel", AutoReel ? 1 : 0) == 1;
        AutoRelease = PlayerPrefs.GetInt(P + "autoRelease", AutoRelease ? 1 : 0) == 1;
        AnchorLift = PlayerPrefs.GetInt(P + "anchorLift", AnchorLift ? 1 : 0) == 1;

        Quality = PlayerPrefs.GetInt(P + "quality", QualitySettings.GetQualityLevel());
        Fullscreen = PlayerPrefs.GetInt(P + "fullscreen", Fullscreen ? 1 : 0) == 1;
        ResW = PlayerPrefs.GetInt(P + "resW", 0);
        ResH = PlayerPrefs.GetInt(P + "resH", 0);
        VSync = PlayerPrefs.GetInt(P + "vsync", VSync ? 1 : 0) == 1;
        FpsLimit = PlayerPrefs.GetInt(P + "fps", 0);

        for (int i = 0; i < keys.Length; i++)
            keys[i] = (KeyCode)PlayerPrefs.GetInt(P + "key" + i, (int)DefaultKeys[i]);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(P + "sens", MouseSens);
        PlayerPrefs.SetInt(P + "invertY", InvertY ? 1 : 0);
        PlayerPrefs.SetFloat(P + "fov", Fov);
        PlayerPrefs.SetInt(P + "slowmo", SlowMo ? 1 : 0);
        PlayerPrefs.SetInt(P + "smartAim", SmartAim ? 1 : 0);
        PlayerPrefs.SetInt(P + "autoReel", AutoReel ? 1 : 0);
        PlayerPrefs.SetInt(P + "autoRelease", AutoRelease ? 1 : 0);
        PlayerPrefs.SetInt(P + "anchorLift", AnchorLift ? 1 : 0);

        PlayerPrefs.SetInt(P + "quality", Quality);
        PlayerPrefs.SetInt(P + "fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(P + "resW", ResW);
        PlayerPrefs.SetInt(P + "resH", ResH);
        PlayerPrefs.SetInt(P + "vsync", VSync ? 1 : 0);
        PlayerPrefs.SetInt(P + "fps", FpsLimit);

        for (int i = 0; i < keys.Length; i++) PlayerPrefs.SetInt(P + "key" + i, (int)keys[i]);
        PlayerPrefs.Save();
    }

    // Áp dụng giá trị hiện tại và báo cho các hệ thống khác. screen = true khi đổi toàn màn hình / độ phân giải.
    public static void Commit(bool screen = false)
    {
        ApplySystem();
        if (screen) ApplyScreen();
        if (Changed != null) Changed();
    }

    public static void ApplySystem()
    {
        AudioListener.volume = Mathf.Clamp01(Master);
        if (Quality >= 0 && Quality < QualitySettings.names.Length && Quality != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(Quality, true);
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = FpsLimit > 0 ? FpsLimit : -1;
    }

    public static void ApplyScreen()
    {
        FullScreenMode mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        int w = ResW > 0 ? ResW : Screen.width;
        int h = ResH > 0 ? ResH : Screen.height;
        Screen.SetResolution(w, h, mode);
    }

    // ---------- Độ phân giải ----------
    static List<Vector2Int> resList;

    public static List<Vector2Int> Resolutions()
    {
        if (resList != null) return resList;
        resList = new List<Vector2Int>();
        foreach (var r in Screen.resolutions)
        {
            if (r.width < 800) continue;
            var v = new Vector2Int(r.width, r.height);
            if (!resList.Contains(v)) resList.Add(v);
        }
        if (resList.Count == 0) resList.Add(new Vector2Int(Screen.width, Screen.height));
        return resList;
    }

    public static int ResolutionIndex()
    {
        var list = Resolutions();
        int w = ResW > 0 ? ResW : Screen.width;
        int h = ResH > 0 ? ResH : Screen.height;
        for (int i = 0; i < list.Count; i++)
            if (list[i].x == w && list[i].y == h) return i;
        return list.Count - 1;
    }

    // ---------- Mặc định ----------
    // 0 = Điều khiển, 1 = Âm thanh, 2 = Đồ họa, 3 = Phím tắt
    public static void ResetSection(int section)
    {
        switch (section)
        {
            case 0:
                MouseSens = 1f; InvertY = false; Fov = 75f; SlowMo = true;
                SmartAim = true; AutoReel = true; AutoRelease = true; AnchorLift = true;
                Commit();
                break;
            case 1:
                Master = 1f; Music = 0.7f; Sfx = 0.9f;
                Commit();
                break;
            case 2:
                Fullscreen = true; VSync = true; FpsLimit = 0; ResW = 0; ResH = 0;
                Commit(true);
                break;
            case 3:
                for (int i = 0; i < keys.Length; i++) keys[i] = DefaultKeys[i];
                Commit();
                break;
        }
    }

    // ---------- Tên phím hiển thị ----------
    public static string KeyName(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Space: return "Space";
            case KeyCode.LeftShift: return "Shift trái";
            case KeyCode.RightShift: return "Shift phải";
            case KeyCode.LeftControl: return "Ctrl trái";
            case KeyCode.RightControl: return "Ctrl phải";
            case KeyCode.LeftAlt: return "Alt trái";
            case KeyCode.Tab: return "Tab";
            case KeyCode.UpArrow: return "Mũi tên lên";
            case KeyCode.DownArrow: return "Mũi tên xuống";
            case KeyCode.LeftArrow: return "Mũi tên trái";
            case KeyCode.RightArrow: return "Mũi tên phải";
            default: return k.ToString();
        }
    }
}
