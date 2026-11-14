using UnityEngine;

// Hoá titan. G = biến hình / trở lại người.
// Khi là titan: tắt hẳn bộ cơ động (không móc, không gas, không kiếm), chỉ chạy bộ và đấm tay đôi.
//   Chuột trái / F = đấm nhanh (luân phiên hai tay)   Chuột phải = đấm mạnh hai tay
//   WASD = đi, Shift = chạy, Space = nhảy
// Đấm trúng gáy gây sát thương gấp 3. Hết giờ hoặc nhấn G lần nữa thì về dạng người và phải chờ hồi.
// Tự gắn vào người chơi bởi GameManager (không dùng ở Chapter 0).
[DisallowMultipleComponent]
[RequireComponent(typeof(ODMController))]
public class TitanForm : MonoBehaviour
{
    [Header("Biến hình")]
    public KeyCode transformKey = KeyCode.G;
    public float formHeight = 10f;
    public float formRadius = 1.3f;
    public float duration = 45f;
    public float cooldown = 30f;
    public float transformTime = 1.2f;
    [Range(0f, 1f)] public float damageTakenMultiplier = 0.5f;

    [Header("Di chuyển")]
    public float walkSpeed = 12f;
    public float runSpeed = 20f;
    public float acceleration = 40f;
    public float jumpSpeed = 11f;
    public float gravity = -30f;

    [Header("Đấm")]
    public float punchDamage = 45f;
    public float heavyMultiplier = 2f;
    public float napeMultiplier = 3f;
    public float punchReach = 7.5f;
    public float punchAngle = 60f;
    public float punchTime = 0.32f;
    public float heavyPunchTime = 0.55f;
    public float punchCooldown = 0.15f;

    public bool IsTitan => state == State.Transforming || state == State.Titan;

    enum State { Human, Transforming, Titan }

    ODMController odm;
    PlayerCombat combat;
    Health health;
    CharacterController controller;
    Transform cam;
    ThirdPersonObservationCamera thirdPerson;
    GameManager manager;

    State state = State.Human;
    float stateTimer;     // Transforming: thời gian đã qua. Titan: thời gian còn lại.
    float cooldownLeft;
    float flash;

    // Giá trị gốc để khôi phục
    float oldHeight, oldRadius, oldStep;
    Vector3 oldCenter, oldCamLocal;
    float eyeLocalY;
    static readonly string[] ViewmodelNames = { "ODM_Viewmodel", "WRAD_Sword_Viewmodel" };
    readonly System.Collections.Generic.List<GameObject> hiddenViewmodels = new System.Collections.Generic.List<GameObject>();
    FirstPersonSwordViewmodel swordViewmodel;

    // Chuyển động
    Vector3 velocity;
    float bobPhase;

    // Đấm
    [Header("Model")]
    public string modelName = "AttackTitan";
    // Chỉ dùng khi model không có xương Head / eyeFromHead: camera đứng trước mặt trước nhất của thân.
    public float eyeForward = 0.25f;
    public float eyeUp = 0.1f;

    [Header("Animation đấm (clip Humanoid trong FBX của model)")]
    public string punchClipName = "Punching";
    public float punchAnimSpeed = 0.9f;   // tốc độ phát cú đấm đơn (chuột trái)
    public float comboAnimSpeed = 1f;     // tốc độ phát chuỗi đấm liên hoàn (chuột phải)

    [Header("Animation chạy (clip Humanoid lặp, trong Resources)")]
    public string runClipPath = "Titans/AttackTitanRigged/AttackTitanTreadmillRunning";
    public string runClipName = "Running";
    public float runAnimSpeed = 1f;       // tốc độ phát clip khi chạy bằng runSpeed (Shift)
    public float minRunAnimSpeed = 0.6f;  // tốc độ phát tối thiểu khi đi chậm
    [Range(0f, 1f)] public float headFollow = 0.6f; // camera đi theo chuyển động đầu của animation bao nhiêu
    // Thu nhỏ xương đầu để camera (đặt trong đầu) không thấy mặt trong của đầu/miệng/tóc.
    // Đổi lại bóng đổ không có đầu; dạng titan chỉ dùng góc nhìn thứ nhất nên không thấy được.
    public bool collapseHead = true;
    // Tư thế thủ của clip giữ nắm đấm sát cằm -> che giữa màn hình. Lúc không đấm thì hạ tay xuống và
    // tách sang hai bên (độ), khi ra đòn giảm dần về 0 để cú đấm vẫn vươn thẳng ra giữa.
    public float guardLowerUpperArm = 12f;
    public float guardLowerForearm = 15f;
    public float guardSpread = 20f;
    public float legSwingAngle = 28f;
    public float kneeBendAngle = 35f;

    Transform bodyRoot;            // gốc model, đặt ở chân capsule
    Transform holder;
    TitanProceduralAnim proc;
    TitanPunchAnimator punchAnim;
    Transform headBone;
    Transform[] legBones;          // đùi trái, đùi phải, cẳng chân trái, cẳng chân phải
    Transform[] armBones;          // cánh tay trên trái, phải, cẳng tay trái, phải
    Vector3 eyeInHead;             // điểm mắt so với xương đầu (theo hướng xương, không theo scale)
    Vector3 restEyeHolder;         // điểm mắt lúc thủ thế, trong không gian holder
    float walkAmount;

    // Các đoạn trong clip Mixamo "Punching" (30 fps): khung bắt đầu, khung trúng đích, khung kết thúc.
    // Cú đấm đơn luân phiên: trái - phải - trái - phải. Chuột phải phát chuỗi 4 cú liên hoàn.
    const float ClipFps = 30f;
    static readonly Vector3Int[] lightSegments =
    {
        new Vector3Int(8, 14, 19),   // trái
        new Vector3Int(29, 34, 39),  // phải
        new Vector3Int(50, 55, 59),  // trái
        new Vector3Int(39, 43, 49),  // phải
    };
    const int comboStart = 50, comboEnd = 90;
    static readonly int[] comboHits = { 55, 62, 69, 76 };
    int lightIndex;
    float[] pendingHits = new float[0];
    int nextHit;
    bool clipHeavy;

    float punchT = 1f;
    float punchLen;
    bool punchHeavy;
    bool punchRightArm;
    bool punchHitDone;
    float punchCooldownLeft;

    void Awake()
    {
        odm = GetComponent<ODMController>();
        combat = GetComponent<PlayerCombat>();
        health = GetComponent<Health>();
        controller = GetComponent<CharacterController>();
        thirdPerson = GetComponent<ThirdPersonObservationCamera>();
    }

    void Start()
    {
        cam = odm.cameraTransform;
        manager = FindAnyObjectByType<GameManager>();
        if (combat == null) combat = GetComponent<PlayerCombat>();
        if (health == null) health = GetComponent<Health>();
        if (thirdPerson == null) thirdPerson = GetComponent<ThirdPersonObservationCamera>();
    }

    void Update()
    {
        if (Time.timeScale <= 0.0001f) return;
        float dt = Time.deltaTime;

        flash = Mathf.Max(0f, flash - dt * 1.4f);
        if (cooldownLeft > 0f) cooldownLeft -= dt;

        switch (state)
        {
            case State.Human:
                if (GameInput.GetKeyDown(transformKey))
                {
                    if (cooldownLeft > 0f) Say("Chưa thể hoá titan (" + Mathf.CeilToInt(cooldownLeft) + "s)");
                    else BeginTransform();
                }
                break;

            case State.Transforming:
                stateTimer += dt;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateTimer / transformTime));
                SetCameraLocalY(Mathf.Lerp(oldCamLocal.y, eyeLocalY, k));
                if (stateTimer >= transformTime)
                {
                    state = State.Titan;
                    stateTimer = duration;
                    health.DamageMultiplier = damageTakenMultiplier;
                    Say("TITAN! Chuột trái/phải: đấm");
                }
                break;

            case State.Titan:
                stateTimer -= dt;
                TitanLook();
                TitanMove(dt);
                TitanPunch(dt);
                if (GameInput.GetKeyDown(transformKey) || stateTimer <= 0f) EndTransform();
                break;
        }
    }

    // ================= BIẾN HÌNH =================
    void BeginTransform()
    {
        if (cam == null) return;

        odm.ReleaseAllHooks();
        odm.ResetMotion();
        odm.suspended = true;
        if (combat != null) combat.enabled = false;
        if (thirdPerson != null) thirdPerson.enabled = false; // OnDisable tự thoát camera góc thứ ba

        // Ẩn mọi viewmodel dạng người: bộ cơ động và tay cầm kiếm. Component kiếm tự bật hiển thị mỗi khung
        // (khi cúi xuống / chém) nên phải tắt cả component chứ không chỉ ẩn đối tượng.
        hiddenViewmodels.Clear();
        foreach (string n in ViewmodelNames)
        {
            Transform vm = cam.Find(n);
            if (vm != null && vm.gameObject.activeSelf) { vm.gameObject.SetActive(false); hiddenViewmodels.Add(vm.gameObject); }
        }
        swordViewmodel = GetComponent<FirstPersonSwordViewmodel>();
        if (swordViewmodel != null && swordViewmodel.enabled) swordViewmodel.enabled = false;
        else swordViewmodel = null;

        // Phình capsule giữ nguyên vị trí bàn chân.
        oldHeight = controller.height;
        oldRadius = controller.radius;
        oldCenter = controller.center;
        oldStep = controller.stepOffset;
        float feetY = transform.position.y + oldCenter.y - oldHeight * 0.5f;

        controller.enabled = false;
        controller.height = formHeight;
        controller.radius = formRadius;
        controller.center = Vector3.zero;
        controller.stepOffset = 1.2f;
        transform.position = new Vector3(transform.position.x, feetY + formHeight * 0.5f + 0.05f, transform.position.z);
        controller.enabled = true;

        oldCamLocal = cam.localPosition;
        eyeLocalY = formHeight * 0.5f - 0.9f; // dự phòng nếu model không tải được

        BuildVisual();
        velocity = Vector3.zero;
        punchT = 1f;
        punchCooldownLeft = 0f;

        health.DamageMultiplier = 0f; // bất tử trong lúc biến hình
        flash = 1f;
        state = State.Transforming;
        stateTimer = 0f;
        Say("Hoá titan!");
    }

    void EndTransform()
    {
        float feetY = transform.position.y - formHeight * 0.5f;

        controller.enabled = false;
        controller.height = oldHeight;
        controller.radius = oldRadius;
        controller.center = oldCenter;
        controller.stepOffset = oldStep;
        transform.position = new Vector3(transform.position.x, feetY - oldCenter.y + oldHeight * 0.5f + 0.05f, transform.position.z);
        controller.enabled = true;

        if (cam != null) cam.localPosition = oldCamLocal;
        DestroyVisual();

        foreach (var vm in hiddenViewmodels) if (vm != null) vm.SetActive(true);
        hiddenViewmodels.Clear();
        if (swordViewmodel != null) swordViewmodel.enabled = true; // LateUpdate của nó tự ẩn/hiện lại tay kiếm
        swordViewmodel = null;
        if (thirdPerson != null) thirdPerson.enabled = true;
        if (combat != null) combat.enabled = true;

        odm.Yaw = transform.eulerAngles.y;
        odm.ResetMotion();
        odm.suspended = false;
        health.DamageMultiplier = 1f;

        state = State.Human;
        cooldownLeft = cooldown;
        flash = 1f;
        Say("Trở lại dạng người");
    }

    void OnDisable()
    {
        // Bị tắt giữa chừng (đổi scene, chết...): trả mọi thứ về như cũ.
        if (state != State.Human && odm != null && health != null && controller != null) EndTransform();
    }

    // ================= ĐIỀU KHIỂN TITAN =================
    void TitanLook()
    {
        odm.Yaw += GameInput.GetAxis("Mouse X") * odm.mouseSensitivity;
        odm.Pitch -= GameInput.GetAxis("Mouse Y") * odm.mouseSensitivity;
        odm.Pitch = Mathf.Clamp(odm.Pitch, -70f, 85f);
        transform.rotation = Quaternion.Euler(0f, odm.Yaw, 0f);
        cam.localRotation = Quaternion.Euler(odm.Pitch, 0f, 0f);
    }

    void TitanMove(float dt)
    {
        float h = GameInput.GetAxis("Horizontal");
        float v = GameInput.GetAxis("Vertical");
        Vector3 wish = transform.right * h + transform.forward * v;
        if (wish.sqrMagnitude > 1f) wish.Normalize();

        float speed = GameInput.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        flat = Vector3.MoveTowards(flat, wish * speed, acceleration * dt);
        velocity.x = flat.x;
        velocity.z = flat.z;

        bool grounded = controller.isGrounded;
        if (grounded && velocity.y < 0f) velocity.y = -2f;
        if (grounded && GameInput.GetKeyDown(KeyCode.Space)) velocity.y = jumpSpeed;
        velocity.y += gravity * dt;

        controller.Move(velocity * dt);

        // Bước chân nặng nề: camera nhấp nhô theo nhịp chạy
        float moveAmount = grounded ? flat.magnitude / runSpeed : 0f;
        bobPhase += flat.magnitude * dt * 0.45f;
        float bob = Mathf.Abs(Mathf.Sin(bobPhase)) * 0.35f * moveAmount;
        // Clip chạy đã làm đầu nhấp nhô (camera bám đầu) -> bớt nhấp nhô giả.
        if (punchAnim != null && punchAnim.HasRun) bob *= 1f - punchAnim.RunWeight;
        SetCameraLocalY(eyeLocalY + bob);
        walkAmount = Mathf.MoveTowards(walkAmount, grounded && flat.sqrMagnitude > 0.5f ? flat.magnitude / runSpeed : 0f, dt * 3f);
        if (punchAnim != null)
        {
            bool moving = grounded && flat.sqrMagnitude > 0.5f;
            punchAnim.Locomotion = moving ? Mathf.Clamp01(flat.magnitude / Mathf.Max(0.01f, walkSpeed * 0.5f)) : 0f;
            punchAnim.RunSpeed = Mathf.Max(minRunAnimSpeed, flat.magnitude / Mathf.Max(0.01f, runSpeed)) * runAnimSpeed;
        }
        if (proc != null)
        {
            proc.walkPhase = bobPhase;
            proc.moveAmount = grounded && flat.sqrMagnitude > 0.5f ? 1f : 0f;
        }
    }

    void SetCameraLocalY(float y)
    {
        Vector3 p = cam.localPosition;
        p.y = y;
        cam.localPosition = p;
    }

    // ================= ĐẤM =================
    void TitanPunch(float dt)
    {
        if (punchAnim != null) { ClipPunch(); return; }
        punchCooldownLeft -= dt;

        if (punchT < 1f)
        {
            punchT = Mathf.Min(1f, punchT + dt / punchLen);
            if (!punchHitDone && punchT >= 0.4f)
            {
                punchHitDone = true;
                ResolvePunch(punchDamage * (punchHeavy ? heavyMultiplier : 1f), punchReach * (punchHeavy ? 1.15f : 1f));
            }
        }
        else if (punchCooldownLeft <= 0f)
        {
            bool light = GameInput.GetMouseButtonDown(0) || GameInput.GetKeyDown(KeyCode.F);
            bool heavy = GameInput.GetMouseButtonDown(1);
            if (light || heavy) StartPunch(heavy);
        }

        AnimateFists();
    }

    void StartPunch(bool heavy)
    {
        punchHeavy = heavy;
        punchLen = heavy ? heavyPunchTime : punchTime;
        punchT = 0f;
        punchHitDone = false;
        punchCooldownLeft = punchLen + punchCooldown;
        if (!heavy) punchRightArm = !punchRightArm;
    }

    // Đấm bằng clip: gây sát thương đúng khung nắm đấm duỗi hết. Cú đơn được nối ngay sau khi trúng
    // (bấm liên tục ra combo trái-phải), chuỗi liên hoàn thì phải đánh hết mới ra đòn mới.
    void ClipPunch()
    {
        while (nextHit < pendingHits.Length && punchAnim.CurrentTime >= pendingHits[nextHit])
        {
            nextHit++;
            if (clipHeavy) ResolvePunch(punchDamage * heavyMultiplier * 0.5f, punchReach * 1.1f);
            else ResolvePunch(punchDamage, punchReach);
        }

        bool free = !punchAnim.Playing || (!clipHeavy && nextHit >= pendingHits.Length);
        if (!free) return;

        if (GameInput.GetMouseButtonDown(1))
        {
            clipHeavy = true;
            pendingHits = new float[comboHits.Length];
            for (int i = 0; i < comboHits.Length; i++) pendingHits[i] = comboHits[i] / ClipFps;
            nextHit = 0;
            punchAnim.Play(comboStart / ClipFps, comboEnd / ClipFps, comboAnimSpeed);
        }
        else if (GameInput.GetMouseButtonDown(0) || GameInput.GetKeyDown(KeyCode.F))
        {
            Vector3Int seg = lightSegments[lightIndex];
            lightIndex = (lightIndex + 1) % lightSegments.Length;
            clipHeavy = false;
            pendingHits = new[] { seg.y / ClipFps };
            nextHit = 0;
            punchAnim.Play(seg.x / ClipFps, seg.z / ClipFps, punchAnimSpeed);
        }
    }

    // Sau khi Animator đã dựng tư thế: thêm bước chân và cho camera đi theo đầu.
    void LateUpdate()
    {
        if (state == State.Human || punchAnim == null || headBone == null || cam == null) return;
        if (Time.timeScale <= 0.0001f) return;

        // Có clip chạy thì chân/tay đã do clip điều khiển; bước chân giả và tư thế thủ chỉ bù phần còn lại.
        float noRun = 1f - punchAnim.RunWeight;
        float legAmount = walkAmount * noRun;
        if (legBones != null && legAmount > 0.001f)
        {
            float sw = Mathf.Sin(bobPhase);
            Vector3 axis = transform.right;
            RotateBone(legBones[0], -legSwingAngle * sw * legAmount, axis);
            RotateBone(legBones[1], legSwingAngle * sw * legAmount, axis);
            RotateBone(legBones[2], kneeBendAngle * Mathf.Max(0f, sw) * legAmount, axis);
            RotateBone(legBones[3], kneeBendAngle * Mathf.Max(0f, -sw) * legAmount, axis);
        }

        float guard = (1f - punchAnim.ActionWeight) * noRun;
        if (armBones != null && guard > 0.001f)
        {
            Vector3 right = transform.right, up = transform.up;
            // Xoay quanh trục ngang (dương = hạ xuống), rồi quanh trục đứng để tách hai tay ra hai bên.
            RotateBone(armBones[0], guardLowerUpperArm * guard, right);
            RotateBone(armBones[1], guardLowerUpperArm * guard, right);
            RotateBone(armBones[0], -guardSpread * guard, up);
            RotateBone(armBones[1], guardSpread * guard, up);
            RotateBone(armBones[2], guardLowerForearm * guard, right);
            RotateBone(armBones[3], guardLowerForearm * guard, right);
        }

        // Update đã đặt lại độ cao camera (eyeLocalY + nhấp nhô); ở đây cộng thêm độ lệch của đầu.
        Vector3 rest = holder.TransformPoint(restEyeHolder);
        Vector3 eye = EyeWorld();
        Vector3 delta = transform.InverseTransformVector(eye - rest) * headFollow;
        Vector3 p = cam.localPosition;
        cam.localPosition = new Vector3(delta.x, p.y + delta.y, delta.z);
    }

    Vector3 EyeWorld() => headBone.position + headBone.rotation * eyeInHead;

    static void RotateBone(Transform bone, float angle, Vector3 axis)
    {
        if (bone != null) bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
    }

    void ResolvePunch(float damage, float reach)
    {
        bool hitAny = false;

        foreach (Titan t in Titan.All.ToArray())
        {
            if (t == null || t.IsDead) continue;

            // Gáy: đòn chí mạng
            Vector3 toNape = t.Nape.position - cam.position;
            if (toNape.magnitude <= reach + 1.5f && Vector3.Angle(cam.forward, toNape) <= punchAngle * 0.6f)
            {
                t.Damage(damage * napeMultiplier);
                hitAny = true;
                continue;
            }

            foreach (Collider col in t.BodyColliders)
            {
                if (col == null) continue;
                Vector3 to = col.ClosestPoint(cam.position) - cam.position;
                if (to.magnitude <= reach && Vector3.Angle(cam.forward, to) <= punchAngle)
                {
                    t.Damage(damage);
                    hitAny = true;
                    break;
                }
            }
        }

        if (hitAny) flash = Mathf.Max(flash, 0.12f);
    }

    // Tay đấm bằng animation Humanoid của model: giơ tay rồi đập xuống, luân phiên trái/phải.
    void AnimateFists()
    {
        if (proc == null) return;

        float raise = 0f, slam = 0f;
        if (punchT < 1f)
        {
            if (punchT < 0.4f) raise = punchT / 0.4f;
            else slam = 1f - (punchT - 0.4f) / 0.6f;
        }
        proc.attackRaise = raise;
        proc.attackSlam = slam;
        proc.attackMaskL = punchHeavy || !punchRightArm ? 1f : 0f;
        proc.attackMaskR = punchHeavy || punchRightArm ? 1f : 0f;
    }

    // ================= HÌNH ẢNH =================
    // Dùng model titan thật làm thân người chơi. Model Humanoid có clip đấm: camera đặt ở mắt và bám theo
    // xương đầu, tóc/mắt/răng chỉ đổ bóng để không che tầm nhìn. Model khác: camera đứng trước mặt.
    void BuildVisual()
    {
        var def = TitanModels.Get(modelName);
        GameObject prefab = def != null ? Resources.Load<GameObject>(def.modelPath) : null;
        if (prefab == null) { Debug.LogWarning("[TitanForm] Không tải được model '" + modelName + "', chỉ có camera."); return; }

        // Đo kích thước ở gốc toạ độ, không xoay, để kết quả không phụ thuộc vị trí/hướng người chơi.
        var measure = new GameObject("TitanForm_Measure").transform;
        var model = Instantiate(prefab, measure);
        model.name = def.name;
        Quaternion importRotation = model.transform.localRotation;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, def.yawOffset, 0f) * importRotation;
        // Prefab FBX có scale gốc riêng; đo ở scale 1 để hệ số s bên dưới là tuyệt đối (giống Titan.BuildFromModel).
        model.transform.localScale = Vector3.one;
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);

        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Destroy(measure.gameObject); return; }

        Bounds b = Titan.ExactBounds(renderers);
        if (b.size.y < 0.0001f) b = Titan.CalcBounds(renderers);
        float s = formHeight / b.size.y;
        model.transform.localScale = Vector3.one * s;
        model.transform.localPosition = new Vector3(-b.center.x * s, -(b.center.y - b.extents.y) * s, -b.center.z * s);
        Titan.ApplyMaterials(renderers, def);
        if (def.hideInFirstPerson != null)
        {
            foreach (var r in renderers)
                foreach (string part in def.hideInFirstPerson)
                    if (r.name.Contains(part)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        // feetRoot (chân capsule) > holder (TitanFootGrounder dịch lên/xuống) > model
        bodyRoot = new GameObject("TitanForm_Body").transform;
        bodyRoot.SetParent(transform, false);
        bodyRoot.localPosition = new Vector3(0f, -formHeight * 0.5f, 0f);
        holder = new GameObject("Holder").transform;
        holder.SetParent(bodyRoot, false);
        // Giữ toạ độ cục bộ (đã tính quanh gốc measure) -> model đứng đúng dưới chân người chơi.
        model.transform.SetParent(holder, false);
        Destroy(measure.gameObject);

        var animator = model.GetComponentInChildren<Animator>();
        bool human = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
        if (human)
        {
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            AnimationClip runClip = string.IsNullOrEmpty(runClipPath) ? null : FindClip(runClipPath, runClipName);
            if (!string.IsNullOrEmpty(runClipPath) && runClip == null)
                Debug.LogWarning("[TitanForm] Không tải được clip chạy '" + runClipPath + "', dùng bước chân giả.");
            punchAnim = TitanPunchAnimator.TryCreate(model, FindClip(def.modelPath, punchClipName), 0f, runClip); // dựng tư thế thủ ngay
            legBones = new[]
            {
                animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg),
                animator.GetBoneTransform(HumanBodyBones.RightLowerLeg),
            };
            armBones = new[]
            {
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
            };
        }

        if (headBone != null && def.eyeFromHead != Vector3.zero)
        {
            // Mắt = xương đầu + độ lệch (đơn vị model, nhân scale). Dời model để mắt nằm đúng trên trục camera.
            Vector3 eyeWorld = headBone.position + holder.rotation * (def.eyeFromHead * s);
            eyeInHead = Quaternion.Inverse(headBone.rotation) * (eyeWorld - headBone.position);
            Vector3 eyeLocal = bodyRoot.InverseTransformPoint(eyeWorld);
            model.transform.localPosition -= new Vector3(eyeLocal.x, 0f, eyeLocal.z);
            restEyeHolder = holder.InverseTransformPoint(EyeWorld());
            eyeLocalY = -formHeight * 0.5f + eyeLocal.y;
            if (collapseHead) headBone.localScale = Vector3.one * 0.001f; // animation Humanoid không ghi scale nên giữ nguyên
        }
        else
        {
            // Đầu: dùng xương Head nếu có, không thì ước lượng theo gáy. Camera đứng trước mặt trước nhất của
            // thân (mặt/bụng) một khoảng eyeForward -> không bị kẹt trong đầu.
            Vector3 head = headBone != null
                ? bodyRoot.InverseTransformPoint(headBone.position)
                : new Vector3(0f, formHeight * (def.napeHeight + 0.03f), 0f);
            float frontZ = b.extents.z * s;
            model.transform.localPosition += new Vector3(-head.x, 0f, -frontZ - eyeForward);
            eyeLocalY = -formHeight * 0.5f + head.y + eyeUp;
            headBone = null; // không bám theo đầu khi không có điểm mắt
        }

        if (punchAnim == null) proc = TitanProceduralAnim.TryCreate(model);
        TitanFootGrounder.TryCreate(model, bodyRoot, holder);
    }

    static AnimationClip FindClip(string resourcePath, string clipName)
    {
        AnimationClip fallback = null;
        foreach (var c in Resources.LoadAll<AnimationClip>(resourcePath))
        {
            if (c == null || c.name.StartsWith("__preview__")) continue;
            if (c.name == clipName) return c;
            if (fallback == null && c.humanMotion) fallback = c;
        }
        return fallback;
    }

    void DestroyVisual()
    {
        if (bodyRoot != null) Destroy(bodyRoot.gameObject);
        bodyRoot = null;
        holder = null;
        proc = null;
        punchAnim = null;
        headBone = null;
        legBones = null;
        armBones = null;
        walkAmount = 0f;
        pendingHits = new float[0];
        nextHit = 0;
    }

    void Say(string m)
    {
        if (manager == null) manager = FindAnyObjectByType<GameManager>();
        if (manager != null) manager.Say(m);
    }

    // ================= HUD =================
    void OnGUI()
    {
        float s = Screen.height / 1080f;

        if (flash > 0f)
        {
            Color old = GUI.color;
            GUI.color = new Color(1f, 0.95f, 0.7f, Mathf.Clamp01(flash));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        if (Time.timeScale <= 0.0001f) return;

        float x = 30f * s, y = Screen.height - 150f * s, w = 360f * s, hgt = 22f * s;
        float fill;
        string text;
        Color col;
        if (state == State.Titan)
        {
            fill = Mathf.Clamp01(stateTimer / duration);
            text = "TITAN  " + Mathf.CeilToInt(stateTimer) + "s   (G: trở lại người)";
            col = new Color(0.95f, 0.45f, 0.15f, 0.95f);
        }
        else if (state == State.Human && cooldownLeft > 0f)
        {
            fill = 1f - Mathf.Clamp01(cooldownLeft / cooldown);
            text = "Hồi sức hoá titan  " + Mathf.CeilToInt(cooldownLeft) + "s";
            col = new Color(0.55f, 0.55f, 0.6f, 0.9f);
        }
        else if (state == State.Human)
        {
            fill = 1f;
            text = "G: hoá titan (sẵn sàng)";
            col = new Color(1f, 0.82f, 0.15f, 0.95f);
        }
        else return;

        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(x, y, w, hgt), Texture2D.whiteTexture);
        GUI.color = col;
        GUI.DrawTexture(new Rect(x + 3f * s, y + 3f * s, (w - 6f * s) * fill, hgt - 6f * s), Texture2D.whiteTexture);
        GUI.color = prev;

        var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * s) };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y - 30f * s, 700f * s, 30f * s), text, style);
    }
}
