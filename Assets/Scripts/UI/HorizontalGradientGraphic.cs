using UnityEngine;
using UnityEngine.UI;

/// <summary>Đồ họa UI chuyển màu (mặc định trái → phải; vertical: đáy → đỉnh), dùng cho lớp tối/viền.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class HorizontalGradientGraphic : MaskableGraphic
{
    [SerializeField] Color leftColor = Color.black;
    [SerializeField] Color rightColor = Color.clear;
    [SerializeField] bool vertical;

    // Mặc định chuyển màu trái -> phải. Khi vertical = true: LeftColor ở đáy, RightColor ở đỉnh.
    public Color LeftColor { get => leftColor; set { leftColor = value; SetVerticesDirty(); } }
    public Color RightColor { get => rightColor; set { rightColor = value; SetVerticesDirty(); } }
    public bool Vertical { get => vertical; set { vertical = value; SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        Rect rect = GetPixelAdjustedRect();
        Color a = leftColor, b = vertical ? leftColor : leftColor, c = rightColor, d = rightColor;
        if (vertical) { a = leftColor; b = rightColor; c = rightColor; d = leftColor; }
        helper.AddVert(new Vector3(rect.xMin, rect.yMin), a, Vector2.zero);
        helper.AddVert(new Vector3(rect.xMin, rect.yMax), b, Vector2.up);
        helper.AddVert(new Vector3(rect.xMax, rect.yMax), c, Vector2.one);
        helper.AddVert(new Vector3(rect.xMax, rect.yMin), d, Vector2.right);
        helper.AddTriangle(0, 1, 2);
        helper.AddTriangle(2, 3, 0);
    }
}
