using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Trang Cài đặt dùng chung cho menu chính và menu tạm dừng trong trận.
/// Giao diện là prefab Resources/UI/SettingsCanvas (tab, nút, khung, một hàng mẫu);
/// danh sách cài đặt lấy từ <see cref="SettingsSchema"/>, hàng được nhân từ hàng mẫu mỗi lần đổi mục.
/// Không dùng EventSystem: đọc chuột/bàn phím qua GameInput nên chạy cả khi Time.timeScale = 0.
/// Gọi <see cref="Open"/> từ bất kỳ đâu; onClose chạy khi người chơi quay lại.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    const string PrefabPath = "UI/SettingsCanvas";
    const float RowsHeight = 800f, RowHeight = 72f, RowPitch = 80f;

    static SettingsMenu instance;
    static int escFrame = -1;

    public static bool IsOpen { get { return instance != null && instance.gameObject.activeSelf; } }
    public static bool EscConsumedThisFrame { get { return escFrame == Time.frameCount; } }

    public static void Open(Action onClose)
    {
        if (instance == null)
        {
            var prefab = Resources.Load<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("[Settings] Thiếu prefab Resources/" + PrefabPath); return; }
            var go = Instantiate(prefab);
            go.name = prefab.name;
            instance = go.GetComponent<SettingsMenu>();
        }
        instance.Show(onClose);
    }

    [SerializeField] SettingsButton[] tabButtons = null;
    [SerializeField] SettingsButton resetButton = null;
    [SerializeField] SettingsButton backButton = null;
    [SerializeField] RectTransform rowsRoot = null;
    [SerializeField] SettingsRow rowTemplate = null;
    [SerializeField] Text note = null;

    class Entry
    {
        public RectTransform rt, dragZone;
        public Action<Vector2> onClick, onDrag;
        public Action<int> onAdjust;
        public Action<bool> onFocus;
        public Action refresh;
    }

    readonly List<Entry> tabEntries = new List<Entry>();
    readonly List<Entry> rowEntries = new List<Entry>();
    readonly List<Entry> items = new List<Entry>();
    Entry resetEntry, backEntry, dragging, capturing;
    int captureAction;
    int sel, tab;
    Vector2 lastMouse;
    Action onClose;
    bool built;

    // Font manga là font hệ thống tạo lúc chạy nên không lưu được trong prefab: gán lại cho mọi chữ (kể cả hàng mẫu đang tắt).
    void Awake()
    {
        var font = MangaStyle.Font;
        foreach (var t in GetComponentsInChildren<Text>(true)) t.font = font;
    }

    // ---------- Mở / đóng ----------
    void Show(Action close)
    {
        onClose = close;
        gameObject.SetActive(false);
        gameObject.SetActive(true); // tắt rồi bật để chạy lại hiệu ứng trượt vào
        if (!built) BuildStatic();
        BuildRows(tab);
        sel = 0;
        dragging = capturing = null;
        lastMouse = GameInput.MousePosition;
        ApplyFocus();
    }

    void Close()
    {
        GameSettings.Save();
        MangaSfx.Back();
        gameObject.SetActive(false);
        var cb = onClose;
        onClose = null;
        if (cb != null) cb();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void BuildStatic()
    {
        built = true;
        rowTemplate.gameObject.SetActive(false);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            var b = tabButtons[i];
            b.label.text = SettingsSchema.TabNames[i];
            tabEntries.Add(new Entry
            {
                rt = b.Rect,
                onClick = p => BuildRows(index),
                onFocus = f => b.Focus(f, tab == index)
            });
        }
        resetEntry = ButtonEntry(resetButton, () => { GameSettings.ResetSection(tab); BuildRows(tab); });
        backEntry = ButtonEntry(backButton, Close);
    }

    static Entry ButtonEntry(SettingsButton b, Action click)
    {
        return new Entry { rt = b.Rect, onClick = p => click(), onFocus = f => b.Focus(f, false) };
    }

    // ---------- Dựng hàng ----------
    void BuildRows(int t)
    {
        tab = t;
        capturing = null;
        for (int i = rowsRoot.childCount - 1; i >= 0; i--)
        {
            var c = rowsRoot.GetChild(i).gameObject;
            if (c != rowTemplate.gameObject && c != note.gameObject) Destroy(c);
        }
        rowEntries.Clear();

        // Khung hàng cao 800: tối đa 80/hàng, nhiều hàng thì tự ép khoảng cách cho vừa khung (hàng cao 72, chừa lề 12).
        var defs = SettingsSchema.Tab(t);
        float pitch = Mathf.Min(RowPitch, (RowsHeight - 2f * 12f - RowHeight) / Mathf.Max(1, defs.Count - 1));
        float top = defs.Count * RowPitch > RowsHeight - 2f * 12f ? RowsHeight * 0.5f - 12f - RowHeight * 0.5f : 330f;
        int n = 0;
        foreach (var def in defs)
        {
            var row = Instantiate(rowTemplate, rowsRoot);
            row.gameObject.name = "Row_" + def.Id;
            row.gameObject.SetActive(true);
            var rt = (RectTransform)row.transform;
            rt.anchoredPosition = new Vector2(0f, top - pitch * n);
            var slide = row.GetComponent<MangaSlideIn>();
            if (slide != null) slide.Init(new Vector2(70f, 0f), 0.03f * n, 1f, 0.3f);
            row.label.text = def.Label;
            rowEntries.Add(MakeEntry(row, def));
            n++;
        }

        string text = SettingsSchema.TabNotes[Mathf.Clamp(t, 0, SettingsSchema.TabNotes.Length - 1)];
        note.gameObject.SetActive(text != null);
        if (text != null)
        {
            note.text = text;
            note.rectTransform.anchoredPosition = new Vector2(0f, top - pitch * n - 60f);
        }

        items.Clear();
        items.AddRange(tabEntries);
        items.AddRange(rowEntries);
        items.Add(resetEntry);
        items.Add(backEntry);
        if (sel >= items.Count) sel = items.Count - 1;
        ApplyFocus();
    }

    Entry MakeEntry(SettingsRow row, SettingDef def)
    {
        var e = new Entry { rt = (RectTransform)row.transform };
        e.onFocus = row.Focus;
        row.ShowKind(def.Kind);

        switch (def.Kind)
        {
            case SettingKind.Slider:
                e.dragZone = row.sliderZone;
                e.refresh = () =>
                {
                    float v = def.GetFloat();
                    row.SetSlider(Mathf.InverseLerp(def.Min, def.Max, v), def.FormatFloat(v));
                };
                e.onDrag = sp =>
                {
                    Vector2 lp;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(row.sliderTrack, sp, null, out lp);
                    float tt = Mathf.InverseLerp(row.sliderTrack.rect.xMin, row.sliderTrack.rect.xMax, lp.x);
                    float v = Mathf.Lerp(def.Min, def.Max, tt);
                    if (def.Step > 0f) v = Mathf.Round(v / def.Step) * def.Step;
                    def.Apply(Mathf.Clamp(v, def.Min, def.Max));
                    e.refresh();
                };
                e.onAdjust = d =>
                {
                    float s = Mathf.Max(def.Step, (def.Max - def.Min) * 0.04f);
                    def.Apply(Mathf.Clamp(def.GetFloat() + d * s, def.Min, def.Max));
                    e.refresh();
                };
                break;

            case SettingKind.Toggle:
                e.refresh = () => row.SetToggle(def.GetBool());
                e.onClick = p => { def.Apply(!def.GetBool()); e.refresh(); };
                e.onAdjust = d => { def.Apply(d > 0); e.refresh(); };
                break;

            case SettingKind.Cycle:
                e.refresh = () => row.cycleText.text = def.CurrentOption();
                Action<int> stepBy = d =>
                {
                    int n = def.Options().Length;
                    def.ApplyIndex(((def.GetIndex() + d) % n + n) % n);
                    e.refresh();
                };
                e.onAdjust = d => stepBy(d);
                e.onClick = p =>
                {
                    int dir = 1;
                    if (!float.IsNaN(p.x))
                    {
                        Vector2 lp;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(row.ctrl, p, null, out lp);
                        dir = lp.x < 0f ? -1 : 1;
                    }
                    stepBy(dir);
                };
                break;

            default:
                e.refresh = () =>
                {
                    bool cap = capturing == e;
                    row.SetKey(cap, cap ? "NHẤN PHÍM..." : GameSettings.KeyName(GameSettings.KeyOf(def.KeyAction)));
                };
                e.onClick = p =>
                {
                    capturing = e;
                    captureAction = def.KeyAction;
                    e.refresh();
                };
                break;
        }
        e.refresh();
        return e;
    }

    // ---------- Điều khiển ----------
    void ApplyFocus()
    {
        for (int i = 0; i < items.Count; i++)
            if (items[i].onFocus != null) items[i].onFocus(i == sel);
    }

    void SetSel(int i)
    {
        if (i == sel || i < 0 || i >= items.Count) return;
        sel = i;
        ApplyFocus();
        MangaSfx.Hover();
    }

    int HitTest(Vector2 mp)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            if (it.rt == null || !it.rt.gameObject.activeInHierarchy) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(it.rt, mp, null)) return i;
        }
        return -1;
    }

    void Activate(Entry it, Vector2 mp)
    {
        if (it == null) return;
        if (it.onDrag != null && it.dragZone != null && RectTransformUtility.RectangleContainsScreenPoint(it.dragZone, mp, null))
        {
            dragging = it;
            it.onDrag(mp);
            MangaSfx.Click();
            return;
        }
        if (it.onClick != null)
        {
            MangaSfx.Click();
            it.onClick(mp);
        }
    }

    void Move(int dir)
    {
        if (items.Count == 0) return;
        SetSel((sel + dir + items.Count) % items.Count);
    }

    void SwitchTab(int d)
    {
        BuildRows((tab + d + tabButtons.Length) % tabButtons.Length);
        MangaSfx.Click();
    }

    void Update()
    {
        if (capturing != null) { UpdateCapture(); return; }

        Vector2 mp = GameInput.MousePosition;
        bool moved = (mp - lastMouse).sqrMagnitude > 1f;
        lastMouse = mp;

        if (dragging != null)
        {
            if (GameInput.GetMouseButton(0)) dragging.onDrag(mp);
            else dragging = null;
            return;
        }

        if (moved)
        {
            int h = HitTest(mp);
            if (h >= 0) SetSel(h);
        }
        if (GameInput.GetMouseButtonDown(0))
        {
            int h = HitTest(mp);
            if (h >= 0)
            {
                SetSel(h);
                Activate(items[h], mp);
            }
        }

        if (GameInput.GetKeyDownRaw(KeyCode.UpArrow) || GameInput.GetKeyDownRaw(KeyCode.W)) Move(-1);
        if (GameInput.GetKeyDownRaw(KeyCode.DownArrow) || GameInput.GetKeyDownRaw(KeyCode.S)) Move(1);

        if (items.Count > 0 && sel < items.Count)
        {
            var cur = items[sel];
            if (cur.onAdjust != null)
            {
                if (GameInput.GetKeyDownRaw(KeyCode.LeftArrow) || GameInput.GetKeyDownRaw(KeyCode.A)) { cur.onAdjust(-1); MangaSfx.Hover(); }
                if (GameInput.GetKeyDownRaw(KeyCode.RightArrow) || GameInput.GetKeyDownRaw(KeyCode.D)) { cur.onAdjust(1); MangaSfx.Hover(); }
            }
            if (GameInput.GetKeyDownRaw(KeyCode.Return) || GameInput.GetKeyDownRaw(KeyCode.Space))
                Activate(cur, new Vector2(float.NaN, float.NaN));
        }

        if (GameInput.GetKeyDownRaw(KeyCode.Q)) SwitchTab(-1);
        if (GameInput.GetKeyDownRaw(KeyCode.E)) SwitchTab(1);
        if (GameInput.GetKeyDownRaw(KeyCode.Escape))
        {
            escFrame = Time.frameCount;
            Close();
        }
    }

    // Chờ bấm phím mới cho một hành động
    void UpdateCapture()
    {
        if (GameInput.GetKeyDownRaw(KeyCode.Escape))
        {
            escFrame = Time.frameCount;
            capturing = null;
            RefreshRows();
            MangaSfx.Back();
            return;
        }
        KeyCode k;
        if (GameInput.AnyKeyDown(out k))
        {
            GameSettings.Rebind(captureAction, k);
            capturing = null;
            RefreshRows();
            MangaSfx.Click();
        }
    }

    void RefreshRows()
    {
        foreach (var e in rowEntries) if (e.refresh != null) e.refresh();
    }
}
