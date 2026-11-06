using UnityEngine;

// ODM Gear Controller - v3 (góc nhìn thứ nhất, có lớp hỗ trợ tự động)
// Chuột trái = móc trái, chuột phải = móc phải (giữ để duy trì, thả để nhả dây)
// Space = thu dây MẠNH, Shift = gas (khi ở trên không hoặc đang móc)
// Tự động (có công tắc): smart aim, rút dây chùng, thu dây nhẹ khi nhìn về phía móc, tự nhả đúng lúc.
// Mọi con số là điểm khởi đầu, cần tự tune khi chơi thử.

[RequireComponent(typeof(CharacterController))]
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
    public Material ropeMaterial;
    public float ropeWidth = 0.065f;
    public float hookProjectileSpeed = 300f;

    [Header("Hook Audio")]
    public AudioClip hookLaunchClip;
    public AudioClip hookReleaseClip;
    [Range(0f, 1f)] public float hookLaunchVolume = 0.32f;
    [Range(0f, 1f)] public float hookReleaseVolume = 0.28f;

    [Header("Background Music")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float backgroundMusicVolume = 0.24f;
    [Tooltip("The normal exploration loop ends here. The remaining track is reserved for Titan transformation.")]
    [Min(1f)] public float backgroundMusicLoopEnd = 142f;

    [Header("Debug")]
    public bool showDebugGizmos = true;
    public bool logRoofCollisionDebug = true;

    public bool AimHasTarget { get; private set; }
    public float GasNormalized => gas / maxGas;
    public float Speed => velocity.magnitude;
    public bool IsGrounded => isGrounded;
    public bool IsGassing => GameInput.GetKey(KeyCode.LeftShift) && gas > 0f;
    public int ActiveHookCount => (hookLeft.active ? 1 : 0) + (hookRight.active ? 1 : 0);
    public bool LeftHookActive => hookLeft.active;
    public bool RightHookActive => hookRight.active;
    public void RefillGas(float amount) { gas = Mathf.Min(maxGas, gas + amount); }

    public void RecoverTo(Vector3 position)
    {
        if (controller == null) controller = GetComponent<CharacterController>();
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
        velocity = Vector3.zero;
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
    private readonly Collider[] groundHits = new Collider[16];
    private readonly Collider[] overlapHits = new Collider[32];
    private readonly RaycastHit[] roofHits = new RaycastHit[16];
    private Collider lastContactCollider;
    private Vector3 lastContactPoint;
    private Vector3 lastContactNormal;
    private Vector3 lastContactMoveDirection;
    private float lastContactTime = -10f;
    private float nextRoofDebugLog;
    private bool loggedRoofContactSample;
    private RaycastHit currentGroundSurface;
    private bool hasCurrentGroundSurface;

    private class HookState
    {
        public bool active;
        public Vector3 anchorPoint;
        public Transform anchorTransform;
        public Vector3 anchorLocalPoint;
        public bool followsTransform;
        public float visualProgress;
        public float ropeLength;
        public bool attached;
    }

    private HookState hookLeft = new HookState();
    private HookState hookRight = new HookState();
    private LineRenderer lineLeft;
    private LineRenderer lineRight;
    private AudioSource hookAudio;
    private AudioSource backgroundMusicAudio;

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
        Cursor.lockState = CursorLockMode.Locked;

        yaw = transform.eulerAngles.y;
        gas = maxGas;

        lineLeft = CreateRopeLine("RopeLine_Left");
        lineRight = CreateRopeLine("RopeLine_Right");

        hookAudio = gameObject.AddComponent<AudioSource>();
        hookAudio.playOnAwake = false;
        hookAudio.loop = false;
        hookAudio.spatialBlend = 0f;
        if (hookLaunchClip == null) hookLaunchClip = Resources.Load<AudioClip>("Audio/ODM/HookLaunch");
        if (hookReleaseClip == null) hookReleaseClip = Resources.Load<AudioClip>("Audio/ODM/HookRelease");

        if (backgroundMusic != null)
        {
            backgroundMusicAudio = gameObject.AddComponent<AudioSource>();
            backgroundMusicAudio.clip = backgroundMusic;
            backgroundMusicAudio.playOnAwake = false;
            backgroundMusicAudio.loop = false;
            backgroundMusicAudio.spatialBlend = 0f;
            backgroundMusicAudio.volume = backgroundMusicVolume;
            backgroundMusicAudio.Play();
        }
    }

    void Update()
    {
        Vector3 frameStartPosition = transform.position;
        float dt = Time.deltaTime;

        HandleLook();

        // A hook embedded in a Titan must travel with its body instead of remaining
        // at the world-space point where the shot originally landed.
        RefreshAnchor(hookLeft);
        RefreshAnchor(hookRight);

        aimTimer -= dt;
        if (aimTimer <= 0f)
        {
            AimHasTarget = FindAnchor(null, out _);
            aimTimer = AIM_INTERVAL;
        }

        HandleHookInput();

        // CharacterController không có fixed simulation riêng. Chia frame dài thành các bước nhỏ
        // để gravity và rope constraint cho cùng kết quả ở các mức FPS khác nhau.
        float remaining = Mathf.Min(dt, 0.1f);
        float stepLimit = Mathf.Max(0.001f, maxSimulationStep);
        while (remaining > 0.000001f)
        {
            float step = Mathf.Min(remaining, stepLimit);
            SimulateMovement(step);
            MoveWithWallCollision(velocity * step);
            remaining -= step;
        }

        UpdateFov(dt);
        UpdateRopeVisuals();
        UpdateBackgroundMusic();
        LogRoofContact(frameStartPosition);
    }

    void UpdateBackgroundMusic()
    {
        if (backgroundMusicAudio == null || backgroundMusicAudio.clip == null || !backgroundMusicAudio.isPlaying)
            return;

        float loopEnd = Mathf.Min(backgroundMusicLoopEnd, backgroundMusicAudio.clip.length);
        if (backgroundMusicAudio.time >= loopEnd)
            backgroundMusicAudio.time = 0f;
    }

    void SimulateMovement(float dt)
    {
        ResolveControllerOverlap();
        bool wasGrounded = isGrounded;
        UpdateGrounded();

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
            LoosenRope(hookLeft);
            LoosenRope(hookRight);
            GroundMovement(dt);
        }
        else
            AirMovement(dt, hooked);
    }

    void ResolveControllerOverlap()
    {
        if (controller == null || !controller.enabled) return;

        Vector3 scale = transform.lossyScale;
        float worldHeight = controller.height * Mathf.Abs(scale.y);
        float worldRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        Vector3 center = transform.TransformPoint(controller.center);
        float halfSegment = Mathf.Max(0f, worldHeight * 0.5f - worldRadius);
        Vector3 top = center + Vector3.up * halfSegment;
        Vector3 bottom = center - Vector3.up * halfSegment;
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, worldRadius,
                                                   overlapHits, groundLayer,
                                                   QueryTriggerInteraction.Ignore);
        Vector3 correction = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider other = overlapHits[i];
            if (other == null || other == controller || other.transform.IsChildOf(transform)) continue;
            if (Physics.ComputePenetration(controller, transform.position, transform.rotation,
                                           other, other.transform.position, other.transform.rotation,
                                           out Vector3 direction, out float distance))
                correction += direction * distance;
        }

        if (correction.sqrMagnitude < 0.000001f) return;
        correction = Vector3.ClampMagnitude(correction, 0.75f);
        controller.enabled = false;
        transform.position += correction + correction.normalized * 0.015f;
        controller.enabled = true;
        velocity = Vector3.ProjectOnPlane(velocity, correction.normalized);
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
    void HandleLook()
    {
        yaw += GameInput.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= GameInput.GetAxis("Mouse Y") * mouseSensitivity;
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

    // ================= GROUND CHECK =================
    void UpdateGrounded()
    {
        hasCurrentGroundSurface = false;
        Vector3 scale = transform.lossyScale;
        float worldHeight = controller.height * Mathf.Abs(scale.y);
        float worldRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        Vector3 feet = transform.TransformPoint(controller.center)
                     + Vector3.down * (worldHeight * 0.5f - worldRadius + 0.1f);
        bool sphereGrounded = false;
        int count = Physics.OverlapSphereNonAlloc(feet, worldRadius * 0.95f, groundHits,
                                                  groundLayer, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = groundHits[i];
            if (hit != null && !hit.transform.IsChildOf(transform))
            {
                sphereGrounded = true;
                break;
            }
        }
        isGrounded = sphereGrounded || controller.isGrounded;

        // A sloped MeshCollider can sit between the controller's lower
        // hemisphere and the overlap volume for one frame.  A short downward
        // ray makes roof contact deterministic instead of switching to air
        // movement and appearing stuck.
        Vector3 rayOrigin = transform.TransformPoint(controller.center)
                          + Vector3.up * 0.2f;
        float rayLength = worldHeight * 0.5f + 0.45f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit,
                            rayLength, groundLayer, QueryTriggerInteraction.Ignore)
            && !groundHit.transform.IsChildOf(transform)
            && groundHit.normal.y > 0.05f)
        {
            currentGroundSurface = groundHit;
            hasCurrentGroundSurface = true;
            isGrounded = true;
        }
    }

    void LoosenRope(HookState hook)
    {
        if (!hook.active) return;

        float dist = Vector3.Distance(transform.position, hook.anchorPoint);
        if (dist > maxHookRange)
        {
            hook.active = false;
            return;
        }
        hook.ropeLength = Mathf.Max(hook.ropeLength, dist);
    }

    // ================= GROUND MOVEMENT =================
    void GroundMovement(float dt)
    {
        bool brakingLanding = landingBrakeWindow > 0f && GameInput.GetAxis("Vertical") < -0.5f;
        Vector3 wishDir = GetWishDirection(brakingLanding);
        bool boosting = GameInput.GetKey(KeyCode.LeftShift) && gas > 0f && wishDir.sqrMagnitude > 0.0001f;
        bool slidingRoof = TryGetSteepGround(out Vector3 roofNormal, out Vector3 downhill);
        float speed = boosting ? groundBoostSpeed : (slidingRoof ? 0f : walkSpeed);

        // Normal walking handles ordinary roof pitches.  A steeper roof only
        // becomes climbable while gas is held, which keeps rooftops useful
        // without letting the player casually walk up walls.
        controller.slopeLimit = boosting ? gasSlopeLimit : 89f;

        if (boosting && TryFindRoofSurface(wishDir, out RaycastHit roof))
        {
            Vector3 alongRoof = Vector3.ProjectOnPlane(wishDir, roof.normal);
            if (alongRoof.sqrMagnitude > 0.0001f)
            {
                alongRoof.Normalize();
                velocity = Vector3.MoveTowards(velocity, alongRoof * speed, groundAccel * dt);
                gas = Mathf.Max(0f, gas - gasDrain * groundGasDrainFactor * dt);
                return;
            }
        }

        if (slidingRoof && !boosting)
        {
            // Gravity projected onto the roof gives the real downhill
            // direction. Player input cannot create walking traction here;
            // use gas or reel the rope to get off the slope.
            if (downhill.sqrMagnitude > 0.0001f)
            {
                downhill.Normalize();
                Vector3 surfaceVelocity = Vector3.ProjectOnPlane(velocity, roofNormal);
                surfaceVelocity += downhill * roofSlideAcceleration * dt;
                surfaceVelocity = Vector3.ClampMagnitude(surfaceVelocity, roofSlideMaxSpeed);
                velocity = surfaceVelocity - roofNormal * roofSurfaceGrip;
                return;
            }
        }

        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        flat = Vector3.MoveTowards(flat, wishDir * speed, groundAccel * dt);
        velocity.x = flat.x;
        velocity.z = flat.z;

        velocity.y = -2f;

        if (boosting)
            gas = Mathf.Max(0f, gas - gasDrain * groundGasDrainFactor * dt);
    }

    bool TryFindRoofSurface(Vector3 wishDir, out RaycastHit best)
    {
        best = default;
        Vector3 forward = wishDir.normalized;
        if (forward.sqrMagnitude < 0.0001f) return false;
        Vector3 origin = transform.position + forward * gasRoofProbeDistance + Vector3.up * gasRoofProbeHeight;

        int count = Physics.RaycastNonAlloc(origin, Vector3.down, roofHits,
                                            gasRoofProbeHeight + controller.height + 2f,
                                            groundLayer, QueryTriggerInteraction.Ignore);
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = roofHits[i];
            if (candidate.collider == null || candidate.transform.IsChildOf(transform)) continue;
            float slope = candidate.normal.y;
            if (slope < 0.08f || slope > 0.995f) continue;
            Vector3 along = Vector3.ProjectOnPlane(forward, candidate.normal);
            if (along.sqrMagnitude < 0.0001f) continue;
            float ahead = Vector3.Dot(along.normalized, forward);
            if (ahead < 0.25f) continue;
            float score = ahead * 10f + candidate.point.y;
            if (score <= bestScore) continue;
            bestScore = score;
            best = candidate;
        }

        if (bestScore == float.NegativeInfinity) return false;
        float angle = Vector3.Angle(best.normal, Vector3.up);
        return angle > 2f && angle <= gasSlopeLimit;
    }

    bool TryGetSteepGround(out Vector3 surfaceNormal, out Vector3 downhill)
    {
        RaycastHit ground;
        if (hasCurrentGroundSurface)
            ground = currentGroundSurface;
        else
        {
            Vector3 origin = transform.position + Vector3.up * 1.25f;
            if (!Physics.Raycast(origin, Vector3.down, out ground, 3.5f, groundLayer,
                                 QueryTriggerInteraction.Ignore)
                || ground.transform.IsChildOf(transform))
            {
                surfaceNormal = Vector3.up;
                downhill = Vector3.zero;
                return false;
            }
        }

        // Do not trust a single imported MeshCollider triangle: this roof pack
        // has hidden ridge/seam triangles whose normals do not match what the
        // player sees. Estimate the visible surface from nearby hit heights.
        float radius = Mathf.Max(controller.radius * 1.25f, 0.55f);
        float sumXX = 0f, sumZZ = 0f, sumXH = 0f, sumZH = 0f;
        SampleGroundGradient(ground, new Vector3(radius, 0f, 0f), ref sumXX, ref sumZZ, ref sumXH, ref sumZH);
        SampleGroundGradient(ground, new Vector3(-radius, 0f, 0f), ref sumXX, ref sumZZ, ref sumXH, ref sumZH);
        SampleGroundGradient(ground, new Vector3(0f, 0f, radius), ref sumXX, ref sumZZ, ref sumXH, ref sumZH);
        SampleGroundGradient(ground, new Vector3(0f, 0f, -radius), ref sumXX, ref sumZZ, ref sumXH, ref sumZH);

        float gradientX = sumXX > 0.0001f ? sumXH / sumXX : 0f;
        float gradientZ = sumZZ > 0.0001f ? sumZH / sumZZ : 0f;
        float gradient = Mathf.Sqrt(gradientX * gradientX + gradientZ * gradientZ);
        float angle = Mathf.Atan(gradient) * Mathf.Rad2Deg;

        surfaceNormal = new Vector3(-gradientX, 1f, -gradientZ).normalized;
        downhill = new Vector3(-gradientX, -gradient * gradient, -gradientZ);
        if (downhill.sqrMagnitude > 0.0001f) downhill.Normalize();
        return angle >= roofSlideMinAngle;
    }

    void SampleGroundGradient(RaycastHit center, Vector3 offset,
                              ref float sumXX, ref float sumZZ,
                              ref float sumXH, ref float sumZH)
    {
        Vector3 origin = center.point + offset + Vector3.up * 2f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit sample, 4f,
                             groundLayer, QueryTriggerInteraction.Ignore))
            return;
        if (sample.collider != center.collider) return;

        float heightDelta = sample.point.y - center.point.y;
        if (Mathf.Abs(heightDelta) > 1.5f) return;

        sumXX += offset.x * offset.x;
        sumZZ += offset.z * offset.z;
        sumXH += offset.x * heightDelta;
        sumZH += offset.z * heightDelta;
    }

    // ================= AIR MOVEMENT =================
    void AirMovement(float dt, bool hooked)
    {
        bool dualHooked = hookLeft.active && hookRight.active;
        float speedLimit = maxSpeed * (dualHooked ? dualHookMaxSpeedMultiplier : 1f);
        bool boosting = GameInput.GetKey(KeyCode.LeftShift) && gas > 0f;

        velocity.y += gravity * dt;

        Vector3 wish = GetWishDirection();
        if ((!airControlRequiresGas || boosting) && wish.sqrMagnitude > 0.0001f)
        {
            Vector3 wishDir = wish.normalized;
            float along = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), wishDir);
            float add = Mathf.Clamp(airControlMaxSpeed - along, 0f, airControl * dt);
            velocity += wishDir * add;
        }

        if (boosting)
        {
            // Gas đẩy theo hướng camera; dây giới hạn bán kính và tạo cung đu.
            Vector3 gasDirection = cameraTransform.forward.normalized;
            float speedBeforeSteer = velocity.magnitude;
            if (steerRate > 0f && speedBeforeSteer > 0.5f)
            {
                velocity = Vector3.RotateTowards(velocity,
                                                 gasDirection * speedBeforeSteer,
                                                 steerRate * Mathf.Deg2Rad * dt,
                                                 0f);
            }

            float gasMultiplier = dualHooked ? dualHookGasMultiplier : 1f;
            velocity += gasDirection * gasForce * gasMultiplier * dt;
            gas = Mathf.Max(0f, gas - gasDrain * (hooked ? hookedGasDrainFactor : 1f) * dt);

            if (velocity.magnitude > speedLimit)
                velocity = velocity.normalized * speedLimit;
        }

        if (!hooked) return;

        if (autoTakeUpSlack)
        {
            TakeUpSlack(hookLeft, dt);
            TakeUpSlack(hookRight, dt);
        }

        bool strong = GameInput.GetKey(KeyCode.Space);
        float reelMultiplier = dualHooked ? dualHookReelMultiplier : 1f;
        ReelPull(hookLeft, dt, strong, reelMultiplier);
        ReelPull(hookRight, dt, strong, reelMultiplier);

        if (velocity.magnitude > speedLimit)
            velocity = velocity.normalized * speedLimit;

        float speedBeforeConstraint = velocity.magnitude;
        Vector3 predicted = transform.position + velocity * dt;
        bool constrained = false;
        for (int i = 0; i < constraintIterations; i++)
        {
            if ((i & 1) == 0)
            {
                if (hookLeft.active) constrained |= ApplyRopeConstraint(hookLeft, ref predicted);
                if (hookRight.active) constrained |= ApplyRopeConstraint(hookRight, ref predicted);
            }
            else
            {
                if (hookRight.active) constrained |= ApplyRopeConstraint(hookRight, ref predicted);
                if (hookLeft.active) constrained |= ApplyRopeConstraint(hookLeft, ref predicted);
            }
        }

        if (dt > 0f)
            velocity = (predicted - transform.position) / dt;

        if (constrained && cornerMomentumRetention > 0f && velocity.sqrMagnitude > 0.0001f)
        {
            float currentSpeed = velocity.magnitude;
            float retainedSpeed = Mathf.Lerp(currentSpeed,
                                             Mathf.Max(currentSpeed, speedBeforeConstraint),
                                             cornerMomentumRetention);
            velocity *= Mathf.Min(retainedSpeed, speedLimit) / currentSpeed;
        }

        velocity *= Mathf.Pow(ropeDamping, dt * 60f);

        if (velocity.magnitude > speedLimit)
            velocity = velocity.normalized * speedLimit;
    }

    void TakeUpSlack(HookState hook, float dt)
    {
        if (!hook.active || hook.attached) return;
        float dist = Vector3.Distance(transform.position, hook.anchorPoint);
        float target = Mathf.Max(attachDistance, dist);
        if (hook.ropeLength > target)
            hook.ropeLength = Mathf.MoveTowards(hook.ropeLength, target, slackTakeUpSpeed * dt);
    }

    void ReelPull(HookState hook, float dt, bool strong, float forceMultiplier)
    {
        if (!hook.active || hook.attached) return;

        Vector3 toAnchor = hook.anchorPoint - transform.position;
        float dist = toAnchor.magnitude;
        Vector3 dir = toAnchor / Mathf.Max(dist, 0.0001f);

        float intensity = strong ? 1f : 0f;
        if (!strong && autoReel)
        {
            float look = Vector3.Dot(cameraTransform.forward, dir);
            intensity = Mathf.InverseLerp(autoReelLookDot, 0.97f, look) * autoReelStrength;
        }
        if (reelOnlyWhenApproaching && Vector3.Dot(velocity, dir) < -reelAwayThreshold)
            intensity = 0f;

        if (intensity <= 0f) return;

        if (dist <= attachDistance)
        {
            hook.ropeLength = attachDistance;
            return;
        }

        if (strong)
            hook.ropeLength = Mathf.Max(attachDistance, hook.ropeLength - reelSpeed * dt);

        float approachSpeed = Vector3.Dot(velocity, dir);
        float cap = reelMaxSpeed * forceMultiplier * Mathf.Max(0.5f, intensity);
        float add = Mathf.Min(reelForce * forceMultiplier * intensity * dt,
                              Mathf.Max(0f, cap - approachSpeed));
        velocity += dir * add;
    }

    bool IsAttached(HookState hook)
    {
        return hook.active && hook.attached;
    }

    bool ApplyRopeConstraint(HookState hook, ref Vector3 predicted)
    {
        Vector3 toPos = predicted - hook.anchorPoint;
        float dist = toPos.magnitude;
        if (dist > hook.ropeLength && dist > 0.0001f)
        {
            predicted = hook.anchorPoint + toPos / dist * hook.ropeLength;
            return true;
        }

        return false;
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

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        lastContactCollider = hit.collider;
        lastContactPoint = hit.point;
        lastContactNormal = hit.normal;
        lastContactMoveDirection = hit.moveDirection;
        lastContactTime = Time.time;
        ApplyWallContact(hit.normal);
    }

    void LogRoofContact(Vector3 frameStartPosition)
    {
        if (!logRoofCollisionDebug || Time.time < nextRoofDebugLog) return;

        float input = new Vector2(GameInput.GetAxis("Horizontal"), GameInput.GetAxis("Vertical")).magnitude;
        bool tryingToMove = input > 0.2f || GameInput.GetKey(KeyCode.LeftShift)
                           || (hookLeft.active || hookRight.active) && GameInput.GetKey(KeyCode.Space);
        float moved = Vector3.Distance(frameStartPosition, transform.position);
        bool stuck = tryingToMove && moved <= 0.025f;
        Vector3 scale = transform.lossyScale;
        float worldHeight = controller.height * Mathf.Abs(scale.y);
        float worldRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        Vector3 worldCenter = transform.TransformPoint(controller.center);
        float capsuleBottomY = worldCenter.y - worldHeight * 0.5f;

        string groundInfo = "groundHit=none";
        float groundAngle = 0f;
        if (Physics.Raycast(worldCenter + Vector3.up * 0.2f, Vector3.down,
                            out RaycastHit ground, worldHeight + 2f, groundLayer,
                            QueryTriggerInteraction.Ignore)
            && !ground.transform.IsChildOf(transform))
        {
            groundAngle = Vector3.Angle(ground.normal, Vector3.up);
            groundInfo = $"ground={ground.collider.name}, pointY={ground.point.y:F3}, "
                       + $"bottomY={capsuleBottomY:F3}, gap={capsuleBottomY - ground.point.y:F3}, "
                       + $"normal={ground.normal}, angle={groundAngle:F1}";
            Debug.DrawRay(ground.point, ground.normal * 3f, Color.cyan, 1f);
        }

        // Keep normal gameplay logs quiet. Sample the first surface under the
        // player even when the collider reports it as flat; imported roof
        // meshes can contain hidden horizontal triangles around the ridge.
        if (!stuck && loggedRoofContactSample) return;
        if (!stuck) loggedRoofContactSample = true;
        nextRoofDebugLog = Time.time + 0.75f;

        string contactInfo = Time.time - lastContactTime < 0.75f && lastContactCollider != null
            ? $"contact={lastContactCollider.name}, point={lastContactPoint}, normal={lastContactNormal}, "
              + $"angle={Vector3.Angle(lastContactNormal, Vector3.up):F1}, moveDir={lastContactMoveDirection}"
            : "contact=none";

        string message = $"[RoofCollision] {(stuck ? "STUCK" : "CONTACT")} pos={transform.position}, "
                       + $"moved={moved:F4}, velocity={velocity}, "
                       + $"grounded={isGrounded}/{controller.isGrounded}, "
                       + $"slopeLimit={controller.slopeLimit:F1}, slideMin={roofSlideMinAngle:F1}, "
                       + $"cachedGround={hasCurrentGroundSurface}, radius={worldRadius:F2}; "
                       + groundInfo + "; " + contactInfo;
        if (stuck) Debug.LogWarning(message, this);
        else Debug.Log(message, this);
    }

    void MoveWithWallCollision(Vector3 displacement)
    {
        if (displacement.sqrMagnitude < 0.0000001f)
        {
            controller.Move(displacement);
            return;
        }

        Vector3 scale = transform.lossyScale;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = controller.height * Mathf.Abs(scale.y);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 center = transform.TransformPoint(controller.center);
        Vector3 top = center + Vector3.up * halfSegment;
        Vector3 bottom = center - Vector3.up * halfSegment;
        Vector3 direction = displacement.normalized;
        float distance = displacement.magnitude;
        float castRadius = Mathf.Max(0.01f, radius - wallCollisionSkin);

        if (Physics.CapsuleCast(bottom, top, castRadius, direction, out RaycastHit hit,
                                distance + wallCollisionSkin, movementCollisionMask,
                                QueryTriggerInteraction.Ignore)
            && !hit.transform.IsChildOf(transform))
        {
            // Let CharacterController resolve real roof slopes.  The custom
            // wall slide below is for vertical walls; applying it to a roof
            // would erase the uphill component and trap the player in place.
            if (isGrounded && hit.normal.y > 0.55f)
            {
                controller.Move(displacement);
                return;
            }

            float safeDistance = Mathf.Max(0f, hit.distance - wallCollisionSkin);
            if (safeDistance < distance)
            {
                controller.Move(direction * safeDistance);

                // Preserve movement that runs along the wall, while discarding
                // the component that would push the controller through it.
                Vector3 remaining = displacement - direction * safeDistance;
                Vector3 slide = Vector3.ProjectOnPlane(remaining, hit.normal);
                if (slide.sqrMagnitude > 0.0000001f)
                    controller.Move(slide);

                ApplyWallContact(hit.normal);
                return;
            }
        }

        controller.Move(displacement);
    }

    void ApplyWallContact(Vector3 normal)
    {
        if (isGrounded && normal.y > 0.1f) return;

        float into = Vector3.Dot(velocity, normal); // < 0: đang lao vào bề mặt
        if (into >= 0f) return;

        Vector3 incoming = velocity;
        Vector3 alongWall = Vector3.ProjectOnPlane(incoming, normal);
        bool isWall = Mathf.Abs(normal.y) < 0.5f;
        bool hooked = hookLeft.active || hookRight.active;

        if (wallGlideAssist && hooked && isWall)
        {
            // Đổi một phần cú đâm trực diện thành chuyển động theo mặt tường về hướng camera.
            // Người chơi vẫn phải nhìn/chọn hướng cua, nhưng va quệt không làm mất sạch đà.
            Vector3 desired = Vector3.ProjectOnPlane(cameraTransform.forward, normal);
            if (desired.sqrMagnitude > 0.0001f)
            {
                desired.Normalize();
                float targetAlong = incoming.magnitude * wallGlideMomentum;
                float currentAlong = Vector3.Dot(alongWall, desired);
                if (currentAlong < targetAlong)
                    alongWall += desired * (targetAlong - currentAlong);
            }

            alongWall += normal * wallSeparationSpeed;
        }

        float speedLimit = maxSpeed * (ActiveHookCount >= 2 ? dualHookMaxSpeedMultiplier : 1f);
        velocity = Vector3.ClampMagnitude(alongWall, speedLimit);
    }

    // ================= HOOK INPUT =================
    void HandleHookInput()
    {
        bool leftDown  = GameInput.GetMouseButtonDown(0);
        bool leftHeld  = GameInput.GetMouseButton(0);
        bool leftUp    = GameInput.GetMouseButtonUp(0);
        bool rightDown = GameInput.GetMouseButtonDown(1);
        bool rightHeld = GameInput.GetMouseButton(1);
        bool rightUp   = GameInput.GetMouseButtonUp(1);

        if (toggleHooks)
        {
            if (leftDown)
            {
                if (hookLeft.active) ReleaseHook(hookLeft, -1f);
                else TryFireHook(hookLeft, hipOriginLeft);
            }

            if (rightDown)
            {
                if (hookRight.active) ReleaseHook(hookRight, 1f);
                else TryFireHook(hookRight, hipOriginRight);
            }
        }
        else
        {
            if (leftDown) TryFireHook(hookLeft, hipOriginLeft);
            if (leftUp) ReleaseHook(hookLeft, -1f);

            if (rightDown) TryFireHook(hookRight, hipOriginRight);
            if (rightUp) ReleaseHook(hookRight, 1f);
        }

        AutoReleaseCheck(hookLeft);
        AutoReleaseCheck(hookRight);
    }

    // Tự nhả khi: dây căng, đã vượt qua điểm neo (neo nằm sau lưng), đang bay lên và đủ nhanh.
    // Nhả xong phải bấm lại phím mới bắn được dây mới.
    void AutoReleaseCheck(HookState hook)
    {
        if (!autoRelease || !hook.active || hook.attached) return;
        if (!autoReleaseWhileReeling && GameInput.GetKey(KeyCode.Space)) return; // tùy chọn: đang chủ động thu dây thì không nhả
        if (velocity.magnitude < autoReleaseMinSpeed || velocity.y <= 0.5f) return;

        Vector3 toAnchor = hook.anchorPoint - transform.position;
        if (toAnchor.magnitude < hook.ropeLength - 0.5f) return; // dây chưa căng

        Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 flatTo = new Vector3(toAnchor.x, 0f, toAnchor.z);
        if (flatVel.sqrMagnitude < 0.01f || flatTo.sqrMagnitude < 0.01f) return;

        if (Vector3.Dot(flatVel.normalized, flatTo.normalized) < autoReleaseBehindDot)
            ReleaseHook(hook, hook == hookLeft ? -1f : 1f);
    }

    void TryFireHook(HookState hook, Transform hip)
    {
        if (!FindAnchor(hip, out RaycastHit hit)) return;

        Vector3 anchor = LiftAnchor(hit);

        hook.active = true;
        hook.anchorPoint = anchor;
        hook.anchorTransform = hit.collider != null ? hit.collider.transform : null;
        hook.followsTransform = hook.anchorTransform != null;
        hook.anchorLocalPoint = hook.anchorTransform != null
            ? hook.anchorTransform.InverseTransformPoint(anchor)
            : Vector3.zero;
        hook.visualProgress = 0f;
        hook.ropeLength = Vector3.Distance(transform.position, anchor);
        hook.attached = false;
        PlayHookSound(hookLaunchClip, hookLaunchVolume, hook == hookLeft ? -1f : 1f);

        if (groundHookYank && isGrounded && velocity.y <= 0.1f)
        {
            Vector3 dir = (anchor - transform.position).normalized;
            Vector3 yank = new Vector3(dir.x, Mathf.Max(dir.y, 0.35f), dir.z).normalized * groundHookYankSpeed;
            velocity += yank;
            isGrounded = false;
        }
    }

    void ReleaseHook(HookState hook, float side)
    {
        if (!hook.active) return;
        hook.active = false;
        hook.followsTransform = false;
        hook.anchorTransform = null;
        PlayHookSound(hookReleaseClip, hookReleaseVolume, side);
    }

    void PlayHookSound(AudioClip clip, float volume, float side)
    {
        if (hookAudio == null || clip == null) return;
        hookAudio.panStereo = side * 0.18f;
        hookAudio.pitch = Random.Range(0.97f, 1.03f);
        hookAudio.PlayOneShot(clip, volume);
    }

    void RefreshAnchor(HookState hook)
    {
        if (!hook.active || !hook.followsTransform) return;
        if (hook.anchorTransform == null)
        {
            hook.active = false;
            hook.followsTransform = false;
            return;
        }
        hook.anchorPoint = hook.anchorTransform.TransformPoint(hook.anchorLocalPoint);
    }

    // Bắn trúng tường nhà thì nâng điểm neo lên gần mái: neo càng cao thì càng đu được lên cao và có chỗ để vung.
    Vector3 LiftAnchor(RaycastHit hit)
    {
        if (!liftAnchorToRoof) return hit.point;
        if (hit.normal.y > liftWallMaxNormalY) return hit.point;          // không phải mặt tường
        if (hit.point.y - transform.position.y >= liftSkipIfAbovePlayer) return hit.point;

        Collider c = hit.collider;
        if (c == null || !SupportsClosestPoint(c)) return hit.point;

        float topY = c.bounds.max.y - liftRoofMargin;
        float wantY = Mathf.Min(topY, hit.point.y + liftMaxHeight);
        wantY = Mathf.Min(wantY, transform.position.y + maxHookRange * 0.7f);
        if (wantY <= hit.point.y + 0.75f) return hit.point;

        Vector3 raised = new Vector3(hit.point.x, wantY, hit.point.z) + hit.normal * 0.5f;
        Vector3 p = c.ClosestPoint(raised);

        if (Vector3.Distance(transform.position, p) > maxHookRange * 1.1f) return hit.point;

        // Dây không được xuyên qua nhà khác
        Vector3 from = cameraTransform.position;
        Vector3 to = p + hit.normal * 0.1f;
        if (Physics.Linecast(from, to, hookableLayer, QueryTriggerInteraction.Ignore)) return hit.point;

        return p;
    }

    static bool SupportsClosestPoint(Collider c)
    {
        if (c is BoxCollider || c is SphereCollider || c is CapsuleCollider) return true;
        MeshCollider m = c as MeshCollider;
        return m != null && m.convex;
    }

    // Ưu tiên tia thẳng/SphereCast từ tâm camera (tôn trọng ý định nhắm).
    // Trượt thì quét hình nón và chọn điểm có điểm số cao nhất.
    // hip = null thì bỏ qua kiểm tra bị che (dùng cho chấm ngắm).
    bool FindAnchor(Transform hip, out RaycastHit hit)
    {
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        // Titan body colliders are triggers so they cannot physically pin the
        // player. Hook casts deliberately include them; movement casts ignore
        // triggers and therefore continue to collide only with world geometry.
        bool found = Physics.Raycast(ray, out hit, maxHookRange, hookableLayer, QueryTriggerInteraction.Collide);
        if (!found && assistRadius > 0f)
            found = Physics.SphereCast(ray, assistRadius, out hit, maxHookRange, hookableLayer, QueryTriggerInteraction.Collide);

        if (found && (hip == null || HipClear(hip, hit.point)))
            return true;

        if (smartAim)
            return SmartScan(hip, out hit);

        hit = default;
        return false;
    }

    bool SmartScan(Transform hip, out RaycastHit best)
    {
        best = default;
        bool any = false;
        float bestScore = float.NegativeInfinity;

        Vector3 origin = cameraTransform.position;
        Vector3 fwd = cameraTransform.forward;

        Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);
        bool moving = flatVel.sqrMagnitude > 4f;
        if (moving) flatVel.Normalize();

        int rings = Mathf.Max(1, smartAimRings);
        int samples = Mathf.Max(3, smartAimSamples);

        for (int r = 1; r <= rings; r++)
        {
            float angle = smartAimAngle * r / rings;
            Vector3 tilted = Quaternion.AngleAxis(angle, cameraTransform.up) * fwd;

            for (int k = 0; k < samples; k++)
            {
                // Lệch pha giữa các vòng để phủ đều hơn
                float roll = 360f * k / samples + ((r % 2) * 0.5f * 360f / samples);
                Vector3 dir = Quaternion.AngleAxis(roll, fwd) * tilted;

                if (!Physics.Raycast(origin, dir, out RaycastHit h, maxHookRange, hookableLayer, QueryTriggerInteraction.Collide))
                    continue;
                if (h.distance < minHookDistance) continue;
                if (hip != null && !HipClear(hip, h.point)) continue;

                float look = 1f - angle / smartAimAngle; // 1 = sát tâm ngắm

                float height = Mathf.Clamp01((h.point.y - transform.position.y) / (maxHookRange * 0.5f));

                float momentum = 0f;
                if (moving)
                {
                    Vector3 flatTo = h.point - transform.position;
                    flatTo.y = 0f;
                    if (flatTo.sqrMagnitude > 0.01f)
                        momentum = Vector3.Dot(flatTo.normalized, flatVel);
                }

                float score = look * aimLookWeight + height * aimHeightWeight + momentum * aimMomentumWeight;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = h;
                    any = true;
                }
            }
        }
        return any;
    }

    bool HipClear(Transform hip, Vector3 point)
    {
        Vector3 toHit = point - hip.position;
        float d = toHit.magnitude;
        return !(d > 0.2f && Physics.Raycast(hip.position, toHit / d, d - 0.1f, hookableLayer, QueryTriggerInteraction.Ignore));
    }

    // ================= ROPE VISUAL =================
    LineRenderer CreateRopeLine(string lineName)
    {
        GameObject obj = new GameObject(lineName);
        obj.transform.SetParent(transform);

        LineRenderer lr = obj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = ropeWidth;
        lr.endWidth = ropeWidth * 0.72f;
        lr.useWorldSpace = true;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;

        if (ropeMaterial != null)
        {
            lr.material = ropeMaterial;
        }
        else
        {
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(0.34f, 0.34f, 0.36f, 1f);
            lr.endColor = new Color(0.46f, 0.43f, 0.43f, 1f);
        }

        lr.enabled = false;
        return lr;
    }

    void UpdateRopeVisuals()
    {
        UpdateSingleLine(lineLeft, hookLeft, hipOriginLeft);
        UpdateSingleLine(lineRight, hookRight, hipOriginRight);
    }

    void UpdateSingleLine(LineRenderer line, HookState hook, Transform origin)
    {
        if (hook.active)
        {
            line.enabled = true;
            Vector3 start = origin != null ? origin.position : transform.position;
            float distance = Vector3.Distance(start, hook.anchorPoint);
            float duration = distance / Mathf.Max(1f, hookProjectileSpeed);
            hook.visualProgress = Mathf.MoveTowards(hook.visualProgress, 1f,
                Time.unscaledDeltaTime / Mathf.Max(0.025f, duration));
            float eased = 1f - Mathf.Pow(1f - hook.visualProgress, 3f);

            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, Vector3.Lerp(start, hook.anchorPoint, eased));
        }
        else
        {
            line.enabled = false;
        }
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
