using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Quản lý vòng lặp game: đợt titan, thắng/thua, pause, HUD chiến đấu.
// Tự tạo khi vào scene có ODMController (không cần sửa scene).
// Esc = pause. R (khi thắng/thua) hoặc nút "Chơi lại" = tải lại scene.
public class GameManager : MonoBehaviour
{
    public enum State { Title, Loading, Playing, Paused, Won, Lost }

    [Header("Đợt titan")]
    public int totalWaves = 3;
    public int firstWaveTitans = 2;
    public int titansPerWave = 2;
    public float minTitanHeight = 9f;
    public float maxTitanHeight = 18f;
    public float spawnMinDist = 50f;
    public float spawnMaxDist = 90f;
    public float timeBetweenWaves = 4f;

    [Header("Nhiệm vụ")]
    public bool trostMission = true; // false = chế độ đợt titan đơn giản

    [Header("Người chơi")]
    public float playerMaxHealth = 100f;
    public float voidY = -40f; // rơi xuống dưới mức này là thua

    [Header("Màn hình chờ")]
    public float minBriefingSeconds = 4f;   // hiện màn giới thiệu nhiệm vụ ít nhất bấy nhiêu giây

    public State Current { get; private set; } = State.Title;

    ODMController odm;
    TrostMission mission;
    PlayerCombat combat;
    Health playerHealth;
    Camera cam;

    int wave;
    bool waitingNextWave;
    float nextWaveTimer;
    string message = "";
    float messageTimer;

    GUIStyle label, title, button, small;
    float styleScale = -1f;

    MangaMenu menu;
    static bool skipTitle;   // "Chơi lại" bỏ qua menu chính, "Về menu chính" thì không
    int kills;
    float playTime;
    string hint = "";

    // RuntimeInitializeOnLoadMethod chỉ chạy một lần khi mở game, nên phải nghe sceneLoaded
    // để tạo lại GameManager sau khi "Chơi lại" (load lại scene).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AutoCreate();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { AutoCreate(); }

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
        GameSettings.Changed += RebuildHint;
        RebuildHint();

        // Giao diện manga: menu chính -> màn hình chờ -> chơi. Thế giới được dựng sau khi bấm "Bắt đầu".
        menu = MangaMenu.Create();
        menu.Play += BeginLoading;
        menu.Resume += () => SetPaused(false);
        menu.Restart += Restart;
        menu.MainMenu += GoToMainMenu;
        menu.Quit += QuitGame;

        Current = State.Title;
        ApplyState(true);
        if (skipTitle) { skipTitle = false; BeginLoading(); }
        else menu.ShowTitle();
    }

    void BeginLoading()
    {
        if (Current != State.Title) return;
        Current = State.Loading;
        ApplyState(true);
        menu.ShowBriefing();
        StartCoroutine(LoadRoutine());
    }

    IEnumerator LoadRoutine()
    {
        float start = Time.unscaledTime;
        menu.SetProgress(0.1f, "Đang dựng khu phố Trost...");
        yield return null;
        yield return null; // để màn hình chờ kịp hiện ra trước khi dựng (dựng thành phố gây khựng một nhịp)

        if (trostMission)
        {
            try { BuildTrost(); }
            catch (System.Exception e) { Debug.LogError("[Trost] Dựng khu phố lỗi: " + e); }
        }
        else StartWave(1);
        menu.SetProgress(0.75f, "Titan đang tiến vào khu phố...");
        yield return null;

        while (Time.unscaledTime - start < minBriefingSeconds) yield return null;

        menu.SetBriefingReady();
        while (!menu.BriefingConfirmed) yield return null;

        StartPlaying();
    }

    void StartPlaying()
    {
        Current = State.Playing;
        ApplyState(false);
        menu.Hide();
        Say(mission != null ? "Hạ gục titan! Gas cạn thì chạy tới cột sáng xanh để tiếp tế." : "Hạ gục hết titan!");
    }

    void RebuildHint()
    {
        hint = GameSettings.KeyName(GameSettings.KeyOf(4)) + ": chém (xa thì lướt tới gáy)   " +
               GameSettings.KeyName(GameSettings.KeyOf(5)) + ": thay kiếm   " +
               GameSettings.KeyName(GameSettings.KeyOf(0)) + "/" + GameSettings.KeyName(GameSettings.KeyOf(1)) + ": móc   " +
               GameSettings.KeyName(GameSettings.KeyOf(2)) + ": thu dây   " +
               GameSettings.KeyName(GameSettings.KeyOf(3)) + ": gas   " +
               GameSettings.KeyName(GameSettings.KeyOf(6)) + ": bật/tắt làm chậm thời gian   Esc: tạm dừng";
    }

    void BuildTrost()
    {
        // Layer của vật thể móc được = bit đầu tiên của hookableLayer
        int mask = odm.hookableLayer.value;
        int layer = 3;
        for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) { layer = i; break; }
        if (mask == 0)
        {
            Debug.LogWarning("[Trost] hookableLayer đang trống, dùng layer 3.");
            odm.hookableLayer = 1 << layer; // để móc được nhà dù scene chưa gán layer
        }

        // Tắt các khối thử nghiệm cũ trong scene
        foreach (var go in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.transform.parent == null && System.Text.RegularExpressions.Regex.IsMatch(go.name, @"^Cube( \(\d+\))?$"))
                go.SetActive(false);

        var map = TrostCity.Build(odm.transform.position, layer);
        Titan.ObstacleMask = 1 << layer;
        Titan.GroundSampler = p => map.GroundAt(p);
        Titan.Field = map.floorRect;
        Titan.HasField = true;
        Physics.SyncTransforms(); // đăng ký collider nhà mới trước khi người chơi / titan quét vật lý

        mission = gameObject.AddComponent<TrostMission>();
        mission.Init(this, odm, combat, map);
    }

    public void EndGame(bool won) { SetEnd(won ? State.Won : State.Lost); }

    void OnDestroy()
    {
        Titan.Killed -= OnTitanKilled;
        PlayerCombat.Feedback -= Say;
        GameSettings.Changed -= RebuildHint;
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        Time.timeScale = 1f;
    }

    void Update()
    {
        messageTimer -= Time.unscaledDeltaTime;

        if (GameInput.GetKeyDownRaw(KeyCode.Escape))
        {
            if (Current == State.Playing) SetPaused(true);
            else if (Current == State.Paused && !menu.InSettings && !menu.EscConsumedThisFrame) SetPaused(false);
        }
        else if ((Current == State.Won || Current == State.Lost) && GameInput.GetKeyDownRaw(KeyCode.R))
        {
            Restart();
        }

        if (Current != State.Playing) return;
        playTime += Time.unscaledDeltaTime;

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

        var def = TitanModels.PickRandom(); // null nếu chưa có model nào => titan placeholder
        float h = Random.Range(minTitanHeight, maxTitanHeight) * (def != null ? def.heightMultiplier : 1f);
        Titan.Create(p, h, odm.transform, def);
    }

    void OnTitanKilled(Titan t)
    {
        if (Current != State.Playing) return;
        kills++;
        if (mission != null) return; // nhiệm vụ Trost tự theo dõi tiến trình

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
        if (pause) menu.ShowPause(); else menu.Hide();
    }

    void SetEnd(State s)
    {
        if (Current != State.Playing && Current != State.Paused) return; // đã kết thúc rồi
        Current = s;
        ApplyState(true);
        menu.ShowResult(s == State.Won, kills, playTime);
    }

    void ApplyState(bool stopped)
    {
        Time.timeScale = stopped ? 0f : 1f;
        if (!stopped) Time.fixedDeltaTime = 0.02f; // TimeFocus có thể để lại fixedDeltaTime của chế độ làm chậm
        if (odm != null) odm.enabled = !stopped;
        Cursor.lockState = stopped ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = stopped;
    }

    void Restart()
    {
        skipTitle = true;
        ReloadScene();
    }

    void GoToMainMenu()
    {
        skipTitle = false;
        ReloadScene();
    }

    void ReloadScene()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void QuitGame()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void Say(string m)
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

        if (Current == State.Playing) DrawHud(s); // các màn hình khác do MangaMenu (UGUI) vẽ
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
        small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * s), alignment = TextAnchor.MiddleCenter };
        small.normal.textColor = Color.white;
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
        string waveInfo = mission != null ? "Titan còn: " + Titan.All.Count : "Đợt " + wave + "/" + totalWaves + "   |   Titan còn: " + Titan.All.Count;
        GUI.Label(new Rect(30 * s, 66 * s, 700 * s, 36 * s), waveInfo, label);
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
                if (sp.z <= 0f || sp.z > 120f || sp.x < 0f || sp.x > Screen.width || sp.y < 0f || sp.y > Screen.height) continue;

                bool inRange = Vector3.Distance(cam.transform.position, t.Nape.position) <= combat.napeReach;
                Color c = inRange ? (combat.CanKillNow ? new Color(1f, 0.9f, 0.2f) : new Color(1f, 0.55f, 0.1f))
                                  : new Color(1f, 0.2f, 0.15f, 0.7f);
                float size = (inRange ? 20f : 10f) * s;
                Box(new Rect(sp.x - size / 2f, Screen.height - sp.y - size / 2f, size, size), c);
            }
        }

        if (mission != null) DrawMission(s);

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
                  hint,
                  new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(20 * s) });
    }

    void DrawMission(float s)
    {
        // Mục tiêu ở giữa phía trên
        var head = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(28 * s), fontStyle = FontStyle.Bold };
        Box(new Rect(Screen.width / 2f - 480 * s, 20 * s, 960 * s, 44 * s), new Color(0, 0, 0, 0.45f));
        GUI.Label(new Rect(Screen.width / 2f - 480 * s, 20 * s, 960 * s, 44 * s), mission.ObjectiveText, head);

        if (cam == null) return;

        // Đánh dấu điểm tiếp tế, cổng
        foreach (var mk in mission.Markers)
        {
            float dist = Vector3.Distance(cam.transform.position, mk.pos);
            string txt = mk.label + "  " + Mathf.RoundToInt(dist) + " m";
            Vector3 sp = cam.WorldToScreenPoint(mk.pos + Vector3.up * 8f);

            Color old = GUI.color;
            if (sp.z > 0f)
            {
                float x = Mathf.Clamp(sp.x, 80 * s, Screen.width - 80 * s);
                float y = Mathf.Clamp(Screen.height - sp.y, 90 * s, Screen.height - 90 * s);
                Box(new Rect(x - 8 * s, y - 8 * s, 16 * s, 16 * s), mk.color);
                small.normal.textColor = mk.color;
                GUI.Label(new Rect(x - 120 * s, y - 38 * s, 240 * s, 26 * s), txt, small);
            }
            else
            {
                // Ở sau lưng: hiện ở cạnh màn hình phía bên cần quay
                bool right = cam.transform.InverseTransformPoint(mk.pos).x >= 0f;
                small.normal.textColor = mk.color;
                GUI.Label(new Rect(right ? Screen.width - 300 * s : 20 * s, Screen.height * 0.5f, 280 * s, 26 * s), (right ? txt + "  >>" : "<<  " + txt), small);
            }
            GUI.color = old;
        }
        small.normal.textColor = Color.white;
    }
}
