using UnityEngine;

// ODM Gear Controller - v3 (góc nhìn thứ nhất, có lớp hỗ trợ tự động)
// Q = bắn/giữ móc trái, E = bắn/giữ móc phải (nhả phím = thả dây)
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
    public float walkSpeed = 4f;
    public float groundBoostSpeed = 12f;
    public float groundGasDrainFactor = 0.5f;
    public float groundAccel = 30f;
    public float gravity = -20f;

    [Header("Air")]
    public float airControl = 14f;
    public float airControlMaxSpeed = 12f;

    [Header("Hook")]
    public LayerMask hookableLayer;
    public float maxHookRange = 40f;
    public float assistRadius = 2.5f;
    public float ropeDamping = 0.998f;
    public int constraintIterations = 3;

    [Header("Reel")]
    public float reelForce = 35f;
    public float reelMaxSpeed = 18f;
    public float attachDistance = 1.2f;
    public bool requireReelToAttach = true;

    [Header("Smart Assist - Aim")]
    public bool smartAim = true;           // tia thẳng trượt thì quét hình nón tìm điểm tốt nhất
    public float smartAimAngle = 35f;      // nửa góc của nón quét (độ)
    public int smartAimRings = 2;          // số vòng quét
    public int smartAimSamples = 8;        // số tia mỗi vòng (tổng tia = rings * samples)
    public float minHookDistance = 4f;     // bỏ qua điểm quá gần (móc vào đó vô ích)
    public float aimLookWeight = 1f;       // ưu tiên điểm gần tâm ngắm
    public float aimHeightWeight = 0.6f;   // ưu tiên điểm cao hơn người chơi (swing cần neo ở trên)
    public float aimMomentumWeight = 0.5f; // ưu tiên điểm nằm theo hướng đang bay

    [Header("Smart Assist - Rope")]
    public bool autoTakeUpSlack = true;    // dây tự rút theo khoảng cách ngắn nhất đã đạt, không bao giờ chùng
    public bool autoReel = true;           // nhìn về phía móc thì dây tự kéo nhẹ
    public float autoReelStrength = 0.6f;  // 0..1, so với kéo mạnh bằng Space
    public float autoReelLookDot = 0.6f;   // dot(hướng nhìn, hướng tới móc) bắt đầu kéo từ mức này
    public bool autoRelease = true;       // tự nhả khi đã vượt qua điểm neo và đang bay lên (mặc định tắt)
    public float autoReleaseMinSpeed = 10f;
    public float autoReleaseBehindDot = -0.2f; // điểm neo nằm sau lưng tới mức này thì nhả (âm = sau lưng)

    [Header("Smart Assist - Dễ chơi hơn")]
    public bool reelOnlyWhenApproaching = true;   // Space không kéo khi bạn đang bay ra xa điểm neo (không bị hãm đà)
    public float reelAwayThreshold = 2f;          // bay ra xa nhanh hơn mức này (m/s) thì Space ngừng kéo
    public bool autoReleaseWhileReeling = true;   // vẫn tự nhả dây khi vượt qua điểm neo kể cả đang giữ Space

    [Header("Smart Assist - Neo cao (đu được lên cao)")]
    public bool liftAnchorToRoof = true;        // bắn trúng tường nhà thì điểm neo được nâng lên gần mái nhà
    public float liftMaxHeight = 16f;           // nâng tối đa bao nhiêu mét so với chỗ ngắm trúng
    public float liftRoofMargin = 0.6f;         // chừa cách mép mái
    public float liftSkipIfAbovePlayer = 8f;   // điểm ngắm đã cao hơn người chơi mức này thì không nâng nữa
    public float liftWallMaxNormalY = 0.35f;    // chỉ nâng khi trúng mặt tường (pháp tuyến gần nằm ngang)
    public bool groundHookYank = true;          // bắn móc khi đang đứng đất: bị giật lên không ngay, khỏi phải nhấn Space
    public float groundHookYankSpeed = 10f;
    public float swingKeepSpeed = 14f;          // đu nhanh hơn mức này thì lướt sát đất không bị hãm như đang đi bộ

    [Header("Gas")]
    public float gasForce = 32f;
    public float steerRate = 130f;
    public float maxSpeed = 45f;
    public float maxGas = 140f;
    public float gasDrain = 12f;
    public float gasRegen = 40f;
    public float hookedGasDrainFactor = 0.6f;   // đang có dây móc thì Shift tốn gas ít hơn
    public float airGasRegen = 5f;              // hồi gas chậm khi đang trên không mà không bấm Shift
    public float gasRegenDelay = 1f;            // phải ngừng dùng Shift bấy nhiêu giây mới bắt đầu hồi

    [Header("Wall Collision")]
    public float bounceRestitution = 0.45f;   // 0 = trượt dọc tường, 1 = nảy lại nguyên tốc độ
    public float bounceMinSpeed = 4f;         // tốc độ văng ra tối thiểu khi va mạnh
    public float hardImpactSpeed = 7f;        // va nhanh hơn mức này: nảy ra và nhả dây gần chỗ va
    public float anchorReleaseRadius = 8f;    // nhả móc có điểm neo trong bán kính này quanh chỗ va
    public float attachMaxImpactSpeed = 6f;   // chỉ bám tường khi chạm nhẹ (đang giữ Space)

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
    public void RefillGas(float amount) { gas = Mathf.Min(maxGas, gas + amount); }

    // Cho hệ thống chiến đấu (lướt tới gáy titan) cộng thêm vận tốc
    public void AddVelocity(Vector3 v) { velocity += v; }

    private CharacterController controller;
    private Camera cam;

    private Vector3 velocity;
    private bool isGrounded;
    private float gas;

    private float pitch;
    private float yaw;

    private float logTimer = 0f;
    private const float LOG_INTERVAL = 0.15f;

    private float gasIdle = 0f;
    private float aimTimer = 0f;
    private const float AIM_INTERVAL = 0.05f; // cập nhật chấm ngắm 20 lần/giây cho nhẹ

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
        UpdateGrounded();

        bool hooked = hookLeft.active || hookRight.active;

        // Đang đứng trên đất: mặt đất đỡ người, dây không được phép kéo lê như con lắc.
        // Chỉ chuyển sang chế độ dây (AirMovement) khi: đang thu dây bằng Space, hoặc đã bám vào bề mặt.
        bool standing = isGrounded && velocity.y <= 0.1f;
        bool swingingFast = hooked && velocity.sqrMagnitude > swingKeepSpeed * swingKeepSpeed;
        bool ropeTakesOver = hooked && (GameInput.GetKey(KeyCode.Space) || IsAttached(hookLeft) || IsAttached(hookRight) || swingingFast);

        if (standing && !ropeTakesOver)
        {
            LoosenRope(hookLeft);
            LoosenRope(hookRight);
            GroundMovement(dt);
        }
        else
            AirMovement(dt, hooked);

        UpdateFov(dt);
        UpdateRopeVisuals();

        controller.Move(velocity * dt);
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
        Vector3 feet = transform.position + controller.center
                     + Vector3.down * (controller.height * 0.5f - controller.radius + 0.1f);
        isGrounded = Physics.CheckSphere(feet, controller.radius * 0.95f, groundLayer, QueryTriggerInteraction.Ignore)
                     || controller.isGrounded;
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
        Vector3 wishDir = GetWishDirection();
        bool boosting = GameInput.GetKey(KeyCode.LeftShift) && gas > 0f && wishDir.sqrMagnitude > 0.0001f;
        float speed = boosting ? groundBoostSpeed : walkSpeed;

        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        flat = Vector3.MoveTowards(flat, wishDir * speed, groundAccel * dt);
        velocity.x = flat.x;
        velocity.z = flat.z;

        velocity.y = -2f;

        if (boosting)
        {
            gas = Mathf.Max(0f, gas - gasDrain * groundGasDrainFactor * dt);

            logTimer += dt;
            if (logTimer >= LOG_INTERVAL)
            {
                float currentSpeed = new Vector2(velocity.x, velocity.z).magnitude;
                Debug.Log($"<color=#55ff55>[GAS - GROUND]</color> Speed: {currentSpeed:F1} m/s (Target: {speed:F1} m/s) | Gas: {gas:F0}/{maxGas}");
                logTimer = 0f;
            }
        }
        else
            gas = Mathf.Min(maxGas, gas + gasRegen * dt);
    }

    // ================= AIR MOVEMENT =================
    void AirMovement(float dt, bool hooked)
    {
        bool boosting = GameInput.GetKey(KeyCode.LeftShift) && gas > 0f;
        if (boosting) gasIdle = 0f;
        else
        {
            gasIdle += dt;
            if (gasIdle >= gasRegenDelay) gas = Mathf.Min(maxGas, gas + airGasRegen * dt);
        }

        if (hooked && (IsAttached(hookLeft) || IsAttached(hookRight)))
        {
            // Đang bám tường: nhấn Shift để bật ra theo hướng nhìn (thoát trạng thái dính tường)
            if (GameInput.GetKeyDown(KeyCode.LeftShift) && gas > 0f)
            {
                hookLeft.attached = false;
                hookRight.attached = false;
                velocity = cameraTransform.forward * wallLaunchSpeed;
                gas = Mathf.Max(0f, gas - wallLaunchGasCost);
                return;
            }
            velocity = Vector3.zero;
            return;
        }

        velocity.y += gravity * dt;

        Vector3 wish = GetWishDirection();
        if (wish.sqrMagnitude > 0.0001f)
        {
            Vector3 wishDir = wish.normalized;
            float along = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), wishDir);
            float add = Mathf.Clamp(airControlMaxSpeed - along, 0f, airControl * dt);
            velocity += wishDir * add;
        }

        if (GameInput.GetKey(KeyCode.LeftShift) && gas > 0f)
        {
            float currentSpeed = velocity.magnitude;
            if (currentSpeed > 0.5f)
            {
                velocity = Vector3.RotateTowards(velocity, cameraTransform.forward * currentSpeed,
                                                 steerRate * Mathf.Deg2Rad * dt, 0f);
            }

            velocity += cameraTransform.forward * gasForce * dt;
            gas = Mathf.Max(0f, gas - gasDrain * (hooked ? hookedGasDrainFactor : 1f) * dt);

            if (velocity.magnitude > maxSpeed)
                velocity = velocity.normalized * maxSpeed;
        }

        if (!hooked) return;

        // Rút dây chùng: độ dài dây chỉ giảm theo khoảng cách ngắn nhất đã đạt
        if (autoTakeUpSlack)
        {
            TakeUpSlack(hookLeft);
            TakeUpSlack(hookRight);
        }

        // Thu dây: Space = kéo mạnh; không bấm thì kéo nhẹ nếu đang nhìn về phía móc
        bool strong = GameInput.GetKey(KeyCode.Space);
        ReelPull(hookLeft, dt, strong);
        ReelPull(hookRight, dt, strong);

        if (velocity.magnitude > maxSpeed)
            velocity = velocity.normalized * maxSpeed;

        Vector3 predicted = transform.position + velocity * dt;
        for (int i = 0; i < constraintIterations; i++)
        {
            if (hookLeft.active) ApplyRopeConstraint(hookLeft, ref predicted);
            if (hookRight.active) ApplyRopeConstraint(hookRight, ref predicted);
        }

        if (dt > 0f)
            velocity = (predicted - transform.position) / dt;

        velocity *= Mathf.Pow(ropeDamping, dt * 60f);
    }

    void TakeUpSlack(HookState hook)
    {
        if (!hook.active || hook.attached) return;
        float dist = Vector3.Distance(transform.position, hook.anchorPoint);
        // Không rút ngắn hơn attachDistance, tránh nhân vật bị kẹt sát điểm neo
        hook.ropeLength = Mathf.Max(attachDistance, Mathf.Min(hook.ropeLength, dist));
    }

    void ReelPull(HookState hook, float dt, bool strong)
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
            if (strong) hook.attached = true; // chỉ bám khi chủ động giữ Space
            return;
        }

        float approachSpeed = Vector3.Dot(velocity, dir);
        float cap = reelMaxSpeed * Mathf.Max(0.5f, intensity);
        float add = Mathf.Min(reelForce * intensity * dt, Mathf.Max(0f, cap - approachSpeed));
        velocity += dir * add;

        hook.ropeLength = Mathf.Min(hook.ropeLength, dist);
    }

    bool IsAttached(HookState hook)
    {
        return hook.active && hook.attached;
    }

    void ApplyRopeConstraint(HookState hook, ref Vector3 predicted)
    {
        Vector3 toPos = predicted - hook.anchorPoint;
        float dist = toPos.magnitude;
        if (dist > hook.ropeLength && dist > 0.0001f)
            predicted = hook.anchorPoint + toPos / dist * hook.ropeLength;
    }

    Vector3 GetWishDirection()
    {
        float h = GameInput.GetAxis("Horizontal");
        float v = GameInput.GetAxis("Vertical");
        Vector3 fwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 dir = fwd * v + right * h;
        return dir.sqrMagnitude > 1f ? dir.normalized : dir;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        bool hooked = hookLeft.active || hookRight.active;
        bool wantsAttach = !requireReelToAttach || GameInput.GetKey(KeyCode.Space);
        bool hookableSurface = ((1 << hit.gameObject.layer) & hookableLayer.value) != 0;

        bool wall = hit.normal.y < 0.7f; // tường hoặc trần; mặt gần nằm ngang coi là mặt đất/mái
        float into = Vector3.Dot(velocity, hit.normal); // < 0: đang lao vào bề mặt
        float impact = -into;

        // Chạm nhẹ khi đang giữ Space: bám vào tường. Chạm mạnh: không bám mà nảy ra.
        if (hooked && wantsAttach && hookableSurface && wall && impact <= attachMaxImpactSpeed)
        {
            hookLeft.attached = hookLeft.active;
            hookRight.attached = hookRight.active;
            return;
        }

        if (into < 0f)
        {
            Vector3 along = Vector3.ProjectOnPlane(velocity, hit.normal);

            if (wall && impact >= hardImpactSpeed)
            {
                // Va mạnh vào tường: nảy ra. Dây neo gần chỗ va phải nhả, nếu không dây căng sẽ kéo lại gây cảm giác "dính".
                float away = Mathf.Max(impact * bounceRestitution, bounceMinSpeed);
                velocity = along + hit.normal * away;
                ReleaseHooksNear(hit.point);
            }
            else
            {
                velocity = along;
            }
        }
    }

    void ReleaseHooksNear(Vector3 point)
    {
        if (hookLeft.active && Vector3.Distance(hookLeft.anchorPoint, point) <= anchorReleaseRadius)
        {
            hookLeft.active = false;
            hookLeft.attached = false;
        }
        if (hookRight.active && Vector3.Distance(hookRight.anchorPoint, point) <= anchorReleaseRadius)
        {
            hookRight.active = false;
            hookRight.attached = false;
        }
    }

    // ================= HOOK INPUT =================
    void HandleHookInput()
    {
        bool leftDown  = GameInput.GetKeyDown(KeyCode.Q) || GameInput.GetMouseButtonDown(0);
        bool leftUp    = GameInput.GetKeyUp(KeyCode.Q)   || GameInput.GetMouseButtonUp(0);
        bool rightDown = GameInput.GetKeyDown(KeyCode.E) || GameInput.GetMouseButtonDown(1);
        bool rightUp   = GameInput.GetKeyUp(KeyCode.E)   || GameInput.GetMouseButtonUp(1);

        if (leftDown)  TryFireHook(hookLeft, hipOriginLeft);
        if (leftUp)    hookLeft.active = false;

        if (rightDown) TryFireHook(hookRight, hipOriginRight);
        if (rightUp)   hookRight.active = false;

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