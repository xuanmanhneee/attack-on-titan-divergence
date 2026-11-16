using UnityEngine;
using UnityEngine.UI;

/// <summary>Nút của trang cài đặt (tab, mặc định, quay lại): nền xanh đen, thanh nhấn, đổi màu khi được chọn.</summary>
public class SettingsButton : MonoBehaviour
{
    public RectTransform face;
    public MangaPanel panel;
    public Text label;

    static readonly Color Normal = new Color(0.035f, 0.055f, 0.062f, 0.88f);

    public RectTransform Rect { get { return (RectTransform)transform; } }

    public void Focus(bool focused, bool active)
    {
        panel.color = focused || active ? MangaStyle.ButtonHover : Normal;
        face.anchoredPosition = focused ? new Vector2(-8f, 0f) : Vector2.zero;
    }
}
