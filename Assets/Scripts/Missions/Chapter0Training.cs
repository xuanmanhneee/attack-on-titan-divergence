using UnityEngine;
using UnityEngine.UI;

// Chapter 0 is a safe, non-combat onboarding course. The level geometry and
// checkpoint references are baked into Chapter0_TrainingCamp by the editor builder.
public class Chapter0Training : MonoBehaviour
{
    public ODMController player;
    public Transform[] checkpoints;
    public float checkpointRadius = 8f;
    public float voidY = -15f;

    [Header("UI (canvas trong scene)")]
    [SerializeField] Text titleText = null;
    [SerializeField] Text objectiveText = null;
    [SerializeField] RectTransform marker = null;
    [SerializeField] Text markerText = null;
    [SerializeField] GameObject pauseOverlay = null;

    readonly string[] objectives =
    {
        "Làm quen góc nhìn: rê chuột, sau đó di chuyển bằng W A S D",
        "Chạy qua cổng xanh và luồn qua các cọc huấn luyện",
        "Giữ Shift trên mặt đất để dùng gas tăng tốc đến sân ODM",
        "Giữ chuột trái/phải để móc tháp; giữ Space để thu dây",
        "Dùng Shift khi đang móc để lấy đà, bay qua cổng trên cao",
        "Hạ cánh trong vòng tròn kết thúc"
    };

    int step;
    bool looked;
    bool moved;
    bool usedGroundGas;
    bool usedHook;
    bool reachedFlightSpeed;
    bool complete;
    bool paused;
    Vector3 respawnPosition;
    Quaternion respawnRotation;

    void Start()
    {
        if (player == null) player = FindAnyObjectByType<ODMController>();
        if (player == null) { enabled = false; return; }

        respawnPosition = player.transform.position;
        respawnRotation = player.transform.rotation;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (player == null) return;

        if (GameInput.GetKeyDown(KeyCode.Escape)) TogglePause();
        if (paused) return;
        if (GameInput.GetKeyDown(KeyCode.R)) Respawn();
        if (player.transform.position.y < voidY) Respawn();

        looked |= Mathf.Abs(GameInput.GetAxis("Mouse X")) + Mathf.Abs(GameInput.GetAxis("Mouse Y")) > 0.1f;
        moved |= Mathf.Abs(GameInput.GetAxis("Horizontal")) + Mathf.Abs(GameInput.GetAxis("Vertical")) > 0.1f;
        usedGroundGas |= player.IsGrounded && GameInput.GetKey(KeyCode.LeftShift);
        usedHook |= player.ActiveHookCount > 0;
        reachedFlightSpeed |= player.Speed >= 15f;

        if (complete) return;

        if (step == 0)
        {
            if (looked && moved) Advance();
            return;
        }

        Transform target = CurrentTarget();
        if (target == null) return;
        Vector3 delta = player.transform.position - target.position;
        delta.y *= 0.35f;
        if (delta.magnitude > checkpointRadius) return;

        if (step == 2 && !usedGroundGas) return;
        if (step == 3 && !usedHook) return;
        if (step == 4 && (!usedHook || !reachedFlightSpeed)) return;
        Advance();
    }

    Transform CurrentTarget()
    {
        int index = step - 1;
        return checkpoints != null && index >= 0 && index < checkpoints.Length ? checkpoints[index] : null;
    }

    void Advance()
    {
        if (step > 0)
        {
            Transform reached = CurrentTarget();
            if (reached != null)
            {
                respawnPosition = reached.position + Vector3.up * 1.5f;
                respawnRotation = reached.rotation;
            }
        }

        step++;
        if (step >= objectives.Length)
        {
            complete = true;
            step = objectives.Length - 1;
        }
    }

    void Respawn()
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.SetPositionAndRotation(respawnPosition, respawnRotation);
        if (cc != null) cc.enabled = true;
    }

    void TogglePause()
    {
        paused = !paused;
        Time.timeScale = paused ? 0f : 1f;
        player.enabled = !paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    // ---------- UI (canvas dựng sẵn trong scene) ----------
    void LateUpdate()
    {
        if (player == null || titleText == null) return;

        titleText.text = complete ? "CHAPTER 0 — HOÀN THÀNH" : "CHAPTER 0 — TRẠI HUẤN LUYỆN";
        objectiveText.text = complete ? "Bạn đã sẵn sàng cho nhiệm vụ đầu tiên." : objectives[step];
        pauseOverlay.SetActive(paused);
        marker.gameObject.SetActive(TryPlaceMarker());
    }

    // Đặt dấu mục tiêu lên màn hình. Trả về false nếu không có / nằm sau lưng.
    bool TryPlaceMarker()
    {
        if (complete) return false;
        Transform target = CurrentTarget();
        Camera cam = player.cameraTransform != null ? player.cameraTransform.GetComponent<Camera>() : Camera.main;
        if (target == null || cam == null) return false;

        Vector3 screen = cam.WorldToScreenPoint(target.position + Vector3.up * 3f);
        if (screen.z <= 0f) return false;

        float x = Mathf.Clamp(screen.x, 70f, Screen.width - 70f);
        float yTop = Mathf.Clamp(Screen.height - screen.y, 130f, Screen.height - 100f);
        float canvasScale = Screen.height / 1080f; // canvas Scale With Screen Size, match chiều cao
        marker.anchoredPosition = new Vector2(x, Screen.height - yTop) / canvasScale;
        markerText.text = "MỤC TIÊU  " + Mathf.RoundToInt(Vector3.Distance(player.transform.position, target.position)) + " m";
        return true;
    }
}
