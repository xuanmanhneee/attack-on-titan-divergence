using UnityEngine;

// Chapter 0 is a safe, non-combat onboarding course. The level geometry and
// checkpoint references are baked into Chapter0_TrainingCamp by the editor builder.
public class Chapter0Training : MonoBehaviour
{
    public ODMController player;
    public Transform[] checkpoints;
    public float checkpointRadius = 8f;
    public float voidY = -15f;

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
    GUIStyle titleStyle;
    GUIStyle textStyle;
    GUIStyle centerStyle;

    void Start()
    {
        if (player == null) player = FindFirstObjectByType<ODMController>();
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

    void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        textStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
        centerStyle = new GUIStyle(titleStyle) { fontSize = 44 };
    }

    static void DrawBox(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }

    void OnGUI()
    {
        if (player == null) return;
        EnsureStyles();
        float scale = Mathf.Max(0.7f, Screen.height / 1080f);

        Rect panel = new Rect(Screen.width * 0.5f - 440f * scale, 24f * scale, 880f * scale, 92f * scale);
        DrawBox(panel, new Color(0.025f, 0.04f, 0.055f, 0.82f));
        GUI.Label(new Rect(panel.x, panel.y + 5f * scale, panel.width, 34f * scale),
            complete ? "CHAPTER 0 — HOÀN THÀNH" : "CHAPTER 0 — TRẠI HUẤN LUYỆN", titleStyle);
        GUI.Label(new Rect(panel.x + 20f * scale, panel.y + 43f * scale, panel.width - 40f * scale, 42f * scale),
            complete ? "Bạn đã sẵn sàng cho nhiệm vụ đầu tiên." : objectives[step], textStyle);

        if (!complete)
        {
            Transform target = CurrentTarget();
            Camera cam = player.cameraTransform != null ? player.cameraTransform.GetComponent<Camera>() : Camera.main;
            if (target != null && cam != null)
            {
                Vector3 screen = cam.WorldToScreenPoint(target.position + Vector3.up * 3f);
                if (screen.z > 0f)
                {
                    float x = Mathf.Clamp(screen.x, 70f, Screen.width - 70f);
                    float y = Mathf.Clamp(Screen.height - screen.y, 130f, Screen.height - 100f);
                    DrawBox(new Rect(x - 7f, y - 7f, 14f, 14f), new Color(0.2f, 1f, 0.65f));
                    GUI.Label(new Rect(x - 120f, y + 10f, 240f, 30f),
                        "MỤC TIÊU  " + Mathf.RoundToInt(Vector3.Distance(player.transform.position, target.position)) + " m", textStyle);
                }
            }
        }

        GUI.Label(new Rect(0f, Screen.height - 48f * scale, Screen.width, 36f * scale),
            "R: trở lại checkpoint   •   Esc: tạm dừng", textStyle);

        if (paused)
        {
            DrawBox(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.65f));
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 80f), "TẠM DỪNG", centerStyle);
        }
    }
}
