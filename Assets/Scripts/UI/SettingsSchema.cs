using System;
using System.Collections.Generic;
using UnityEngine;

public enum SettingKind { Slider, Toggle, Cycle, Key }

// Mô tả MỘT cài đặt: nhãn, kiểu, cách đọc/ghi GameSettings. Mọi giao diện (SettingsMenu ở menu chính,
// MangaMenu trong trận, hay giao diện mới) chỉ cần duyệt SettingsSchema rồi vẽ; không tự viết lại switch theo id.
public class SettingDef
{
    public string Id, Label;
    public SettingKind Kind;
    public float Min, Max, Step;
    public Func<float> GetFloat;
    public Action<float> SetFloat;
    public Func<float, string> Format;
    public Func<bool> GetBool;
    public Action<bool> SetBool;
    public Func<string[]> Options;
    public Func<int> GetIndex;
    public Action<int> SetIndex;
    public int KeyAction;

    public float Float { get { return GetFloat(); } }
    public string FormatFloat(float v) { return Format != null ? Format(v) : v.ToString("0.##"); }

    // Ghi giá trị rồi áp dụng + báo Changed (đổi toàn màn hình / độ phân giải thì áp dụng màn hình).
    public void Apply(float v) { SetFloat(v); GameSettings.Commit(); }
    public void Apply(bool v) { SetBool(v); GameSettings.Commit(Id == "fullscreen"); }
    public void ApplyIndex(int i) { SetIndex(i); GameSettings.Commit(Id == "resolution"); }

    public string CurrentOption()
    {
        var o = Options();
        return o[Mathf.Clamp(GetIndex(), 0, o.Length - 1)];
    }
}

public static class SettingsSchema
{
    // Chỉ số khớp GameSettings.ResetSection
    public static readonly string[] TabNames = { "ĐIỀU KHIỂN", "ÂM THANH", "ĐỒ HỌA", "PHÍM TẮT" };

    // Ghi chú cuối mỗi mục (null = không có)
    public static readonly string[] TabNotes =
    {
        null,
        "Game chưa có nhạc nền và hiệu ứng riêng; hiện các thanh này áp dụng cho âm thanh giao diện. Thả file vào Assets/Resources/UI/Audio để thay.",
        "Khi bật VSync, giới hạn FPS có thể bị bỏ qua. Độ phân giải chỉ có tác dụng rõ khi chạy chế độ cửa sổ.",
        "Di chuyển bằng W/A/S/D và chuột (cố định). Gán trùng phím sẽ đổi chỗ với hành động kia. Esc để hủy khi đang chờ phím."
    };

    static List<SettingDef>[] tabs;
    static Dictionary<string, SettingDef> byId;

    public static List<SettingDef> Tab(int tab) { Build(); return tabs[Mathf.Clamp(tab, 0, tabs.Length - 1)]; }

    public static SettingDef Find(string id)
    {
        Build();
        SettingDef d;
        return byId.TryGetValue(id, out d) ? d : null;
    }

    static string Pct(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }

    static SettingDef Slider(string id, string label, float min, float max, float step,
                             Func<float> get, Action<float> set, Func<float, string> fmt)
    {
        return new SettingDef { Id = id, Label = label, Kind = SettingKind.Slider, Min = min, Max = max, Step = step, GetFloat = get, SetFloat = set, Format = fmt };
    }

    static SettingDef Toggle(string id, string label, Func<bool> get, Action<bool> set)
    {
        return new SettingDef { Id = id, Label = label, Kind = SettingKind.Toggle, GetBool = get, SetBool = set };
    }

    static SettingDef Cycle(string id, string label, Func<string[]> options, Func<int> get, Action<int> set)
    {
        return new SettingDef { Id = id, Label = label, Kind = SettingKind.Cycle, Options = options, GetIndex = get, SetIndex = set };
    }

    static void Build()
    {
        if (tabs != null) return;

        var control = new List<SettingDef>
        {
            Slider("sens", "Độ nhạy chuột", 0.2f, 3f, 0.05f, () => GameSettings.MouseSens, v => GameSettings.MouseSens = v, v => "x" + v.ToString("0.00")),
            Toggle("invertY", "Đảo trục Y (chuột)", () => GameSettings.InvertY, v => GameSettings.InvertY = v),
            Slider("fov", "Góc nhìn (FOV)", 60f, 100f, 1f, () => GameSettings.Fov, v => GameSettings.Fov = v, v => v.ToString("0") + "°"),
            Toggle("slowmo", "Làm chậm thời gian trên không", () => GameSettings.SlowMo, v => GameSettings.SlowMo = v),
            Toggle("smartAim", "Ngắm hỗ trợ (smart aim)", () => GameSettings.SmartAim, v => GameSettings.SmartAim = v),
            Toggle("autoReel", "Tự thu dây khi nhìn về phía móc", () => GameSettings.AutoReel, v => GameSettings.AutoReel = v),
            Toggle("autoRelease", "Tự nhả dây khi vượt điểm neo", () => GameSettings.AutoRelease, v => GameSettings.AutoRelease = v),
            Toggle("anchorLift", "Neo móc lên mái nhà", () => GameSettings.AnchorLift, v => GameSettings.AnchorLift = v),
            Toggle("watchPrologue", "Xem cutscene vào game", () => GameSettings.WatchPrologue, v => GameSettings.WatchPrologue = v),
            Cycle("titanCutscene", "Cutscene hoá titan", () => GameSettings.TitanCutsceneOptions,
                  () => GameSettings.TitanCutscene, i => GameSettings.TitanCutscene = i)
        };

        var audio = new List<SettingDef>
        {
            Slider("master", "Âm lượng tổng", 0f, 1f, 0.05f, () => GameSettings.Master, v => GameSettings.Master = v, Pct),
            Slider("music", "Nhạc nền", 0f, 1f, 0.05f, () => GameSettings.Music, v => GameSettings.Music = v, Pct),
            Slider("sfx", "Hiệu ứng âm thanh", 0f, 1f, 0.05f, () => GameSettings.Sfx, v => GameSettings.Sfx = v, Pct)
        };

        var gfx = new List<SettingDef>
        {
            Cycle("quality", "Chất lượng đồ họa", () => QualitySettings.names,
                  () => GameSettings.Quality >= 0 ? GameSettings.Quality : QualitySettings.GetQualityLevel(),
                  i => GameSettings.Quality = i),
            Toggle("fullscreen", "Toàn màn hình", () => GameSettings.Fullscreen, v => GameSettings.Fullscreen = v),
            Cycle("resolution", "Độ phân giải", ResolutionNames, GameSettings.ResolutionIndex,
                  i => { var r = GameSettings.Resolutions()[i]; GameSettings.ResW = r.x; GameSettings.ResH = r.y; }),
            Toggle("vsync", "Đồng bộ dọc (VSync)", () => GameSettings.VSync, v => GameSettings.VSync = v),
            Cycle("fps", "Giới hạn FPS", FpsNames,
                  () => Mathf.Max(0, Array.IndexOf(GameSettings.FpsOptions, GameSettings.FpsLimit)),
                  i => GameSettings.FpsLimit = GameSettings.FpsOptions[i]),
            Toggle("screenShake", "Rung màn hình (dạng titan)", () => GameSettings.ScreenShake, v => GameSettings.ScreenShake = v)
        };

        var keys = new List<SettingDef>();
        for (int a = 0; a < GameSettings.ActionLabels.Length; a++)
            keys.Add(new SettingDef { Id = "key" + a, Label = GameSettings.ActionLabels[a], Kind = SettingKind.Key, KeyAction = a });

        tabs = new[] { control, audio, gfx, keys };
        byId = new Dictionary<string, SettingDef>();
        foreach (var t in tabs) foreach (var d in t) byId[d.Id] = d;
    }

    static string[] ResolutionNames()
    {
        var list = GameSettings.Resolutions();
        var names = new string[list.Count];
        for (int i = 0; i < names.Length; i++) names[i] = list[i].x + " x " + list[i].y;
        return names;
    }

    static string[] FpsNames()
    {
        var o = GameSettings.FpsOptions;
        var names = new string[o.Length];
        for (int i = 0; i < names.Length; i++) names[i] = o[i] == 0 ? "Không giới hạn" : o[i].ToString();
        return names;
    }
}
