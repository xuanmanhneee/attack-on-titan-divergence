using System.Collections.Generic;
using UnityEngine;

// Dựng thân titan (placeholder bằng khối hoặc model FBX). Tách từ Titan.cs; không chứa AI.
static class TitanBuilder
{
    public static void Build(Titan t) { BuildBody(t); }

    static void BuildBody(Titan t)
    {
        var def = t.def;
        if (def != null && BuildFromModel(t)) return;

        if (def != null && !string.IsNullOrEmpty(def.proceduralStyle))
        {
            BuildPureTitan(t, def.proceduralStyle);
            return;
        }

        Color skin = new Color(0.78f, 0.58f, 0.45f);
        Color dark = new Color(0.62f, 0.44f, 0.34f);

        t.legL = Pivot(t, "LegL_Pivot", new Vector3(-0.8f, 4.4f, 0f));
        t.legR = Pivot(t, "LegR_Pivot", new Vector3(0.8f, 4.4f, 0f));
        Part(t, PrimitiveType.Capsule, t.legL, new Vector3(0f, -2.2f, 0f), new Vector3(1.3f, 2.2f, 1.3f), dark, true);
        Part(t, PrimitiveType.Capsule, t.legR, new Vector3(0f, -2.2f, 0f), new Vector3(1.3f, 2.2f, 1.3f), dark, true);

        Part(t, PrimitiveType.Capsule, t.transform, new Vector3(0f, 6.2f, 0f), new Vector3(3.0f, 2.0f, 2.0f), skin, true);

        t.armL = Pivot(t, "ArmL_Pivot", new Vector3(-2.0f, 7.6f, 0f));
        t.armR = Pivot(t, "ArmR_Pivot", new Vector3(2.0f, 7.6f, 0f));
        Part(t, PrimitiveType.Capsule, t.armL, new Vector3(0f, -1.8f, 0f), new Vector3(0.9f, 1.8f, 0.9f), skin, true);
        Part(t, PrimitiveType.Capsule, t.armR, new Vector3(0f, -1.8f, 0f), new Vector3(0.9f, 1.8f, 0.9f), skin, true);

        Part(t, PrimitiveType.Sphere, t.transform, new Vector3(0f, 9.0f, 0.1f), new Vector3(2.0f, 2.2f, 2.0f), skin, true);
        Part(t, PrimitiveType.Cube, t.transform, new Vector3(0f, 8.9f, 1.15f), new Vector3(0.35f, 0.35f, 0.6f), dark, false);

        // Điểm yếu ở gáy (phía sau cổ): quả cầu đỏ để dễ nhìn
        var nape = Part(t, PrimitiveType.Sphere, t.transform, new Vector3(0f, 8.5f, -1.0f), Vector3.one * 0.9f,
                        new Color(1f, 0.15f, 0.1f), false);
        nape.name = "t.Nape";
        t.Nape = nape.transform;
    }

    // Titan vô tri có tỷ lệ cơ thể và khuôn mặt cố tình bất thường. Các model này
    // dùng chung animation khớp đơn giản, nhẹ hơn FBX và không chiếm vai trò của
    // Titan Shifter (Tiến Công, Thiết Giáp, Búa Chiến).
    static void BuildPureTitan(Titan t, string style)
    {
        var def = t.def;
        Color skin = new Color(0.80f, 0.58f, 0.43f);
        Color shadow = new Color(0.55f, 0.30f, 0.22f);
        Color sclera = new Color(0.92f, 0.86f, 0.72f);
        Color pupil = new Color(0.08f, 0.045f, 0.025f);
        Color mouth = new Color(0.20f, 0.025f, 0.02f);
        Color teeth = new Color(0.92f, 0.82f, 0.60f);

        float shoulder = 1.85f;
        float armLength = 1.9f;
        float legWidth = 1.15f;
        Vector3 torsoScale = new Vector3(2.75f, 2.05f, 1.75f);
        Vector3 headScale = new Vector3(1.65f, 1.8f, 1.55f);
        float headY = 8.9f;

        switch (style)
        {
            case "longface":
                skin = new Color(0.72f, 0.49f, 0.35f);
                shoulder = 1.65f;
                armLength = 2.2f;
                torsoScale = new Vector3(2.35f, 2.0f, 1.55f);
                headScale = new Vector3(1.35f, 2.35f, 1.35f);
                headY = 8.75f;
                break;
            case "potbelly":
                skin = new Color(0.86f, 0.64f, 0.49f);
                shoulder = 2.1f;
                armLength = 1.65f;
                legWidth = 1.35f;
                torsoScale = new Vector3(3.8f, 2.35f, 2.8f);
                headScale = new Vector3(1.55f, 1.55f, 1.5f);
                headY = 8.55f;
                break;
            case "pinhead":
                skin = new Color(0.68f, 0.44f, 0.31f);
                shoulder = 2.2f;
                armLength = 2.35f;
                torsoScale = new Vector3(3.25f, 2.25f, 1.9f);
                headScale = new Vector3(1.0f, 1.15f, 1.0f);
                headY = 9.25f;
                break;
        }

        t.legL = Pivot(t, "LegL_Pivot", new Vector3(-0.78f, 4.35f, 0f));
        t.legR = Pivot(t, "LegR_Pivot", new Vector3(0.78f, 4.35f, 0f));
        Part(t, PrimitiveType.Capsule, t.legL, new Vector3(0f, -2.15f, 0f), new Vector3(legWidth, 2.15f, legWidth), shadow, true);
        Part(t, PrimitiveType.Capsule, t.legR, new Vector3(0f, -2.15f, 0f), new Vector3(legWidth, 2.15f, legWidth), shadow, true);

        Part(t, PrimitiveType.Capsule, t.transform, new Vector3(0f, 6.15f, 0f), torsoScale, skin, true);

        t.armL = Pivot(t, "ArmL_Pivot", new Vector3(-shoulder, 7.65f, 0f));
        t.armR = Pivot(t, "ArmR_Pivot", new Vector3(shoulder, 7.65f, 0f));
        Part(t, PrimitiveType.Capsule, t.armL, new Vector3(0f, -armLength, 0f), new Vector3(0.82f, armLength, 0.82f), skin, true);
        Part(t, PrimitiveType.Capsule, t.armR, new Vector3(0f, -armLength, 0f), new Vector3(0.82f, armLength, 0.82f), skin, true);

        Part(t, PrimitiveType.Sphere, t.transform, new Vector3(0f, headY, 0.05f), headScale, skin, true);

        float faceZ = headScale.z * 0.48f;
        float eyeY = headY + (style == "longface" ? 0.35f : 0.22f);
        float eyeGap = headScale.x * 0.35f;
        float eyeSize = style == "pinhead" ? 0.22f : 0.30f;
        Part(t, PrimitiveType.Sphere, t.transform, new Vector3(-eyeGap, eyeY, faceZ + 0.08f), Vector3.one * eyeSize, sclera, false);
        Part(t, PrimitiveType.Sphere, t.transform, new Vector3(eyeGap, eyeY, faceZ + 0.08f), Vector3.one * eyeSize, sclera, false);
        Part(t, PrimitiveType.Sphere, t.transform, new Vector3(-eyeGap, eyeY - 0.02f, faceZ + 0.22f), Vector3.one * eyeSize * 0.38f, pupil, false);
        Part(t, PrimitiveType.Sphere, t.transform, new Vector3(eyeGap, eyeY - 0.02f, faceZ + 0.22f), Vector3.one * eyeSize * 0.38f, pupil, false);

        float mouthY = headY - (style == "longface" ? 0.65f : 0.42f);
        Vector3 mouthScale = style == "grinning"
            ? new Vector3(1.20f, 0.38f, 0.16f)
            : new Vector3(headScale.x * 0.48f, 0.30f, 0.14f);
        Part(t, PrimitiveType.Cube, t.transform, new Vector3(0f, mouthY, faceZ + 0.10f), mouthScale, mouth, false);

        if (style == "grinning" || style == "potbelly")
        {
            for (int i = -2; i <= 2; i++)
                Part(t, PrimitiveType.Cube, t.transform,
                    new Vector3(i * 0.22f, mouthY + 0.04f, faceZ + 0.20f),
                    new Vector3(0.16f, 0.25f, 0.08f), teeth, false);
        }

        // Mũi lệch và tai quá khổ giúp silhouette không còn giống một Shifter đẹp/ngầu.
        float noseSide = style == "longface" ? 0.16f : -0.08f;
        Part(t, PrimitiveType.Sphere, t.transform,
            new Vector3(noseSide, headY - 0.1f, faceZ + 0.18f),
            new Vector3(0.26f, style == "longface" ? 0.58f : 0.36f, 0.32f), shadow, false);
        Part(t, PrimitiveType.Sphere, t.transform, new Vector3(-headScale.x * 0.55f, headY, 0f), new Vector3(0.30f, 0.55f, 0.25f), skin, false);
        Part(t, PrimitiveType.Sphere, t.transform, new Vector3(headScale.x * 0.55f, headY + 0.12f, 0f), new Vector3(0.38f, 0.68f, 0.25f), skin, false);

        float napeY = def != null ? 10f * def.napeHeight : 8.5f;
        float napeZ = def != null ? -def.napeBack : -1f;
        var nape = Part(t, PrimitiveType.Sphere, t.transform,
            new Vector3(0f, napeY, napeZ), Vector3.one * 0.7f,
            new Color(1f, 0.15f, 0.1f), false);
        nape.name = "t.Nape";
        t.Nape = nape.transform;
    }

    // ================= MODEL THẬT (FBX trong Resources) =================
    internal static Bounds CalcBounds(Renderer[] rs)
    {
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static bool IsWeaponRenderer(Renderer r)
    {
        string n = r.name.ToLowerInvariant();
        if (n.Contains("wiqi") || n.Contains("weapon")) return true;
        foreach (var m in r.sharedMaterials)
        {
            if (m == null) continue;
            string mn = m.name.ToLowerInvariant();
            if (mn.Contains("wiqi") || mn.Contains("weapon")) return true;
        }
        return false;
    }

    // Đo chính xác từ đỉnh lưới (bake pose hiện tại), bỏ qua vũ khí.
    // Bounds mặc định của SkinnedMeshRenderer hay sai (lấy từ animation) -> titan bị lơ lửng hoặc nhỏ hơn dự tính.
    internal static Bounds ExactBounds(Renderer[] rs)
    {
        bool has = false;
        Bounds b = new Bounds();
        var tmp = new Mesh();
        var verts = new List<Vector3>();

        foreach (var r in rs)
        {
            if (IsWeaponRenderer(r)) continue;

            var smr = r as SkinnedMeshRenderer;
            if (smr != null && smr.sharedMesh != null)
            {
                smr.BakeMesh(tmp, false);
                verts.Clear();
                tmp.GetVertices(verts);
                // BakeMesh(useScale: false) đã gồm scale của xương/renderer -> chỉ còn thiếu vị trí và hướng.
                // Dùng localToWorldMatrix sẽ nhân scale hai lần (renderer FBX Mixamo thường có scale ~100-200).
                Matrix4x4 m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
                for (int i = 0; i < verts.Count; i++)
                {
                    Vector3 w = m.MultiplyPoint3x4(verts[i]);
                    if (!has) { b = new Bounds(w, Vector3.zero); has = true; }
                    else b.Encapsulate(w);
                }
            }
            else
            {
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
        }

        Object.Destroy(tmp);
        return has ? b : CalcBounds(rs);
    }

    static readonly Dictionary<string, Bounds> boundsCache = new Dictionary<string, Bounds>();

    // Kết quả đo được lưu lại theo model để các titan sinh sau không phải bake lại
    static Bounds MeasureModel(Titan t, Renderer[] renderers)
    {
        var def = t.def;
        string key = def.modelPath + "|" + def.yawOffset;
        if (boundsCache.TryGetValue(key, out Bounds rel))
            return new Bounds(rel.center + t.transform.position, rel.size);

        Bounds exact = ExactBounds(renderers);
        Bounds quick = CalcBounds(renderers);
        if (Titan.DebugLogs) Debug.Log("[Titan] " + def.name + ": bounds mặc định cao " + quick.size.y.ToString("F2") +
                  ", đo chính xác cao " + exact.size.y.ToString("F2") +
                  " (đáy lệch " + (exact.min.y - quick.min.y).ToString("F2") + ")");

        boundsCache[key] = new Bounds(exact.center - t.transform.position, exact.size);
        return exact;
    }

    static bool BuildFromModel(Titan t)
    {
        var def = t.def;
        if (def == null || string.IsNullOrEmpty(def.modelPath)) return false;
        var prefab = Resources.Load<GameObject>(def.modelPath);
        if (prefab == null) return false;

        var visual = new GameObject("Visual").transform;
        visual.SetParent(t.transform, false);

        var model = Object.Instantiate(prefab, visual);
        model.name = def.name;
        model.transform.localPosition = Vector3.zero;
        // Imported FBX prefabs can contain a corrective root rotation (this Meshy
        // model needs -90 degrees on X to convert Z-up to Unity's Y-up). Replacing
        // that rotation with yaw made the Titan lie flat. Add yaw on top of the
        // authored import rotation instead of discarding it.
        Quaternion importRotation = model.transform.localRotation;
        model.transform.localRotation = Quaternion.Euler(0f, def.yawOffset, 0f) * importRotation;
        model.transform.localScale = Vector3.one;

        foreach (var c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);

        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0 || CalcBounds(renderers).size.y < 0.0001f)
        {
            Object.Destroy(visual.gameObject);
            return false;
        }

        // Co giãn về cao 10 đơn vị, đặt chân chạm y=0 và căn giữa theo X/Z (root đang scale 1, chưa xoay)
        Bounds b0 = MeasureModel(t, renderers);
        if (b0.size.y < 0.0001f) b0 = CalcBounds(renderers);
        float s = 10f / b0.size.y;
        Vector3 centerRel = (b0.center - t.transform.position) * s;
        float minY = centerRel.y - b0.extents.y * s;
        model.transform.localScale = Vector3.one * s;
        model.transform.localPosition = new Vector3(-centerRel.x, -minY, -centerRel.z);

        ApplyMaterials(renderers, def);

        // Va chạm đơn giản: một viên nang bao quanh thân
        var colGo = new GameObject("BodyCollider");
        colGo.transform.SetParent(t.transform, false);
        colGo.transform.localPosition = new Vector3(0f, 5f, 0f);
        var cap = colGo.AddComponent<CapsuleCollider>();
        cap.direction = 1;
        cap.height = 10f;
        cap.radius = 1.3f;
        // The attack is resolved explicitly in DoHit. Keeping the Titan body
        // solid lets a kinematic Titan pin the player's CharacterController
        // against roofs and walls with no escape. A trigger remains hookable
        // and slashable without participating in movement collision.
        cap.isTrigger = true;
        t.BodyColliders.Add(cap);

        // Điểm yếu ở gáy (quả cầu đỏ)
        var nape = Part(t, PrimitiveType.Sphere, t.transform,
                        new Vector3(0f, 10f * def.napeHeight, -def.napeBack), Vector3.one * 0.7f,
                        new Color(1f, 0.15f, 0.1f), false);
        nape.name = "t.Nape";
        t.Nape = nape.transform;

        t.sway = visual;
        t.clips = TitanClipPlayer.TryCreate(model, def.modelPath);
        if (t.clips == null) t.proc = TitanProceduralAnim.TryCreate(model);
        t.feet = TitanFootGrounder.TryCreate(model, t.transform, visual);
        return true;
    }

    internal static void ApplyMaterials(Renderer[] renderers, TitanModelDef def)
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
                Material source = mats[i];
                string n = source != null ? source.name.ToLowerInvariant() : "";
                string path = def.bodyTex;
                bool hairPart = n.Contains("hair") || r.name.ToLowerInvariant().Contains("hair");
                if (hairPart) path = def.hairTex; // không có texture tóc riêng thì giữ vật liệu gốc, không dán texture thân lên tóc
                else if ((n.Contains("face") || n.Contains("head")) && !string.IsNullOrEmpty(def.faceTex)) path = def.faceTex;
                else if ((n.Contains("wiqi") || n.Contains("weapon")) && !string.IsNullOrEmpty(def.weaponTex)) path = def.weaponTex;

                // Preserve a prefab's authored textures. Switching an imported material to URP used to
                // create a blank Lit material here, which made Meshy models turn completely white.
                Texture sourceBase = null;
                Texture sourceNormal = null;
                if (source != null)
                {
                    if (source.HasProperty("_BaseMap")) sourceBase = source.GetTexture("_BaseMap");
                    if (sourceBase == null && source.HasProperty("_MainTex")) sourceBase = source.GetTexture("_MainTex");
                    if (source.HasProperty("_BumpMap")) sourceNormal = source.GetTexture("_BumpMap");
                }

                Material m = source != null
                    ? new Material(source)
                    : new Material(lit != null ? lit : Shader.Find("Standard"));
                if (lit != null && m.shader != lit) m.shader = lit;

                var tex = Tex(path);
                Texture baseMap = tex != null ? tex : sourceBase;
                if (baseMap != null)
                {
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", baseMap);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", baseMap);
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                }
                if (sourceNormal != null && m.HasProperty("_BumpMap"))
                {
                    m.SetTexture("_BumpMap", sourceNormal);
                    m.EnableKeyword("_NORMALMAP");
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

    static Transform Pivot(Titan t, string n, Vector3 localPos)
    {
        var go = new GameObject(n);
        go.transform.SetParent(t.transform, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    static GameObject Part(Titan t, PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, bool solid)
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
        if (solid)
        {
            col.isTrigger = true;
            t.BodyColliders.Add(col);
        }
        else Object.Destroy(col);
        return go;
    }

    internal static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursively(c.gameObject, layer);
    }
}
