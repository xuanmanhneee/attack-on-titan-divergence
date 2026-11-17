using UnityEngine;

// ODM Gear Controller - v3 (góc nhìn thứ nhất, có lớp hỗ trợ tự động)
// Chuột trái = móc trái, chuột phải = móc phải (giữ để duy trì, thả để nhả dây)
// Space = thu dây MẠNH, Shift = gas (khi ở trên không hoặc đang móc)
// Tự động (có công tắc): smart aim, rút dây chùng, thu dây nhẹ khi nhìn về phía móc, tự nhả đúng lúc.
// Mọi con số là điểm khởi đầu, cần tự tune khi chơi thử.

// Các phần ODM là component riêng, gắn sẵn trên Player (scene/prefab), không tự thêm lúc chạy.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(ODMHooks), typeof(ODMGroundMovement), typeof(ODMAirMovement))]
[RequireComponent(typeof(ODMCollision), typeof(ODMAntiStuck), typeof(ODMRopeVisual))]
public class ODMController : MonoBehaviour
{
    [Header("References")]
    public Transform cameraTransform;
    public Transform hipOriginLeft;
    public Transform hipOriginRight;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public float baseFov = 75f;
    public float maxFov = 100f;
    public float fovSpeedRef = 40f;

    [Header("Ground")]
    public LayerMask groundLayer;
    public float walkSpeed = 9f;
    public float groundBoostSpeed = 17f;
    public float groundGasDrainFactor = 0.5f;
    public float groundAccel = 45f;
    [Range(45f, 89f)] public float walkableSlopeLimit = 65f;
    [Range(45f, 89f)] public float gasSlopeLimit = 89f;
    [Range(1f, 45f)] public float roofSlideMinAngle = 6f;
    public float roofSlideAcceleration = 24f;
    public float roofSlideMaxSpeed = 14f;
    public float roofSurfaceGrip = 1.5f;
    public float gasRoofProbeDistance = 1.8f;
    public float gasRoofProbeHeight = 3.5f;
    public float gravity = -20f;
    [Range(0f, 1f)] public float landingMomentumRetention = 0.65f;
    [Range(0f, 1f)] public float bracedLandingMomentumRetention = 0.25f;
    public float landingBrakeMinAirTime = 0.08f;
    public float landingBrakeInputWindow = 0.2f;

    [Header("Air")]
    public float airControl = 6f;
    public float airControlMaxSpeed = 8f;
    public bool airControlRequiresGas = true;

    [Header("Hook")]
    public LayerMask hookableLayer;
    public float maxHookRange = 75f;
    public float assistRadius = 0.45f;
    public bool toggleHooks = false;
    public float ropeDamping = 0.998f;
    [Range(0f, 1f)] public float cornerMomentumRetention = 0.35f;
    public int constraintIterations = 6;
    public float maxSimulationStep = 0.008333334f;

    [Header("Reel")]
    public float reelForce = 30f;
    public float reelMaxSpeed = 15f;
    public float reelSpeed = 10f;
    public float attachDistance = 1.2f;
    public bool requireReelToAttach = true;

    [Header("Smart Assist - Aim")]
    public bool smartAim = false;          // manual ODM: chỉ móc đúng nơi người chơi ngắm
    public float smartAimAngle = 35f;      // nửa góc của nón quét (độ)
    public int smartAimRings = 2;          // số vòng quét
    public int smartAimSamples = 8;        // số tia mỗi vòng (tổng tia = rings * samples)
    public float minHookDistance = 4f;     // bỏ qua điểm quá gần (móc vào đó vô ích)
    public float aimLookWeight = 1f;       // ưu tiên điểm gần tâm ngắm
    public float aimHeightWeight = 0.6f;   // ưu tiên điểm cao hơn người chơi (swing cần neo ở trên)
    public float aimMomentumWeight = 0.5f; // ưu tiên điểm nằm theo hướng đang bay

    [Header("Smart Assist - Rope")]
    public bool autoTakeUpSlack = true;
    public float slackTakeUpSpeed = 60f;
    public bool autoReel = false;
    public float autoReelStrength = 0.6f;
    public float autoReelLookDot = 0.6f;
    public bool autoRelease = false;       // chỉ nhả khi người chơi thả nút móc
    public float autoReleaseMinSpeed = 10f;
    public float autoReleaseBehindDot = -0.2f; // điểm neo nằm sau lưng tới mức này thì nhả (âm = sau lưng)

    [Header("Smart Assist - Dễ chơi hơn")]
    public bool reelOnlyWhenApproaching = false;  // manual ODM: Space luôn có quyền thu dây
    public float reelAwayThreshold = 2f;          // bay ra xa nhanh hơn mức này (m/s) thì Space ngừng kéo
    public bool autoReleaseWhileReeling = true;   // vẫn tự nhả dây khi vượt qua điểm neo kể cả đang giữ Space

    [Header("Smart Assist - Neo cao (đu được lên cao)")]
    public bool liftAnchorToRoof = false;       // manual ODM: neo nằm đúng tại điểm bắn trúng
    public float liftMaxHeight = 16f;           // nâng tối đa bao nhiêu mét so với chỗ ngắm trúng
    public float liftRoofMargin = 0.6f;         // chừa cách mép mái
    public float liftSkipIfAbovePlayer = 8f;   // điểm ngắm đã cao hơn người chơi mức này thì không nâng nữa
    public float liftWallMaxNormalY = 0.35f;    // chỉ nâng khi trúng mặt tường (pháp tuyến gần nằm ngang)
    public float swingKeepSpeed = 14f;          // đu nhanh hơn mức này thì lướt sát đất không bị hãm như đang đi bộ

    public bool groundHookYank = false;
    public float groundHookYankSpeed = 10f;

    [Header("Dev")]
    [Tooltip("Bật thì F1 mở được Dev Panel (UI/DevPanel.cs)")]
    public bool devMode = false;

    [Header("Gas")]
    public float gasForce = 26f;
    public float steerRate = 55f;
    public float maxSpeed = 34f;
    public float dualHookGasMultiplier = 1.3f;
    public float dualHookReelMultiplier = 1.25f;
    public float dualHookMaxSpeedMultiplier = 1.18f;
    public float maxGas = 500f;
    public float gasDrain = 12f;
    public float hookedGasDrainFactor = 0.6f;   // đang có dây móc thì Shift tốn gas ít hơn

    [Header("Wall Collision")]
    public bool wallGlideAssist = true;
    [Range(0f, 1f)] public float wallGlideMomentum = 0.55f;
    public float wallSeparationSpeed = 2.5f;
    [Tooltip("Layers that physically block ODM movement. Buildings are on Hookable, so this intentionally includes every layer by default.")]
    public LayerMask movementCollisionMask = ~0;
    [Min(0f)] public float wallCollisionSkin = 0.03f;

    [Header("Wall Launch")]
    public float wallLaunchSpeed = 16f;    // tốc độ bật ra khỏi tường khi nhấn Shift lúc đang bám
    public float wallLaunchGasCost = 10f;

    [Header("Rope Visual")]
    public float hookProjectileSpeed = 300f;

    [Header("Hook Audio")]
    public AudioClip hookLaunchClip;
    public AudioClip hookReleaseClip;
    [Range(0f, 1f)] public float hookLaunchVolume = 0.32f;
    [Range(0f, 1f)] public float hookReleaseVolume = 0.28f;

    [Header("Background Music")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float backgroundMusicVolume = 0.24f;
    [Tooltip("The normal exploration loop ends here (seconds, 145 = 2:25). The remaining track plays when transforming into a Titan.")]
    [Min(1f)] public float backgroundMusicLoopEnd = 145f;
    [Tooltip("Crossfade length when the normal loop wraps back to 0:00.")]
    public float backgroundMusicLoopFade = 3f;
    [Tooltip("On: go back to the normal loop as soon as the Titan form ends. Off: the Titan section plays to the end of the track first.")]
    public bool backgroundMusicReturnOnRevert = false;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool logRoofCollisionDebug = true;

    public bool AimHasTarget { get; private set; }
    public float GasNormalized => gas / maxGas;
    public float Speed => velocity.magnitude;
    public bool IsGrounded => isGrounded;
    PlayerStamina stamina;
    internal Vector3 Velocity { get => velocity; set => velocity = value; }
    internal PlayerStamina Stam { get { if (stamina == null) stamina = GetComponent<PlayerStamina>(); return stamina; } }
    internal bool Exhausted => Stam != null && Stam.Exhausted;
    public bool IsGassing => GameInput.GetKey(KeyCode.LeftShift) && gas > 0f && !Exhausted;
    public int ActiveHookCount => (hookLeft.active ? 1 : 0) + (hookRight.active ? 1 : 0);
    public bool LeftHookActive => hookLeft.active;
    public bool RightHookActive => hookRight.active;
    public void RefillGas(float amount) { gas = Mathf.Min(maxGas, gas + amount); }

    // TitanForm tạm "đóng băng" bộ cơ động: không nhìn, không di chuyển, không móc.
    // Dùng cờ thay vì enabled=false để nhạc nền vẫn lặp và GameManager (pause) không bật lại nhầm.
    [HideInInspector] public bool suspended;
    public float Yaw { get => yaw; set => yaw = value; }
    public float Pitch { get => pitch; set => pitch = value; }

    public void ReleaseAllHooks()
    {
        if (hooks != null)
        {
            hooks.Release(hookLeft, -1f);
            hooks.Release(hookRight, 1f);
        }
        if (ropeVisual != null) ropeVisual.HideAll();
    }

    public void ResetMotion()
    {
        velocity = Vector3.zero;
        airborneTime = 0f;
        landingBrakeWindow = 0f;
    }

    public void RecoverTo(Vector3 position)
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
        velocity = Vector3.zero;
        if (antiStuck != null) antiStuck.ResetHistory();
        airborneTime = 0f;
        landingBrakeWindow = 0f;
    }

    private CharacterController controller;
    private Camera cam;

    private Vector3 velocity;
    private bool isGrounded;
    private float airborneTime;
    private float landingBrakeWindow;
    private bool landingBraceApplied;
    private float gas;

    private float pitch;
    private float yaw;

    private float aimTimer = 0f;
    private const float AIM_INTERVAL = 0.05f; // cập nhật chấm ngắm 20 lần/giây cho nhẹ

    private ODMAntiStuck antiStuck;
    private ODMGroundMovement groundMove;
    private ODMAirMovement airMove;
    private ODMCollision collision;

    private HookState hookLeft = new HookState();
    private HookState hookRight = new HookState();
    private ODMHooks hooks;
    private ODMRopeVisual ropeVisual;
    public BackgroundMusicPlayer Music { get; private set; }

    // Giá trị gốc (từ scene) để nhân / cộng với cài đặt của người chơi
    private float defSens, defBaseFov, defMaxFov;

    void Awake()
    {
        defSens = mouseSensitivity;
        defBaseFov = baseFov;
        defMaxFov = maxFov;
    }

    void OnEnable()
    {
        GameSettings.Changed += ApplySettings;
        ApplySettings();
    }

    void OnDisable()
    {
        GameSettings.Changed -= ApplySettings;
    }

    // Cài đặt người chơi (menu Cài đặt) ghi đè lên các thông số trong Inspector
    void ApplySettings()
    {
        mouseSensitivity = defSens * GameSettings.MouseSens;
        baseFov = GameSettings.Fov;
        maxFov = Mathf.Max(baseFov + 5f, defMaxFov + (GameSettings.Fov - defBaseFov));
        smartAim = GameSettings.SmartAim;
        autoReel = GameSettings.AutoReel;
        autoRelease = GameSettings.AutoRelease;
        liftAnchorToRoof = GameSettings.AnchorLift;
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        // Give the 1.7 m controller enough clearance to step onto the low
        // training/checkpoint plates without catching its lower hemisphere on
        // their vertical edge. Visual feet are aligned to this capsule.
        // Do not let CharacterController reject the roof geometry before our
        // gas-vs-walking rule gets a chance to run. GroundMovement gates steep
        // roofs without gas; gas movement is projected along the roof plane.
        controller.slopeLimit = 89f;
        controller.stepOffset = Mathf.Clamp(Mathf.Max(controller.stepOffset, 0.45f),
                                            0f, controller.height - 0.05f);
        controller.minMoveDistance = 0f;
        cam = cameraTransform.GetComponent<Camera>();
        // Capsule bán kính 0.35 nhưng near clip 0.3 + FOV rộng: góc mặt phẳng near lọt qua tường -> nhìn xuyên.
        cam.nearClipPlane = 0.08f;
        Cursor.lockState = CursorLockMode.Locked;

        yaw = transform.eulerAngles.y;
        gas = maxGas;
        controller.excludeLayers = 1 << 2; // titan (layer 2) không chặn/đẩy người chơi khi bay

        antiStuck = GetComponent<ODMAntiStuck>();
        antiStuck.Init(controller, movementCollisionMask, logRoofCollisionDebug);

        groundMove = GetComponent<ODMGroundMovement>();
        groundMove.Init(this, controller);

        airMove = GetComponent<ODMAirMovement>();
        airMove.Init(this, hookLeft, hookRight);

        collision = GetComponent<ODMCollision>();
        collision.Init(this, controller, antiStuck, groundMove, hookLeft, hookRight);

        ropeVisual = GetComponent<ODMRopeVisual>();

        hooks = GetComponent<ODMHooks>();
        hooks.Init(this, hookLeft, hookRight);
        hooks.Fired += OnHookFired;

        if (backgroundMusic != null)
        {
            Music = gameObject.AddComponent<BackgroundMusicPlayer>();
            Music.clip = backgroundMusic;
            Music.volume = backgroundMusicVolume;
            Music.loopEnd = Mathf.Min(backgroundMusicLoopEnd, Mathf.Max(1f, backgroundMusic.length - 1f));
            Music.loopFade = backgroundMusicLoopFade;
            Music.returnOnRevert = backgroundMusicReturnOnRevert;
        }
    }

    void Update()
    {
        if (suspended) return;

        Vector3 frameStartPosition = transform.position;
        float dt = Time.deltaTime;

        HandleLook();
        UpdateAudio();

        // A hook embedded in a Titan must travel with its body instead of remaining
        // at the world-space point where the shot originally landed.
        hooks.RefreshAnchors();

        aimTimer -= dt;
        if (aimTimer <= 0f)
        {
            AimHasTarget = hooks.FindAnchor(null, out _);
            aimTimer = AIM_INTERVAL;
        }

        hooks.HandleInput();

        // CharacterController không có fixed simulation riêng. Chia frame dài thành các bước nhỏ
        // để gravity và rope constraint cho cùng kết quả ở các mức FPS khác nhau.
        float remaining = Mathf.Min(dt, 0.1f);
        float stepLimit = Mathf.Max(0.001f, maxSimulationStep);
        while (remaining > 0.000001f)
        {
            float step = Mathf.Min(remaining, stepLimit);
            SimulateMovement(step);
            Vector3 requested = velocity * step;
            Vector3 before = transform.position;
            collision.Move(requested);
            if (antiStuck.Track(requested, transform.position - before, isGrounded))
                OnRecoveredFromEmbedding();
            remaining -= step;
        }

        UpdateFov(dt);
        UpdateRopeVisuals();
        collision.LogRoofContact(frameStartPosition);
    }

    void SimulateMovement(float dt)
    {
        collision.ResolveOverlap();
        bool wasGrounded = isGrounded;
        isGrounded = groundMove.UpdateGrounded();

        if (!isGrounded)
        {
            if (wasGrounded)
            {
                landingBrakeWindow = 0f;
                landingBraceApplied = false;
            }
            airborneTime += dt;
        }
        else
        {
            if (!wasGrounded && airborneTime >= landingBrakeMinAirTime)
            {
                bool braced = GameInput.GetAxis("Vertical") < -0.5f;
                ApplyLandingBrake(braced);
                landingBrakeWindow = landingBrakeInputWindow;
                landingBraceApplied = braced;
            }
            airborneTime = 0f;
        }

        if (isGrounded && landingBrakeWindow > 0f)
        {
            if (!landingBraceApplied && GameInput.GetAxis("Vertical") < -0.5f)
            {
                ApplyLateLandingBrace();
                landingBraceApplied = true;
            }
            landingBrakeWindow = Mathf.Max(0f, landingBrakeWindow - dt);
        }

        bool hooked = hookLeft.active || hookRight.active;
        bool standing = isGrounded && velocity.y <= 0.1f;

        // Khi đứng trên đất, dây chỉ tiếp quản chuyển động nếu người chơi chủ động thu bằng Space.
        bool wantsRopeMovement = hooked && GameInput.GetKey(KeyCode.Space);
        if (standing && !wantsRopeMovement)
        {
            airMove.LoosenRope(hookLeft);
            airMove.LoosenRope(hookRight);
            bool brakingLanding = landingBrakeWindow > 0f && GameInput.GetAxis("Vertical") < -0.5f;
            groundMove.Move(dt, GetWishDirection(brakingLanding), ref velocity, ref gas);
        }
        else
            airMove.Move(dt, hooked, GetWishDirection(), ref velocity, ref gas);
    }


    // Bắn dây khi đang đứng đất: giật người lên về phía điểm neo.
    void OnHookFired(Vector3 anchor)
    {
        if (groundHookYank && isGrounded && velocity.y <= 0.1f)
        {
            Vector3 dir = (anchor - transform.position).normalized;
            Vector3 yank = new Vector3(dir.x, Mathf.Max(dir.y, 0.35f), dir.z).normalized * groundHookYankSpeed;
            velocity += yank;
            isGrounded = false;
        }
    }

    void OnRecoveredFromEmbedding()
    {
        velocity = Vector3.zero;
        // Dây không được giật người chơi ngược lại chỗ vừa kẹt.
        Vector3 p = transform.position;
        if (hookLeft.active) hookLeft.ropeLength = Mathf.Max(hookLeft.ropeLength, Vector3.Distance(p, hookLeft.anchorPoint));
        if (hookRight.active) hookRight.ropeLength = Mathf.Max(hookRight.ropeLength, Vector3.Distance(p, hookRight.anchorPoint));
    }

    void ApplyLandingBrake(bool braced)
    {
        Vector3 flat = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float speed = flat.magnitude;
        if (speed < 0.1f) return;

        // Cú đáp chậm chỉ ghì nhẹ; cú đáp tốc độ ODM giữ ít đà hơn để không bị
        // hất văng khỏi mái ngay sau khi chân vừa chạm bề mặt.
        float impact = Mathf.InverseLerp(walkSpeed, maxSpeed, speed);
        float softRetention = braced ? 0.5f : 0.9f;
        float hardRetention = braced ? bracedLandingMomentumRetention : landingMomentumRetention;
        float retention = Mathf.Lerp(softRetention, hardRetention, impact);
        flat *= retention;
        velocity.x = flat.x;
        velocity.z = flat.z;
    }

    void ApplyLateLandingBrace()
    {
        // Đã ghì tự động một nhịp; S trong cửa sổ tiếp đất giảm phần đà còn lại
        // xuống gần mức của một cú cuộn người chủ động.
        float extraRetention = bracedLandingMomentumRetention / Mathf.Max(0.01f, landingMomentumRetention);
        velocity.x *= extraRetention;
        velocity.z *= extraRetention;
    }

    // ================= LOOK =================
    // Tiếng xì gas, gió theo tốc độ, cảnh báo gas thấp
    float lastGas = -1f;
    bool lowGasWarned;
    void UpdateAudio()
    {
        if (lastGas < 0f) lastGas = gas;
        float drop = lastGas - gas;   // gas giảm so với khung trước = đang xả
        lastGas = gas;
        GameAudio.SetLoops(drop > 0.0005f ? 1f : 0f, velocity.magnitude / Mathf.Max(1f, maxSpeed));

        float g = gas / Mathf.Max(1f, maxGas);
        if (g < 0.2f) { if (!lowGasWarned) { lowGasWarned = true; GameAudio.Play2D(Sfx.LowGas, 0.6f); } }
        else if (g > 0.35f) lowGasWarned = false;
    }

    void HandleLook()
    {
        yaw += GameInput.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= GameInput.GetAxis("Mouse Y") * mouseSensitivity * (GameSettings.InvertY ? -1f : 1f);
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void UpdateFov(float dt)
    {
        if (cam == null) return;
        float t = Mathf.Clamp01(velocity.magnitude / fovSpeedRef);
        float target = Mathf.Lerp(baseFov, maxFov, t);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, 5f * dt);
    }

    Vector3 GetWishDirection(bool suppressBackward = false)
    {
        float h = GameInput.GetAxis("Horizontal");
        float v = GameInput.GetAxis("Vertical");
        if (suppressBackward && v < 0f) v = 0f;
        Vector3 fwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 dir = fwd * v + right * h;
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }



    // ================= ROPE VISUAL =================
    void UpdateRopeVisuals()
    {
        Vector3 startL = hipOriginLeft != null ? hipOriginLeft.position : transform.position;
        Vector3 startR = hipOriginRight != null ? hipOriginRight.position : transform.position;
        ropeVisual.DrawLeft(hookLeft.active, startL, hookLeft.anchorPoint, ref hookLeft.visualProgress, hookProjectileSpeed);
        ropeVisual.DrawRight(hookRight.active, startR, hookRight.anchorPoint, ref hookRight.visualProgress, hookProjectileSpeed);
    }

    // ================= DEBUG =================
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + velocity * 0.3f);

        Gizmos.color = Color.cyan;
        if (hookLeft.active) Gizmos.DrawWireSphere(hookLeft.anchorPoint, 0.3f);
        if (hookRight.active) Gizmos.DrawWireSphere(hookRight.anchorPoint, 0.3f);
    }
}
