using UnityEngine;
using UnityEngine.UI;

/// <summary>Một hàng cài đặt: nhãn + (thanh trượt | công tắc | chọn mục | phím). Prefab chứa đủ bốn nhóm, runtime chỉ bật nhóm cần.</summary>
public class SettingsRow : MonoBehaviour
{
    public Text label;
    public Image bg, accent;
    public RectTransform ctrl;

    public GameObject sliderGroup;
    public RectTransform sliderTrack, sliderFill, sliderHandle, sliderZone;
    public Text sliderValue;

    public GameObject toggleGroup;
    public MangaPanel togglePill;
    public Text toggleText;

    public GameObject cycleGroup;
    public Text cycleText;

    public GameObject keyGroup;
    public MangaPanel keyPill;
    public Text keyText;

    const float TrackWidth = 320f;

    public void ShowKind(SettingKind k)
    {
        sliderGroup.SetActive(k == SettingKind.Slider);
        toggleGroup.SetActive(k == SettingKind.Toggle);
        cycleGroup.SetActive(k == SettingKind.Cycle);
        keyGroup.SetActive(k == SettingKind.Key);
    }

    public void Focus(bool f)
    {
        var red = MangaStyle.Red;
        bg.color = f ? new Color(red.r, red.g, red.b, 0.12f) : new Color(0f, 0f, 0f, 0f);
        accent.gameObject.SetActive(f);
        label.color = f ? red : MangaStyle.Ink;
    }

    public void SetSlider(float t01, string text)
    {
        sliderFill.sizeDelta = new Vector2(TrackWidth * t01, 8f);
        sliderHandle.anchoredPosition = new Vector2(TrackWidth * t01, 0f);
        sliderValue.text = text;
    }

    public void SetToggle(bool on)
    {
        togglePill.color = on ? MangaStyle.Red : MangaStyle.Paper;
        toggleText.color = on ? MangaStyle.Paper : MangaStyle.Ink;
        toggleText.text = on ? "BẬT" : "TẮT";
    }

    public void SetKey(bool capturing, string text)
    {
        keyPill.color = capturing ? MangaStyle.Red : MangaStyle.Paper;
        keyText.color = capturing ? MangaStyle.Paper : MangaStyle.Ink;
        keyText.text = text;
    }
}
