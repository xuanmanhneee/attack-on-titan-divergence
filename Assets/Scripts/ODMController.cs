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
    public bool autoTakeUpSlack = true;    // tang cuốn giữ dây căng; không tạo lực kéo hay tự chọn hướng
    public float slackTakeUpSpeed = 60f;
    public bool autoReel = false;          // manual ODM: nhìn về móc không tự kéo
    public float autoReelStrength = 0.6f;  // 0..1, so với kéo mạnh bằng Space
    public float autoReelLookDot = 0.6f;   // dot(hướng nhìn, hướng tới móc) bắt đầu kéo từ mức này
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
    public bool groundHookYank = false;         // bắn móc không tự tạo vận tốc
    public float groundHookYankSpeed = 10f;
    public float swingKeepSpeed = 14f;          // đu nhanh hơn mức này thì lướt sát đất không bị hãm như đang đi bộ

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

    [Header("Wall Launch")]
    public float wallLaunchSpeed = 16f;    // tốc độ bật ra khỏi tường khi nhấn Shift lúc đang bám
    public float wallLaunchGasCost = 10f;

    [Header("Rope Visual")]
    public Material ropeMaterial;
    public float ropeWidth = 0.03f;

    [Header("Debug")]
    public bool showDebugGizmos = true;

    public bool AimHasTarget { get; private set; }
    public float GasNormalized => gas / maxGas;
    public float Speed => velocity.magnitude;
    public bool IsGrounded => isGrounded;
    public int ActiveHookCount => (hookLeft.active ? 1 : 0) + (hookRight.active ? 1 : 0);
    public void RefillGas(float amount) { gas = Mathf.Min(maxGas, gas + amount); }

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

    private class HookState
    {
        public bool active;
        public Vector3 anchorPoint;
        public float ropeLength;
        public bool attached;
    }

    private HookState hookLeft = new HookState();
    private HookState hookRight = new HookState();
    private LineRenderer lineLeft;
    private LineRenderer lineRight;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        cam = cameraTransform.GetComponent<Camera>();
        Cursor.lockState = CursorLockMode.Locked;

        yaw = transform.eulerAngles.y;
        gas = maxGas;

        lineLeft = CreateRopeLine("RopeLine_Left");
        lineRight = CreateRopeLine("RopeLine_Right");
    }

    void Update()
    {
        float dt = Time.deltaTime;

        HandleLook();

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
            controller.Move(velocity * step);
            remaining -= step;
        }

        UpdateFov(dt);
        UpdateRopeVisuals();
    }

    void SimulateMovement(float dt)
    {
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

        // Chạm đất hoặc mái thì ưu tiên chạy, kể cả vẫn đang giữ móc. Dây nhả thêm
        // theo bước chân và chỉ tiếp quản trở lại khi người chơi chủ động thu bằng Space.
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
    }

    // Khi đứng trên đất, dây tự nhả thêm theo khoảng cách (đi xa điểm neo thì dây dài ra).
    // Bước ra khỏi mép thì dây đang căng đúng độ dài lúc đó và bắt đầu đu.
    // Đi quá xa tầm móc thì dây tự đứt.
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
        float speed = boosting ? groundBoostSpeed : walkSpeed;

        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        flat = Vector3.MoveTowards(flat, wishDir * speed, groundAccel * dt);
        velocity.x = flat.x;
        velocity.z = flat.z;

        velocity.y = -2f;

        if (boosting)
            gas = Mathf.Max(0f, gas - gasDrain * groundGasDrainFactor * dt);
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

        if (GameInput.GetKey(KeyCode.LeftShift) && gas > 0f)
        {
            // Một quy tắc duy nhất: gas luôn đẩy theo camera. Dây chỉ giới hạn bán kính,
            // nên nhìn sang bên khi đang bám một móc sẽ tạo cung bẻ hướng dễ đoán.
            Vector3 gasDirection = cameraTransform.forward.normalized;

            // Uốn quỹ đạo mượt theo camera thay vì đổi hướng vận tốc tức thời.
            // A/D vẫn bổ sung một lượng air control nhỏ ở khối phía trên.
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

        // Tang cuốn thụ động chỉ lấy phần dây chùng. Nó không rút ngắn hơn khoảng cách
        // hiện tại nên không tạo lực và không điều khiển thay người chơi.
        if (autoTakeUpSlack)
        {
            TakeUpSlack(hookLeft, dt);
            TakeUpSlack(hookRight, dt);
        }

        // Thu dây: Space = kéo mạnh; không bấm thì kéo nhẹ nếu đang nhìn về phía móc
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
            // Đổi thứ tự mỗi vòng để hai dây không ưu tiên cố định dây trái.
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

        // Projection dây thật làm mất toàn bộ động lượng hướng ra ngoài. ODM kiểu anime
        // chuyển một phần năng lượng đó sang tiếp tuyến để cú bẻ góc vẫn nhanh và dễ đọc.
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

        // Ý định của người chơi: kéo mạnh nếu bấm Space, kéo nhẹ nếu đang nhìn về phía móc
        float intensity = strong ? 1f : 0f;
        if (!strong && autoReel)
        {
            float look = Vector3.Dot(cameraTransform.forward, dir);
            intensity = Mathf.InverseLerp(autoReelLookDot, 0.97f, look) * autoReelStrength;
        }
        // Đang bay ra xa điểm neo (đã vượt qua nó): Space không hãm lại, giữ nguyên đà
        if (reelOnlyWhenApproaching && Vector3.Dot(velocity, dir) < -reelAwayThreshold) intensity = 0f;

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
        float add = Mathf.Min(reelForce * forceMultiplier * intensity * dt, Mathf.Max(0f, cap - approachSpeed));
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
        float into = Vector3.Dot(velocity, hit.normal); // < 0: đang lao vào bề mặt
        if (into >= 0f) return;

        Vector3 incoming = velocity;
        Vector3 alongWall = Vector3.ProjectOnPlane(incoming, hit.normal);
        bool isWall = Mathf.Abs(hit.normal.y) < 0.5f;
        bool hooked = hookLeft.active || hookRight.active;

        if (wallGlideAssist && hooked && isWall)
        {
            // Đổi một phần cú đâm trực diện thành chuyển động theo mặt tường về hướng camera.
            // Người chơi vẫn phải nhìn/chọn hướng cua, nhưng va quệt không làm mất sạch đà.
            Vector3 desired = Vector3.ProjectOnPlane(cameraTransform.forward, hit.normal);
            if (desired.sqrMagnitude > 0.0001f)
            {
                desired.Normalize();
                float targetAlong = incoming.magnitude * wallGlideMomentum;
                float currentAlong = Vector3.Dot(alongWall, desired);
                if (currentAlong < targetAlong)
                    alongWall += desired * (targetAlong - currentAlong);
            }

            alongWall += hit.normal * wallSeparationSpeed;
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
                if (hookLeft.active) hookLeft.active = false;
                else TryFireHook(hookLeft, hipOriginLeft);
            }

            if (rightDown)
            {
                if (hookRight.active) hookRight.active = false;
                else TryFireHook(hookRight, hipOriginRight);
            }
        }
        else
        {
            if (leftDown) TryFireHook(hookLeft, hipOriginLeft);
            if (leftUp) hookLeft.active = false;

            if (rightDown) TryFireHook(hookRight, hipOriginRight);
            if (rightUp) hookRight.active = false;
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
            hook.active = false;
    }

    void TryFireHook(HookState hook, Transform hip)
    {
        if (!FindAnchor(hip, out RaycastHit hit)) return;

        Vector3 anchor = LiftAnchor(hit);

        hook.active = true;
        hook.anchorPoint = anchor;
        hook.ropeLength = Vector3.Distance(transform.position, anchor);
        hook.attached = false;

        // Đang đứng đất: giật người lên không ngay lập tức (đỡ phải giữ Space mới rời đất được)
        if (groundHookYank && isGrounded && velocity.y <= 0.1f)
        {
            Vector3 dir = (anchor - transform.position).normalized;
            Vector3 yank = new Vector3(dir.x, Mathf.Max(dir.y, 0.35f), dir.z).normalized * groundHookYankSpeed;
            velocity += yank;
            isGrounded = false;
        }
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

        bool found = Physics.Raycast(ray, out hit, maxHookRange, hookableLayer, QueryTriggerInteraction.Ignore);
        if (!found && assistRadius > 0f)
            found = Physics.SphereCast(ray, assistRadius, out hit, maxHookRange, hookableLayer, QueryTriggerInteraction.Ignore);

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

                if (!Physics.Raycast(origin, dir, out RaycastHit h, maxHookRange, hookableLayer, QueryTriggerInteraction.Ignore))
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
        lr.endWidth = ropeWidth;
        lr.useWorldSpace = true;

        if (ropeMaterial != null)
        {
            lr.material = ropeMaterial;
        }
        else
        {
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.white;
            lr.endColor = Color.white;
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
            line.SetPosition(0, origin != null ? origin.position : transform.position);
            line.SetPosition(1, hook.anchorPoint);
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
