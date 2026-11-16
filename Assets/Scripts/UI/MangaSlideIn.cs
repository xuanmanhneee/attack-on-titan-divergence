using UnityEngine;

// Trượt vào từ vị trí lệch `from`, có trễ `delay`, quá đà nhẹ rồi chốt (ease-out-back).
// scaleFrom < 1: phóng từ nhỏ ra (kiểu "bật" lên). Chạy theo thời gian thật nên hoạt động khi game dừng.
public class MangaSlideIn : MonoBehaviour
{
    // Các giá trị này được lưu trong scene/prefab: Init chạy trong Editor (lúc bake), lúc chạy chỉ phát lại hiệu ứng.
    [SerializeField] Vector2 from;
    [SerializeField] float delay;
    [SerializeField] float duration = 0.38f;
    [SerializeField] float scaleFrom = 1f;
    [SerializeField] Vector2 target;
    [SerializeField] bool ready;
    RectTransform rt;
    float t;

    public MangaSlideIn Init(Vector2 fromOffset, float delaySeconds, float startScale = 1f, float seconds = 0.38f)
    {
        from = fromOffset;
        delay = delaySeconds;
        scaleFrom = startScale;
        duration = seconds;
        rt = (RectTransform)transform;
        target = rt.anchoredPosition;
        ready = true;
        Restart();
        return this;
    }

    // Đưa đối tượng về vị trí cuối (dùng khi bake trong Editor để scene không bị lệch vị trí).
    public void ResetToTarget()
    {
        rt = (RectTransform)transform;
        rt.anchoredPosition = target;
        rt.localScale = Vector3.one;
    }

    void OnEnable()
    {
        if (rt == null) rt = (RectTransform)transform;
        if (ready) Restart();
    }

    void Restart()
    {
        t = -delay;
        Apply(0f);
    }

    void Update()
    {
        if (!ready || t >= duration) return;
        t += Time.unscaledDeltaTime;
        Apply(Mathf.Clamp01(t / duration));
    }

    void Apply(float k)
    {
        float c1 = 0f, c3 = c1 + 1f;
        float e = k >= 1f ? 1f : 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f);
        rt.anchoredPosition = target + from * (1f - e);
        float s = Mathf.LerpUnclamped(scaleFrom, 1f, e);
        rt.localScale = new Vector3(s, s, 1f);
    }
}
