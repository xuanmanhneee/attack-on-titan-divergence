using UnityEngine;

// Bảng dev ẩn. F1 để mở/đóng. Mở ra thì game tạm dừng và hiện con trỏ.
public class DevPanel : MonoBehaviour
{
    public const KeyCode ToggleKey = KeyCode.F1;

    bool open;
    float prevTimeScale = 1f;
    CursorLockMode prevLock;
    bool prevCursorVisible;
    bool godMode, infGas, infStamina, noCooldown;
    ODMController odm; Health health; PlayerStamina stamina; TitanForm form;
    Rect win = new Rect(20, 20, 280, 10);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindAnyObjectByType<DevPanel>() != null) return;
        var go = new GameObject("DevPanel");
        DontDestroyOnLoad(go);
        go.AddComponent<DevPanel>();
    }

    bool Bind()
    {
        if (odm == null) odm = FindAnyObjectByType<ODMController>();
        if (odm == null) return false;
        if (health == null) health = odm.GetComponent<Health>();
        if (stamina == null) stamina = odm.GetComponent<PlayerStamina>();
        if (form == null) form = odm.GetComponent<TitanForm>();
        return true;
    }

    bool wasDev;

    void Update()
    {
        if (GameInput.GetKeyDownRaw(ToggleKey))
        {
            if (open) Close();
            else if (Bind() && odm.devMode && (form == null || !form.IsTransforming)) Open();
        }
        if (!Bind()) return;
        // Vừa tick Dev Mode thì tự bật hết các công tắc (vẫn tắt tay từng cái được trong panel)
        if (odm.devMode && !wasDev) godMode = infGas = infStamina = noCooldown = true;
        wasDev = odm.devMode;
        if (!odm.devMode) { godMode = infGas = infStamina = noCooldown = false; return; }
        if (godMode && health != null) health.Heal(health.maxHealth);
        if (noCooldown && form != null) form.DevResetCooldown();
        if (infGas) odm.RefillGas(odm.maxGas);
        if (infStamina && stamina != null && (form == null || !form.IsTitan)) stamina.Set(stamina.Max);
    }

    void Open()
    {
        open = true;
        prevTimeScale = Time.timeScale;
        prevLock = Cursor.lockState;
        prevCursorVisible = Cursor.visible;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Close()
    {
        open = false;
        Time.timeScale = prevTimeScale;
        Cursor.lockState = prevLock;
        Cursor.visible = prevCursorVisible;
    }

    void OnDisable() { if (open) Close(); }

    void OnGUI()
    {
        if (!open) return;
        win = GUILayout.Window(98761, win, Draw, "DEV PANEL (" + ToggleKey + ")");
    }

    void Draw(int id)
    {
        if (GUILayout.Button("Hồi đầy máu + gas + thể lực"))
        {
            if (health != null) health.SetCurrent(health.maxHealth);
            odm.RefillGas(odm.maxGas);
            if (stamina != null) stamina.Set(stamina.Max);
        }
        if (form != null)
        {
            GUILayout.BeginHorizontal();
            if (form.IsTitan)
            {
                if (GUILayout.Button("Về người")) { Close(); form.DevRevert(); }
                if (GUILayout.Button("Đầy time titan")) form.DevRefillTitanTime();
            }
            else if (form.DevCanTransform && GUILayout.Button("Hoá titan ngay")) { Close(); form.DevForceTransform(); }
            GUILayout.EndHorizontal();
        }
        godMode = GUILayout.Toggle(godMode, "Bất tử");
        infGas = GUILayout.Toggle(infGas, "Gas vô hạn");
        infStamina = GUILayout.Toggle(infStamina, "Thể lực vô hạn (dạng người)");
        noCooldown = GUILayout.Toggle(noCooldown, "Hoá titan không hồi chiêu");
        GUI.DragWindow();
    }
}
