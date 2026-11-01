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
    [SerializeField] Vector3 wristMidpoint = new Vector3(0f, -0.48f, 0.60f);
    [SerializeField, Min(0.2f)] float wristSeparation = 0.58f;
    [SerializeField, Min(0.2f)] float swordLength = 1.15f;
    [SerializeField, Range(0f, 1f)] float revealPitch = 0.18f;
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
        armsObject.name = "WRAD_Arms";
        armsInstance = armsObject.transform;
        armsInstance.localPosition = Vector3.zero;
        armsInstance.localRotation = Quaternion.identity;
        armsInstance.localScale = Vector3.one;

        Transform leftWrist = FindDeep(armsInstance, "wrist.l");
        Transform rightWrist = FindDeep(armsInstance, "wrist.r");
        leftSocket = FindDeep(armsInstance, "socket.l") ?? leftWrist;
        rightSocket = FindDeep(armsInstance, "socket.r") ?? rightWrist;
        if (leftWrist == null || rightWrist == null || leftSocket == null || rightSocket == null)
        {
            Debug.LogError("WRAD arms are missing wrist/socket bones.", this);
            return;
        }

        FitArmsToCamera(leftWrist, rightWrist);
        ApplyArmsMaterial();
        CurlGripFingers(".l", -1f);
        CurlGripFingers(".r", 1f);

        leftSword = BuildSword("Left_ImportedSword", leftSocket);
        rightSword = BuildSword("Right_ImportedSword", rightSocket);

        List<Renderer> renderers = new List<Renderer>();
        foreach (Renderer renderer in viewRoot.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderers.Add(renderer);
        }
        viewRenderers = renderers.ToArray();
        SetVisible(false);
    }

    void FitArmsToCamera(Transform leftWrist, Transform rightWrist)
    {
        Vector3 left = armsInstance.InverseTransformPoint(leftWrist.position);
        Vector3 right = armsInstance.InverseTransformPoint(rightWrist.position);
        Vector3 span = right - left;
        if (span.sqrMagnitude < 0.000001f) return;

        Quaternion alignAcrossView = Quaternion.FromToRotation(span.normalized, Vector3.right);
        float scale = wristSeparation / span.magnitude;
        Vector3 midpoint = (left + right) * 0.5f;
        armsInstance.localRotation = alignAcrossView;
        armsInstance.localScale = Vector3.one * scale;
        armsInstance.localPosition = wristMidpoint - alignAcrossView * (midpoint * scale);
    }

    void ApplyArmsMaterial()
    {
        if (armsAlbedo == null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        Material material = new Material(shader) { name = "WRAD_Arms_Viewmodel" };
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

    void CurlGripFingers(string suffix, float side)
    {
        string[] names = { "finger_index1", "finger_middle1", "finger_ring1", "finger_pinky1" };
        foreach (string prefix in names)
        {
            Transform first = FindDeep(armsInstance, prefix + suffix);
            if (first == null) continue;
            first.localRotation *= Quaternion.Euler(0f, 0f, -34f * side);
            if (first.childCount > 0)
            {
                Transform second = first.GetChild(0);
                second.localRotation *= Quaternion.Euler(0f, 0f, -42f * side);
                if (second.childCount > 0)
                    second.GetChild(0).localRotation *= Quaternion.Euler(0f, 0f, -34f * side);
            }
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
        SetAxis(ref grip, longAxis, Axis(bounds.min, longAxis) + length * 0.12f);
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
        bool visible = lookDown >= revealPitch || slashTime >= 0f;
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
        PoseSword(leftSword, leftSocket, -1f);
        PoseSword(rightSword, rightSocket, 1f);
    }

    void PoseSword(Transform sword, Transform socket, float side)
    {
        if (sword == null || socket == null) return;
        sword.position = socket.position;
        sword.rotation = viewCamera.transform.rotation * Quaternion.Euler(-10f, 0f, 7f * side);
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
