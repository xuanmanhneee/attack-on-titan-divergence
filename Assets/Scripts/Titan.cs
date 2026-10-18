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
    TitanModelDef def;        // null = dùng placeholder dựng bằng khối
    Transform sway;           // pivot để lắc nhẹ khi dùng model thật chưa có animation
    TitanClipPlayer clips;    // animation có sẵn trong FBX (nếu có)

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
        t.BuildBody();
        root.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f); // xoay sau khi dựng để tính bounds đúng
        root.transform.localScale = Vector3.one * t.k;

        // Layer "Ignore Raycast" (2): tia móc dây / raycast mặc định không trúng titan
        SetLayerRecursively(root, 2);

        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        t.health = root.AddComponent<Health>();
        t.health.Init((80f + heightMeters * 6f) * (def != null ? def.healthMultiplier : 1f));
        t.health.Died += t.Die;

        All.Add(t);
        return t;
    }

    void BuildBody()
    {
        if (def != null && BuildFromModel()) return;

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

    // ================= MODEL THẬT (FBX trong Resources) =================
    static Bounds CalcBounds(Renderer[] rs)
    {
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    bool BuildFromModel()
    {
        var prefab = Resources.Load<GameObject>(def.modelPath);
        if (prefab == null) return false;

        var visual = new GameObject("Visual").transform;
        visual.SetParent(transform, false);

        var model = Instantiate(prefab, visual);
        model.name = def.name;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, def.yawOffset, 0f);
        model.transform.localScale = Vector3.one;

        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);

        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0 || CalcBounds(renderers).size.y < 0.0001f)
        {
            Destroy(visual.gameObject);
            return false;
        }

        // Co giãn về cao 10 đơn vị, đặt chân chạm y=0 và căn giữa theo X/Z (root đang scale 1, chưa xoay)
        Bounds b0 = CalcBounds(renderers);
        float s = 10f / b0.size.y;
        Vector3 centerRel = (b0.center - transform.position) * s;
        float minY = centerRel.y - b0.extents.y * s;
        model.transform.localScale = Vector3.one * s;
        model.transform.localPosition = new Vector3(-centerRel.x, -minY, -centerRel.z);

        ApplyMaterials(renderers);

        // Va chạm đơn giản: một viên nang bao quanh thân
        var colGo = new GameObject("BodyCollider");
        colGo.transform.SetParent(transform, false);
        colGo.transform.localPosition = new Vector3(0f, 5f, 0f);
        var cap = colGo.AddComponent<CapsuleCollider>();
        cap.direction = 1;
        cap.height = 10f;
        cap.radius = 1.3f;
        BodyColliders.Add(cap);

        // Điểm yếu ở gáy (quả cầu đỏ)
        var nape = Part(PrimitiveType.Sphere, transform,
                        new Vector3(0f, 10f * def.napeHeight, -def.napeBack), Vector3.one * 0.7f,
                        new Color(1f, 0.15f, 0.1f), false);
        nape.name = "Nape";
        Nape = nape.transform;

        sway = visual;
        clips = TitanClipPlayer.TryCreate(model, def.modelPath);
        return true;
    }

    void ApplyMaterials(Renderer[] renderers)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        var cache = new Dictionary<string, Texture2D>();
        Texture2D Tex(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (!cache.TryGetValue(path, out var t)) cache[path] = t = Resources.Load<Texture2D>(path);
            return t;
        }

        foreach (var r in renderers)
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                string path = def.bodyTex;
                if (n.Contains("hair") && !string.IsNullOrEmpty(def.hairTex)) path = def.hairTex;
                else if ((n.Contains("face") || n.Contains("head")) && !string.IsNullOrEmpty(def.faceTex)) path = def.faceTex;
                else if ((n.Contains("wiqi") || n.Contains("weapon")) && !string.IsNullOrEmpty(def.weaponTex)) path = def.weaponTex;

                Material m = lit != null ? new Material(lit) : (mats[i] != null ? new Material(mats[i]) : new Material(Shader.Find("Standard")));
                var tex = Tex(path);
                if (tex != null)
                {
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                }
                if (n.Contains("hair") && m.HasProperty("_AlphaClip"))
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.5f);
                    m.SetFloat("_Cull", 0f);
                    m.EnableKeyword("_ALPHATEST_ON");
                }
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
                m.name = n;
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
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

        if (clips != null)
        {
            bool attacking = state == State.Windup || state == State.Recover;
            clips.SetRole(attacking ? TitanClipPlayer.Role.Attack : moving ? TitanClipPlayer.Role.Walk : TitanClipPlayer.Role.Idle);
        }
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
