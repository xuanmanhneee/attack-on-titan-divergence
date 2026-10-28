using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Chapter0SceneBuilder
{
    const string ScenePath = "Assets/Scenes/Chapter0_TrainingCamp.unity";
    const string RootPath = "Assets/Chapter0";
    const int Hookable = 3;

    static Material grassMat, timberMat, stoneMat, canvasMat, greenMat, orangeMat, roofMat;

    [MenuItem("Attack on Titan/Build Chapter 0 Training Camp")]
    public static void BuildFromMenu() => Build();

    public static void Build()
    {
        EnsureFolders();
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == "Terrain" || go.name.StartsWith("Cube"))
                Object.DestroyImmediate(go);
        }

        CreateMaterials();
        Terrain terrain = CreateTerrain();
        Transform environment = new GameObject("Chapter0_Environment").transform;
        BuildCamp(environment, terrain);

        ODMController player = Object.FindFirstObjectByType<ODMController>();
        if (player == null) throw new System.InvalidOperationException("SampleScene has no ODMController player.");
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, -215f), Quaternion.identity);
        if (controller != null) controller.enabled = true;
        player.groundLayer = ~0;
        player.hookableLayer = 1 << Hookable;
        player.maxHookRange = 90f;

        GameObject chapter = new GameObject("Chapter0_Training");
        Chapter0Training training = chapter.AddComponent<Chapter0Training>();
        training.player = player;
        training.checkpoints = CreateCheckpoints(chapter.transform);

        EditorSceneManager.SaveScene(scene, ScenePath, false);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = chapter;
        Debug.Log("[Chapter 0] Built " + ScenePath);
    }

    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(RootPath)) AssetDatabase.CreateFolder("Assets", "Chapter0");
        if (!AssetDatabase.IsValidFolder(RootPath + "/Terrain")) AssetDatabase.CreateFolder(RootPath, "Terrain");
        if (!AssetDatabase.IsValidFolder(RootPath + "/Materials")) AssetDatabase.CreateFolder(RootPath, "Materials");
        if (!AssetDatabase.IsValidFolder(RootPath + "/Textures")) AssetDatabase.CreateFolder(RootPath, "Textures");
    }

    static Material MakeMaterial(string name, Color color)
    {
        string path = RootPath + "/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void CreateMaterials()
    {
        grassMat = MakeMaterial("Grass", new Color(0.28f, 0.42f, 0.19f));
        timberMat = MakeMaterial("Timber", new Color(0.34f, 0.19f, 0.09f));
        stoneMat = MakeMaterial("Stone", new Color(0.46f, 0.45f, 0.42f));
        canvasMat = MakeMaterial("Canvas", new Color(0.72f, 0.66f, 0.51f));
        greenMat = MakeMaterial("CheckpointGreen", new Color(0.12f, 0.9f, 0.48f));
        orangeMat = MakeMaterial("TrainingOrange", new Color(0.95f, 0.42f, 0.08f));
        roofMat = MakeMaterial("Roof", new Color(0.30f, 0.12f, 0.08f));
    }

    static Texture2D MakeTerrainTexture(string name, Color a, Color b)
    {
        string path = RootPath + "/Textures/" + name + ".asset";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;
        tex = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat };
        Color[] pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.17f, y * 0.17f);
                pixels[y * 64 + x] = Color.Lerp(a, b, noise);
            }
        tex.SetPixels(pixels);
        tex.Apply(true);
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }

    static TerrainLayer MakeLayer(string name, Texture2D texture, float tile)
    {
        string path = RootPath + "/Terrain/" + name + ".terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer { name = name };
            AssetDatabase.CreateAsset(layer, path);
        }
        layer.diffuseTexture = texture;
        layer.tileSize = new Vector2(tile, tile);
        EditorUtility.SetDirty(layer);
        return layer;
    }

    static Terrain CreateTerrain()
    {
        string dataPath = RootPath + "/Terrain/Chapter0Terrain.asset";
        TerrainData old = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
        if (old != null) AssetDatabase.DeleteAsset(dataPath);

        TerrainData data = new TerrainData
        {
            heightmapResolution = 257,
            alphamapResolution = 256,
            baseMapResolution = 256,
            size = new Vector3(500f, 24f, 500f)
        };
        AssetDatabase.CreateAsset(data, dataPath);

        float[,] heights = new float[257, 257];
        for (int z = 0; z < 257; z++)
            for (int x = 0; x < 257; x++)
            {
                float nx = x / 256f;
                float nz = z / 256f;
                float edge = Mathf.Clamp01((Mathf.Abs(nx - 0.5f) + Mathf.Abs(nz - 0.5f) - 0.55f) * 1.5f);
                heights[z, x] = 0.045f + Mathf.PerlinNoise(nx * 4f, nz * 4f) * 0.018f + edge * 0.05f;
            }
        data.SetHeights(0, 0, heights);

        TerrainLayer grass = MakeLayer("Grass", MakeTerrainTexture("GrassTexture", new Color(0.20f, 0.31f, 0.12f), new Color(0.38f, 0.48f, 0.20f)), 12f);
        TerrainLayer dirt = MakeLayer("Dirt", MakeTerrainTexture("DirtTexture", new Color(0.30f, 0.20f, 0.11f), new Color(0.52f, 0.37f, 0.20f)), 9f);
        TerrainLayer rock = MakeLayer("Rock", MakeTerrainTexture("RockTexture", new Color(0.30f, 0.30f, 0.29f), new Color(0.52f, 0.51f, 0.48f)), 10f);
        data.terrainLayers = new[] { grass, dirt, rock };

        float[,,] splat = new float[256, 256, 3];
        for (int z = 0; z < 256; z++)
            for (int x = 0; x < 256; x++)
            {
                float wx = x / 255f * 500f - 250f;
                float wz = z / 255f * 500f - 250f;
                float road = 1f - Mathf.SmoothStep(7f, 18f, Mathf.Abs(wx));
                float yards = Mathf.Max(
                    1f - Mathf.SmoothStep(26f, 42f, Vector2.Distance(new Vector2(wx, wz), new Vector2(0f, 15f))),
                    1f - Mathf.SmoothStep(22f, 36f, Vector2.Distance(new Vector2(wx, wz), new Vector2(0f, 188f))));
                float dirtWeight = Mathf.Clamp01(Mathf.Max(road, yards) * 0.88f);
                float rockWeight = Mathf.Clamp01((Mathf.Abs(wx) - 185f) / 65f) * 0.65f;
                splat[z, x, 0] = Mathf.Max(0.02f, 1f - dirtWeight - rockWeight);
                splat[z, x, 1] = dirtWeight;
                splat[z, x, 2] = rockWeight;
                float sum = splat[z, x, 0] + splat[z, x, 1] + splat[z, x, 2];
                for (int i = 0; i < 3; i++) splat[z, x, i] /= sum;
            }
        data.SetAlphamaps(0, 0, splat);

        GameObject go = Terrain.CreateTerrainGameObject(data);
        go.name = "Chapter0_Terrain";
        go.transform.position = new Vector3(-250f, -2f, -250f);
        Terrain terrain = go.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 8f;
        return terrain;
    }

    static float Ground(Terrain terrain, float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;

    static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material mat, int layer = 0)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.layer = layer;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = mat;
        return go;
    }

    static void BuildCamp(Transform root, Terrain terrain)
    {
        Transform structures = new GameObject("Camp_Structures").transform;
        structures.SetParent(root);
        Transform course = new GameObject("Movement_Course").transform;
        course.SetParent(root);
        Transform odm = new GameObject("ODM_Training_Yard").transform;
        odm.SetParent(root);
        Transform dressing = new GameObject("Environment_Dressing").transform;
        dressing.SetParent(root);

        // Timber perimeter, with an open approach at the south end.
        for (int i = -10; i <= 10; i++)
        {
            float x = i * 11f;
            Post(dressing, terrain, x, -238f, 7f);
            Post(dressing, terrain, x, 232f, 7f);
        }
        for (int i = -20; i <= 20; i++)
        {
            float z = i * 11f;
            Post(dressing, terrain, -118f, z, 7f);
            Post(dressing, terrain, 118f, z, 7f);
        }

        // Barracks frame the route without blocking the player's sightline.
        for (int side = -1; side <= 1; side += 2)
        {
            Barracks(structures, terrain, side * 70f, -170f, 36f, 18f);
            Barracks(structures, terrain, side * 70f, -105f, 42f, 20f);
            Barracks(structures, terrain, side * 78f, 155f, 38f, 18f);
        }

        // Start gate and slalom: teaches precise ground movement before ODM.
        Gate(course, terrain, new Vector3(0f, 0f, -170f), 14f, 7f, greenMat);
        for (int i = 0; i < 8; i++)
        {
            float z = -148f + i * 10f;
            float x = (i % 2 == 0 ? -1f : 1f) * 5.5f;
            float y = Ground(terrain, x, z);
            Primitive("Slalom_Post_" + i, PrimitiveType.Cylinder, course, new Vector3(x, y + 2.3f, z), new Vector3(0.55f, 2.3f, 0.55f), orangeMat);
        }
        Gate(course, terrain, new Vector3(0f, 0f, -60f), 12f, 6f, orangeMat);

        // ODM towers are all Hookable and placed within the 90 m hook range.
        Tower(odm, terrain, -22f, -5f, 26f);
        Tower(odm, terrain, 24f, 25f, 32f);
        Tower(odm, terrain, -26f, 58f, 38f);
        Tower(odm, terrain, 27f, 92f, 42f);
        Tower(odm, terrain, -22f, 128f, 34f);
        HighGate(odm, terrain, 0f, 128f, 22f);

        // Landing ring and finish pavilion.
        float finishY = Ground(terrain, 0f, 188f);
        Primitive("Finish_Ring", PrimitiveType.Cylinder, course, new Vector3(0f, finishY + 0.12f, 188f), new Vector3(9f, 0.12f, 9f), greenMat);
        Gate(course, terrain, new Vector3(0f, 0f, 208f), 18f, 9f, greenMat);

        // Sparse tree line creates depth and supplies emergency hook targets.
        for (int i = 0; i < 36; i++)
        {
            float angle = i * 137.5f * Mathf.Deg2Rad;
            float radius = 135f + (i % 5) * 18f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            if (Mathf.Abs(x) < 45f && z > -225f && z < 220f) x += Mathf.Sign(x == 0f ? 1f : x) * 65f;
            Tree(dressing, terrain, x, z, 9f + (i % 4) * 1.5f);
        }
    }

    static void Post(Transform parent, Terrain terrain, float x, float z, float height)
    {
        float y = Ground(terrain, x, z);
        Primitive("Palisade", PrimitiveType.Cylinder, parent, new Vector3(x, y + height * 0.5f, z), new Vector3(1.1f, height * 0.5f, 1.1f), timberMat, Hookable);
    }

    static void Barracks(Transform parent, Terrain terrain, float x, float z, float width, float depth)
    {
        float y = Ground(terrain, x, z);
        Primitive("Barracks", PrimitiveType.Cube, parent, new Vector3(x, y + 5f, z), new Vector3(width, 10f, depth), canvasMat, Hookable);
        GameObject roof = Primitive("Barracks_Roof", PrimitiveType.Cube, parent, new Vector3(x, y + 11f, z), new Vector3(width + 2f, 3f, depth + 2f), roofMat, Hookable);
        roof.transform.rotation = Quaternion.Euler(0f, 0f, 8f);
    }

    static void Gate(Transform parent, Terrain terrain, Vector3 center, float width, float height, Material accent)
    {
        float y = Ground(terrain, center.x, center.z);
        Primitive("Gate_Post", PrimitiveType.Cube, parent, new Vector3(center.x - width * 0.5f, y + height * 0.5f, center.z), new Vector3(1.2f, height, 1.2f), timberMat, Hookable);
        Primitive("Gate_Post", PrimitiveType.Cube, parent, new Vector3(center.x + width * 0.5f, y + height * 0.5f, center.z), new Vector3(1.2f, height, 1.2f), timberMat, Hookable);
        Primitive("Gate_Header", PrimitiveType.Cube, parent, new Vector3(center.x, y + height, center.z), new Vector3(width + 1.2f, 1.2f, 1.2f), accent, Hookable);
    }

    static void Tower(Transform parent, Terrain terrain, float x, float z, float height)
    {
        float y = Ground(terrain, x, z);
        Primitive("ODM_Tower", PrimitiveType.Cube, parent, new Vector3(x, y + height * 0.5f, z), new Vector3(5f, height, 5f), stoneMat, Hookable);
        Primitive("ODM_Platform", PrimitiveType.Cube, parent, new Vector3(x, y + height, z), new Vector3(9f, 1f, 9f), timberMat, Hookable);
        for (int side = -1; side <= 1; side += 2)
            Primitive("Tower_Brace", PrimitiveType.Cube, parent, new Vector3(x + side * 4f, y + height * 0.62f, z), new Vector3(0.7f, height * 0.45f, 0.7f), timberMat, Hookable)
                .transform.rotation = Quaternion.Euler(0f, 0f, side * 24f);
    }

    static void HighGate(Transform parent, Terrain terrain, float x, float z, float height)
    {
        float y = Ground(terrain, x, z);
        Primitive("Aerial_Gate_Left", PrimitiveType.Cube, parent, new Vector3(x - 11f, y + height * 0.5f, z), new Vector3(2f, height, 2f), timberMat, Hookable);
        Primitive("Aerial_Gate_Right", PrimitiveType.Cube, parent, new Vector3(x + 11f, y + height * 0.5f, z), new Vector3(2f, height, 2f), timberMat, Hookable);
        Primitive("Aerial_Gate_Header", PrimitiveType.Cube, parent, new Vector3(x, y + height, z), new Vector3(24f, 2f, 2f), orangeMat, Hookable);
    }

    static void Tree(Transform parent, Terrain terrain, float x, float z, float height)
    {
        float y = Ground(terrain, x, z);
        Primitive("Training_Tree_Trunk", PrimitiveType.Cylinder, parent, new Vector3(x, y + height * 0.5f, z), new Vector3(1.2f, height * 0.5f, 1.2f), timberMat, Hookable);
        Primitive("Training_Tree_Crown", PrimitiveType.Sphere, parent, new Vector3(x, y + height + 3f, z), new Vector3(6f, 5f, 6f), grassMat, Hookable);
    }

    static Transform[] CreateCheckpoints(Transform parent)
    {
        Vector3[] positions =
        {
            new Vector3(0f, 2.5f, -60f),
            new Vector3(0f, 2.5f, -20f),
            new Vector3(0f, 8f, 58f),
            new Vector3(0f, 16f, 132f),
            new Vector3(0f, 2.5f, 188f)
        };
        var result = new Transform[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject go = new GameObject("Checkpoint_" + (i + 1));
            go.transform.SetParent(parent);
            go.transform.position = positions[i];
            result[i] = go.transform;
        }
        return result;
    }

    static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.path != ScenePath) scenes.Add(scene);
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
