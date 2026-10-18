using UnityEngine;
using UnityEngine.SceneManagement;

// Quản lý vòng lặp game: đợt titan, thắng/thua, pause, HUD chiến đấu.
// Tự tạo khi vào scene có ODMController (không cần sửa scene).
// Esc = pause. R (khi thắng/thua) hoặc nút "Chơi lại" = tải lại scene.
public class GameManager : MonoBehaviour
{
    public enum State { Playing, Paused, Won, Lost }

    [Header("Đợt titan")]
    public int totalWaves = 3;
    public int firstWaveTitans = 2;
    public int titansPerWave = 2;
    public float minTitanHeight = 9f;
    public float maxTitanHeight = 18f;
    public float spawnMinDist = 50f;
    public float spawnMaxDist = 90f;
    public float timeBetweenWaves = 4f;

    [Header("Người chơi")]
    public float playerMaxHealth = 100f;
    public float voidY = -40f; // rơi xuống dưới mức này là thua

    public State Current { get; private set; } = State.Playing;

    ODMController odm;
    PlayerCombat combat;
    Health playerHealth;
    Camera cam;

    int wave;
    bool waitingNextWave;
    float nextWaveTimer;
    string message = "";
    float messageTimer;

    GUIStyle label, title, button;
    float styleScale = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindFirstObjectByType<GameManager>() != null) return;
        if (FindFirstObjectByType<ODMController>() == null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }

    void Start()
    {
        Time.timeScale = 1f;
        Titan.ResetAll();

        odm = FindFirstObjectByType<ODMController>();
        cam = odm.cameraTransform != null ? odm.cameraTransform.GetComponent<Camera>() : Camera.main;

        combat = odm.GetComponent<PlayerCombat>();
        if (combat == null) combat = odm.gameObject.AddComponent<PlayerCombat>();

        if (odm.GetComponent<TimeFocus>() == null) odm.gameObject.AddComponent<TimeFocus>();

        playerHealth = odm.GetComponent<Health>();
        if (playerHealth == null) playerHealth = odm.gameObject.AddComponent<Health>();
        playerHealth.Init(playerMaxHealth);
        playerHealth.Died += OnPlayerDied;

        Titan.Killed += OnTitanKilled;
        PlayerCombat.Feedback += Say;

        StartWave(1);
    }

    void OnDestroy()
    {
        Titan.Killed -= OnTitanKilled;
        PlayerCombat.Feedback -= Say;
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        Time.timeScale = 1f;
    }

    void Update()
    {
        messageTimer -= Time.unscaledDeltaTime;

        if (Current == State.Playing || Current == State.Paused)
        {
            if (GameInput.GetKeyDown(KeyCode.Escape)) SetPaused(Current == State.Playing);
        }
        else if (GameInput.GetKeyDown(KeyCode.R))
        {
            Restart();
        }

        if (Current != State.Playing) return;

        if (odm.transform.position.y < voidY) playerHealth.TakeDamage(9999f);

        if (waitingNextWave)
        {
            nextWaveTimer -= Time.deltaTime;
            if (nextWaveTimer <= 0f)
            {
                waitingNextWave = false;
                StartWave(wave + 1);
            }
        }
    }

    // ================= ĐỢT TITAN =================
    void StartWave(int n)
    {
        wave = n;
        int count = firstWaveTitans + (n - 1) * titansPerWave;
        for (int i = 0; i < count; i++) SpawnTitan();
        Say("Đợt " + n + "/" + totalWaves + ": " + count + " titan xuất hiện!");
    }

    void SpawnTitan()
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir == Vector2.zero) dir = Vector2.right;
        float dist = Random.Range(spawnMinDist, spawnMaxDist);
        Vector3 p = odm.transform.position + new Vector3(dir.x, 0f, dir.y) * dist;

        var terr = Terrain.activeTerrain;
        p.y = terr != null ? terr.SampleHeight(p) + terr.transform.position.y : odm.transform.position.y - 1f;

        Titan.Create(p, Random.Range(minTitanHeight, maxTitanHeight), odm.transform);
    }

    void OnTitanKilled(Titan t)
    {
        if (Current != State.Playing) return;

        int left = Titan.All.Count;
        if (left > 0) { Say("Còn " + left + " titan"); return; }

        if (wave >= totalWaves)
        {
            SetEnd(State.Won);
        }
        else
        {
            waitingNextWave = true;
            nextWaveTimer = timeBetweenWaves;
            Say("Đợt " + wave + " hoàn thành! Đợt tiếp theo sắp bắt đầu...");
        }
    }

    void OnPlayerDied()
    {
        SetEnd(State.Lost);
    }

    // ================= TRẠNG THÁI =================
    void SetPaused(bool pause)
    {
        Current = pause ? State.Paused : State.Playing;
        ApplyState(pause);
    }

    void SetEnd(State s)
    {
        Current = s;
        ApplyState(true);
    }

    void ApplyState(bool stopped)
    {
        Time.timeScale = stopped ? 0f : 1f;
        if (odm != null) odm.enabled = !stopped;
        Cursor.lockState = stopped ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = stopped;
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Say(string m)
    {
        message = m;
        messageTimer = 3f;
    }

    // ================= GIAO DIỆN (IMGUI) =================
    void OnGUI()
    {
        if (odm == null) return;
        float s = Screen.height / 1080f;
        EnsureStyles(s);

        if (Current == State.Playing) DrawHud(s);
        else DrawMenu(s);
    }

    void EnsureStyles(float s)
    {
        if (Mathf.Approximately(styleScale, s) && label != null) return;
        styleScale = s;
        label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * s) };
        label.normal.textColor = Color.white;
        title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(72 * s), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        title.normal.textColor = Color.white;
        button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(32 * s) };
    }

    static void Box(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    void DrawHud(float s)
    {
        // Máu
        Box(new Rect(30 * s, 30 * s, 420 * s, 26 * s), new Color(0, 0, 0, 0.55f));
        Color hp = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.3f, 0.9f, 0.3f), playerHealth.Normalized);
        Box(new Rect(33 * s, 33 * s, 414 * s * playerHealth.Normalized, 20 * s), hp);
        GUI.Label(new Rect(460 * s, 26 * s, 200 * s, 36 * s), "HP " + Mathf.CeilToInt(playerHealth.Current), label);

        // Thông tin
        string blades = combat.IsReloading ? "đang thay kiếm..." : Mathf.RoundToInt(combat.Durability) + "%";
        GUI.Label(new Rect(30 * s, 66 * s, 700 * s, 36 * s), "Đợt " + wave + "/" + totalWaves + "   |   Titan còn: " + Titan.All.Count, label);
        GUI.Label(new Rect(30 * s, 100 * s, 700 * s, 36 * s), "Kiếm: " + blades + "   (dự phòng: " + combat.Spare + ")", label);

        string speedMsg = combat.CanKillNow ? "ĐỦ TỐC ĐỘ CHÉM GÁY" : "Chưa đủ tốc độ chém gáy (" + combat.killSpeed.ToString("F0") + " m/s)";
        Color old = label.normal.textColor;
        label.normal.textColor = combat.CanKillNow ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 1f, 1f, 0.7f);
        GUI.Label(new Rect(30 * s, 134 * s, 800 * s, 36 * s), speedMsg, label);
        var focus = odm.GetComponent<TimeFocus>();
        if (focus != null && focus.Active)
            GUI.Label(new Rect(30 * s, 168 * s, 800 * s, 36 * s), "TẬP TRUNG (thời gian chậm)", label);
        label.normal.textColor = old;

        // Chấm đỏ đánh dấu gáy titan
        if (cam != null)
        {
            foreach (Titan t in Titan.All)
            {
                if (t == null || t.Nape == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(t.Nape.position);
                if (sp.z <= 0f || sp.z > 120f) continue;

                bool inRange = Vector3.Distance(cam.transform.position, t.Nape.position) <= combat.napeReach;
                Color c = inRange ? (combat.CanKillNow ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 0.55f, 0.1f))
                                  : new Color(1f, 0.2f, 0.15f, 0.7f);
                float size = (inRange ? 20f : 10f) * s;
                Box(new Rect(sp.x - size / 2f, Screen.height - sp.y - size / 2f, size, size), c);
            }
        }

        // Thông báo giữa màn hình
        if (messageTimer > 0f)
        {
            Color mc = title.normal.textColor;
            title.normal.textColor = new Color(1f, 1f, 1f, Mathf.Clamp01(messageTimer));
            int fs = title.fontSize; title.fontSize = Mathf.RoundToInt(40 * s);
            GUI.Label(new Rect(0, 160 * s, Screen.width, 80 * s), message, title);
            title.fontSize = fs; title.normal.textColor = mc;
        }

        // Gợi ý phím
        GUI.Label(new Rect(Screen.width / 2f - 450 * s, Screen.height - 50 * s, 900 * s, 36 * s),
                  "F: chém (xa thì lướt tới gáy)   R: thay kiếm   Q/E: móc   Space: thu dây   Shift: gas   T: bật/tắt làm chậm thời gian   Esc: tạm dừng",
                  new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(20 * s) });
    }

    void DrawMenu(float s)
    {
        Box(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, 0.65f));

        string head = Current == State.Paused ? "TẠM DỪNG" : Current == State.Won ? "CHIẾN THẮNG!" : "BẠN ĐÃ GỤC";
        GUI.Label(new Rect(0, Screen.height * 0.25f, Screen.width, 120 * s), head, title);

        float bw = 360 * s, bh = 64 * s, x = (Screen.width - bw) / 2f, y = Screen.height * 0.45f;

        if (Current == State.Paused && GUI.Button(new Rect(x, y, bw, bh), "Tiếp tục", button))
            SetPaused(false);
        if (GUI.Button(new Rect(x, y + (bh + 16 * s), bw, bh), "Chơi lại", button))
            Restart();
        if (GUI.Button(new Rect(x, y + 2 * (bh + 16 * s), bw, bh), "Thoát", button))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
