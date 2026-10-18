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
    static void ClearStatics() { All.Clear(); Killed = null; }

    public static void ResetAll() { All.Clear(); }

    [Header("Movement")]
    public float walkSpeed = 2.5f;
    public float runSpeed = 6f;
    public float turnSpeed = 100f;
    public float detectRange = 80f;
    public float loseRange = 140f;

    [Header("Attack")]
    public float attackDamage = 35f;
    public float windupTime = 0.9f;
    public float recoverTime = 1.6f;

    public Transform Nape { get; private set; }
    public readonly List<Collider> BodyColliders = new List<Collider>();
    public bool IsDead => health == null || health.IsDead;
    public float Height => 10f * k;

    enum State { Wander, Chase, Windup, Recover, Dead }

    Health health;
    Transform target;
    float k = 1f; // hệ số kích thước (mô hình gốc cao 10m)
    State state = State.Wander;
    float stateTimer;
    Vector3 wanderPoint;
    float wanderTimer;
    bool moving;
    float walkPhase;
    Transform armL, armR, legL, legR;

    float AttackReach => 4.5f * k;
    float ArmHeight => 7.5f * k;

    // ================= TẠO TITAN =================
    public static Titan Create(Vector3 groundPos, float heightMeters, Transform target)
    {
        var root = new GameObject("Titan");
        root.transform.position = groundPos;
        root.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

        var t = root.AddComponent<Titan>();
        t.k = heightMeters / 10f;
        t.target = target;
        t.BuildBody();
        root.transform.localScale = Vector3.one * t.k;

        // Layer "Ignore Raycast" (2): tia móc dây / raycast mặc định không trúng titan
        SetLayerRecursively(root, 2);

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        t.health = root.AddComponent<Health>();
        t.health.Init(80f + heightMeters * 6f);
        t.health.Died += t.Die;

        All.Add(t);
        return t;
    }

    void BuildBody()
    {
        Color skin = new Color(0.78f, 0.58f, 0.45f);
        Color dark = new Color(0.62f, 0.44f, 0.34f);

        legL = Pivot("LegL_Pivot", new Vector3(-0.8f, 4.4f, 0f));
        legR = Pivot("LegR_Pivot", new Vector3(0.8f, 4.4f, 0f));
        Part(PrimitiveType.Capsule, legL, new Vector3(0f, -2.2f, 0f), new Vector3(1.3f, 2.2f, 1.3f), dark, true);
        Part(PrimitiveType.Capsule, legR, new Vector3(0f, -2.2f, 0f), new Vector3(1.3f, 2.2f, 1.3f), dark, true);

        Part(PrimitiveType.Capsule, transform, new Vector3(0f, 6.2f, 0f), new Vector3(3.0f, 2.0f, 2.0f), skin, true);

        armL = Pivot("ArmL_Pivot", new Vector3(-2.0f, 7.6f, 0f));
        armR = Pivot("ArmR_Pivot", new Vector3(2.0f, 7.6f, 0f));
        Part(PrimitiveType.Capsule, armL, new Vector3(0f, -1.8f, 0f), new Vector3(0.9f, 1.8f, 0.9f), skin, true);
        Part(PrimitiveType.Capsule, armR, new Vector3(0f, -1.8f, 0f), new Vector3(0.9f, 1.8f, 0.9f), skin, true);

        Part(PrimitiveType.Sphere, transform, new Vector3(0f, 9.0f, 0.1f), new Vector3(2.0f, 2.2f, 2.0f), skin, true);
        Part(PrimitiveType.Cube, transform, new Vector3(0f, 8.9f, 1.15f), new Vector3(0.35f, 0.35f, 0.6f), dark, false);

        // Điểm yếu ở gáy (phía sau cổ): quả cầu đỏ để dễ nhìn
        var nape = Part(PrimitiveType.Sphere, transform, new Vector3(0f, 8.5f, -1.0f), Vector3.one * 0.9f,
                        new Color(1f, 0.15f, 0.1f), false);
        nape.name = "Nape";
        Nape = nape.transform;
    }

    Transform Pivot(string n, Vector3 localPos)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    GameObject Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, bool solid)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;

        var r = go.GetComponent<Renderer>();
        var m = new Material(r.sharedMaterial);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        r.material = m;

        var col = go.GetComponent<Collider>();
        if (solid) BodyColliders.Add(col);
        else Destroy(col);
        return go;
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursively(c.gameObject, layer);
    }

    // ================= AI =================
    void Update()
    {
        if (IsDead || target == null) return;
        float dt = Time.deltaTime;
        moving = false;

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        bool inArmHeight = target.position.y - transform.position.y < ArmHeight;

        switch (state)
        {
            case State.Wander:
                if (dist < detectRange) { state = State.Chase; break; }
                wanderTimer -= dt;
                Vector3 toPoint = wanderPoint - transform.position; toPoint.y = 0f;
                if (wanderTimer <= 0f || toPoint.magnitude < 2f)
                {
                    Vector2 r = UnityEngine.Random.insideUnitCircle * 40f;
                    wanderPoint = transform.position + new Vector3(r.x, 0f, r.y);
                    wanderTimer = UnityEngine.Random.Range(5f, 9f);
                }
                MoveToward(wanderPoint, walkSpeed, dt);
                SetArms(0f);
                break;

            case State.Chase:
                if (dist > loseRange) { state = State.Wander; break; }
                if (dist <= AttackReach && inArmHeight)
                {
                    state = State.Windup;
                    stateTimer = windupTime;
                    break;
                }
                MoveToward(target.position, runSpeed, dt);
                SetArms(0f);
                break;

            case State.Windup:
                FaceTarget(dt);
                stateTimer -= dt;
                SetArms(Mathf.Lerp(0f, -150f, 1f - Mathf.Clamp01(stateTimer / windupTime)));
                if (stateTimer <= 0f)
                {
                    DoHit();
                    state = State.Recover;
                    stateTimer = recoverTime;
                }
                break;

            case State.Recover:
                stateTimer -= dt;
                SetArms(Mathf.Lerp(0f, 30f, Mathf.Clamp01(stateTimer / recoverTime)));
                if (stateTimer <= 0f) state = State.Chase;
                break;
        }

        SnapToGround(dt);
        AnimateLegs(dt);
    }

    void DoHit()
    {
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

        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * dt);

        if (dir.magnitude < 2f) return; // đã sát mục tiêu, không đi xuyên qua
        transform.position += transform.forward * speed * dt;
        moving = true;
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
        var terr = Terrain.activeTerrain;
        if (terr == null) return;
        Vector3 p = transform.position;
        float gy = terr.SampleHeight(p) + terr.transform.position.y;
        p.y = Mathf.Lerp(p.y, gy, 10f * dt);
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
        float swing = moving ? Mathf.Sin(walkPhase) * 30f : 0f;
        legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
    }

    void SetArms(float pitch)
    {
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
        All.Remove(this);
        Killed?.Invoke(this);
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
