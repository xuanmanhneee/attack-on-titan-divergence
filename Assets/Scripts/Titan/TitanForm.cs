using UnityEngine;

// Hoá titan. G = biến hình / trở lại người.
// Khi là titan: tắt hẳn bộ cơ động (không móc, không gas, không kiếm), chỉ chạy bộ và đấm tay đôi.
//   Chuột trái / F = đấm nhanh (luân phiên hai tay)   Chuột phải = đấm mạnh hai tay
//   WASD = đi, Shift = chạy, Space = nhảy
// Đấm trúng gáy gây sát thương gấp 3. Hết giờ hoặc nhấn G lần nữa thì về dạng người và phải chờ hồi.
// Tự gắn vào người chơi bởi GameManager (không dùng ở Chapter 0).
[DisallowMultipleComponent]
[RequireComponent(typeof(ODMController))]
[RequireComponent(typeof(PlayerStamina))]
public class TitanForm : MonoBehaviour
{
    [Header("Biến hình")]
    public KeyCode transformKey = KeyCode.G;
    public float formHeight = 10f;
    public float formRadius = 1.3f;
    // Thời gian titan = (thể lực còn lại sau khi trả phí biến hình) / titanDrainPerSecond. Mọi số liệu ở PlayerStamina.stats.
    public float cooldown = 30f;
    [Tooltip("Thế giới đứng hình sau khi bấm G, chờ animation cắn tay (chưa có, tạm để trống). Nhạc titan đã chạy từ đầu đoạn này.")]
    public float biteTime = 2f;
    [Tooltip("Chuỗi sét chớp rồi sét lớn đánh xuống, sau đoạn cắn tay.")]
    public float lightningTime = 2.2f;
    float TransformTotal => biteTime + lightningTime;
    [Range(0f, 1f)] public float damageTakenMultiplier = 0.5f;

    [Header("Di chuyển")]
    public float walkSpeed = 12f;
    public float runSpeed = 20f;
    public float acceleration = 40f;
    // Titan 10m nên trọng lực phải mạnh hơn người nhiều thì mới thấy "nặng" (độ cao nhảy ~ jumpSpeed^2 / 2g ≈ 2m).
    public float jumpSpeed = 18f;
    public float gravity = -80f;
    [Header("Rung màn hình")]
    public float stepShake = 0.16f;       // rung mỗi bước chân khi chạy (0..1)
    public float landShakeMin = 0.35f;    // rung tiếp đất nhẹ nhất
    public float landShakeScale = 1f;     // nhân thêm theo tốc độ rơi
    public float shakeDecay = 2.6f;       // giảm mỗi giây

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
    PlayerStamina stamina;
    float titanStamina;      // thể lực lúc bắt đầu titan (sau phí biến hình)
    float titanTotal = 1f;   // tổng thời gian titan của lần biến hình này
    float humanHealth;       // máu dạng người, cất lại lúc biến hình

    ODMController odm;
    PlayerCombat combat;
    Health health;
    CharacterController controller;
    internal Transform cam;
    ThirdPersonObservationCamera thirdPerson;
    GameManager manager;

    internal TitanFormVisual visual;
    TitanFormCombat punch;
    readonly CameraShake shake = new CameraShake();

    State state = State.Human;
    float stateTimer;     // Transforming: thời gian đã qua. Titan: thời gian còn lại.
    float cooldownLeft;
    internal float flash;

    // Giá trị gốc để khôi phục
    float oldHeight, oldRadius, oldStep;
    Vector3 oldCenter, oldCamLocal;
    internal float eyeLocalY;
    static readonly string[] ViewmodelNames = { "ODM_Viewmodel", "WRAD_Sword_Viewmodel" };
    readonly System.Collections.Generic.List<GameObject> hiddenViewmodels = new System.Collections.Generic.List<GameObject>();
    FirstPersonSwordViewmodel swordViewmodel;

    // Chuyển động
    Vector3 velocity;
    internal float bobPhase;

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





    void Awake()
    {
        visual = new TitanFormVisual(this);
        punch = new TitanFormCombat(this);
        shake.Decay = shakeDecay;
        odm = GetComponent<ODMController>();
        stamina = GetComponent<PlayerStamina>();
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
        // Lúc biến hình thế giới đứng im (timeScale = 0), chỉ đồng hồ thật chạy.
        if (state == State.Transforming) { UpdateFrozenTransform(); return; }

        if (Time.timeScale <= 0.0001f) return;
        float dt = Time.deltaTime;

        flash = Mathf.Max(0f, flash - dt * 1.4f);
        if (cooldownLeft > 0f) cooldownLeft -= dt;

        switch (state)
        {
            case State.Human:
                if (GameInput.GetKeyDown(transformKey))
                {
                    if (stamina.Exhausted || stamina.Current < stamina.stats.transformMinStamina) Say("Không đủ thể lực để hoá titan (cần " + Mathf.CeilToInt(stamina.stats.transformMinStamina / stamina.Max * 100f) + "%, đang có " + stamina.Percent + "%)");
                    else if (cooldownLeft > 0f) Say("Chưa thể hoá titan (" + Mathf.CeilToInt(cooldownLeft) + "s)");
                    else if (!Cutscene.IsPlaying) Cutscene.PlayFirstTitan(BeginTransform); // lần đầu có cutscene, các lần sau chỉ VFX
                }
                break;

            case State.Titan:
                stateTimer -= dt;
                stamina.Set(titanStamina * Mathf.Clamp01(stateTimer / titanTotal)); // thể lực tụt dần theo thời gian titan còn lại
                TitanLook();
                TitanMove(dt);
                punch.Update(dt);
                if (GameInput.GetKeyDown(transformKey) || stateTimer <= 0f) EndTransform();
                break;
        }
    }

    // ================= BIẾN HÌNH =================
    bool frozen; // đang giữ timeScale = 0 vì biến hình
    bool lightningStarted;

    void UpdateFrozenTransform()
    {
        // Đang mở menu tạm dừng / kết thúc ván: để GameManager tự quản timeScale, đồng hồ biến hình đứng yên.
        if (manager != null && manager.Current != GameManager.State.Playing) return;

        Time.timeScale = 0f; // chống việc thoát pause trả timeScale về 1 giữa chừng
        stateTimer += Time.unscaledDeltaTime;

        // Giai đoạn 1 (0 -> biteTime): đứng hình, chỗ để chèn animation cắn tay + cutscene chuyển hoá.
        // Giai đoạn 2 (biteTime -> hết): sét chớp, sét lớn, camera nâng lên tầm mắt titan.
        if (!lightningStarted && stateTimer >= biteTime)
        {
            lightningStarted = true;
            TransformLightning.Play(transform.position, lightningTime);
        }
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((stateTimer - biteTime) / Mathf.Max(0.01f, lightningTime)));
        CameraShake.SetLocalY(cam, Mathf.Lerp(oldCamLocal.y, eyeLocalY, k));
        if (stateTimer >= TransformTotal)
        {
            Unfreeze();
            state = State.Titan;
            stamina.Locked = true;
            stamina.Set(stamina.Current - stamina.stats.Cost(stamina.stats.transformCost)); // phí biến hình
            titanStamina = stamina.Current;
            titanTotal = Mathf.Max(1f, titanStamina / Mathf.Max(0.01f, stamina.stats.Cost(stamina.stats.titanDrainPerSecond)));
            stateTimer = titanTotal;
            humanHealth = health.Current;      // cất máu dạng người
            health.SetCurrent(health.maxHealth); // titan luôn đầy máu mỗi lần biến hình
            health.DamageMultiplier = damageTakenMultiplier;
            // Gầm ngay khi thời gian chạy lại. Chỉ phát khi có file thật Assets/Resources/Audio/Roar.(wav|mp3|ogg);
            // bản tổng hợp mặc định nghe khó chịu nên bỏ qua.
            if (Resources.Load<AudioClip>("Audio/Roar") != null) GameAudio.Play2D(Sfx.Roar, 1f, 1f);
            Say("TITAN! Chuột trái/phải: đấm");
        }
    }

    void Unfreeze()
    {
        if (!frozen) return;
        frozen = false;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

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

        Titan.Lure(transform.position); // tiếng động lớn kéo titan quanh đó tới
        visual.Build();
        shake.Reset();
        velocity = Vector3.zero;
        punch.Reset();

        health.DamageMultiplier = 0f; // bất tử trong lúc biến hình
        // Nhạc titan chạy ngay từ lúc bấm G (crossfade ngắn). Sét bắt đầu sau biteTime, xem UpdateFrozenTransform.
        if (odm.Music != null) odm.Music.SetTitan(true, 1f);
        lightningStarted = false;
        frozen = true;
        Time.timeScale = 0f; // dừng thời gian suốt lúc biến hình (xem UpdateFrozenTransform)
        state = State.Transforming;
        stateTimer = 0f;
        Say("Hoá titan!");
    }

    void EndTransform()
    {
        Unfreeze(); // bị tắt giữa lúc đang đứng hình thì trả thời gian lại
        if (odm != null && odm.Music != null) { odm.Music.SetDuck(1f); odm.Music.SetTitan(false, 2f); }
        float feetY = transform.position.y - formHeight * 0.5f;

        controller.enabled = false;
        controller.height = oldHeight;
        controller.radius = oldRadius;
        controller.center = oldCenter;
        controller.stepOffset = oldStep;
        transform.position = new Vector3(transform.position.x, feetY - oldCenter.y + oldHeight * 0.5f + 0.05f, transform.position.z);
        controller.enabled = true;

        if (cam != null) cam.localPosition = oldCamLocal;
        shake.Reset();
        visual.Destroy();
        punch.Reset();

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

        // Trả máu dạng người + đổi phần thể lực (thời gian titan) còn lại thành máu
        if (state == State.Titan && !health.IsDead)
        {
            health.SetCurrent(Mathf.Min(health.maxHealth, humanHealth + stamina.Current / Mathf.Max(0.01f, stamina.stats.staminaPerHealth)));
            stamina.Set(0f);
        }

        if (stamina != null) stamina.Locked = false;
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

    // Sau khi Animator đã dựng tư thế: thêm bước chân và cho camera đi theo đầu.
    void LateUpdate()
    {
        if (state == State.Human) return;
        visual.LateUpdate();
    }

    // ================= ĐIỀU KHIỂN TITAN =================
    void TitanLook()
    {
        odm.Yaw += GameInput.GetAxis("Mouse X") * odm.mouseSensitivity;
        odm.Pitch -= GameInput.GetAxis("Mouse Y") * odm.mouseSensitivity;
        odm.Pitch = Mathf.Clamp(odm.Pitch, -70f, 85f);
        transform.rotation = Quaternion.Euler(0f, odm.Yaw, 0f);
        cam.localRotation = Quaternion.Euler(odm.Pitch + shake.Pitch, 0f, shake.Roll);
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

        float preMoveVy = velocity.y;
        controller.Move(velocity * dt);

        // Tiếp đất: rơi càng nhanh rung càng mạnh.
        bool nowGrounded = controller.isGrounded;
        if (nowGrounded && !shake.WasGrounded && preMoveVy < -6f)
            shake.Add(Mathf.Clamp01(landShakeMin + (-preMoveVy - 6f) / 30f * landShakeScale));
        shake.WasGrounded = nowGrounded;

        // Bước chân nặng nề: camera nhấp nhô theo nhịp chạy
        float moveAmount = grounded ? flat.magnitude / runSpeed : 0f;
        float prevStep = Mathf.Floor(bobPhase / Mathf.PI);
        bobPhase += flat.magnitude * dt * 0.45f;
        if (grounded && Mathf.Floor(bobPhase / Mathf.PI) != prevStep) shake.Add(stepShake * Mathf.Clamp01(moveAmount * 1.3f)); // mỗi bước chạm đất
        shake.Update(dt);
        float bob = Mathf.Abs(Mathf.Sin(bobPhase)) * 0.35f * moveAmount;
        // Clip chạy đã làm đầu nhấp nhô (camera bám đầu) -> bớt nhấp nhô giả.
        if (visual.punchAnim != null && visual.punchAnim.HasRun) bob *= 1f - visual.punchAnim.RunWeight;
        CameraShake.SetLocalY(cam, eyeLocalY + bob + shake.Y);
        visual.walkAmount = Mathf.MoveTowards(visual.walkAmount, grounded && flat.sqrMagnitude > 0.5f ? flat.magnitude / runSpeed : 0f, dt * 3f);
        if (visual.punchAnim != null)
        {
            bool moving = grounded && flat.sqrMagnitude > 0.5f;
            visual.punchAnim.Locomotion = moving ? Mathf.Clamp01(flat.magnitude / Mathf.Max(0.01f, walkSpeed * 0.5f)) : 0f;
            visual.punchAnim.RunSpeed = Mathf.Max(minRunAnimSpeed, flat.magnitude / Mathf.Max(0.01f, runSpeed)) * runAnimSpeed;
        }
        if (visual.proc != null)
        {
            visual.proc.walkPhase = bobPhase;
            visual.proc.moveAmount = grounded && flat.sqrMagnitude > 0.5f ? 1f : 0f;
        }
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

        float x = 30f * s, y = Screen.height - 330f * s, w = 360f * s, hgt = 22f * s;
        float fill;
        string text;
        Color col;
        if (state == State.Titan)
        {
            fill = Mathf.Clamp01(stateTimer / titanTotal);
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
