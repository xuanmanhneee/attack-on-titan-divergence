using UnityEngine;
using UnityEngine.UI;

// Phần dựng (trong Editor) trang Settings của menu chính: thẻ lớn, cột tab bên trái, bảng hàng bên phải.
// Việc nối với GameSettings lúc chạy nằm ở SettingsMenu.
public partial class MainMenuController
{
    static readonly string[] SettingTabs = { "CONTROLS", "AUDIO", "GRAPHICS", "KEY BINDINGS" };
    static readonly string[] ActionNames = { "FIRE LEFT HOOK", "FIRE RIGHT HOOK", "HARD REEL", "GAS / BOOST", "SLASH", "SLOW TIME" };

    void BuildSettings(Transform parent)
    {
        settingsOverlay = RectObject("Settings Overlay", parent).gameObject;
        Stretch(settingsOverlay.GetComponent<RectTransform>());
        settingsOverlay.AddComponent<Image>();

        var card = BuildCard(settingsOverlay.transform, "Settings Card", 1560f, 900f, "SETTINGS",
            "SYSTEM CONFIGURATION  /  CHANGES SAVE AUTOMATICALLY", "SYSTEM 02  /  CONTROLS, AUDIO, GRAPHICS, KEYS");

        // Cột tab bên trái
        var tabs = new Button[SettingTabs.Length];
        var actives = new GameObject[SettingTabs.Length];
        for (int i = 0; i < SettingTabs.Length; i++)
        {
            tabs[i] = SimpleButton(card, SettingTabs[i], () => { });
            tabs[i].name = "TAB " + SettingTabs[i];
            SetRect(tabs[i].GetComponent<RectTransform>(), 0f, 1f, 0f, 1f, 56f, -190f - i * 74f - 62f, 336f, -190f - i * 74f);
            var active = Image("Active", tabs[i].transform, null, new Color(Brass.r, Brass.g, Brass.b, 0.28f));
            Stretch(active.rectTransform);
            active.transform.SetSiblingIndex(1);
            actives[i] = active.gameObject;
        }
        var reset = SimpleButton(card, "RESET SECTION", () => { });
        reset.name = "RESET SECTION";
        SetRect(reset.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f, 56f, 120f, 336f, 182f);

        // Bảng hàng bên phải: mỗi tab một panel
        var panels = new GameObject[SettingTabs.Length];
        var rows = new System.Collections.Generic.List<SettingsMenu.Row>();
        for (int i = 0; i < SettingTabs.Length; i++)
        {
            var panel = RectObject("Panel " + SettingTabs[i], card);
            SetRect(panel, 0f, 0f, 1f, 1f, 370f, 124f, -56f, -176f);
            panels[i] = panel.gameObject;
        }

        var p0 = panels[0].GetComponent<RectTransform>();
        rows.Add(SliderRow(p0, 0, "sens", "MOUSE SENSITIVITY", 0.2f, 3f, 0.05f));
        rows.Add(ToggleRow(p0, 1, "invertY", "INVERT Y AXIS"));
        rows.Add(SliderRow(p0, 2, "fov", "FIELD OF VIEW", 60f, 100f, 1f));
        rows.Add(ToggleRow(p0, 3, "slowmo", "SLOW TIME IN AIR"));
        rows.Add(ToggleRow(p0, 4, "smartAim", "AIM ASSIST"));
        rows.Add(ToggleRow(p0, 5, "autoReel", "AUTO REEL TOWARD HOOK"));
        rows.Add(ToggleRow(p0, 6, "autoRelease", "AUTO RELEASE PAST ANCHOR"));
        rows.Add(ToggleRow(p0, 7, "anchorLift", "ANCHOR TO ROOFTOPS"));

        var p1 = panels[1].GetComponent<RectTransform>();
        rows.Add(SliderRow(p1, 0, "master", "MASTER VOLUME", 0f, 1f, 0.05f));
        rows.Add(SliderRow(p1, 1, "music", "MUSIC VOLUME", 0f, 1f, 0.05f));
        rows.Add(SliderRow(p1, 2, "sfx", "SFX VOLUME", 0f, 1f, 0.05f));

        var p2 = panels[2].GetComponent<RectTransform>();
        rows.Add(CycleRow(p2, 0, "quality", "GRAPHICS QUALITY"));
        rows.Add(ToggleRow(p2, 1, "fullscreen", "FULLSCREEN"));
        rows.Add(CycleRow(p2, 2, "resolution", "RESOLUTION"));
        rows.Add(ToggleRow(p2, 3, "vsync", "VSYNC"));
        rows.Add(CycleRow(p2, 4, "fps", "FRAME RATE LIMIT"));
        rows.Add(ToggleRow(p2, 5, "screenShake", "SCREEN SHAKE (TITAN FORM)"));
        PanelNote(p2, 6, "With VSync on, the frame rate limit may be ignored. Resolution only matters in windowed mode.");

        var p3 = panels[3].GetComponent<RectTransform>();
        for (int a = 0; a < ActionNames.Length; a++)
            rows.Add(KeyRow(p3, a, "key" + a, ActionNames[a], a));
        PanelNote(p3, ActionNames.Length, "Movement is fixed to W/A/S/D and the mouse. Binding a key that is already used swaps the two actions. Esc cancels.");

        var menu = settingsOverlay.AddComponent<SettingsMenu>();
        menu.Setup(tabs, actives, panels, rows.ToArray(), reset);

        var close = SimpleButton(card, "APPLY & CLOSE", () => ShowSettings(false));
        SetRect(close.GetComponent<RectTransform>(), 0.5f, 0f, 0.5f, 0f, -190f, 32f, 190f, 94f);
        settingsOverlay.SetActive(false);
    }

    RectTransform BaseRow(RectTransform panel, int index, string label)
    {
        var row = RectObject(label, panel);
        SetRect(row, 0f, 1f, 1f, 1f, 0f, -index * 68f - 60f, 0f, -index * 68f);
        var bg = row.gameObject.AddComponent<Image>();
        bg.color = new Color(0.045f, 0.065f, 0.072f, 0.9f);
        bg.raycastTarget = false;
        var bar = Image("Accent", row, null, Brass);
        SetRect(bar.rectTransform, 0f, 0f, 0f, 1f, 0f, 0f, 4f, 0f);
        var text = Text("Label", row, label, 17, Ivory, TextAnchor.MiddleLeft, false);
        SetRect(text.rectTransform, 0f, 0f, 0.5f, 1f, 26f, 0f, 0f, 0f);
        return row;
    }

    SettingsMenu.Row SliderRow(RectTransform panel, int index, string id, string label, float min, float max, float step)
    {
        var row = BaseRow(panel, index, label);
        var value = Text("Value", row, "", 20, Brass, TextAnchor.MiddleRight, false);
        SetRect(value.rectTransform, 1f, 0f, 1f, 1f, -130f, 0f, -24f, 0f);

        var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(row, false);
        SetRect(sliderObject.GetComponent<RectTransform>(), 1f, 0.5f, 1f, 0.5f, -560f, -7f, -150f, 7f);
        var slider = sliderObject.GetComponent<Slider>();
        slider.minValue = min; slider.maxValue = max;

        var track = Image("Track", sliderObject.transform, null, new Color(0.13f, 0.155f, 0.16f, 1f));
        Stretch(track.rectTransform);
        var fillArea = RectObject("Fill Area", sliderObject.transform);
        Stretch(fillArea);
        var fill = Image("Fill", fillArea, null, Brass);
        Stretch(fill.rectTransform);
        var handleArea = RectObject("Handle Slide Area", sliderObject.transform);
        Stretch(handleArea);
        var handle = Image("Handle", handleArea, null, Ivory);
        SetRect(handle.rectTransform, 0f, 0.5f, 0f, 0.5f, -9f, -9f, 9f, 9f);
        handle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var handleFrame = handle.gameObject.AddComponent<Outline>();
        handleFrame.effectColor = Brass;
        handleFrame.effectDistance = new Vector2(2f, -2f);
        handle.raycastTarget = true;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        return new SettingsMenu.Row { id = id, kind = SettingsMenu.Kind.Slider, step = step, slider = slider, value = value };
    }

    SettingsMenu.Row ToggleRow(RectTransform panel, int index, string id, string label)
    {
        var row = BaseRow(panel, index, label);
        var state = Text("State", row, "", 16, Brass, TextAnchor.MiddleRight, false);
        SetRect(state.rectTransform, 1f, 0f, 1f, 1f, -170f, 0f, -80f, 0f);

        var toggleObject = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
        toggleObject.transform.SetParent(row, false);
        SetRect(toggleObject.GetComponent<RectTransform>(), 1f, 0.5f, 1f, 0.5f, -66f, -16f, -22f, 16f);
        var background = toggleObject.GetComponent<Image>(); background.color = ButtonIdle;
        var frame = toggleObject.AddComponent<Outline>();
        frame.effectColor = new Color(Brass.r, Brass.g, Brass.b, 0.8f);
        frame.effectDistance = new Vector2(1.5f, -1.5f);
        var check = Image("Checkmark", toggleObject.transform, null, Brass);
        SetRect(check.rectTransform, 0f, 0f, 1f, 1f, 6f, 6f, -6f, -6f);
        var toggle = toggleObject.GetComponent<Toggle>();
        toggle.targetGraphic = background; toggle.graphic = check;
        return new SettingsMenu.Row { id = id, kind = SettingsMenu.Kind.Toggle, toggle = toggle, value = state };
    }

    SettingsMenu.Row CycleRow(RectTransform panel, int index, string id, string label)
    {
        var row = BaseRow(panel, index, label);
        var prev = ArrowButton(row, "‹", -450f, -402f);
        var value = Text("Value", row, "", 18, Ivory, TextAnchor.MiddleCenter, false);
        SetRect(value.rectTransform, 1f, 0f, 1f, 1f, -398f, 0f, -90f, 0f);
        var next = ArrowButton(row, "›", -86f, -38f);
        return new SettingsMenu.Row { id = id, kind = SettingsMenu.Kind.Cycle, prev = prev, next = next, value = value };
    }

    SettingsMenu.Row KeyRow(RectTransform panel, int index, string id, string label, int action)
    {
        var row = BaseRow(panel, index, label);
        var button = SimpleButton(row, "KEY", () => { });
        button.name = "Key Button";
        SetRect(button.GetComponent<RectTransform>(), 1f, 0.5f, 1f, 0.5f, -370f, -22f, -24f, 22f);
        return new SettingsMenu.Row { id = id, kind = SettingsMenu.Kind.Key, keyAction = action, keyButton = button };
    }

    Button ArrowButton(Transform parent, string glyph, float left, float right)
    {
        var go = new GameObject("Arrow " + glyph, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), 1f, 0.5f, 1f, 0.5f, left, -22f, right, 22f);
        var image = go.GetComponent<Image>(); image.color = ButtonIdle;
        var button = go.GetComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = ButtonHover; colors.selectedColor = ButtonHover;
        colors.pressedColor = new Color(0.42f, 0.27f, 0.11f, 1f);
        button.colors = colors;
        var frame = go.AddComponent<Outline>();
        frame.effectColor = new Color(Brass.r, Brass.g, Brass.b, 0.6f);
        frame.effectDistance = new Vector2(1.5f, -1.5f);
        var text = Text("Label", go.transform, glyph, 30, Brass, TextAnchor.MiddleCenter, false);
        Stretch(text.rectTransform);
        return button;
    }

    void PanelNote(RectTransform panel, int index, string text)
    {
        var note = Text("Note", panel, text, 15, Muted, TextAnchor.UpperLeft, false);
        note.horizontalOverflow = HorizontalWrapMode.Wrap;
        SetRect(note.rectTransform, 0f, 1f, 1f, 1f, 8f, -index * 68f - 64f, -8f, -index * 68f - 8f);
    }
}
