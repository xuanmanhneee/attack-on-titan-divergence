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
    // Attack on Titan scale: ordinary Titans reach roughly 3-15 m. This combat
    // pool favours the imposing 9-15 m classes; smaller Titans can be added separately.
    public float minTitanHeight = 9f;
    public float maxTitanHeight = 15f;
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

    // HUD (MangaHud) đọc các giá trị này
    public TrostMission Mission => mission;
    public int Wave => wave;
    public int TotalWaves => totalWaves;
    public string Hint => hint;

    ODMController odm;
    TrostMission mission;
    PlayerCombat combat;
    Health playerHealth;
    Camera cam;
    TrostMap activeTrostMap;

    int wave;
    bool waitingNextWave;
    float nextWaveTimer;
    string message = "";
    float messageTimer;

    MangaMenu menu;
    MangaHud hud;
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
        if (FindAnyObjectByType<GameManager>() != null) return;
        // Chapter 0 has its own non-combat progression and HUD.  Do not start
        // Trost (or spawn combat waves) merely because it reuses ODMController.
        if (FindAnyObjectByType<Chapter0Training>() != null) return;
        if (FindAnyObjectByType<ODMController>() == null) return;
        new GameObject("GameManager").AddComponent<GameManager>();
    }

    void Start()
    {
        Time.timeScale = 1f;
        Titan.ResetAll();
        GameAudio.Battle(false);
        GameAudio.SetLoops(0f, 0f);

        odm = FindAnyObjectByType<ODMController>();
        cam = odm.cameraTransform != null ? odm.cameraTransform.GetComponent<Camera>() : Camera.main;

        // Titans use the player's first hookable layer so ODM raycasts can hit them
        // in every game mode, not only during the Trost mission.
        int hookMask = odm.hookableLayer.value;
        Titan.HookableLayer = -1;
        for (int i = 0; i < 32; i++)
            if ((hookMask & (1 << i)) != 0) { Titan.HookableLayer = i; break; }

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
        hud = MangaHud.Create(this, odm, combat, playerHealth, cam);
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
        GameAudio.Warmup();
        menu.SetProgress(0.9f, "Chuẩn bị âm thanh...");
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
        GameAudio.Battle(true);
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
        foreach (var go in FindObjectsByType<GameObject>())
            if (go.transform.parent == null && System.Text.RegularExpressions.Regex.IsMatch(go.name, @"^Cube( \(\d+\))?$"))
                go.SetActive(false);

        // Chapter 1 is a baked Unity scene.  Keep the procedural builder only
        // as a fallback for legacy/test scenes which have no authored map.
        TrostMapAuthoring authoredMap = FindAnyObjectByType<TrostMapAuthoring>();
        var map = authoredMap != null
            ? authoredMap.CreateRuntimeMap(odm.transform.position)
            : TrostCity.Build(odm.transform.position, layer);
        activeTrostMap = map;
        layer = map.buildingLayer;
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

        if (activeTrostMap != null && odm.transform.position.y < activeTrostMap.groundY - 10f)
        {
            // Recover from a missed roof/floor seam instead of dropping out of
            // the chapter. The authored map owns the safe rooftop start point.
            odm.RecoverTo(activeTrostMap.playerStart);
            Say("Mất thăng bằng — trở lại nóc nhà.");
        }
        else if (odm.transform.position.y < voidY) playerHealth.TakeDamage(9999f);

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
        GameAudio.Duck(pause);
        if (pause) menu.ShowPause(); else menu.Hide();
    }

    void SetEnd(State s)
    {
        if (Current != State.Playing && Current != State.Paused) return; // đã kết thúc rồi
        Current = s;
        ApplyState(true);
        GameAudio.Battle(false);
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
        if (hud != null) hud.Toast(m);
    }
}
