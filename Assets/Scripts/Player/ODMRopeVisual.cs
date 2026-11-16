using UnityEngine;

// Vẽ hai sợi dây hook (LineRenderer). ODMController gọi Draw mỗi frame; không chứa logic vật lý.
public class ODMRopeVisual : MonoBehaviour
{
    public Material ropeMaterial;
    public float ropeWidth = 0.065f;

    LineRenderer lineLeft;
    LineRenderer lineRight;

    void Awake()
    {
        lineLeft = CreateLine("RopeLine_Left");
        lineRight = CreateLine("RopeLine_Right");
    }

    public void HideAll()
    {
        if (lineLeft != null) lineLeft.enabled = false;
        if (lineRight != null) lineRight.enabled = false;
    }

    public void DrawLeft(bool active, Vector3 start, Vector3 anchor, ref float progress, float projectileSpeed)
        => Draw(lineLeft, active, start, anchor, ref progress, projectileSpeed);

    public void DrawRight(bool active, Vector3 start, Vector3 anchor, ref float progress, float projectileSpeed)
        => Draw(lineRight, active, start, anchor, ref progress, projectileSpeed);

    LineRenderer CreateLine(string lineName)
    {
        GameObject obj = new GameObject(lineName);
        obj.transform.SetParent(transform);

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = ropeWidth;
        lr.endWidth = ropeWidth * 0.72f;
        lr.useWorldSpace = true;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;

        if (ropeMaterial != null)
        {
            lr.material = ropeMaterial;
        }
        else
        {
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(0.34f, 0.34f, 0.36f, 1f);
            lr.endColor = new Color(0.46f, 0.43f, 0.43f, 1f);
        }

        lr.enabled = false;
        return lr;
    }

    static void Draw(LineRenderer line, bool active, Vector3 start, Vector3 anchor, ref float progress, float projectileSpeed)
    {
        if (line == null) return;
        if (!active)
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;
        float distance = Vector3.Distance(start, anchor);
        float duration = distance / Mathf.Max(1f, projectileSpeed);
        progress = Mathf.MoveTowards(progress, 1f, Time.unscaledDeltaTime / Mathf.Max(0.025f, duration));
        float eased = 1f - Mathf.Pow(1f - progress, 3f);

        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, Vector3.Lerp(start, anchor, eased));
    }
}
