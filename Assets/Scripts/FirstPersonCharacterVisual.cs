using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

/// <summary>
/// Presentation-only behavior for the local player's full-body model.
/// The camera sits inside the body, so it renders only through its separate viewmodel.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class FirstPersonCharacterVisual : MonoBehaviour
{
    [SerializeField] Animator humanoidAnimator;
    [SerializeField] Camera firstPersonCamera;
    [SerializeField] bool proceduralArmPose = true;
    [SerializeField] bool showLowerBodyInFirstPerson;
    [SerializeField] bool fullBodyShadowsOnly = false;
    [SerializeField, Min(0.08f)] float firstPersonNearClip = 0.12f;

    ODMController odm;
    float visualAirTime;
    bool visualRunning;

    static readonly int RunningParam = Animator.StringToHash("isRunning");
    static readonly int GassingParam = Animator.StringToHash("isGasing");
    static readonly int FallingParam = Animator.StringToHash("isFalling");

    public Animator HumanoidAnimator => humanoidAnimator;

    public void SetThirdPersonView(bool enabled)
    {
        if (enabled)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.SetActive(true);
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            }
        }
        else
        {
            ApplyFirstPersonVisibility();
        }
    }

    public void Configure(Animator animator, Camera camera)
    {
        humanoidAnimator = animator;
        firstPersonCamera = camera;
        ApplyFirstPersonVisibility();
    }

    void Update()
    {
        if (humanoidAnimator == null || odm == null || humanoidAnimator.runtimeAnimatorController == null)
            return;

        if (odm.IsGrounded)
            visualAirTime = 0f;
        else
            visualAirTime += Time.deltaTime;

        // A CharacterController can report one airborne frame while sliding over
        // seams.  A short grace period and separate enter/exit speed thresholds
        // prevent the Animator from rapidly bouncing between states.
        bool visuallyFalling = visualAirTime > 0.09f;
        float runningThreshold = visualRunning ? 0.65f : 1.45f;
        visualRunning = !visuallyFalling && odm.Speed > runningThreshold;
        humanoidAnimator.SetBool(RunningParam, visualRunning);
        humanoidAnimator.SetBool(FallingParam, visuallyFalling);
        humanoidAnimator.SetBool(GassingParam, odm.IsGassing);
    }

    void Awake()
    {
        if (humanoidAnimator == null)
            humanoidAnimator = GetComponentInChildren<Animator>(true);

        if (firstPersonCamera == null)
            firstPersonCamera = GetComponentInParent<ODMController>()?.cameraTransform?.GetComponent<Camera>();

        odm = GetComponentInParent<ODMController>();

        ApplyFirstPersonVisibility();
    }

    IEnumerator Start()
    {
        // Wait until Animator has evaluated its first pose. Aligning from the Head
        // bone can leave the rest of a differently-proportioned avatar underground.
        yield return null;
        AlignFeetToController();
    }

    void AlignFeetToController()
    {
        CharacterController characterController = GetComponentInParent<CharacterController>();
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (characterController == null || renderers.Length == 0)
            return;

        Bounds bodyBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bodyBounds.Encapsulate(renderers[i].bounds);

        float worldHeight = characterController.height
            * Mathf.Abs(characterController.transform.lossyScale.y);
        float controllerBottom = characterController.transform.TransformPoint(characterController.center).y
            - worldHeight * 0.5f;
        float verticalCorrection = controllerBottom - bodyBounds.min.y;
        if (float.IsFinite(verticalCorrection))
            transform.position += Vector3.up * verticalCorrection;
    }

    /// <summary>Re-evaluate the animated bounds after enabling third-person meshes.</summary>
    public void SnapFeetToController() => AlignFeetToController();

    void LateUpdate()
    {
        if (!proceduralArmPose || humanoidAnimator == null || !humanoidAnimator.isHuman)
            return;

        bool grappling = odm != null && odm.ActiveHookCount > 0;
        PoseArm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -1f, grappling);
        PoseArm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 1f, grappling);
    }

    void PoseArm(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone,
                 float side, bool grappling)
    {
        Transform upper = humanoidAnimator.GetBoneTransform(upperBone);
        Transform lower = humanoidAnimator.GetBoneTransform(lowerBone);
        Transform hand = humanoidAnimator.GetBoneTransform(handBone);
        if (upper == null || lower == null || hand == null) return;

        Vector3 upperLocal = grappling
            ? new Vector3(0.28f * side, -0.62f, 0.73f)
            : new Vector3(0.18f * side, -0.97f, 0.12f);
        Vector3 lowerLocal = grappling
            ? new Vector3(0.08f * side, -0.08f, 0.99f)
            : new Vector3(0.06f * side, -0.84f, 0.54f);

        AimBone(upper, lower, transform.TransformDirection(upperLocal.normalized));
        AimBone(lower, hand, transform.TransformDirection(lowerLocal.normalized));
    }

    static void AimBone(Transform bone, Transform child, Vector3 desiredWorldDirection)
    {
        Vector3 current = child.position - bone.position;
        if (current.sqrMagnitude < 0.000001f) return;
        bone.rotation = Quaternion.FromToRotation(current.normalized, desiredWorldDirection) * bone.rotation;
    }

    void ApplyFirstPersonVisibility()
    {
        if (firstPersonCamera != null)
            firstPersonCamera.nearClipPlane = Mathf.Max(firstPersonCamera.nearClipPlane, firstPersonNearClip);

        Renderer[] bodyRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in bodyRenderers)
        {
            bool visibleLowerBody = showLowerBodyInFirstPerson
                && !fullBodyShadowsOnly
                && IsLowerBodyRenderer(renderer);
            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
            renderer.shadowCastingMode = visibleLowerBody
                ? ShadowCastingMode.On
                : ShadowCastingMode.ShadowsOnly;
            if (visibleLowerBody && renderer is SkinnedMeshRenderer skinned)
                skinned.updateWhenOffscreen = true;
        }
    }

    static bool IsLowerBodyRenderer(Renderer renderer)
    {
        string meshName = renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null
            ? skinned.sharedMesh.name
            : string.Empty;
        string part = (renderer.name + " " + meshName).ToLowerInvariant();
        return part.Contains("legs")
            || part.Contains("waist")
            || part.Contains("boots")
            || part.Contains("boot strings")
            || part.Contains("odm base")
            || part.Contains("complete_odm")
            || part.Contains("odm_");
    }
}
