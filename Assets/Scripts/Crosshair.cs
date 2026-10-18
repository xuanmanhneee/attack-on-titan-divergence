using UnityEngine;
using UnityEngine.UI;

// Gắn vào Image tâm ngắm ở giữa màn hình.
// Đổi màu và kích thước khi có điểm móc hợp lệ (ODMController.AimHasTarget).

[RequireComponent(typeof(Image))]
public class Crosshair : MonoBehaviour
{
    public ODMController player;

    public Color idleColor = new Color(1f, 1f, 1f, 0.5f);
    public Color targetColor = new Color(0.2f, 1f, 0.3f, 1f);
    public float idleSize = 8f;
    public float targetSize = 16f;
    public float smooth = 15f;

    private Image image;
    private RectTransform rect;

    void Awake()
    {
        image = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
        image.raycastTarget = false; // không chặn click của UI khác
    }

    void Update()
    {
        if (player == null) return;

        bool hasTarget = player.AimHasTarget;
        float t = smooth * Time.deltaTime;

        image.color = Color.Lerp(image.color, hasTarget ? targetColor : idleColor, t);

        float size = Mathf.Lerp(rect.sizeDelta.x, hasTarget ? targetSize : idleSize, t);
        rect.sizeDelta = new Vector2(size, size);
    }
}