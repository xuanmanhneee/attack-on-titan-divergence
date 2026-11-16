using System;
using UnityEngine;

// Trạng thái của một sợi dây hook. Dùng chung giữa ODMController (vật lý) và ODMHooks (bắn/nhả/ngắm).
public class HookState
{
    public bool active;
    public Vector3 anchorPoint;
    public Transform anchorTransform;
    public Vector3 anchorLocalPoint;
    public bool followsTransform;
    public float visualProgress;
    public float ropeLength;
    public bool attached;
}

// Bắn, nhả, tự nhả và ngắm điểm neo cho hai dây hook. Các thông số (layer, tầm, smart aim...)
// vẫn nằm trên ODMController để giữ giá trị đã lưu trong scene; lớp này chỉ đọc chúng.
// Vật lý dây (căng, thu, ràng buộc) vẫn ở ODMController.
public class ODMHooks : MonoBehaviour
{
    ODMController odm;
    HookState left;
    HookState right;
    AudioSource hookAudio;

    // Phát ra khi bắn dây thành công (điểm neo). ODMController dùng để xử lý cú giật khi đang đứng đất.
    public event Action<Vector3> Fired;

    public void Init(ODMController odm, HookState left, HookState right)
    {
        this.odm = odm;
        this.left = left;
        this.right = right;

        hookAudio = gameObject.AddComponent<AudioSource>();
        hookAudio.playOnAwake = false;
        hookAudio.loop = false;
        hookAudio.spatialBlend = 0f;
        if (odm.hookLaunchClip == null) odm.hookLaunchClip = Resources.Load<AudioClip>("Audio/ODM/HookLaunch");
        if (odm.hookReleaseClip == null) odm.hookReleaseClip = Resources.Load<AudioClip>("Audio/ODM/HookRelease");
    }

    // ================= HOOK INPUT =================
    public void HandleInput()
    {
        // Chuột trái/phải hoặc phím móc trái/phải trong Cài đặt (mặc định Q/E, GameInput tự đổi theo phím đã gán).
        bool leftHeld = GameInput.GetMouseButton(0) || GameInput.GetKey(KeyCode.Q);
        bool rightHeld = GameInput.GetMouseButton(1) || GameInput.GetKey(KeyCode.E);
        bool leftDown = GameInput.GetMouseButtonDown(0) || GameInput.GetKeyDown(KeyCode.Q);
        bool rightDown = GameInput.GetMouseButtonDown(1) || GameInput.GetKeyDown(KeyCode.E);
        bool leftUp = (GameInput.GetMouseButtonUp(0) || GameInput.GetKeyUp(KeyCode.Q)) && !leftHeld;
        bool rightUp = (GameInput.GetMouseButtonUp(1) || GameInput.GetKeyUp(KeyCode.E)) && !rightHeld;

        if (odm.toggleHooks)
        {
            if (leftDown)
            {
                if (left.active) Release(left, -1f);
                else TryFire(left, odm.hipOriginLeft);
            }

            if (rightDown)
            {
                if (right.active) Release(right, 1f);
                else TryFire(right, odm.hipOriginRight);
            }
        }
        else
        {
            if (leftDown) TryFire(left, odm.hipOriginLeft);
            if (leftUp) Release(left, -1f);

            if (rightDown) TryFire(right, odm.hipOriginRight);
            if (rightUp) Release(right, 1f);

            // Nhả phím lúc đang tạm dừng thì sự kiện "nhả" bị bỏ lỡ -> kiểm tra theo trạng thái giữ phím.
            if (left.active && !leftHeld) Release(left, -1f);
            if (right.active && !rightHeld) Release(right, 1f);
        }

        // Nhả phím lúc đang tạm dừng thì sự kiện "nhả" bị bỏ lỡ -> kiểm tra theo trạng thái giữ phím
        if (left.active && !(GameInput.GetKey(KeyCode.Q) || GameInput.GetMouseButton(0))) left.active = false;
        if (right.active && !(GameInput.GetKey(KeyCode.E) || GameInput.GetMouseButton(1))) right.active = false;

        AutoReleaseCheck(left);
        AutoReleaseCheck(right);
    }

    // Tự nhả khi: dây căng, đã vượt qua điểm neo (neo nằm sau lưng), đang bay lên và đủ nhanh.
    // Nhả xong phải bấm lại phím mới bắn được dây mới.
    void AutoReleaseCheck(HookState hook)
    {
        if (!odm.autoRelease || !hook.active || hook.attached) return;
        if (!odm.autoReleaseWhileReeling && GameInput.GetKey(KeyCode.Space)) return; // tùy chọn: đang chủ động thu dây thì không nhả
        Vector3 velocity = odm.Velocity;
        if (velocity.magnitude < odm.autoReleaseMinSpeed || velocity.y <= 0.5f) return;

        Vector3 toAnchor = hook.anchorPoint - transform.position;
        if (toAnchor.magnitude < hook.ropeLength - 0.5f) return; // dây chưa căng

        Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);
        Vector3 flatTo = new Vector3(toAnchor.x, 0f, toAnchor.z);
        if (flatVel.sqrMagnitude < 0.01f || flatTo.sqrMagnitude < 0.01f) return;

        if (Vector3.Dot(flatVel.normalized, flatTo.normalized) < odm.autoReleaseBehindDot)
            Release(hook, hook == left ? -1f : 1f);
    }

    void TryFire(HookState hook, Transform hip)
    {
        if (odm.Exhausted) return; // kiệt sức: không bắn móc được (HUD đã báo)
        if (!FindAnchor(hip, out RaycastHit hit)) return;
        PlayerStamina stam = odm.Stam;
        if (stam != null && !stam.TrySpend(stam.stats.hookFireCost)) return;

        Vector3 anchor = LiftAnchor(hit);

        hook.active = true;
        hook.anchorPoint = anchor;
        hook.anchorTransform = hit.collider != null ? hit.collider.transform : null;
        hook.followsTransform = hook.anchorTransform != null;
        hook.anchorLocalPoint = hook.anchorTransform != null
            ? hook.anchorTransform.InverseTransformPoint(anchor)
            : Vector3.zero;
        hook.visualProgress = 0f;
        hook.ropeLength = Vector3.Distance(transform.position, anchor);
        hook.attached = false;
        PlayHookSound(odm.hookLaunchClip, odm.hookLaunchVolume, hook == left ? -1f : 1f);

        GameAudio.Play2D(Sfx.HookFire, 0.7f, UnityEngine.Random.Range(0.95f, 1.08f));
        GameAudio.Play(Sfx.HookHit, anchor, 0.9f, UnityEngine.Random.Range(0.92f, 1.05f), Mathf.Min(0.35f, hook.ropeLength / 120f));
        GameVfx.Burst(anchor, new Color(0.82f, 0.8f, 0.72f, 0.8f), 10, 4f, 0.5f, 0.6f);
        Fired?.Invoke(anchor);
    }

    public void Release(HookState hook, float side)
    {
        if (!hook.active) return;
        hook.active = false;
        hook.followsTransform = false;
        hook.anchorTransform = null;
        PlayHookSound(odm.hookReleaseClip, odm.hookReleaseVolume, side);
    }

    void PlayHookSound(AudioClip clip, float volume, float side)
    {
        if (hookAudio == null || clip == null) return;
        hookAudio.panStereo = side * 0.18f;
        hookAudio.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
        hookAudio.PlayOneShot(clip, volume * GameSettings.Sfx);
    }

    public void RefreshAnchors()
    {
        RefreshAnchor(left);
        RefreshAnchor(right);
    }

    void RefreshAnchor(HookState hook)
    {
        if (!hook.active || !hook.followsTransform) return;
        if (hook.anchorTransform == null)
        {
            hook.active = false;
            hook.followsTransform = false;
            return;
        }
        hook.anchorPoint = hook.anchorTransform.TransformPoint(hook.anchorLocalPoint);
    }

    // Bắn trúng tường nhà thì nâng điểm neo lên gần mái: neo càng cao thì càng đu được lên cao và có chỗ để vung.
    Vector3 LiftAnchor(RaycastHit hit)
    {
        if (!odm.liftAnchorToRoof) return hit.point;
        if (hit.normal.y > odm.liftWallMaxNormalY) return hit.point;          // không phải mặt tường
        if (hit.point.y - transform.position.y >= odm.liftSkipIfAbovePlayer) return hit.point;

        Collider c = hit.collider;
        if (c == null || !SupportsClosestPoint(c)) return hit.point;

        float topY = c.bounds.max.y - odm.liftRoofMargin;
        float wantY = Mathf.Min(topY, hit.point.y + odm.liftMaxHeight);
        wantY = Mathf.Min(wantY, transform.position.y + odm.maxHookRange * 0.7f);
        if (wantY <= hit.point.y + 0.75f) return hit.point;

        Vector3 raised = new Vector3(hit.point.x, wantY, hit.point.z) + hit.normal * 0.5f;
        Vector3 p = c.ClosestPoint(raised);

        if (Vector3.Distance(transform.position, p) > odm.maxHookRange * 1.1f) return hit.point;

        // Dây không được xuyên qua nhà khác
        Vector3 from = odm.cameraTransform.position;
        Vector3 to = p + hit.normal * 0.1f;
        if (Physics.Linecast(from, to, odm.hookableLayer, QueryTriggerInteraction.Ignore)) return hit.point;

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
    public bool FindAnchor(Transform hip, out RaycastHit hit)
    {
        Transform cam = odm.cameraTransform;
        Ray ray = new Ray(cam.position, cam.forward);

        // Titan body colliders are triggers so they cannot physically pin the
        // player. Hook casts deliberately include them; movement casts ignore
        // triggers and therefore continue to collide only with world geometry.
        bool found = Physics.Raycast(ray, out hit, odm.maxHookRange, odm.hookableLayer, QueryTriggerInteraction.Collide);
        if (!found && odm.assistRadius > 0f)
            found = Physics.SphereCast(ray, odm.assistRadius, out hit, odm.maxHookRange, odm.hookableLayer, QueryTriggerInteraction.Collide);

        if (found && (hip == null || HipClear(hip, hit.point)))
            return true;

        if (odm.smartAim)
            return SmartScan(hip, out hit);

        hit = default;
        return false;
    }

    bool SmartScan(Transform hip, out RaycastHit best)
    {
        best = default;
        bool any = false;
        float bestScore = float.NegativeInfinity;

        Transform cam = odm.cameraTransform;
        Vector3 origin = cam.position;
        Vector3 fwd = cam.forward;

        Vector3 velocity = odm.Velocity;
        Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);
        bool moving = flatVel.sqrMagnitude > 4f;
        if (moving) flatVel.Normalize();

        int rings = Mathf.Max(1, odm.smartAimRings);
        int samples = Mathf.Max(3, odm.smartAimSamples);

        for (int r = 1; r <= rings; r++)
        {
            float angle = odm.smartAimAngle * r / rings;
            Vector3 tilted = Quaternion.AngleAxis(angle, cam.up) * fwd;

            for (int k = 0; k < samples; k++)
            {
                // Lệch pha giữa các vòng để phủ đều hơn
                float roll = 360f * k / samples + ((r % 2) * 0.5f * 360f / samples);
                Vector3 dir = Quaternion.AngleAxis(roll, fwd) * tilted;

                if (!Physics.Raycast(origin, dir, out RaycastHit h, odm.maxHookRange, odm.hookableLayer, QueryTriggerInteraction.Collide))
                    continue;
                if (h.distance < odm.minHookDistance) continue;
                if (hip != null && !HipClear(hip, h.point)) continue;

                float look = 1f - angle / odm.smartAimAngle; // 1 = sát tâm ngắm

                float height = Mathf.Clamp01((h.point.y - transform.position.y) / (odm.maxHookRange * 0.5f));

                float momentum = 0f;
                if (moving)
                {
                    Vector3 flatTo = h.point - transform.position;
                    flatTo.y = 0f;
                    if (flatTo.sqrMagnitude > 0.01f)
                        momentum = Vector3.Dot(flatTo.normalized, flatVel);
                }

                float score = look * odm.aimLookWeight + height * odm.aimHeightWeight + momentum * odm.aimMomentumWeight;
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
        return !(d > 0.2f && Physics.Raycast(hip.position, toHit / d, d - 0.1f, odm.hookableLayer, QueryTriggerInteraction.Ignore));
    }
}
