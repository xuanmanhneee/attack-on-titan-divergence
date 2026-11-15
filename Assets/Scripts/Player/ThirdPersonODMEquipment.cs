using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the parts that must follow the animated full-body rig: hand-held
/// blades, hip launch boxes, reels and gas canisters.  The rope origins are
/// the actual nozzle transforms, so the LineRenderers never begin at the
/// player's centre.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public sealed class ThirdPersonODMEquipment : MonoBehaviour
{
    FirstPersonCharacterVisual characterVisual;
    Animator animator;
    ODMController odm;

    Transform gearRoot;
    Transform leftSword;
    Transform rightSword;
    Transform leftHand;
    Transform rightHand;
    Transform leftForearm;
    Transform rightForearm;
    Transform hips;

    Material darkMetal;
    Material midMetal;
    Material bladeMetal;
    Material leather;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallIfMissing()
    {
        FirstPersonCharacterVisual visual = FindAnyObjectByType<FirstPersonCharacterVisual>();
        if (visual != null && visual.GetComponent<ThirdPersonODMEquipment>() == null)
            visual.gameObject.AddComponent<ThirdPersonODMEquipment>();
    }

    void Awake()
    {
        characterVisual = GetComponent<FirstPersonCharacterVisual>();
        animator = characterVisual != null ? characterVisual.HumanoidAnimator : GetComponentInChildren<Animator>(true);
        odm = GetComponentInParent<ODMController>();
    }

    void Start()
    {
        if (animator == null || !animator.isHuman || odm == null)
            return;

        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        leftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        if (hips == null || leftHand == null || rightHand == null)
            return;

        CreateMaterials();
        BuildGear();
        leftSword = BuildSword("ThirdPersonSword_Left");
        rightSword = BuildSword("ThirdPersonSword_Right");
        UpdateEquipmentPose();

        ThirdPersonObservationCamera observer = odm.GetComponent<ThirdPersonObservationCamera>();
        characterVisual?.SetThirdPersonView(observer != null && observer.IsThirdPersonActive);
    }

    void LateUpdate()
    {
        UpdateEquipmentPose();
    }

    void BuildGear()
    {
        gearRoot = new GameObject("Complete_ODM_Gear").transform;
        gearRoot.SetParent(transform, false);

        // Central regulator and two full gas bottles on the lower back.
        CreatePart("ODM_Regulator", PrimitiveType.Cube, gearRoot,
            new Vector3(0f, 0.08f, -0.13f), new Vector3(0.24f, 0.18f, 0.12f),
            Quaternion.identity, darkMetal);
        CreatePart("ODM_GasTank_Left", PrimitiveType.Cylinder, gearRoot,
            new Vector3(-0.095f, 0.02f, -0.18f), new Vector3(0.052f, 0.22f, 0.052f),
            Quaternion.Euler(8f, 0f, -5f), midMetal);
        CreatePart("ODM_GasTank_Right", PrimitiveType.Cylinder, gearRoot,
            new Vector3(0.095f, 0.02f, -0.18f), new Vector3(0.052f, 0.22f, 0.052f),
            Quaternion.Euler(8f, 0f, 5f), midMetal);
        CreatePart("ODM_TankBrace", PrimitiveType.Cube, gearRoot,
            new Vector3(0f, 0.02f, -0.225f), new Vector3(0.27f, 0.055f, 0.035f),
            Quaternion.identity, darkMetal);

        Transform leftBox = BuildLauncher("ODM_Launcher_Left", -1f);
        Transform rightBox = BuildLauncher("ODM_Launcher_Right", 1f);

        Transform leftExit = NewSocket("CableExit_Left", leftBox,
            new Vector3(-0.105f, 0.055f, 0.13f));
        Transform rightExit = NewSocket("CableExit_Right", rightBox,
            new Vector3(0.105f, 0.055f, 0.13f));
        odm.hipOriginLeft = leftExit;
        odm.hipOriginRight = rightExit;
    }

    Transform BuildLauncher(string name, float side)
    {
        Transform launcher = new GameObject(name).transform;
        launcher.SetParent(gearRoot, false);
        launcher.localPosition = new Vector3(0.29f * side, -0.01f, -0.015f);
        launcher.localRotation = Quaternion.Euler(0f, 0f, -5f * side);

        CreatePart("ODM_Box", PrimitiveType.Cube, launcher,
            Vector3.zero, new Vector3(0.18f, 0.30f, 0.22f), Quaternion.identity, darkMetal);
        CreatePart("ODM_Reel", PrimitiveType.Cylinder, launcher,
            new Vector3(0.105f * side, 0.025f, -0.015f), new Vector3(0.065f, 0.038f, 0.065f),
            Quaternion.Euler(0f, 0f, 90f), midMetal);
        CreatePart("ODM_Rail", PrimitiveType.Cube, launcher,
            new Vector3(0f, 0.12f, 0.025f), new Vector3(0.13f, 0.035f, 0.24f),
            Quaternion.identity, midMetal);
        CreatePart("ODM_Nozzle", PrimitiveType.Cylinder, launcher,
            new Vector3(0.105f * side, 0.055f, 0.105f), new Vector3(0.028f, 0.065f, 0.028f),
            Quaternion.Euler(90f, 0f, 0f), bladeMetal);
        return launcher;
    }

    Transform BuildSword(string name)
    {
        Transform sword = new GameObject(name).transform;
        sword.SetParent(transform, false);
        CreatePart("Sword_Grip", PrimitiveType.Cube, sword,
            new Vector3(0f, 0f, -0.055f), new Vector3(0.045f, 0.045f, 0.17f),
            Quaternion.identity, leather);
        CreatePart("Sword_Pommel", PrimitiveType.Cube, sword,
            new Vector3(0f, 0f, -0.155f), new Vector3(0.07f, 0.055f, 0.04f),
            Quaternion.identity, darkMetal);
        CreatePart("Sword_ControlHousing", PrimitiveType.Cube, sword,
            new Vector3(0f, 0f, 0.025f), new Vector3(0.105f, 0.075f, 0.11f),
            Quaternion.identity, darkMetal);
        CreatePart("Sword_Guard", PrimitiveType.Cube, sword,
            new Vector3(0f, 0.045f, 0.055f), new Vector3(0.18f, 0.026f, 0.045f),
            Quaternion.identity, darkMetal);
        CreatePart("Sword_Trigger", PrimitiveType.Cube, sword,
            new Vector3(0.045f, 0.045f, -0.035f), new Vector3(0.018f, 0.055f, 0.065f),
            Quaternion.Euler(15f, 0f, 0f), midMetal);
        CreatePart("Sword_BladeSocket", PrimitiveType.Cube, sword,
            new Vector3(0f, 0f, 0.13f), new Vector3(0.075f, 0.055f, 0.14f),
            Quaternion.identity, midMetal);
        CreatePart("Sword_Blade", PrimitiveType.Cube, sword,
            new Vector3(0f, 0f, 0.48f), new Vector3(0.052f, 0.012f, 0.84f),
            Quaternion.identity, bladeMetal);
        CreatePart("Sword_BladeTip", PrimitiveType.Cube, sword,
            new Vector3(0f, 0f, 0.91f), new Vector3(0.051f, 0.011f, 0.075f),
            Quaternion.Euler(0f, 45f, 0f), bladeMetal);
        return sword;
    }

    void UpdateEquipmentPose()
    {
        if (odm == null)
            return;

        if (gearRoot != null && hips != null)
        {
            gearRoot.position = hips.position - odm.transform.up * 0.13f - odm.transform.forward * 0.08f;
            gearRoot.rotation = odm.transform.rotation;
        }

        PoseSword(leftSword, leftHand, leftForearm, -1f);
        PoseSword(rightSword, rightHand, rightForearm, 1f);
    }

    void PoseSword(Transform sword, Transform hand, Transform forearm, float side)
    {
        if (sword == null || hand == null)
            return;

        Vector3 direction = forearm != null ? hand.position - forearm.position : hand.forward;
        if (direction.sqrMagnitude < 0.0001f)
            direction = odm.transform.forward;
        direction.Normalize();
        direction = Quaternion.AngleAxis(3f * side, odm.transform.up) * direction;
        Vector3 up = Vector3.Cross(direction, odm.transform.right * side);
        if (up.sqrMagnitude < 0.001f)
            up = odm.transform.up;
        sword.position = hand.position + direction * 0.055f + odm.transform.right * (0.065f * side);
        sword.rotation = Quaternion.LookRotation(direction, up.normalized);
    }

    Transform CreatePart(string name, PrimitiveType primitive, Transform parent,
                         Vector3 localPosition, Vector3 localScale,
                         Quaternion localRotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(primitive);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;
        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
        renderer.receiveShadows = true;
        return part.transform;
    }

    static Transform NewSocket(string name, Transform parent, Vector3 localPosition)
    {
        Transform socket = new GameObject(name).transform;
        socket.SetParent(parent, false);
        socket.localPosition = localPosition;
        socket.localRotation = Quaternion.identity;
        return socket;
    }

    void CreateMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        darkMetal = NewMaterial(shader, "ODM Dark Metal", new Color(0.075f, 0.085f, 0.09f), 0.78f, 0.48f);
        midMetal = NewMaterial(shader, "ODM Steel", new Color(0.25f, 0.28f, 0.29f), 0.82f, 0.58f);
        bladeMetal = NewMaterial(shader, "ODM Blade", new Color(0.62f, 0.67f, 0.69f), 0.92f, 0.75f);
        leather = NewMaterial(shader, "ODM Grip", new Color(0.055f, 0.035f, 0.022f), 0.05f, 0.24f);
    }

    static Material NewMaterial(Shader shader, string name, Color color, float metallic, float smoothness)
    {
        Material material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }
}
