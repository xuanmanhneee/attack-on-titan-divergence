#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PlayerModelSetup
{
    const float TrostErenHeight = 1.70f;
    const float TrostErenRadius = 0.35f;
    const float TrostErenEyeHeight = 1.56f;
    const float FirstPersonNearClip = 0.12f;
    const float FirstPersonBodyBackOffset = 0.18f;

    const string ModelPath = "Assets/Models/Player/ErenHuman/Eren 53k.fbx";
    const string MaterialFolder = "Assets/Models/Player/ErenHuman/MaterialsURP";
    const string PrefabPath = "Assets/Models/Player/ErenHuman/ErenHuman_PlayerVisual.prefab";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string FpsArmsPath = "Assets/Models/Player/WRADArms/wrad-arms-main/arms.fbx";
    const string FpsArmsTexturePath = "Assets/Models/Player/WRADArms/wrad-arms-main/arm_albedo_pale.png";
    const string RealisticOdmPath = "Assets/Models/Weapons/odm_gear.glb";

    [MenuItem("Attack on Titan/Setup Eren Player Visual")]
    public static void Setup()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureTextureImport("Assets/Models/Player/ErenHuman/clothes_bump.png", true);
        ConfigureTextureImport("Assets/Models/Player/ErenHuman/tSurvey_bump.png", true);

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
            throw new FileNotFoundException("Cannot load Eren FBX", ModelPath);

        GameObject visualPrefab = BuildVisualPrefab(model);
        InstallIntoSampleScene(visualPrefab);
        AssetDatabase.SaveAssets();
        CapturePreview();
        Debug.Log("Eren human visual installed into SampleScene.");
    }

    public static void CapturePreview()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ODMController odm = UnityEngine.Object.FindFirstObjectByType<ODMController>();
        FirstPersonCharacterVisual installed = odm != null
            ? odm.GetComponentInChildren<FirstPersonCharacterVisual>(true)
            : null;
        Transform visual = installed != null ? installed.transform : null;
        if (visual == null)
            throw new InvalidOperationException("ErenHumanVisual is not installed in SampleScene.");

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        var originalShadowModes = new ShadowCastingMode[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originalShadowModes[i] = renderers[i].shadowCastingMode;
            renderers[i].shadowCastingMode = ShadowCastingMode.On;
        }

        Bounds bounds = CalculateBounds(visual.gameObject);
        GameObject cameraObject = new GameObject("ErenPreviewCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
        camera.fieldOfView = 35f;
        camera.transform.position = bounds.center + new Vector3(0f, bounds.extents.y * 0.05f, bounds.size.y * 2.2f);
        camera.transform.LookAt(bounds.center + Vector3.up * bounds.extents.y * 0.05f);

        RenderTexture target = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
        Texture2D image = new Texture2D(768, 768, TextureFormat.RGBA32, false);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
        image.Apply();

        string outputDirectory = Path.GetFullPath("TempAssetInspect");
        Directory.CreateDirectory(outputDirectory);
        string outputPath = Path.Combine(outputDirectory, "ErenHumanPreview.png");
        File.WriteAllBytes(outputPath, image.EncodeToPNG());

        RenderTexture.active = null;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].shadowCastingMode = originalShadowModes[i];

        Debug.Log($"Eren preview saved to {outputPath}; bounds={bounds.size}; scene={scene.name}");
    }

    static GameObject BuildVisualPrefab(GameObject model)
    {
        EnsureFolder(MaterialFolder);
        GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
        if (instance == null)
            throw new InvalidOperationException("Could not instantiate Eren model.");

        instance.name = "ErenHumanVisual";
        Dictionary<string, Material> converted = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null) continue;

                if (!converted.TryGetValue(source.name, out Material replacement))
                {
                    replacement = CreateUrpMaterial(source);
                    converted[source.name] = replacement;
                }
                materials[i] = replacement;
            }
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = IsFirstPersonBodyPart(renderer)
                ? ShadowCastingMode.On
                : ShadowCastingMode.ShadowsOnly;
        }

        Animator animator = instance.GetComponentInChildren<Animator>(true);
        FirstPersonCharacterVisual presentation = instance.GetComponent<FirstPersonCharacterVisual>();
        if (presentation == null)
            presentation = instance.AddComponent<FirstPersonCharacterVisual>();
        presentation.Configure(animator, null);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
        return prefab;
    }

    static bool IsFirstPersonBodyPart(Renderer renderer)
    {
        string meshName = renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null
            ? skinned.sharedMesh.name
            : string.Empty;
        string part = (renderer.name + " " + meshName).ToLowerInvariant();
        return part.Contains("legs")
            || part.Contains("waist")
            || part.Contains("boots")
            || part.Contains("boot strings")
            || part.Contains("odm base");
    }

    static Material CreateUrpMaterial(Material source)
    {
        string safeName = SanitizeFileName(source.name);
        string path = $"{MaterialFolder}/{safeName}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("URP Lit shader is unavailable.");

        Texture baseMap = ReadTexture(source, "_BaseMap", "_MainTex");
        Texture normalMap = ReadTexture(source, "_BumpMap");
        // Imported FBX shader colors are not reliable linear-space tints. When a
        // diffuse texture exists, white preserves the texture's authored colors.
        Color color = baseMap != null ? Color.white : ReadColor(source, "_BaseColor", "_Color");

        Material material = new Material(shader)
        {
            name = $"{source.name}_URP",
            color = color
        };
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", source.name.IndexOf("highlight", StringComparison.OrdinalIgnoreCase) >= 0 ? 0.65f : 0.25f);

        if (baseMap != null)
            material.SetTexture("_BaseMap", baseMap);
        if (normalMap != null)
        {
            material.SetTexture("_BumpMap", normalMap);
            material.EnableKeyword("_NORMALMAP");
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Texture ReadTexture(Material material, params string[] names)
    {
        foreach (string name in names)
            if (material.HasProperty(name) && material.GetTexture(name) != null)
                return material.GetTexture(name);
        return null;
    }

    static Color ReadColor(Material material, params string[] names)
    {
        foreach (string name in names)
            if (material.HasProperty(name))
                return material.GetColor(name);
        return Color.white;
    }

    static void InstallIntoSampleScene(GameObject visualPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ODMController odm = UnityEngine.Object.FindFirstObjectByType<ODMController>();
        if (odm == null)
            throw new InvalidOperationException("SampleScene has no ODMController.");

        FirstPersonCharacterVisual[] oldVisuals = odm.GetComponentsInChildren<FirstPersonCharacterVisual>(true);
        foreach (FirstPersonCharacterVisual oldVisual in oldVisuals)
            UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);

        // Keep gameplay measurements in metres. The old non-uniform root scale made
        // the 2 m CharacterController 3.6 m tall in world space.
        odm.transform.localScale = Vector3.one;
        CharacterController controller = odm.GetComponent<CharacterController>();
        controller.height = TrostErenHeight;
        controller.radius = TrostErenRadius;
        controller.center = Vector3.zero;
        if (odm.cameraTransform != null)
        {
            odm.cameraTransform.localPosition = Vector3.up * (TrostErenEyeHeight - TrostErenHeight * 0.5f);
            Camera firstPersonCamera = odm.cameraTransform.GetComponent<Camera>();
            if (firstPersonCamera != null)
                firstPersonCamera.nearClipPlane = FirstPersonNearClip;
        }

        GameObject visual = PrefabUtility.InstantiatePrefab(visualPrefab, scene) as GameObject;
        visual.name = "ErenHumanVisual";
        visual.transform.SetParent(odm.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        Bounds bounds = CalculateBounds(visual);
        float targetWorldHeight = controller.height * Mathf.Abs(odm.transform.lossyScale.y);
        float scale = targetWorldHeight / Mathf.Max(0.01f, bounds.size.y);
        visual.transform.localScale *= scale;

        Animator animator = visual.GetComponentInChildren<Animator>(true);
        bounds = CalculateBounds(visual);
        Vector3 controllerCenter = odm.transform.TransformPoint(controller.center);
        float controllerBottom = controllerCenter.y - targetWorldHeight * 0.5f;
        visual.transform.position += Vector3.up * (controllerBottom - bounds.min.y);
        visual.transform.position -= odm.transform.forward * FirstPersonBodyBackOffset;

        FirstPersonCharacterVisual presentation = visual.GetComponent<FirstPersonCharacterVisual>();
        presentation.Configure(animator, odm.cameraTransform.GetComponent<Camera>());

        InstallCableExitSockets(odm, visual, animator);
        MeshRenderer capsuleRenderer = odm.GetComponent<MeshRenderer>();
        if (capsuleRenderer != null)
            capsuleRenderer.enabled = false;

        DisablePlaceholderRenderer(odm.hipOriginLeft);
        DisablePlaceholderRenderer(odm.hipOriginRight);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void InstallCableExitSockets(ODMController odm, GameObject visual, Animator animator)
    {
        Renderer odmRenderer = null;
        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.gameObject.name.IndexOf("ODM base", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                odmRenderer = renderer;
                break;
            }
        }

        Transform hips = animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Hips)
            : visual.transform;
        if (hips == null) hips = visual.transform;

        Bounds gearBounds = odmRenderer != null ? odmRenderer.bounds : CalculateBounds(visual);
        Vector3 sideOffset = visual.transform.right * Mathf.Max(0.12f, gearBounds.extents.x * 0.9f);
        Vector3 rearOffset = -visual.transform.forward * gearBounds.extents.z * 0.65f;
        Vector3 center = gearBounds.center + rearOffset;

        odm.hipOriginLeft = CreateSocket("CableExit_Left", hips, center - sideOffset);
        odm.hipOriginRight = CreateSocket("CableExit_Right", hips, center + sideOffset);
    }

    static Transform CreateSocket(string name, Transform parent, Vector3 worldPosition)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing.gameObject);

        GameObject socket = new GameObject(name);
        socket.transform.SetParent(parent, true);
        socket.transform.position = worldPosition;
        socket.transform.rotation = parent.rotation;
        return socket.transform;
    }

    static void DisablePlaceholderRenderer(Transform origin)
    {
        if (origin == null) return;
        Renderer renderer = origin.GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = false;
    }

    static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void ConfigureTextureImport(string path, bool normalMap)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        TextureImporterType desired = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType == desired) return;
        importer.textureType = desired;
        importer.SaveAndReimport();
    }

    static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = $"{current}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }

    static string SanitizeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return name;
    }
}
#endif
