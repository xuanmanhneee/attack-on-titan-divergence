using System.IO;
using UnityEditor;
using UnityEngine;

public static class TitanModelSetup
{
    const string SourceFolder = "Assets/Models/Titans/Meshy_AI_Bulging_Eyed_Goblin_1021155027_texture_fbx";
    const string FbxPath = SourceFolder + "/Meshy_AI_Bulging_Eyed_Goblin_1021155027_texture.fbx";
    const string AlbedoPath = SourceFolder + "/Meshy_AI_Bulging_Eyed_Goblin_1021155027_texture.png";
    const string NormalPath = SourceFolder + "/Meshy_AI_Bulging_Eyed_Goblin_1021155027_texture_normal.png";
    const string ResourceFolder = "Assets/Resources/Titans";
    const string MaterialPath = ResourceFolder + "/MeshyGluttonousTitan.mat";
    const string PrefabPath = ResourceFolder + "/MeshyGluttonousTitan.prefab";
    const string PreviewFolder = "Temp/TitanPreview";

    [InitializeOnLoadMethod]
    static void AutoBuildWhenReady()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) return;
        EditorApplication.delayCall += TryAutoBuild;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall += TryAutoBuild;
    }

    static void TryAutoBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) Build();
    }

    [MenuItem("Attack on Titan/Build Meshy Gluttonous Titan")]
    public static void Build()
    {
        Directory.CreateDirectory(ResourceFolder);
        AssetDatabase.Refresh();

        ConfigureTexture(AlbedoPath, false);
        ConfigureTexture(NormalPath, true);
        ConfigureModel();

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (source == null)
            throw new FileNotFoundException("Could not import Meshy Titan FBX", FbxPath);

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader) { name = "MeshyGluttonousTitan" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", albedo);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.7f);
            material.EnableKeyword("_NORMALMAP");
        }
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
        EditorUtility.SetDirty(material);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = "MeshyGluttonousTitan";
        int triangles = 0;
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;

            if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
                triangles += skinned.sharedMesh.triangles.Length / 3;
            else if (renderer is MeshRenderer)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                    triangles += filter.sharedMesh.triangles.Length / 3;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        Object.DestroyImmediate(instance);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Directory.CreateDirectory(PreviewFolder);
        RenderPreview(prefab, PreviewFolder + "/front.png", 0f);
        RenderPreview(prefab, PreviewFolder + "/right.png", 90f);
        RenderPreview(prefab, PreviewFolder + "/back.png", 180f);

        Debug.Log($"[TitanModelSetup] Built {PrefabPath}; triangles={triangles}; animator={source.GetComponentInChildren<Animator>() != null}");
    }

    static void ConfigureTexture(string path, bool normal)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = !normal;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    static void ConfigureModel()
    {
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer == null) return;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.isReadable = false;
        importer.SaveAndReimport();
    }

    static Bounds RendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void RenderPreview(GameObject prefab, string path, float angle)
    {
        var model = Object.Instantiate(prefab);
        var initial = RendererBounds(model);
        float scale = 8f / Mathf.Max(0.001f, initial.size.y);
        model.transform.localScale = Vector3.one * scale;
        var bounds = RendererBounds(model);
        model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        bounds = RendererBounds(model);

        var cameraObject = new GameObject("PreviewCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.14f, 0.17f);
        camera.fieldOfView = 32f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 1000f;
        Vector3 target = bounds.center;
        float radius = Mathf.Max(bounds.extents.magnitude, 1f);
        float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, distance);
        camera.transform.position = target + offset;
        camera.transform.LookAt(target);

        Debug.Log($"[TitanModelSetup] preview {angle:F0}: initial={initial.size}, scaled={bounds.size}, distance={distance:F2}");

        var lightObject = new GameObject("PreviewLight");
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2.2f;
        light.transform.rotation = Quaternion.Euler(35f, -35f + angle, 0f);
        var fillObject = new GameObject("PreviewFill");
        var fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.8f;
        fill.transform.rotation = Quaternion.Euler(25f, 145f + angle, 0f);

        var rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());

        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(fillObject);
        Object.DestroyImmediate(lightObject);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(model);
    }
}
