using UnityEngine;
using UnityEngine.UI;

// Khung truyện: hình bình hành (skew), viền mực dày, bóng đổ đặc lệch xuống phải.
// Màu `color` là màu nền; viền và bóng luôn là mực.
[RequireComponent(typeof(CanvasRenderer))]
public class MangaPanel : MaskableGraphic
{
    public float skew = 0f;                              // > 0: nghiêng kiểu "/", < 0: kiểu "\"
    public float border = 5f;
    public Vector2 shadow = new Vector2(8f, -8f);
    public Color ink = new Color(0.753f, 0.541f, 0.243f, 0.75f);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        // Giao diện phẳng kiểu codex: không nghiêng, không bóng đổ, viền đồng mảnh
        float sa = 0f, sb = 0f;
        Vector2 tl = new Vector2(r.xMin + sa, r.yMax);
        Vector2 tr = new Vector2(r.xMax - sb, r.yMax);
        Vector2 br = new Vector2(r.xMax - sa, r.yMin);
        Vector2 bl = new Vector2(r.xMin + sb, r.yMin);

        Color inkC = new Color(ink.r, ink.g, ink.b, ink.a * color.a);

        float border = Mathf.Min(this.border, 2f);
        if (border > 0.01f)
        {
            AddQuad(vh, tl, tr, br, bl, inkC);
            float h = Mathf.Max(1f, r.height);
            float k = 1f;
            float bx = border * k;
            if (r.width > 2f * bx + 2f && h > 2f * border + 2f)
            {
                AddQuad(vh,
                    new Vector2(tl.x + bx, tl.y - border), new Vector2(tr.x - bx, tr.y - border),
                    new Vector2(br.x - bx, br.y + border), new Vector2(bl.x + bx, bl.y + border), color);
            }
        }
        else
        {
            AddQuad(vh, tl, tr, br, bl, color);
        }
    }

    static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, col, Vector2.zero);
        vh.AddVert(b, col, Vector2.zero);
        vh.AddVert(c, col, Vector2.zero);
        vh.AddVert(d, col, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i);
    }
}
