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
    [Tooltip("Corrects the imported arm rig after its wrists are aligned to the camera. Use this only for the first-person rig.")]
    // Keep the imported forearm pose after aligning the wrists. A 180-degree roll
    // puts the elbows above the hands and makes the arms cover the swords.
    [SerializeField] Vector3 armRigRotationCorrection = Vector3.zero;
    [Tooltip("Small local offset applied after the rig correction, useful for keeping the forearms out of the camera clip plane.")]
    [SerializeField] Vector3 armRigPositionCorrection = new Vector3(0f, 0f, 0.02f);
    [Tooltip("Keeps only wrist/hand/finger-weighted triangles from the imported arm mesh.")]
    [SerializeField] bool cropForearmsToHands = true;
    [Tooltip("Length of forearm retained next to each wrist, in imported arm-model units.")]
    [SerializeField, Min(0f)] float retainedForearmLength = 0.28f;
    [Tooltip("Pitch of the sword toward/away from the camera.")]
    [SerializeField] float swordPitch = 0f;
    [Tooltip("Rolls the blade around its length so the blade face is vertical in the hand.")]
    [SerializeField] float swordRoll = 270f;
    [Tooltip("Moves the sword relative to the wrist socket in camera-local space.")]
    [SerializeField] Vector3 swordPositionOffset = Vector3.zero;
    // Do not reveal the full arm model for a small camera pitch. The arms should
    // appear only when the player deliberately looks down; slashes still force
    // them visible through slashTime below.
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
        // The imported socket helpers are positioned for the source arm rig and
        // do not sit in the palm after the viewmodel is fitted to the camera.
        // Anchor the sword grip to the actual wrist bones instead.
        leftSocket = leftWrist;
        rightSocket = rightWrist;
        if (leftWrist == null || rightWrist == null || leftSocket == null || rightSocket == null)
        {
            Debug.LogError("WRAD arms are missing wrist/socket bones.", this);
            return;
        }

        FitArmsToCamera(leftWrist, rightWrist);
        if (cropForearmsToHands)
            CropForearmsToHands(leftWrist, rightWrist);
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

        // Align the wrist line to the camera's X axis first. The old implementation
        // used FromToRotation alone, which leaves the roll around that axis
        // under-constrained; depending on the imported FBX orientation the arms
        // could end up facing backward or twisting the swords away from the hands.
        Quaternion alignAcrossView = Quaternion.FromToRotation(span.normalized, Vector3.right);
        Quaternion correctedRotation = Quaternion.Euler(armRigRotationCorrection) * alignAcrossView;
        float scale = wristSeparation / span.magnitude;
        Vector3 midpoint = (left + right) * 0.5f;
        armsInstance.localRotation = correctedRotation;
        armsInstance.localScale = Vector3.one * scale;
        armsInstance.localPosition = wristMidpoint
            - correctedRotation * (midpoint * scale)
            + armRigPositionCorrection;
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

    void CropForearmsToHands(Transform leftWrist, Transform rightWrist)
    {
        foreach (SkinnedMeshRenderer renderer in armsInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh source = renderer.sharedMesh;
            if (source == null || source.subMeshCount == 0) continue;

            bool[] handBones = new bool[renderer.bones.Length];
            bool[] forearmBones = new bool[renderer.bones.Length];
            for (int i = 0; i < renderer.bones.Length; i++)
            {
                string boneName = renderer.bones[i] != null
                    ? renderer.bones[i].name.ToLowerInvariant()
                    : string.Empty;
                handBones[i] = boneName.Contains("wrist")
                    || boneName.Contains("hand")
                    || boneName.Contains("finger")
                    || boneName.Contains("thumb")
                    || boneName.Contains("index")
                    || boneName.Contains("middle")
                    || boneName.Contains("ring")
                    || boneName.Contains("pinky");
                forearmBones[i] = boneName.Contains("forearm");
            }

            Vector3[] wristPositions =
            {
                renderer.transform.InverseTransformPoint(leftWrist.position),
                renderer.transform.InverseTransformPoint(rightWrist.position)
            };

            BoneWeight[] weights = source.boneWeights;
            Mesh cropped = Instantiate(source);
            cropped.name = source.name + "_HandsOnly";
            cropped.subMeshCount = source.subMeshCount;

            for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                int[] indices = source.GetIndices(subMesh);
                List<int> kept = new List<int>(indices.Length);
                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    int handVertices = 0;
                    int shortForearmVertices = 0;
                    if (HasHandInfluence(weights[indices[i]], handBones)) handVertices++;
                    if (HasHandInfluence(weights[indices[i + 1]], handBones)) handVertices++;
                    if (HasHandInfluence(weights[indices[i + 2]], handBones)) handVertices++;
                    if (HasShortForearmInfluence(weights[indices[i]], source.vertices[indices[i]], forearmBones, wristPositions)) shortForearmVertices++;
                    if (HasShortForearmInfluence(weights[indices[i + 1]], source.vertices[indices[i + 1]], forearmBones, wristPositions)) shortForearmVertices++;
                    if (HasShortForearmInfluence(weights[indices[i + 2]], source.vertices[indices[i + 2]], forearmBones, wristPositions)) shortForearmVertices++;
                    if (handVertices >= 2 || (handVertices >= 1 && shortForearmVertices >= 2))
                    {
                        kept.Add(indices[i]);
                        kept.Add(indices[i + 1]);
                        kept.Add(indices[i + 2]);
                    }
                }
                cropped.SetIndices(kept, MeshTopology.Triangles, subMesh, false);
            }

            renderer.sharedMesh = cropped;
        }
    }

    static bool HasHandInfluence(BoneWeight weight, bool[] handBones)
    {
        return (weight.weight0 > 0.2f && IsHandBone(weight.boneIndex0, handBones))
            || (weight.weight1 > 0.2f && IsHandBone(weight.boneIndex1, handBones))
            || (weight.weight2 > 0.2f && IsHandBone(weight.boneIndex2, handBones))
            || (weight.weight3 > 0.2f && IsHandBone(weight.boneIndex3, handBones));
    }

    bool HasShortForearmInfluence(BoneWeight weight, Vector3 vertex, bool[] forearmBones, Vector3[] wristPositions)
    {
        bool forearmWeighted = (weight.weight0 > 0.2f && IsHandBone(weight.boneIndex0, forearmBones))
            || (weight.weight1 > 0.2f && IsHandBone(weight.boneIndex1, forearmBones))
            || (weight.weight2 > 0.2f && IsHandBone(weight.boneIndex2, forearmBones))
            || (weight.weight3 > 0.2f && IsHandBone(weight.boneIndex3, forearmBones));
        if (!forearmWeighted) return false;

        float nearestWristDistance = Mathf.Min(
            Vector3.Distance(vertex, wristPositions[0]),
            Vector3.Distance(vertex, wristPositions[1]));
        return nearestWristDistance <= retainedForearmLength;
    }

    static bool IsHandBone(int index, bool[] handBones)
    {
        return index >= 0 && index < handBones.Length && handBones[index];
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
        // Keep the first-person weapon out of view during traversal. It appears
        // only for the slash itself, including when the player is airborne.
        bool visible = slashTime >= 0f;
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
        sword.position = socket.position
            + viewCamera.transform.right * swordPositionOffset.x
            + viewCamera.transform.up * swordPositionOffset.y
            + viewCamera.transform.forward * swordPositionOffset.z;

        sword.rotation = viewCamera.transform.rotation * Quaternion.Euler(swordPitch, 0f, swordRoll + 7f * side);
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
