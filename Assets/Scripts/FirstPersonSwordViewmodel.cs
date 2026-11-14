using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// First-person dual-sword presentation built exclusively from imported assets.
/// WRAD's rigged arms provide the hands, wrists and weapon sockets; no procedural
/// primitive geometry is created by this component.
/// </summary>
[DisallowMultipleComponent]
public sealed class FirstPersonSwordViewmodel : MonoBehaviour
{
    [SerializeField] ODMController odm;
    [SerializeField] Camera viewCamera;
    [SerializeField] GameObject armsPrefab;
    [SerializeField] GameObject swordPrefab;
    [SerializeField] Texture2D armsAlbedo;

    [Header("Presentation")]
    [SerializeField] Vector3 armsPosition = new Vector3(0f, 0f, 0.30f);
    [SerializeField, Min(0.1f)] float armsScale = 0.8f;
    [SerializeField, Min(0.2f)] float swordLength = 1.0f;
    [Tooltip("Show the viewmodel all the time (tuning aid).")]
    [SerializeField] bool alwaysVisible = false;
    [Header("Arm pose (view space, left arm; the right arm is mirrored)")]
    [Tooltip("Wrist position relative to the camera.")]
    [SerializeField] Vector3 handPosition = new Vector3(-0.26f, -0.20f, 0.50f);
    [Tooltip("Direction from elbow to wrist. The elbow sits below the screen.")]
    [SerializeField] Vector3 forearmDirection = new Vector3(0.25f, 0.35f, 0.9f);
    [SerializeField] float forearmRoll = 0f;
    [Tooltip("Direction the sword blade points.")]
    [SerializeField] Vector3 bladeDirection = new Vector3(0.02f, 0.6f, 0.8f);
    [Tooltip("Direction the fingers point (wrist to fingertips).")]
    [SerializeField] Vector3 fingerDirection = new Vector3(0.25f, 0.3f, 0.9f);
    [Header("Grip")]
    [Tooltip("Flexion of finger segments 1/2/3 around the bone's local X axis.")]
    [SerializeField] Vector3 fingerCurl = new Vector3(70f, 85f, 60f);
    [SerializeField] Vector3 thumbCurl = new Vector3(20f, 30f, 30f);
    [SerializeField] float leftCurlSign = 1f;
    [SerializeField] float rightCurlSign = 1f;
    [Tooltip("Sword grip point in the hand bone's local space.")]
    [SerializeField] Vector3 leftGripOffset = new Vector3(0.55f, 0.85f, 0.1f);
    [SerializeField] Vector3 rightGripOffset = new Vector3(0.1f, 0.85f, 0.1f);
    [Tooltip("Sword rotation relative to the hand bone (blade = +Z).")]
    [SerializeField] Vector3 leftGripEuler = new Vector3(0f, 0f, 0f);
    [SerializeField] Vector3 rightGripEuler = new Vector3(0f, 0f, 0f);
    [SerializeField, Range(0f, 1f)] float revealPitch = 0.48f;
    [SerializeField, Min(0f)] float lookDownLift = 0.34f;

    Transform viewRoot;
    Transform armsInstance;
    Transform leftSocket;
    Transform rightSocket;
    Transform leftSword;
    Transform rightSword;
    Renderer[] viewRenderers;
    Vector3 baseRootPosition;
    float slashTime = -1f;
    float slashSide = -1f;
    float nextSlashSide = -1f;

    const float RaiseTime = 0.14f;
    const float CutTime = 0.18f;
    const float RecoverTime = 0.20f;
    const float SlashDuration = RaiseTime + CutTime + RecoverTime;

    public void Configure(ODMController controller, Camera camera, GameObject arms,
                          GameObject sword, Texture2D albedo)
    {
        odm = controller;
        viewCamera = camera;
        armsPrefab = arms;
        swordPrefab = sword;
        armsAlbedo = albedo;
    }

    public void PlaySlash()
    {
        slashTime = 0f;
        if (odm != null && odm.LeftHookActive != odm.RightHookActive)
            slashSide = odm.LeftHookActive ? -1f : 1f;
        else
        {
            slashSide = nextSlashSide;
            nextSlashSide = -nextSlashSide;
        }
    }

    void Awake()
    {
        if (odm == null) odm = GetComponent<ODMController>();
        if (viewCamera == null && odm != null && odm.cameraTransform != null)
            viewCamera = odm.cameraTransform.GetComponent<Camera>();
        Build();
    }

    void OnDestroy()
    {
        if (viewRoot != null)
            Destroy(viewRoot.gameObject);
    }

    class Joint
    {
        public Transform bone;
        public Quaternion bind;
    }

    // Per side (0 = left, 1 = right).
    readonly Transform[] upperBones = new Transform[2];
    readonly Transform[] forearmBones = new Transform[2];
    readonly Transform[] handBones = new Transform[2];
    readonly Quaternion[] forearmBind = new Quaternion[2]; // in view space
    float forearmLength;                                   // rig units
    Joint[][] fingerJoints; // [0]=left, [1]=right; each 4 fingers x 3 + thumb x 3

    void Build()
    {
        if (viewRoot != null || viewCamera == null || armsPrefab == null || swordPrefab == null)
            return;

        Transform stale = viewCamera.transform.Find("WRAD_Sword_Viewmodel");
        if (stale != null) Destroy(stale.gameObject);

        viewRoot = new GameObject("WRAD_Sword_Viewmodel").transform;
        viewRoot.SetParent(viewCamera.transform, false);
        baseRootPosition = Vector3.zero;

        GameObject armsObject = Instantiate(armsPrefab, viewRoot);
        armsObject.name = "FPS_Arms";
        armsInstance = armsObject.transform;
        armsInstance.localPosition = armsPosition;
        armsInstance.localRotation = Quaternion.identity;
        armsInstance.localScale = Vector3.one * armsScale;

        leftSocket = FindDeep(armsInstance, "hand.L");
        rightSocket = FindDeep(armsInstance, "hand.R");
        if (leftSocket == null || rightSocket == null)
        {
            Debug.LogError("FPS arms are missing hand bones.", this);
            return;
        }

        // The rig drives the hands with separate IK controls. Without IK the
        // hands would stay behind when the arm is posed, so attach them to the
        // forearms and drive the whole arm with plain forward kinematics.
        AttachHand(leftSocket, "forearm.L_end");
        AttachHand(rightSocket, "forearm.R_end");

        upperBones[0] = FindDeep(armsInstance, "upper_arm.L");
        upperBones[1] = FindDeep(armsInstance, "upper_arm.R");
        forearmBones[0] = FindDeep(armsInstance, "forearm.L");
        forearmBones[1] = FindDeep(armsInstance, "forearm.R");
        handBones[0] = leftSocket;
        handBones[1] = rightSocket;
        for (int side = 0; side < 2; side++)
            forearmBind[side] = Quaternion.Inverse(viewRoot.rotation) * forearmBones[side].rotation;
        Transform forearmEnd = FindDeep(armsInstance, "forearm.L_end");
        forearmLength = Vector3.Distance(armsInstance.InverseTransformPoint(forearmBones[0].position),
                                         armsInstance.InverseTransformPoint(forearmEnd.position));

        fingerJoints = new[] { CollectFingers(".L"), CollectFingers(".R") };

        ApplyArmsMaterial();
        ApplyPose();

        leftSword = BuildSword("Left_ImportedSword", leftSocket);
        rightSword = BuildSword("Right_ImportedSword", rightSocket);

        List<Renderer> renderers = new List<Renderer>();
        foreach (Renderer renderer in viewRoot.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            renderers.Add(renderer);
        }
        viewRenderers = renderers.ToArray();
        SetVisible(alwaysVisible);
    }

    void AttachHand(Transform hand, string endBoneName)
    {
        Transform control = hand.parent;
        Transform end = FindDeep(armsInstance, endBoneName);
        if (control == null || end == null) return;
        control.SetParent(end, true);
    }

    Joint MakeJoint(string boneName)
    {
        Transform bone = FindDeep(armsInstance, boneName);
        return new Joint { bone = bone, bind = bone != null ? bone.localRotation : Quaternion.identity };
    }

    Joint[] CollectFingers(string suffix)
    {
        List<Joint> list = new List<Joint>();
        foreach (string finger in new[] { "f_index", "f_middle", "f_ring", "f_pinky", "thumb" })
            for (int i = 1; i <= 3; i++)
                list.Add(MakeJoint(finger + ".0" + i + suffix));
        return list.ToArray();
    }

    static Vector3 Mirror(Vector3 v, int side) => side == 0 ? v : new Vector3(-v.x, v.y, v.z);

    // Poses are re-applied every frame so the Inspector values can be tuned live.
    void ApplyPose()
    {
        if (forearmBones[0] == null || fingerJoints == null) return;
        for (int side = 0; side < 2; side++)
        {
            Transform forearm = forearmBones[side];
            Transform upper = upperBones[side];
            Transform hand = handBones[side];
            if (forearm == null || hand == null) continue;

            Vector3 dir = Mirror(forearmDirection, side).normalized;
            Vector3 wrist = Mirror(handPosition, side);
            Vector3 elbow = wrist - dir * (forearmLength * armsScale);
            Quaternion bind = forearmBind[side];
            float roll = side == 0 ? forearmRoll : -forearmRoll;
            Quaternion q = Quaternion.AngleAxis(roll, dir)
                         * Quaternion.FromToRotation(bind * Vector3.up, dir) * bind;

            Quaternion worldRot = viewRoot.rotation * q;
            Vector3 worldPos = viewRoot.TransformPoint(elbow);
            // The mesh has no real upper arm; keep it rigid with the forearm so
            // the blend zone at the elbow does not stretch back to the shoulder.
            if (upper != null) { upper.position = worldPos; upper.rotation = worldRot; }
            forearm.position = worldPos;
            forearm.rotation = worldRot;

            Vector3 blade = Mirror(bladeDirection, side).normalized;
            Vector3 fingers = Mirror(fingerDirection, side);
            hand.rotation = viewRoot.rotation * Quaternion.LookRotation(blade, fingers);
        }
        for (int side = 0; side < 2; side++)
        {
            float sign = side == 0 ? leftCurlSign : rightCurlSign;
            Joint[] joints = fingerJoints[side];
            for (int j = 0; j < joints.Length; j++)
            {
                if (joints[j].bone == null) continue;
                bool thumb = j >= 12;
                Vector3 curl = thumb ? thumbCurl : fingerCurl;
                float angle = (j % 3) == 0 ? curl.x : (j % 3) == 1 ? curl.y : curl.z;
                joints[j].bone.localRotation = joints[j].bind * Quaternion.Euler(angle * sign, 0f, 0f);
            }
        }
    }

    void ApplyArmsMaterial()
    {
        if (armsAlbedo == null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        Material material = new Material(shader) { name = "FPS_Arms_Viewmodel" };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", armsAlbedo);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", armsAlbedo);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);

        foreach (Renderer renderer in armsInstance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] replacements = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (int i = 0; i < replacements.Length; i++) replacements[i] = material;
            renderer.sharedMaterials = replacements;
        }
    }

    Transform BuildSword(string objectName, Transform socket)
    {
        Transform root = new GameObject(objectName).transform;
        root.SetParent(viewRoot, false);
        GameObject modelObject = Instantiate(swordPrefab, root);
        modelObject.name = "AOT_Gear_Sword";
        Transform model = modelObject.transform;
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;

        Bounds bounds = CalculateLocalBounds(model, root);
        int longAxis = LongestAxis(bounds.size);
        float length = Axis(bounds.size, longAxis);
        float scale = swordLength / Mathf.Max(0.0001f, length);
        Vector3 grip = bounds.center;
        SetAxis(ref grip, longAxis, Axis(bounds.min, longAxis) + length * 0.22f);
        Vector3 bladeDirection = AxisVector(longAxis);
        Quaternion align = Quaternion.FromToRotation(bladeDirection, Vector3.forward);
        model.localRotation = align;
        model.localScale = Vector3.one * scale;
        model.localPosition = -(align * (grip * scale));

        root.position = socket.position;
        return root;
    }

    void LateUpdate()
    {
        if (viewRoot == null || viewCamera == null) return;

        // Chapter 0 has no combat component, but the training scene still
        // needs the exact same dual-sword presentation when F is pressed.
        if (GetComponent<PlayerCombat>() == null
            && (GameInput.GetKeyDown(KeyCode.F) || GameInput.GetMouseButtonDown(2)))
            PlaySlash();

        if (slashTime >= 0f)
        {
            slashTime += Time.deltaTime;
            if (slashTime >= SlashDuration) slashTime = -1f;
        }

        float lookDown = Mathf.Clamp01(Vector3.Dot(viewCamera.transform.forward, Vector3.down));
        // Keep the first-person weapon out of view during traversal. It appears
        // only for the slash itself, including when the player is airborne.
        bool visible = alwaysVisible || slashTime >= 0f || lookDown >= revealPitch;
        SetVisible(visible);

        float lift = lookDownLift * Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(revealPitch, 0.75f, lookDown));
        Vector3 offset = Vector3.up * lift;
        float pitch = 0f;
        float roll = 0f;

        if (slashTime >= 0f)
        {
            float raise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(slashTime / RaiseTime));
            float cut = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((slashTime - RaiseTime) / CutTime));
            float recover = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((slashTime - RaiseTime - CutTime) / RecoverTime));
            Vector3 raised = new Vector3(-0.22f * slashSide, 0.30f, -0.08f);
            Vector3 cutThrough = new Vector3(0.25f * slashSide, -0.30f, 0.22f);
            offset += Vector3.Lerp(Vector3.zero, raised, raise);
            offset = Vector3.Lerp(offset, cutThrough + Vector3.up * lift, cut);
            offset = Vector3.Lerp(offset, Vector3.up * lift, recover);
            pitch = Mathf.Lerp(0f, -18f, raise);
            pitch = Mathf.Lerp(pitch, 24f, cut);
            pitch = Mathf.Lerp(pitch, 0f, recover);
            roll = Mathf.Lerp(0f, -15f * slashSide, raise);
            roll = Mathf.Lerp(roll, 19f * slashSide, cut);
            roll = Mathf.Lerp(roll, 0f, recover);
        }

        viewRoot.localPosition = baseRootPosition + offset;
        viewRoot.localRotation = Quaternion.Euler(pitch, 0f, roll);
        armsInstance.localPosition = armsPosition;
        armsInstance.localScale = Vector3.one * armsScale;
        ApplyPose();
        PoseSword(leftSword, leftSocket, leftGripOffset, leftGripEuler);
        PoseSword(rightSword, rightSocket, rightGripOffset, rightGripEuler);
    }

    void PoseSword(Transform sword, Transform hand, Vector3 gripOffset, Vector3 gripEuler)
    {
        if (sword == null || hand == null) return;
        sword.position = hand.TransformPoint(gripOffset);
        sword.rotation = hand.rotation * Quaternion.Euler(gripEuler);
    }

    void SetVisible(bool visible)
    {
        if (viewRenderers == null) return;
        foreach (Renderer renderer in viewRenderers)
            if (renderer != null) renderer.enabled = visible;
    }

    static Transform FindDeep(Transform root, string target)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == target) return child;
        return null;
    }

    static Bounds CalculateLocalBounds(Transform model, Transform space)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        bool initialized = false;
        Bounds result = new Bounds();
        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                Vector3 local = space.InverseTransformPoint(corner);
                if (!initialized) { result = new Bounds(local, Vector3.zero); initialized = true; }
                else result.Encapsulate(local);
            }
        }
        return result;
    }

    static int LongestAxis(Vector3 size) => size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
    static float Axis(Vector3 value, int axis) => axis == 0 ? value.x : axis == 1 ? value.y : value.z;
    static Vector3 AxisVector(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
    static void SetAxis(ref Vector3 value, int axis, float amount)
    {
        if (axis == 0) value.x = amount;
        else if (axis == 1) value.y = amount;
        else value.z = amount;
    }
}
