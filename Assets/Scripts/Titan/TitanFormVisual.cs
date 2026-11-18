using UnityEngine;
using UnityEngine.Playables;

// Thân model titan của người chơi khi hoá titan: dựng model, animation, camera đặt ở mắt, bước chân/tư thế thủ.
// Tách từ TitanForm. Không phải component: TitanForm tạo và gọi trực tiếp; cấu hình vẫn nằm ở các field public của TitanForm.
class TitanFormVisual
{
    readonly TitanForm f;
    public TitanFormVisual(TitanForm form) { f = form; }

    internal Transform bodyRoot;            // gốc model, đặt ở chân capsule
    internal Transform holder;
    internal TitanProceduralAnim proc;
    internal TitanPunchAnimator punchAnim;
    internal Transform headBone;
    Transform[] legBones;          // đùi trái, đùi phải, cẳng chân trái, cẳng chân phải
    Transform[] armBones;          // cánh tay trên trái, phải, cẳng tay trái, phải
    Vector3 eyeInHead;             // điểm mắt so với xương đầu (theo hướng xương, không theo scale)
    Vector3 restEyeHolder;         // điểm mắt lúc thủ thế, trong không gian holder
    internal float walkAmount;

    // ================= HÌNH ẢNH =================
    // Dùng model titan thật làm thân người chơi. Model Humanoid có clip đấm: camera đặt ở mắt và bám theo
    // xương đầu, tóc/mắt/răng chỉ đổ bóng để không che tầm nhìn. Model khác: camera đứng trước mặt.
    public void Build()
    {
        var def = TitanModels.Get(f.modelName);
        GameObject prefab = def != null ? Resources.Load<GameObject>(def.modelPath) : null;
        if (prefab == null) { Debug.LogWarning("[TitanForm] Không tải được model '" + f.modelName + "', chỉ có camera."); return; }

        // Đo kích thước ở gốc toạ độ, không xoay, để kết quả không phụ thuộc vị trí/hướng người chơi.
        var measure = new GameObject("TitanForm_Measure").transform;
        var model = Object.Instantiate(prefab, measure);
        model.name = def.name;
        Quaternion importRotation = model.transform.localRotation;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, def.yawOffset, 0f) * importRotation;
        // Prefab FBX có scale gốc riêng; đo ở scale 1 để hệ số s bên dưới là tuyệt đối (giống Titan.BuildFromModel).
        model.transform.localScale = Vector3.one;
        foreach (var c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c);

        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Object.Destroy(measure.gameObject); return; }

        Bounds b = TitanBuilder.ExactBounds(renderers);
        if (b.size.y < 0.0001f) b = TitanBuilder.CalcBounds(renderers);
        float s = f.formHeight / b.size.y;
        model.transform.localScale = Vector3.one * s;
        model.transform.localPosition = new Vector3(-b.center.x * s, -(b.center.y - b.extents.y) * s, -b.center.z * s);
        TitanBuilder.ApplyMaterials(renderers, def);
        if (def.hideInFirstPerson != null)
        {
            foreach (var r in renderers)
                foreach (string part in def.hideInFirstPerson)
                    if (r.name.Contains(part)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        // feetRoot (chân capsule) > holder (TitanFootGrounder dịch lên/xuống) > model
        bodyRoot = new GameObject("TitanForm_Body").transform;
        bodyRoot.SetParent(f.transform, false);
        bodyRoot.localPosition = new Vector3(0f, -f.formHeight * 0.5f, 0f);
        holder = new GameObject("Holder").transform;
        holder.SetParent(bodyRoot, false);
        // Giữ toạ độ cục bộ (đã tính quanh gốc measure) -> model đứng đúng dưới chân người chơi.
        model.transform.SetParent(holder, false);
        Object.Destroy(measure.gameObject);

        var animator = model.GetComponentInChildren<Animator>();
        bool human = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;
        if (human)
        {
            headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            AnimationClip runClip = string.IsNullOrEmpty(f.runClipPath) ? null : FindClip(f.runClipPath, f.runClipName);
            if (!string.IsNullOrEmpty(f.runClipPath) && runClip == null)
                Debug.LogWarning("[TitanForm] Không tải được clip chạy '" + f.runClipPath + "', dùng bước chân giả.");
            punchAnim = TitanPunchAnimator.TryCreate(model, FindClip(def.modelPath, f.punchClipName), 0f, runClip); // dựng tư thế thủ ngay
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
            f.eyeLocalY = -f.formHeight * 0.5f + eyeLocal.y;
            if (f.collapseHead) headBone.localScale = Vector3.one * 0.001f; // animation Humanoid không ghi scale nên giữ nguyên
        }
        else
        {
            // Đầu: dùng xương Head nếu có, không thì ước lượng theo gáy. Camera đứng trước mặt trước nhất của
            // thân (mặt/bụng) một khoảng f.eyeForward -> không bị kẹt trong đầu.
            Vector3 head = headBone != null
                ? bodyRoot.InverseTransformPoint(headBone.position)
                : new Vector3(0f, f.formHeight * (def.napeHeight + 0.03f), 0f);
            float frontZ = b.extents.z * s;
            model.transform.localPosition += new Vector3(-head.x, 0f, -frontZ - f.eyeForward);
            f.eyeLocalY = -f.formHeight * 0.5f + head.y + f.eyeUp;
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

    // Tách thân titan khỏi người chơi, để lại xác nằm tại chỗ (có collider để đứng lên được). Trả về điểm gáy trên lưng.
    // Tư thế nằm lấy từ khung cuối của clip "chết" (f.dyingClipPath); không có clip thì xoay úp 90 độ.
    public bool LeaveCorpse(out Vector3 napeTop)
    {
        napeTop = default;
        if (bodyRoot == null) return false;
        var body = bodyRoot;
        Vector3 pivot = body.position;
        float groundY = pivot.y;
        Quaternion yaw = body.rotation;
        Transform head = headBone;

        // Gốc xác không xoay (chỉ có hướng yaw ở bước cuối) để đo hộp AABB khít.
        var root = new GameObject("TitanCorpse").transform;
        root.position = pivot;
        body.rotation = Quaternion.identity;
        body.SetParent(root, true);

        foreach (var mb in body.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
        if (punchAnim != null) Object.DestroyImmediate(punchAnim); // huỷ graph đang điều khiển Animator
        punchAnim = null;
        if (head != null) head.localScale = Vector3.one;           // đầu đã thu nhỏ cho camera
        var rs = body.GetComponentsInChildren<Renderer>();
        foreach (var r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        var animator = body.GetComponentInChildren<Animator>();
        AnimationClip dying = animator != null && !string.IsNullOrEmpty(f.dyingClipPath) ? FindClip(f.dyingClipPath, "") : null;
        if (dying != null)
        {
            // Phát thẳng tới khung cuối rồi đóng băng (màn hình đang trắng nên không thấy cú ngã).
            var graph = UnityEngine.Playables.PlayableGraph.Create("TitanCorpsePose");
            var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "Anim", animator);
            var cp = UnityEngine.Animations.AnimationClipPlayable.Create(graph, dying);
            cp.SetApplyFootIK(false);
            cp.SetSpeed(0);
            cp.SetTime(dying.length);
            output.SetSourcePlayable(cp);
            animator.applyRootMotion = false;
            animator.enabled = true;
            graph.Evaluate(0f);
            graph.Destroy();
            animator.enabled = false;
        }
        else
        {
            if (animator != null) animator.enabled = false;
            body.rotation = Quaternion.AngleAxis(90f, Vector3.right); // ngã úp về phía trước quanh chân
        }

        // Nâng cho nằm đúng mặt đất, rồi gắn hộp va chạm theo thân.
        // Hộp bao của model hơi rộng hơn thân thật nên xác bị lơ lửng: hạ thêm corpseSink (tỉ lệ chiều cao).
        Bounds b = TitanBuilder.ExactBounds(rs);
        float lift = groundY - b.min.y - f.formHeight * f.corpseSink;
        root.position += Vector3.up * lift;
        b.center += Vector3.up * lift;
        var col = new GameObject("Collider").AddComponent<BoxCollider>();
        col.transform.SetParent(root, false);
        col.transform.localPosition = b.center - root.position;
        col.size = b.size * 0.8f;
        Vector3 headPos = head != null ? head.position + Vector3.up * lift : b.center;

        // Quay cả xác về hướng người chơi đang nhìn (quanh chân).
        Vector3 rootPos = root.position;
        root.rotation = yaw;
        headPos = rootPos + yaw * (headPos - rootPos);
        Physics.SyncTransforms();

        root.gameObject.AddComponent<TitanCorpse>().size = f.formHeight;

        // Gáy: điểm đầu/cổ, rọi xuống hộp để lấy mặt lưng.
        napeTop = headPos;
        var ray = new Ray(headPos + Vector3.up * f.formHeight, Vector3.down);
        if (col.Raycast(ray, out RaycastHit hit, f.formHeight * 2f)) napeTop = hit.point;

        bodyRoot = null;
        holder = null;
        proc = null;
        headBone = null;
        legBones = null;
        armBones = null;
        walkAmount = 0f;
        return true;
    }

    public void Destroy()
    {
        if (bodyRoot != null) Object.Destroy(bodyRoot.gameObject);
        bodyRoot = null;
        holder = null;
        proc = null;
        punchAnim = null;
        headBone = null;
        legBones = null;
        armBones = null;
        walkAmount = 0f;
    }


    // Sau khi Animator đã dựng tư thế: thêm bước chân và cho camera đi theo đầu.
    public void LateUpdate()
    {
        if (punchAnim == null || headBone == null || f.cam == null) return;
        if (Time.timeScale <= 0.0001f) return;

        // Có clip chạy thì chân/tay đã do clip điều khiển; bước chân giả và tư thế thủ chỉ bù phần còn lại.
        float noRun = 1f - punchAnim.RunWeight;
        float legAmount = walkAmount * noRun;
        if (legBones != null && legAmount > 0.001f)
        {
            float sw = Mathf.Sin(f.bobPhase);
            Vector3 axis = f.transform.right;
            RotateBone(legBones[0], -f.legSwingAngle * sw * legAmount, axis);
            RotateBone(legBones[1], f.legSwingAngle * sw * legAmount, axis);
            RotateBone(legBones[2], f.kneeBendAngle * Mathf.Max(0f, sw) * legAmount, axis);
            RotateBone(legBones[3], f.kneeBendAngle * Mathf.Max(0f, -sw) * legAmount, axis);
        }

        float guard = (1f - punchAnim.ActionWeight) * noRun;
        if (armBones != null && guard > 0.001f)
        {
            Vector3 right = f.transform.right, up = f.transform.up;
            // Xoay quanh trục ngang (dương = hạ xuống), rồi quanh trục đứng để tách hai tay ra hai bên.
            RotateBone(armBones[0], f.guardLowerUpperArm * guard, right);
            RotateBone(armBones[1], f.guardLowerUpperArm * guard, right);
            RotateBone(armBones[0], -f.guardSpread * guard, up);
            RotateBone(armBones[1], f.guardSpread * guard, up);
            RotateBone(armBones[2], f.guardLowerForearm * guard, right);
            RotateBone(armBones[3], f.guardLowerForearm * guard, right);
        }

        // Update đã đặt lại độ cao camera (f.eyeLocalY + nhấp nhô); ở đây cộng thêm độ lệch của đầu.
        Vector3 rest = holder.TransformPoint(restEyeHolder);
        Vector3 eye = EyeWorld();
        Vector3 delta = f.transform.InverseTransformVector(eye - rest) * f.headFollow;
        // Clip chạy làm đầu cúi/lùi về phía thân: camera đi lùi hoặc xuống sẽ chui vào trong cổ/ngực (thấy mặt trong
        // của model). Chỉ cho camera đi theo đầu khi tiến lên hoặc nhích lên, không bao giờ lùi/hạ vào thân.
        delta.z = Mathf.Max(0f, delta.z);
        delta.y = Mathf.Max(0f, delta.y);
        Vector3 p = f.cam.localPosition;
        f.cam.localPosition = new Vector3(delta.x, p.y + delta.y, delta.z);
    }

    Vector3 EyeWorld() => headBone.position + headBone.rotation * eyeInHead;

    static void RotateBone(Transform bone, float angle, Vector3 axis)
    {
        if (bone != null) bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
    }
}
