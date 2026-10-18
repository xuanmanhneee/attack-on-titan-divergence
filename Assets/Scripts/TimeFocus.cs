using UnityEngine;

// Làm chậm thời gian khi nhân vật ở trên không để kịp ngắm và xử lý.
//  - Trên không: timeScale giảm còn airTimeScale.
//  - Trên không và có gáy titan trong tầm nhìn (đủ gần để lướt/chém): giảm sâu hơn (focusTimeScale).
//  - Trên mặt đất: trở về tốc độ bình thường.
// Nhấn T để bật/tắt. Chuột (nhìn) không bị chậm nên vẫn ngắm nhanh được.
[RequireComponent(typeof(ODMController))]
public class TimeFocus : MonoBehaviour
{
    public bool enabledEffect = true;
    public float airTimeScale = 0.7f;
    public float focusTimeScale = 0.4f;
    public float airDelay = 0.15f;       // phải ở trên không đủ lâu mới chậm (tránh giật khi nhảy nhẹ)
    public float focusRange = 35f;
    public float focusAngle = 45f;
    public float blendSpeed = 6f;

    public bool Active { get; private set; }

    ODMController odm;
    GameManager gm;
    float airTime;
    float current = 1f;

    void Awake()
    {
        odm = GetComponent<ODMController>();
    }

    void Update()
    {
        if (gm == null) gm = FindFirstObjectByType<GameManager>();
        // Chỉ điều khiển thời gian khi đang chơi; pause/thắng/thua do GameManager quản lý
        if (gm != null && gm.Current != GameManager.State.Playing) { current = 1f; Active = false; return; }

        if (GameInput.GetKeyDown(KeyCode.T)) enabledEffect = !enabledEffect;

        airTime = odm.IsGrounded ? 0f : airTime + Time.unscaledDeltaTime;

        float target = 1f;
        if (enabledEffect && airTime >= airDelay)
            target = HasNapeInSight() ? focusTimeScale : airTimeScale;

        current = Mathf.MoveTowards(current, target, blendSpeed * Time.unscaledDeltaTime);
        Active = current < 0.99f;
        Time.timeScale = current;
        Time.fixedDeltaTime = 0.02f * current;
    }

    bool HasNapeInSight()
    {
        Transform cam = odm.cameraTransform;
        foreach (Titan t in Titan.All)
        {
            if (t == null || t.IsDead) continue;
            Vector3 to = t.Nape.position - cam.position;
            if (to.magnitude <= focusRange && Vector3.Angle(cam.forward, to) <= focusAngle) return true;
        }
        return false;
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}
