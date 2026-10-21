using UnityEngine;

// Làm chậm thời gian theo đúng lúc người chơi cần ngắm móc.
//  - Không có móc: mở cửa sổ ngắm chậm để tìm điểm neo tiếp theo.
//  - Chỉ có một móc: chậm sâu hơn để kịp xoay camera và bắn móc thứ hai.
//  - Đủ hai móc: tăng nhịp trở lại; về mặt đất: tốc độ bình thường.
// Nhấn T để bật/tắt. Chuột (nhìn) không bị chậm nên vẫn ngắm nhanh được.
[RequireComponent(typeof(ODMController))]
public class TimeFocus : MonoBehaviour
{
    public bool enabledEffect = true;
    public float dualHookTimeScale = 0.65f;
    public float freeAimTimeScale = 0.55f;
    public float singleHookTimeScale = 0.5f;
    public float airDelay = 0.08f;       // tránh nhấp nháy khi vừa rời một bậc thấp
    public float blendSpeed = 8f;

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
        {
            switch (odm.ActiveHookCount)
            {
                case 0:
                    target = freeAimTimeScale;
                    break;
                case 1:
                    target = singleHookTimeScale;
                    break;
                default:
                    target = dualHookTimeScale;
                    break;
            }
        }

        current = Mathf.MoveTowards(current, target, blendSpeed * Time.unscaledDeltaTime);
        Active = current < 0.99f;
        Time.timeScale = current;
        Time.fixedDeltaTime = 0.02f * current;
    }

    void OnDisable()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}
