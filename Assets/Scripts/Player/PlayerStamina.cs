using System;
using System.Collections.Generic;
using UnityEngine;

// Mọi con số của thể lực nằm ở đây, tính bằng điểm (không phải %), để trang bị / nâng cấp sau này
// chỉ cần cộng vào các *Bonus hoặc đổi CostMultiplier mà không đụng tới logic.
[Serializable]
public class StaminaStats
{
    [Header("Dung lượng và hồi phục")]
    public float maxStamina = 450f;
    [Tooltip("Hồi tối thiểu mỗi giây (rất nhỏ). Luôn nhỏ hơn mọi mức tiêu hao nên không thể vừa hoạt động vừa tự hồi")]
    public float baseRegen = 0.5f;
    [Tooltip("Tổng hồi mỗi giây khi nghỉ ở chỗ an toàn (thay cho hồi nền)")]
    public float restRegen = 12f;
    [Tooltip("Titan chưa thấy bạn nhưng lang thang sát tới mức này thì cũng không nghỉ được")]
    public float safeDistance = 15f;
    [Tooltip("Cửa sổ trượt (giây): cộng quãng đường đã di chuyển trong chừng này giây gần nhất")]
    public float restWindow = 5f;
    [Tooltip("Quãng đường di chuyển trong cửa sổ trên không quá bấy nhiêu mét thì tính là đang nghỉ (đi đi dừng dừng vẫn được)")]
    public float restMaxPath = 10f;
    [Tooltip("Sau khi bị thương, chờ bấy nhiêu giây mới nghỉ được")]
    public float restDelay = 3f;
    [Tooltip("Cạn thể lực thì kiệt sức (không móc/gas/chém) cho tới khi hồi được chừng này")]
    public float exhaustRecover = 45f;

    [Header("Tiêu hao khi hoạt động (điểm/giây)")]
    public float runCost = 0.7f;          // chạy trên đất
    public float swingCostPerHook = 1.2f;   // đang treo trên dây (mỗi dây)
    public float gasBoostCost = 2.2f;     // xả gas lướt

    [Header("Tiêu hao một lần")]
    public float hookFireCost = 1.5f;
    public float slashCost = 4f;
    public float transformCost = 75f;
    public float transformMinStamina = 160f; // phải có từng này mới được biến hình
    [Tooltip("Về dạng người: trừ thêm chừng này thể lực (phần còn lại được giữ)")]
    public float revertCost = 25f;
    [Tooltip("Về dạng người: luôn còn ít nhất chừng này (và hết kiệt sức) để còn chạy thoát được. Nên lớn hơn exhaustRecover")]
    public float revertMinStamina = 60f;

    [Header("Hồi máu (tốn thể lực)")]
    public float healRate = 3f;           // máu hồi mỗi giây khi bị thương
    public float staminaPerHealth = 1f; // 1 máu hồi tốn bấy nhiêu thể lực
    public float healMinStamina = 15f;     // dưới mức này thì không đổi nữa

    [Header("Titan")]
    public float titanDrainPerSecond = 4.5f; // dạng titan đốt bấy nhiêu thể lực mỗi giây (nhanh hơn mọi hoạt động dạng người)

    // Chỗ cho trang bị
    [NonSerialized] public float maxBonus;
    [NonSerialized] public float regenBonus;
    [NonSerialized] public float costMultiplier = 1f;

    public float Max => maxStamina + maxBonus;
    public float Regen => baseRegen + regenBonus;
    public float RestRegen => restRegen + regenBonus;
    public float Cost(float c) { return c * costMultiplier; }
}

// Thể lực dùng chung cho cả dạng người và dạng titan.
// Dạng người: hoạt động tiêu hao, đứng nghỉ ở chỗ an toàn thì hồi nhanh, bị thương thì tự đổi thể lực thành máu.
// Dạng titan: TitanForm khoá hệ này (Locked) và tự trừ thể lực theo thời gian titan còn lại.
public class PlayerStamina : MonoBehaviour
{
    public StaminaStats stats = new StaminaStats();

    public float Current { get; private set; }
    public float Max => stats.Max;
    public int Percent => Mathf.RoundToInt(Normalized * 100f);
    public float Normalized => Max > 0f ? Current / Max : 0f;
    public bool Exhausted { get; private set; }
    public bool Resting { get; private set; }
    [NonSerialized] public bool Locked;

    ODMController odm;
    Health health;
    float lastHurt = -999f;
    readonly Queue<KeyValuePair<float, float>> pathLog = new Queue<KeyValuePair<float, float>>(); // (thời điểm, quãng đường)
    float pathSum, pathPending, pathTimer;
    Vector3 lastPos;
    bool safeCached;
    float safeTimer;

    void Awake()
    {
        Current = stats.Max;
    }

    void Start()
    {
        odm = GetComponent<ODMController>();
        health = GetComponent<Health>();
        lastPos = transform.position;
        if (health != null) health.Damaged += OnDamaged;
    }

    void OnDestroy()
    {
        if (health != null) health.Damaged -= OnDamaged;
    }

    void OnDamaged(float amount) { lastHurt = Time.time; }

    public void Set(float value)
    {
        Current = Mathf.Clamp(value, 0f, Max);
        if (Current <= 0f) Exhausted = true;
        else if (Exhausted && Current >= stats.exhaustRecover) Exhausted = false;
    }

    public bool CanAfford(float cost) { return !Exhausted && Current >= stats.Cost(cost); }

    // Trả phí một lần. Không đủ hoặc đang kiệt sức thì không làm được.
    public bool TrySpend(float cost)
    {
        if (Exhausted) return false;
        cost = stats.Cost(cost);
        if (Current < cost) return false;
        Set(Current - cost);
        return true;
    }

    void Update()
    {
        if (Locked || Time.timeScale <= 0.0001f || odm == null)
        {
            Resting = false;
            // dạng titan / đứng hình: không tính quãng đường, và bỏ nhật ký cũ để lúc trở về không bị cộng dồn nhảy vọt
            lastPos = transform.position; pathLog.Clear(); pathSum = 0f; pathPending = 0f;
            return;
        }
        float dt = Time.deltaTime;

        // ----- tiêu hao liên tục -----
        float drain = 0f;
        bool grounded = odm.IsGrounded;
        int hooks = odm.ActiveHookCount;
        if (hooks > 0 && !grounded) drain += stats.swingCostPerHook * hooks;
        if (grounded && odm.Speed > 3f) drain += stats.runCost;
        if (odm.IsGassing && (!grounded || hooks > 0 || odm.Speed > 3f)) drain += stats.gasBoostCost;
        drain = stats.Cost(drain);

        // ----- hồi phục -----
        // Nghỉ = tổng quãng đường đi trong restWindow giây gần nhất không quá restMaxPath,
        // đang đứng trên đất, không dây, không gas, và không titan nào thấy/đuổi.
        Vector3 pos = transform.position;
        pathPending += (pos - lastPos).magnitude;
        lastPos = pos;
        pathTimer -= dt;
        if (pathTimer <= 0f)
        {
            pathTimer = 0.1f;
            pathLog.Enqueue(new KeyValuePair<float, float>(Time.time, pathPending));
            pathSum += pathPending; pathPending = 0f;
        }
        while (pathLog.Count > 0 && Time.time - pathLog.Peek().Key > stats.restWindow) pathSum -= pathLog.Dequeue().Value;
        bool nearHere = pathSum + pathPending <= stats.restMaxPath;

        safeTimer -= dt;
        if (safeTimer <= 0f) { safeTimer = 0.25f; safeCached = IsSafe(); }
        Resting = grounded && hooks == 0 && !odm.IsGassing && nearHere && safeCached;
        float regen = Resting ? stats.RestRegen : stats.Regen;

        float next = Current + (regen - drain) * dt;

        // ----- bị thương: đổi thể lực thành máu -----
        if (health != null && !health.IsDead && health.Current < health.maxHealth && next > stats.healMinStamina)
        {
            float heal = Mathf.Min(stats.healRate * dt, health.maxHealth - health.Current);
            health.Heal(heal);
            next -= heal * stats.staminaPerHealth;
        }

        Current = Mathf.Clamp(next, 0f, Max);
        if (Current <= 0f) Exhausted = true;
        else if (Exhausted && Current >= stats.exhaustRecover) Exhausted = false;
    }

    bool IsSafe()
    {
        if (Time.time - lastHurt < stats.restDelay) return false;
        float sq = stats.safeDistance * stats.safeDistance;
        Vector3 p = transform.position;
        foreach (Titan t in Titan.All)
        {
            if (t == null || t.IsDead) continue;
            if (t.IsChasing || t.SensesTarget) return false; // bị titan thấy / đang bị đuổi thì không nghỉ được
            if ((t.transform.position - p).sqrMagnitude < sq) return false;
        }
        return true;
    }
}
