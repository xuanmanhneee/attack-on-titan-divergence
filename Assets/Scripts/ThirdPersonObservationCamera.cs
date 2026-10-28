using UnityEngine;

/// <summary>
/// Optional chase camera used to inspect the animated player model while playing.
/// Gameplay aiming continues to use ODMController.cameraTransform, so switching
/// views does not change hook or movement calculations.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(300)]
public sealed class ThirdPersonObservationCamera : MonoBehaviour
{
    [SerializeField] ODMController odm;
    [SerializeField] Camera firstPersonCamera;
    [SerializeField] KeyCode toggleKey = KeyCode.V;
    [SerializeField] Vector3 localOffset = new Vector3(0.32f, 1.25f, -3.8f);
    [SerializeField] Vector3 localLookOffset = new Vector3(0f, 0.34f, 0f);
    [SerializeField] float forwardLook = 0.7f;
    [SerializeField] float pitchHeightInfluence = 0.75f;
    [SerializeField] float followSharpness = 11f;
    [SerializeField] float rotationSharpness = 16f;
    [SerializeField] float collisionRadius = 0.24f;
    [SerializeField] float collisionPadding = 0.16f;
    [SerializeField] float groundClearance = 0.22f;

    readonly RaycastHit[] collisionHits = new RaycastHit[16];
    Camera observationCamera;
    FirstPersonCharacterVisual characterVisual;
    CharacterController characterController;
    Transform firstPersonViewmodel;
    bool thirdPersonActive;
    Vector3 smoothedFocus;
    bool chasePoseReady;

    public bool IsThirdPersonActive => thirdPersonActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallIfMissing()
    {
        ODMController controller = FindAnyObjectByType<ODMController>();
        if (controller != null && controller.GetComponent<ThirdPersonObservationCamera>() == null)
            controller.gameObject.AddComponent<ThirdPersonObservationCamera>();
    }

    void Awake()
    {
        if (odm == null)
            odm = GetComponent<ODMController>();
        if (firstPersonCamera == null && odm != null && odm.cameraTransform != null)
            firstPersonCamera = odm.cameraTransform.GetComponent<Camera>();

        characterVisual = FindWorldCharacterVisual();
        characterController = GetComponent<CharacterController>();
        if (firstPersonCamera != null)
            firstPersonViewmodel = firstPersonCamera.transform.Find("ODM_Viewmodel");
        if (firstPersonViewmodel == null && odm != null)
            firstPersonViewmodel = odm.transform.Find("ODM_Viewmodel");
        BuildObservationCamera();
    }

    void Update()
    {
        if (GameInput.GetKeyDown(toggleKey))
            SetThirdPersonActive(!thirdPersonActive);
    }

    void LateUpdate()
    {
        if (!thirdPersonActive || observationCamera == null)
            return;

        Vector3 up = Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        Vector3 right = Vector3.Cross(up, forward).normalized;

        // The player transform sits at the capsule centre, not at the feet.
        // Derive a stable chest target from the controller so the camera does
        // not make the avatar appear buried when its proportions change.
        float centreY = characterController != null ? characterController.center.y : 0f;
        float height = characterController != null ? characterController.height : 1.7f;
        Vector3 rawFocus = transform.position + up * (centreY + height * 0.20f)
                         + forward * localLookOffset.z + right * localLookOffset.x;
        if (!chasePoseReady)
        {
            smoothedFocus = rawFocus;
            chasePoseReady = true;
        }
        float blend = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
        smoothedFocus = Vector3.Lerp(smoothedFocus, rawFocus, blend);
        Vector3 focus = smoothedFocus + forward * forwardLook;
        float pitch = firstPersonCamera != null
            ? Mathf.DeltaAngle(0f, firstPersonCamera.transform.localEulerAngles.x)
            : 0f;
        float pitchHeight = Mathf.Lerp(-0.2f, pitchHeightInfluence,
            Mathf.InverseLerp(-35f, 65f, pitch));
        Vector3 desired = smoothedFocus
                        - forward * Mathf.Abs(localOffset.z)
                        + right * localOffset.x
                        + up * (localOffset.y + pitchHeight);
        Vector3 travel = desired - focus;
        float distance = travel.magnitude;
        if (distance > 0.001f)
        {
            Vector3 direction = travel / distance;
            int hitCount = Physics.SphereCastNonAlloc(
                focus, collisionRadius, direction, collisionHits, distance,
                ~0, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = collisionHits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                    continue;
                nearest = Mathf.Min(nearest, collisionHits[i].distance);
            }
            desired = focus + direction * Mathf.Max(0.35f, nearest - collisionPadding);
        }

        // Collision shortening can push the chase camera close to sloped
        // terrain. Keep the lens above the surface to avoid underground shots.
        Vector3 groundProbe = desired + up * 1.5f;
        if (Physics.Raycast(groundProbe, Vector3.down, out RaycastHit groundHit,
                            3f, ~0, QueryTriggerInteraction.Ignore)
            && !groundHit.collider.transform.IsChildOf(transform))
            desired.y = Mathf.Max(desired.y, groundHit.point.y + groundClearance);

        observationCamera.transform.position = Vector3.Lerp(
            observationCamera.transform.position, desired, blend);
        float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.unscaledDeltaTime);
        observationCamera.transform.rotation = Quaternion.Slerp(
            observationCamera.transform.rotation,
            Quaternion.LookRotation(focus - observationCamera.transform.position, Vector3.up),
            rotationBlend);
        if (firstPersonCamera != null)
            observationCamera.fieldOfView = firstPersonCamera.fieldOfView;
    }

    void BuildObservationCamera()
    {
        if (observationCamera != null || firstPersonCamera == null)
            return;

        GameObject cameraObject = new GameObject("ThirdPersonObservationCamera");
        observationCamera = cameraObject.AddComponent<Camera>();
        observationCamera.CopyFrom(firstPersonCamera);
        observationCamera.enabled = false;
        cameraObject.tag = "Untagged";
    }

    FirstPersonCharacterVisual FindWorldCharacterVisual()
    {
        foreach (FirstPersonCharacterVisual visual in GetComponentsInChildren<FirstPersonCharacterVisual>(true))
        {
            // The first-person viewmodel contains a disabled clone of the body.
            // Never toggle that clone when switching the world camera.
            if (visual.enabled
                && (firstPersonCamera == null || !visual.transform.IsChildOf(firstPersonCamera.transform)))
                return visual;
        }
        return null;
    }

    void SetThirdPersonActive(bool active)
    {
        BuildObservationCamera();
        if (observationCamera == null || firstPersonCamera == null)
            return;

        thirdPersonActive = active;
        if (active)
        {
            if (characterVisual == null)
                characterVisual = FindWorldCharacterVisual();
            if (characterVisual != null)
            {
                characterVisual.SetThirdPersonView(true);
                characterVisual.SnapFeetToController();
            }
            float centreY = characterController != null ? characterController.center.y : 0f;
            float height = characterController != null ? characterController.height : 1.7f;
            Vector3 focus = transform.position + Vector3.up * (centreY + height * 0.20f);
            smoothedFocus = focus;
            chasePoseReady = true;
            observationCamera.transform.position = focus
                + transform.right * localOffset.x
                + Vector3.up * localOffset.y
                - transform.forward * Mathf.Abs(localOffset.z);
            observationCamera.transform.LookAt(focus);
        }
        else
        {
            chasePoseReady = false;
        }

        firstPersonCamera.enabled = !active;
        observationCamera.enabled = active;
        if (firstPersonViewmodel == null)
            firstPersonViewmodel = firstPersonCamera.transform.Find("ODM_Viewmodel");
        if (firstPersonViewmodel == null && odm != null)
            firstPersonViewmodel = odm.transform.Find("ODM_Viewmodel");
        if (firstPersonViewmodel != null)
            firstPersonViewmodel.gameObject.SetActive(!active);
        if (characterVisual == null)
            characterVisual = FindWorldCharacterVisual();
        if (characterVisual != null && !active)
            characterVisual.SetThirdPersonView(active);
    }

    void OnDisable()
    {
        if (thirdPersonActive)
            SetThirdPersonActive(false);
    }
}
