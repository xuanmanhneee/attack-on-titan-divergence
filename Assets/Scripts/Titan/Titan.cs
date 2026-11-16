using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Titan dựng hoàn toàn bằng code từ các khối cơ bản (placeholder), không cần prefab hay NavMesh.
// Sau này có model Mixamo thì thay phần BuildBody(), giữ nguyên AI và điểm yếu ở gáy (Nape).
// AI: lang thang -> thấy người chơi thì đuổi -> vào tầm thì vung tay đập (có thời gian gồng).
// Bay cao hơn tầm tay (~7.5 * kích thước) thì không bị đánh trúng.
public class Titan : MonoBehaviour
{
    public static readonly List<Titan> All = new List<Titan>();
    public static event Action<Titan> Killed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStatics() { All.Clear(); Killed = null; ObstacleMask = 0; HookableLayer = -1; GroundSampler = null; HasField = false; }

    // Layer của công trình mà titan phải tránh (đặt bởi bản đồ). 0 = không tránh.
    public static int ObstacleMask;

    // Layer used by ODM raycasts. Set by GameManager from the player's hook mask.
    // -1 keeps the old Ignore Raycast fallback for scenes without a manager.
    public static int HookableLayer = -1;

    // Hàm cho biết độ cao mặt đất tại một vị trí (đặt bởi bản đồ). null = dùng Terrain.
    public static Func<Vector3, float> GroundSampler;

    // Vùng đi lại của titan (mặt phẳng XZ). Titan không được bước ra khỏi khu phố.
    public static Rect Field;
    public static bool HasField;
    public static bool DebugLogs;

    public static void ResetAll() { All.Clear(); ObstacleMask = 0; GroundSampler = null; HasField = false; }

    [Header("Movement")]
    public float walkSpeed = 1.6f; // lang thang theo linh cảm: chậm để người chơi có 15-20 giây nghỉ
    // A normal ODM runner tops out around 9 m/s.  Titans must outrun a simple
    // sprint so the player is forced to use hooks, gas, and rooftops.
    public float runSpeed = 17.5f;
    public float turnSpeed = 100f;
    [Header("Cảm nhận người chơi")]
    [Tooltip("Thấy người chơi (có đường nhìn, không bị nhà chắn) trong tầm này thì lao tới, tốc độ tăng vọt")]
    public float detectRange = 60f;
    [Tooltip("Nghe/ngửi thấy người ở sát bên thì biết ngay, bất kể tường chắn")]
    public float senseRange = 14f;
    [Tooltip("Mất dấu quá chừng này giây thì quay về lang thang")]
    public float loseSightTime = 6f;
    public float loseRange = 120f;
    [Tooltip("Lúc chưa thấy: vẫn đại khái biết người ở đâu và lang thang quanh đó, lệch ngẫu nhiên tới bán kính này")]
    public float instinctRadius = 45f;
    public float instinctInterval = 6f;
    [Tooltip("Điểm lang thang luôn cách người chơi ít nhất chừng này (không dồn thẳng vào người)")]
    public float instinctMinRadius = 25f;

    [Header("Không túm tụm")]
    [Tooltip("Số titan tối đa được đuổi cùng lúc; con còn lại chỉ lảng vảng cho tới khi có con bị hạ hoặc mất dấu")]
    public int maxChasers = 3;
    [Tooltip("Titan khác trong bán kính này thì đẩy nhau ra")]
    public float separationRadius = 16f;
    public float separationStrength = 1.6f;

    [Header("Attack")]
    public float attackDamage = 35f;
    public float windupTime = 0.9f;
    public float recoverTime = 1.6f;

    public Transform Nape { get; internal set; }
    public readonly List<Collider> BodyColliders = new List<Collider>();
    public bool IsDead => health == null || health.IsDead;
    public float Height => 10f * k;
    public float HealthNormalized => health != null ? health.Normalized : 0f;

    enum State { Wander, Chase, Windup, Recover, Dead }

    Health health;
    Transform target;
    float k = 1f; // hệ số kích thước (mô hình gốc cao 10m)
    State state = State.Wander;
    float stateTimer;
    Vector3 wanderPoint;
    float lostSightTimer;
    public bool IsChasing => state == State.Chase || state == State.Windup || state == State.Recover;
    float wanderTimer;
    bool moving;
    float walkPhase;
    internal Transform armL, armR, legL, legR;
    internal TitanModelDef def;        // null = dùng placeholder dựng bằng khối
    internal Transform sway;           // pivot để lắc nhẹ khi dùng model thật chưa có animation
    internal TitanClipPlayer clips;    // animation (clip) nếu có
    internal TitanProceduralAnim proc; // animation bằng code cho rig Humanoid nếu không có clip
    internal TitanFootGrounder feet;   // giữ chân chạm đất (rig Humanoid)
    float bornTime;
    bool logged;

    float AttackReach => 4.5f * k;
    float ArmHeight => 7.5f * k;

    // ================= TẠO TITAN =================
    public static Titan Create(Vector3 groundPos, float heightMeters, Transform target, TitanModelDef def = null)
    {
        var root = new GameObject("Titan");
        root.transform.position = groundPos;

        var t = root.AddComponent<Titan>();
        t.k = heightMeters / 10f;
        t.target = target;
        t.def = def;
        // Vary the chase speed per Titan while keeping every one faster than a
        // player's 9 m/s ground sprint.
        t.runSpeed = UnityEngine.Random.Range(15f, 20f);
        TitanBuilder.Build(t);
        root.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f); // xoay sau khi dựng để tính bounds đúng
        root.transform.localScale = Vector3.one * t.k;

        // Put every body collider on the same layer used by ODM anchors so the
        // player can grapple directly to a Titan.
        TitanBuilder.SetLayerRecursively(root, HookableLayer >= 0 ? HookableLayer : 2);

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        // Titan only turns around the world-up axis. Keeping pitch/roll locked also
        // protects the gameplay root if physics settings are changed later.
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        t.health = root.AddComponent<Health>();
        t.health.Init((80f + heightMeters * 6f) * (def != null ? def.healthMultiplier : 1f));
        t.health.Died += t.Die;

        All.Add(t);
        return t;
    }


    // ================= AI =================
    // Titan này có thấy/nghe thấy người chơi lúc này không (HUD/thể lực dùng để biết có được nghỉ không)
    public bool SensesTarget
    {
        get
        {
            if (target == null || IsDead) return false;
            Vector3 to = target.position - transform.position; to.y = 0f;
            return CanSense(to.magnitude);
        }
    }

    static int ChaserCount(Titan self)
    {
        int n = 0;
        foreach (Titan t in All) if (t != null && t != self && !t.IsDead && t.IsChasing) n++;
        return n;
    }

    // Thấy người chơi: trong tầm, không bị vật cản chắn. Rất gần thì biết ngay (nghe/ngửi).
    bool CanSense(float flatDist)
    {
        if (flatDist < senseRange) return true;
        if (flatDist > detectRange) return false;
        if (ObstacleMask == 0) return true;
        Vector3 eye = transform.position + Vector3.up * (Height * 0.8f);
        Vector3 aim = target.position + Vector3.up * 1f;
        return !Physics.Linecast(eye, aim, ObstacleMask, QueryTriggerInteraction.Ignore);
    }

    void Update()
    {
        if (IsDead || target == null) return;
        float dt = Time.deltaTime;
        moving = false;

        LogSpawnDiagnostics();

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        bool inArmHeight = target.position.y - transform.position.y < ArmHeight;

        switch (state)
        {
            case State.Wander: UpdateWander(dist, dt); break;
            case State.Chase: UpdateChase(dist, inArmHeight, dt); break;
            case State.Windup: UpdateWindup(dt); break;
            case State.Recover: UpdateRecover(dt); break;
        }

        SnapToGround(dt);
        AnimateLegs(dt);
        UpdateFootsteps(dt);
        UpdateClipRole();
    }

    void LogSpawnDiagnostics()
    {
        if (logged) return;
        if (bornTime == 0f) bornTime = Time.time;
        if (Time.time - bornTime <= 2f) return;
        logged = true;
        if (DebugLogs) Debug.Log("[Titan] " + (def != null ? def.name : "placeholder") + " cao " + Height.ToString("F1") +
                  "m, y=" + transform.position.y.ToString("F2") +
                  (GroundSampler != null ? ", mặt đất=" + GroundSampler(transform.position).ToString("F2") : "") +
                  (feet != null ? ", dịch chân=" + feet.Shift.ToString("F2") : ", không có Humanoid"));
    }

    void UpdateWander(float dist, float dt)
    {
        if (CanSense(dist) && (dist < senseRange || ChaserCount(this) < maxChasers))
        {
            state = State.Chase;
            lostSightTimer = 0f;
            GameAudio.Play(Sfx.Roar, transform.position + Vector3.up * Height * 0.8f, Mathf.Clamp(Height / 12f, 0.5f, 1.3f), Mathf.Clamp(14f / Height, 0.7f, 1.3f));
            return;
        }
        wanderTimer -= dt;
        Vector3 toPoint = wanderPoint - transform.position; toPoint.y = 0f;
        if (wanderTimer <= 0f || toPoint.magnitude < 2f)
        {
            // Linh cảm: điểm đến luôn nằm quanh vị trí người chơi (lệch ngẫu nhiên), chưa phải đuổi thẳng
            Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(instinctMinRadius, Mathf.Max(instinctMinRadius + 1f, instinctRadius));
            wanderPoint = target.position + new Vector3(r.x, 0f, r.y);
            wanderTimer = UnityEngine.Random.Range(instinctInterval * 0.7f, instinctInterval * 1.3f);
        }
        MoveToward(wanderPoint, walkSpeed, dt);
        SetArms(0f);
    }

    void UpdateChase(float dist, bool inArmHeight, float dt)
    {
        if (CanSense(dist)) lostSightTimer = 0f; else lostSightTimer += dt;
        if (dist > loseRange || lostSightTimer > loseSightTime) { state = State.Wander; wanderTimer = 0f; return; }
        if (dist <= AttackReach && inArmHeight)
        {
            state = State.Windup;
            stateTimer = windupTime;
            return;
        }
        MoveToward(target.position, runSpeed, dt);
        SetArms(0f);
    }

    void UpdateWindup(float dt)
    {
        FaceTarget(dt);
        stateTimer -= dt;
        SetArms(Mathf.Lerp(0f, -150f, 1f - Mathf.Clamp01(stateTimer / windupTime)));
        if (stateTimer <= 0f)
        {
            DoHit();
            state = State.Recover;
            stateTimer = recoverTime;
        }
    }

    void UpdateRecover(float dt)
    {
        stateTimer -= dt;
        SetArms(Mathf.Lerp(0f, 30f, Mathf.Clamp01(stateTimer / recoverTime)));
        if (stateTimer <= 0f) state = State.Chase;
    }

    void UpdateFootsteps(float dt)
    {
        if (!moving) return;
        stepTimer -= dt * (state == State.Chase ? 1.8f : 1f);
        if (stepTimer > 0f) return;
        stepTimer = 0.9f;
        if (Camera.main != null && (Camera.main.transform.position - transform.position).sqrMagnitude < 150f * 150f)
            GameAudio.Play(Sfx.Footstep, transform.position, Mathf.Clamp(Height / 14f, 0.4f, 1.2f), Mathf.Clamp(14f / Height, 0.7f, 1.4f));
    }

    void UpdateClipRole()
    {
        if (clips == null) return;
        bool attacking = state == State.Windup || state == State.Recover;
        clips.SetRole(attacking ? TitanClipPlayer.Role.Attack : moving ? TitanClipPlayer.Role.Walk : TitanClipPlayer.Role.Idle);
    }

    float stepTimer;

    void DoHit()
    {
        GameAudio.Play(Sfx.TitanSmash, transform.position + transform.forward * AttackReach, Mathf.Clamp(Height / 12f, 0.6f, 1.3f));
        Vector3 to = target.position - transform.position;
        float flat = new Vector2(to.x, to.z).magnitude;
        if (flat > AttackReach * 1.3f || target.position.y - transform.position.y > ArmHeight) return;

        var h = target.GetComponent<Health>();
        if (h != null) h.TakeDamage(attackDamage);
    }

    void MoveToward(Vector3 point, float speed, float dt)
    {
        Vector3 dir = point - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        Vector3 dirN = dir.normalized;

        dirN = ApplySeparation(dirN, dir.magnitude);
        dirN = AvoidObstacles(dirN);

        Quaternion want = Quaternion.LookRotation(dirN, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);

        if (dir.magnitude < 2f) return; // đã sát mục tiêu, không đi xuyên qua
        Vector3 displacement = transform.forward * speed * dt;
        if (TryMoveWithoutTunneling(displacement)) moving = true;
        ClampToField();
    }

    // Tách nhau: titan khác ở gần thì đẩy ra để không dồn thành một cục (tắt khi đã sát mục tiêu để còn đánh)
    Vector3 ApplySeparation(Vector3 dirN, float distToPoint)
    {
        if (distToPoint > 15f)
        {
            Vector3 push = Vector3.zero;
            float sepSq = separationRadius * separationRadius;
            foreach (Titan o in All)
            {
                if (o == null || o == this || o.IsDead) continue;
                Vector3 away = transform.position - o.transform.position; away.y = 0f;
                float d2 = away.sqrMagnitude;
                if (d2 < sepSq && d2 > 0.0001f) push += away.normalized * (1f - Mathf.Sqrt(d2) / separationRadius);
            }
            if (push.sqrMagnitude > 0.0001f) dirN = (dirN + push * separationStrength).normalized;
        }
        return dirN;
    }

    // Né công trình: có nhà chắn phía trước thì trượt dọc theo mặt nhà
    Vector3 AvoidObstacles(Vector3 dirN)
    {
        if (ObstacleMask != 0)
        {
            Vector3 o = transform.position + Vector3.up * (3f * k);
            if (Physics.SphereCast(o, 1.2f * k, dirN, out RaycastHit hit, 6f * k, ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 slide = Vector3.ProjectOnPlane(dirN, hit.normal);
                slide.y = 0f;
                dirN = slide.sqrMagnitude > 0.01f ? slide.normalized : Vector3.Cross(Vector3.up, hit.normal).normalized;
            }
        }
        return dirN;
    }

    void ClampToField()
    {
        if (HasField)
        {
            Vector3 fp = transform.position;
            fp.x = Mathf.Clamp(fp.x, Field.xMin + 3f, Field.xMax - 3f);
            fp.z = Mathf.Clamp(fp.z, Field.yMin + 3f, Field.yMax - 3f);
            transform.position = fp;
        }
    }

    bool TryMoveWithoutTunneling(Vector3 displacement)
    {
        if (displacement.sqrMagnitude < 0.000001f) return false;
        if (ObstacleMask == 0)
        {
            transform.position += displacement;
            return true;
        }

        float radius = Mathf.Max(0.45f, 1.05f * k);
        Vector3 bottom = transform.position + Vector3.up * radius;
        Vector3 top = transform.position + Vector3.up * Mathf.Max(radius, 4.5f * k);
        Vector3 direction = displacement.normalized;
        float distance = displacement.magnitude;
        const float skin = 0.08f;

        if (!Physics.CapsuleCast(bottom, top, radius, direction, out RaycastHit hit,
                                 distance + skin, ObstacleMask, QueryTriggerInteraction.Ignore))
        {
            transform.position += displacement;
            return true;
        }

        float safeDistance = Mathf.Max(0f, hit.distance - skin);
        if (safeDistance > 0f) transform.position += direction * safeDistance;

        Vector3 remaining = displacement - direction * safeDistance;
        Vector3 slide = Vector3.ProjectOnPlane(remaining, hit.normal);
        slide.y = 0f;
        if (slide.sqrMagnitude < 0.000001f) return safeDistance > 0f;

        Vector3 slideDirection = slide.normalized;
        float slideDistance = slide.magnitude;
        bottom = transform.position + Vector3.up * radius;
        top = transform.position + Vector3.up * Mathf.Max(radius, 4.5f * k);
        if (Physics.CapsuleCast(bottom, top, radius, slideDirection, out RaycastHit slideHit,
                                slideDistance + skin, ObstacleMask, QueryTriggerInteraction.Ignore))
            slideDistance = Mathf.Max(0f, slideHit.distance - skin);

        if (slideDistance <= 0f) return safeDistance > 0f;
        transform.position += slideDirection * slideDistance;
        return true;
    }

    void FaceTarget(float dt)
    {
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);
    }


    void SnapToGround(float dt)
    {
        if (GroundSampler != null)
        {
            Vector3 q = transform.position;
            float gy = GroundSampler(q);
            q.y = Mathf.Abs(q.y - gy) > 1f
                ? gy
                : Mathf.Lerp(q.y, gy, 10f * dt);
            transform.position = q;
            return;
        }

        var terr = Terrain.activeTerrain;
        if (terr == null) return;

        Vector3 p = transform.position;
        float terrainY = terr.SampleHeight(p) + terr.transform.position.y;
        p.y = Mathf.Lerp(p.y, terrainY, 10f * dt);
        transform.position = p;
    }


    // ================= HOẠT ẢNH ĐƠN GIẢN =================
    void AnimateLegs(float dt)
    {
        if (moving)
        {
            float speed = state == State.Chase ? runSpeed : walkSpeed;
            walkPhase += speed * dt * 0.9f / Mathf.Max(0.3f, k);
        }

        if (proc != null)
        {
            proc.walkPhase = walkPhase;
            proc.moveAmount = moving ? 1f : 0f;
            proc.attackRaise = state == State.Windup ? 1f - Mathf.Clamp01(stateTimer / windupTime) : 0f;
            proc.attackSlam = state == State.Recover ? Mathf.Clamp01(stateTimer / recoverTime) : 0f;
            return;
        }

        // Model thật: nếu không có animation riêng thì lắc nhẹ cả thân (bước nặng nề, ngả người khi đập)
        if (legL == null)
        {
            if (sway != null && clips == null)
            {
                float bob = moving ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.3f : 0f;
                float roll = moving ? Mathf.Sin(walkPhase) * 4f : 0f;
                float pitch = 0f;
                if (state == State.Windup) pitch = -10f * (1f - Mathf.Clamp01(stateTimer / windupTime));
                else if (state == State.Recover) pitch = Mathf.Lerp(0f, 20f, Mathf.Clamp01(stateTimer / recoverTime));
                sway.localPosition = new Vector3(0f, bob, 0f);
                sway.localRotation = Quaternion.Euler(pitch, 0f, roll);
            }
            return;
        }
        float swing = moving ? Mathf.Sin(walkPhase) * 30f : 0f;
        legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
    }

    void SetArms(float pitch)
    {
        if (armL == null) return;
        float idle = moving ? -Mathf.Sin(walkPhase) * 20f : 0f;
        bool attacking = state == State.Windup || state == State.Recover;
        float p = attacking ? pitch : idle;
        armL.localRotation = Quaternion.Euler(p, 0f, 0f);
        armR.localRotation = Quaternion.Euler(attacking ? p : -p, 0f, 0f);
    }

    // ================= SÁT THƯƠNG =================
    public void Damage(float amount)
    {
        if (health != null) health.TakeDamage(amount);
    }

    public void KillByNape()
    {
        if (health != null) health.TakeDamage(9999f);
    }

    void Die()
    {
        state = State.Dead;
        if (clips != null) clips.Stop();
        All.Remove(this);
        Killed?.Invoke(this);
        GameAudio.Play(Sfx.Kill, transform.position + Vector3.up * Height * 0.8f, 1f);
        GameVfx.Steam(transform.position + Vector3.up * Height * 0.5f, Height * 0.22f, 6f);
        StartCoroutine(FallRoutine());
    }

    IEnumerator FallRoutine()
    {
        Quaternion start = transform.rotation;
        Quaternion end = start * Quaternion.Euler(90f, 0f, 0f); // ngã úp về phía trước
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 1.2f;
            float e = Mathf.Clamp01(t);
            transform.rotation = Quaternion.Slerp(start, end, e * e);
            yield return null;
        }
        GameAudio.Play(Sfx.Thud, transform.position, Mathf.Clamp(Height / 12f, 0.6f, 1.3f));
        GameVfx.Burst(transform.position + Vector3.up * 0.5f, new Color(0.75f, 0.72f, 0.65f, 0.7f), 24, 7f, Height * 0.12f, 1.2f);
        yield return new WaitForSeconds(4f);

        Vector3 p0 = transform.position;
        float s = 0f;
        while (s < 3f)
        {
            s += Time.deltaTime;
            transform.position = p0 + Vector3.down * s * 1.5f * k;
            yield return null;
        }
        Destroy(gameObject);
    }
}
