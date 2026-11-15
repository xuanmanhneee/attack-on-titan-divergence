using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Trang Settings của menu chính. Cấu trúc UI (tab, hàng, thanh trượt...) do MainMenuSceneBuilder dựng sẵn trong scene;
/// script này chỉ nối từng hàng với <see cref="GameSettings"/> lúc chạy, giống menu trong trận (MangaMenu):
/// 4 mục Điều khiển / Âm thanh / Đồ họa / Phím tắt, đổi phím, khôi phục mặc định.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public enum Kind { Slider, Toggle, Cycle, Key }

    [Serializable]
    public class Row
    {
        public string id;
        public Kind kind;
        public int tab;
        public float step;
        public int keyAction;
        public Slider slider;
        public Toggle toggle;
        public Text value;
        public Button prev, next, keyButton;
    }

    [SerializeField] Button[] tabButtons = null;
    [SerializeField] GameObject[] tabActive = null;
    [SerializeField] GameObject[] panels = null;
    [SerializeField] Row[] rows = null;
    [SerializeField] Button resetButton = null;

    static readonly int[] FpsOptions = GameSettings.FpsOptions;

    int tab;
    Row capturing;
    int escFrame = -1;
    bool refreshing;
    bool wired;

    public bool EscConsumedThisFrame { get { return escFrame == Time.frameCount; } }

    public void Setup(Button[] tabs, GameObject[] active, GameObject[] tabPanels, Row[] allRows, Button reset)
    {
        tabButtons = tabs; tabActive = active; panels = tabPanels; rows = allRows; resetButton = reset;
    }

    void Wire()
    {
        if (wired) return;
        wired = true;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].onClick.AddListener(() => { MangaSfx.Click(); ShowTab(index); });
        }
        resetButton.onClick.AddListener(() => { MangaSfx.Click(); GameSettings.ResetSection(tab); RefreshAll(); });

        foreach (var row in rows)
        {
            var r = row;
            switch (r.kind)
            {
                case Kind.Slider:
                    r.slider.onValueChanged.AddListener(v =>
                    {
                        if (refreshing) return;
                        if (r.step > 0f) v = Mathf.Round(v / r.step) * r.step;
                        SetFloat(r.id, v);
                        GameSettings.Commit();
                        r.value.text = Format(r.id, v);
                    });
                    break;
                case Kind.Toggle:
                    r.toggle.onValueChanged.AddListener(on =>
                    {
                        if (refreshing) return;
                        SetBool(r.id, on);
                        GameSettings.Commit(r.id == "fullscreen");
                        r.value.text = on ? "ON" : "OFF";
                    });
                    break;
                case Kind.Cycle:
                    r.prev.onClick.AddListener(() => Step(r, -1));
                    r.next.onClick.AddListener(() => Step(r, 1));
                    break;
                case Kind.Key:
                    r.keyButton.onClick.AddListener(() => { capturing = r; MangaSfx.Click(); RefreshRow(r); });
                    break;
            }
        }
    }

    void OnEnable()
    {
        Wire();
        capturing = null;
        ShowTab(0);
    }

    void Update()
    {
        if (capturing == null) return;
        if (GameInput.GetKeyDownRaw(KeyCode.Escape))
        {
            escFrame = Time.frameCount;
            var r = capturing;
            capturing = null;
            RefreshRow(r);
            MangaSfx.Back();
            return;
        }
        KeyCode k;
        if (GameInput.AnyKeyDown(out k))
        {
            var r = capturing;
            capturing = null;
            GameSettings.Rebind(r.keyAction, k);
            RefreshAll();
            MangaSfx.Click();
        }
    }

    void ShowTab(int index)
    {
        tab = index;
        capturing = null;
        for (int i = 0; i < panels.Length; i++)
        {
            panels[i].SetActive(i == index);
            tabActive[i].SetActive(i == index);
        }
        RefreshAll();
    }

    void RefreshAll()
    {
        refreshing = true;
        foreach (var r in rows) RefreshRow(r);
        refreshing = false;
    }

    void RefreshRow(Row r)
    {
        bool was = refreshing;
        refreshing = true;
        switch (r.kind)
        {
            case Kind.Slider:
                float v = GetFloat(r.id);
                r.slider.SetValueWithoutNotify(v);
                r.value.text = Format(r.id, v);
                break;
            case Kind.Toggle:
                bool on = GetBool(r.id);
                r.toggle.SetIsOnWithoutNotify(on);
                r.value.text = on ? "ON" : "OFF";
                break;
            case Kind.Cycle:
                string[] o = Options(r.id);
                r.value.text = o[Mathf.Clamp(GetIndex(r.id), 0, o.Length - 1)];
                break;
            case Kind.Key:
                var label = r.keyButton.transform.Find("Label").GetComponent<Text>();
                label.text = capturing == r ? "PRESS A KEY..." : GameSettings.KeyName(GameSettings.KeyOf(r.keyAction)).ToUpperInvariant();
                break;
        }
        refreshing = was;
    }

    void Step(Row r, int dir)
    {
        int n = Options(r.id).Length;
        SetIndex(r.id, ((GetIndex(r.id) + dir) % n + n) % n);
        MangaSfx.Click();
        RefreshRow(r);
    }

    // ---------- Nối với GameSettings ----------
    static float GetFloat(string id)
    {
        switch (id)
        {
            case "sens": return GameSettings.MouseSens;
            case "fov": return GameSettings.Fov;
            case "master": return GameSettings.Master;
            case "music": return GameSettings.Music;
            default: return GameSettings.Sfx;
        }
    }

    static void SetFloat(string id, float v)
    {
        switch (id)
        {
            case "sens": GameSettings.MouseSens = v; break;
            case "fov": GameSettings.Fov = v; break;
            case "master": GameSettings.Master = v; break;
            case "music": GameSettings.Music = v; break;
            default: GameSettings.Sfx = v; break;
        }
    }

    static string Format(string id, float v)
    {
        switch (id)
        {
            case "sens": return "x" + v.ToString("0.00");
            case "fov": return v.ToString("0") + "°";
            default: return Mathf.RoundToInt(v * 100f) + "%";
        }
    }

    static bool GetBool(string id)
    {
        switch (id)
        {
            case "invertY": return GameSettings.InvertY;
            case "slowmo": return GameSettings.SlowMo;
            case "smartAim": return GameSettings.SmartAim;
            case "autoReel": return GameSettings.AutoReel;
            case "autoRelease": return GameSettings.AutoRelease;
            case "anchorLift": return GameSettings.AnchorLift;
            case "fullscreen": return GameSettings.Fullscreen;
            case "vsync": return GameSettings.VSync;
            default: return GameSettings.ScreenShake;
        }
    }

    static void SetBool(string id, bool v)
    {
        switch (id)
        {
            case "invertY": GameSettings.InvertY = v; break;
            case "slowmo": GameSettings.SlowMo = v; break;
            case "smartAim": GameSettings.SmartAim = v; break;
            case "autoReel": GameSettings.AutoReel = v; break;
            case "autoRelease": GameSettings.AutoRelease = v; break;
            case "anchorLift": GameSettings.AnchorLift = v; break;
            case "fullscreen": GameSettings.Fullscreen = v; break;
            case "vsync": GameSettings.VSync = v; break;
            default: GameSettings.ScreenShake = v; break;
        }
    }

    static string[] Options(string id)
    {
        switch (id)
        {
            case "quality": return QualitySettings.names;
            case "resolution":
                var list = GameSettings.Resolutions();
                var names = new string[list.Count];
                for (int i = 0; i < names.Length; i++) names[i] = list[i].x + " x " + list[i].y;
                return names;
            default:
                var f = new string[FpsOptions.Length];
                for (int i = 0; i < f.Length; i++) f[i] = FpsOptions[i] == 0 ? "UNLIMITED" : FpsOptions[i].ToString();
                return f;
        }
    }

    static int GetIndex(string id)
    {
        switch (id)
        {
            case "quality": return GameSettings.Quality >= 0 ? GameSettings.Quality : QualitySettings.GetQualityLevel();
            case "resolution": return GameSettings.ResolutionIndex();
            default: return Mathf.Max(0, Array.IndexOf(FpsOptions, GameSettings.FpsLimit));
        }
    }

    static void SetIndex(string id, int i)
    {
        switch (id)
        {
            case "quality": GameSettings.Quality = i; GameSettings.Commit(); break;
            case "resolution":
                var r = GameSettings.Resolutions()[i];
                GameSettings.ResW = r.x; GameSettings.ResH = r.y;
                GameSettings.Commit(true);
                break;
            default: GameSettings.FpsLimit = FpsOptions[i]; GameSettings.Commit(); break;
        }
    }
}
