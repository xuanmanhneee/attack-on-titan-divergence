using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Hiệu ứng nút menu: đổi màu, thanh nhấn, trượt chữ khi chọn / rê chuột.</summary>
public class MenuButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] Image background;
    [SerializeField] Image accent;
    [SerializeField] Text label;
    [SerializeField] Text arrow;
    [SerializeField] Color idle;
    [SerializeField] Color hover;
    [SerializeField] Color highlight;
    AudioSource soundSource;
    AudioClip hoverSound;

    public void Configure(Image bg, Image accentBar, Text labelText, Text arrowText, Color idleColor, Color hoverColor, Color highlightColor)
    {
        background = bg; accent = accentBar; label = labelText; arrow = arrowText;
        idle = idleColor; hover = hoverColor; highlight = highlightColor;
    }

    public void SetSound(AudioSource source, AudioClip clip)
    {
        soundSource = source;
        hoverSound = clip;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        EventSystem.current.SetSelectedGameObject(gameObject);
        SetActive(true);
        PlayHover();
    }
    public void OnPointerExit(PointerEventData eventData) { if (EventSystem.current.currentSelectedGameObject != gameObject) SetActive(false); }
    public void OnSelect(BaseEventData eventData) => SetActive(true);
    public void OnDeselect(BaseEventData eventData) => SetActive(false);

    [SerializeField] Outline frame;
    [SerializeField] Text detail;
    [SerializeField] Text number;
    [SerializeField] RectTransform[] slideTargets;
    [SerializeField] Vector2[] slideOrigins;
    [SerializeField] Vector2 arrowOrigin;
    bool active;
    float t;

    public void SetExtras(Outline frameOutline, Text detailText, Text numberText, RectTransform[] slide)
    {
        frame = frameOutline; detail = detailText; number = numberText; slideTargets = slide;
        slideOrigins = new Vector2[slide.Length];
        arrowOrigin = arrow != null ? arrow.rectTransform.anchoredPosition : Vector2.zero;
        for (int i = 0; i < slide.Length; i++) slideOrigins[i] = slide[i].anchoredPosition;
    }

    void SetActive(bool value)
    {
        active = value;
        if (background == null) return;
        background.color = active ? hover : idle;
        accent.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, active ? 10f : 4f);
        label.color = active ? Color.white : new Color(0.91f, 0.88f, 0.8f, 1f);
        arrow.color = active ? Color.white : highlight;
        if (frame != null) frame.effectColor = new Color(highlight.r, highlight.g, highlight.b, active ? 0.95f : 0.28f);
        if (detail != null) detail.color = active ? new Color(0.91f, 0.88f, 0.8f, 1f) : new Color(0.659f, 0.667f, 0.624f, 1f);
    }

    // Trượt nhẹ chữ sang phải và mũi tên nhích theo khi được chọn
    void Update()
    {
        if (slideTargets == null) return;
        t = Mathf.MoveTowards(t, active ? 1f : 0f, Time.unscaledDeltaTime * 9f);
        float e = Mathf.SmoothStep(0f, 1f, t);
        for (int i = 0; i < slideTargets.Length; i++)
            slideTargets[i].anchoredPosition = slideOrigins[i] + new Vector2(10f * e, 0f);
        if (arrow != null) arrow.rectTransform.anchoredPosition = arrowOrigin + new Vector2(-6f * e, 0f);
    }

    void PlayHover()
    {
        if (soundSource != null && hoverSound != null)
            soundSource.PlayOneShot(hoverSound, 0.35f * GameVolumeSettings.SfxVolume);
    }
}
