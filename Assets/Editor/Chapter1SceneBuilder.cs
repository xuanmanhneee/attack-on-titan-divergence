using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-shot authoring utility: it instantiates imported environment prefabs into
// a persistent scene.  Nothing in this class runs in a player build.
public static class Chapter1SceneBuilder
{
    const string ScenePath = "Assets/Scenes/Chapter1_BattleOfTrost.unity";
    const string RootPath = "Assets/Chapter1";
    const string PackageRoot = "Assets/EmaceArt/Slavic World Free";
    const int Hookable = 3;
    static readonly Vector3 Chapter1PlayerStart = new Vector3(-180.241f, 19.708f, -25.488f);

    static readonly string[] HousePaths =
    {
        PackageRoot + "/Prefabs/Town/Building/EA03_Town_House_Comp_01a_PRE.prefab",
        PackageRoot + "/Prefabs/Town/Building/EA03_Town_House_Comp_02a_PRE.prefab",
        PackageRoot + "/Prefabs/Town/Building/EA03_Town_House_Comp_03a_PRE.prefab",
        PackageRoot + "/Prefabs/Town/Building/EA03_Town_House_Comp_03b_PRE.prefab",
        PackageRoot + "/Prefabs/Town/Building/EA03_Town_House_Comp_03c_PRE.prefab",
        PackageRoot + "/Prefabs/Town/Administrative/EA03_Town_Building_Administrative _01a_PRE.prefab"
    };

    [MenuItem("Attack on Titan/Build Chapter 1 Battle of Trost")]
    public static void BuildFromMenu() => Build();

    public static void BuildBatch()
    {
        Build();
        EditorApplication.Exit(0);
    }

    static void Build()
    {
        if (!AssetDatabase.IsValidFolder(PackageRoot))
            throw new InvalidOperationException("Slavic World Free has not been imported yet.");

        EnsureFolder(RootPath);
        EnsureFolder(RootPath + "/Terrain");
        EnsureFolder(RootPath + "/Materials");
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);

        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            if (sceneRoot.name == "Terrain" || sceneRoot.name.StartsWith("Cube") || sceneRoot.name == "Chapter1_Trost")
                UnityEngine.Object.DestroyImmediate(sceneRoot);

        Material stone = MaterialAsset("TrostWall", new Color(0.47f, 0.43f, 0.37f));
        Material road = MaterialAsset("TrostRoad", new Color(0.25f, 0.22f, 0.18f));
        Material ground = MaterialAsset("TrostGround", new Color(0.31f, 0.36f, 0.18f));
        Terrain terrain = CreateTerrain();
        Transform root = new GameObject("Chapter1_Trost").transform;
        Transform city = new GameObject("Baked_City").transform;
        city.SetParent(root);
        Transform markers = new GameObject("Mission_Markers").transform;
        markers.SetParent(root);

        // A baked dirt-and-grass plaza keeps the traversable city readable;
        // the imported terrain remains visible around it as the outer hills.
        // Keep a collision safety floor under the playable district.  Only the
        // decorative road slabs below are non-colliding.
        Box(city, "Trost_Ground", new Vector3(0f, -1.97f, 70f), new Vector3(500f, .18f, 550f), ground, 0);
        BuildRoads(city, road);
        BuildHouses(city, terrain);
        BuildWalls(city, terrain, stone);
        BuildDressing(city, terrain);

        ODMController player = UnityEngine.Object.FindAnyObjectByType<ODMController>();
        if (player == null) throw new InvalidOperationException("SampleScene has no ODMController player.");
        // Deliberately authored rooftop start. Do not infer this from the
        // tallest prefab: rebuilding the city must preserve the chosen spawn.
        MovePlayer(player, Chapter1PlayerStart);
        player.groundLayer = ~0;
        player.hookableLayer = 1 << Hookable;
        player.maxHookRange = 105f;

        TrostMapAuthoring map = root.gameObject.AddComponent<TrostMapAuthoring>();
        map.buildingLayer = Hookable;
        map.groundY = terrain.transform.position.y;
        map.floorRect = new Rect(-250f, -220f, 500f, 620f);
        map.playerStart = Marker(markers, "Player_Start", player.transform.position);
        map.gate = Marker(markers, "Breached_Gate", new Vector3(0f, Ground(terrain, 0f, 330f), 330f));
        map.sealPoint = Marker(markers, "Seal_Point", new Vector3(0f, Ground(terrain, 0f, 303f), 303f));
        map.depots = new[]
        {
            Marker(markers, "Gas_Depot_West", new Vector3(-150f, Ground(terrain, -150f, 10f), 10f)),
            Marker(markers, "Gas_Depot_East", new Vector3(150f, Ground(terrain, 150f, 120f), 120f)),
            Marker(markers, "Gas_Depot_North", new Vector3(-95f, Ground(terrain, -95f, 260f), 260f))
        };
        map.intersections = new[]
        {
            Marker(markers, "Intersection_South", new Vector3(0f, Ground(terrain, 0f, -70f), -70f)),
            Marker(markers, "Intersection_Centre", new Vector3(0f, Ground(terrain, 0f, 75f), 75f)),
            Marker(markers, "Intersection_North", new Vector3(0f, Ground(terrain, 0f, 220f), 220f))
        };

        EditorSceneManager.SaveScene(scene, ScenePath, false);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = root.gameObject;
        Debug.Log("[Chapter 1] Baked Trost scene at " + ScenePath);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static Terrain CreateTerrain()
    {
        const string target = RootPath + "/Terrain/Chapter1Terrain.asset";
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(target);
        if (terrainData == null)
        {
            string[] ids = AssetDatabase.FindAssets("t:TerrainData", new[] { PackageRoot + "/Terrain" });
            if (ids.Length == 0) throw new InvalidOperationException("No TerrainData found in Slavic World Free.");
            TerrainData source = AssetDatabase.LoadAssetAtPath<TerrainData>(AssetDatabase.GUIDToAssetPath(ids[0]));
            terrainData = UnityEngine.Object.Instantiate(source);
            AssetDatabase.CreateAsset(terrainData, target);
        }
        GameObject go = Terrain.CreateTerrainGameObject(terrainData);
        go.name = "Chapter1_Terrain";
        Vector3 size = terrainData.size;
        go.transform.position = new Vector3(-size.x * 0.5f, -2f, -size.z * 0.25f);
        Terrain terrain = go.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.heightmapPixelError = 8f;
        return terrain;
    }

    static void BuildRoads(Transform parent, Material material)
    {
        for (int z = -165; z <= 315; z += 60) Box(parent, "Cross_Road", new Vector3(0f, -1.86f, z), new Vector3(500f, .25f, 14f), material, 0, false);
        for (int x = -210; x <= 210; x += 60) Box(parent, "Main_Road", new Vector3(x, -1.86f, 70f), new Vector3(14f, .25f, 550f), material, 0, false);
    }

    static void BuildHouses(Transform parent, Terrain terrain)
    {
        var prefabs = new List<GameObject>();
        foreach (string path in HousePaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) prefabs.Add(prefab);
        }
        if (prefabs.Count == 0) throw new InvalidOperationException("Town house prefabs were not found in imported package.");

        int index = 0;
        // 60 m blocks give the player a fresh hook target before the previous
        // swing runs out, while the 14 m roads leave clear recovery corridors.
        for (int x = -210; x <= 210; x += 55)
            for (int z = -145; z <= 275; z += 55)
            {
                // Four lanes of larger town buildings, with broad roads for ODM traversal.
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 position = new Vector3(x + side * 20f, Ground(terrain, x + side * 20f, z), z + ((index % 2 == 0) ? 9f : -9f));
                    GameObject instance = PrefabUtility.InstantiatePrefab(prefabs[index % prefabs.Count], parent) as GameObject;
                    instance.name = "Trost_House_" + index.ToString("D2");
                    float groundY = position.y;
                    instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f));
                    // The source assets use aggressive LOD culling.  Trost
                    // needs readable skyline landmarks, so preserve the full
                    // house silhouette at distance.
                    KeepVisibleAtDistance(instance);
                    instance.transform.localScale = Vector3.one * (1.45f + (index % 3) * .10f);
                    SnapBottomToGround(instance, groundY);
                    MakeHookable(instance);
                    TrostMapAuthoring.EnsureSolidHouseBody(instance.transform, Hookable);
                    index++;
                }
            }
    }

    static void BuildWalls(Transform parent, Terrain terrain, Material material)
    {
        const float z = 350f;
        const float height = 50f;
        const float gateWidth = 42f;
        Box(parent, "Wall_West", new Vector3(-(250f + gateWidth) * .5f, Ground(terrain, -136f, z) + height * .5f, z), new Vector3(250f - gateWidth * .5f, height, 18f), material, Hookable);
        Box(parent, "Wall_East", new Vector3((250f + gateWidth) * .5f, Ground(terrain, 136f, z) + height * .5f, z), new Vector3(250f - gateWidth * .5f, height, 18f), material, Hookable);
        Box(parent, "Wall_South", new Vector3(0f, Ground(terrain, 0f, -220f) + height * .5f, -220f), new Vector3(520f, height, 18f), material, Hookable);
        Box(parent, "Wall_Left", new Vector3(-260f, Ground(terrain, -260f, 65f) + height * .5f, 65f), new Vector3(18f, height, 590f), material, Hookable);
        Box(parent, "Wall_Right", new Vector3(260f, Ground(terrain, 260f, 65f) + height * .5f, 65f), new Vector3(18f, height, 590f), material, Hookable);
        for (int i = -3; i <= 3; i++)
            Box(parent, "Gate_Rubble", new Vector3(i * 6f, Ground(terrain, i * 6f, 337f) + 2f, 337f - Mathf.Abs(i) * 2f), new Vector3(7f, 4f + Mathf.Abs(i), 6f), material, Hookable);
    }

    static void BuildDressing(Transform parent, Terrain terrain)
    {
        string[] ids = AssetDatabase.FindAssets("t:Prefab EA03_Nature_Tree", new[] { PackageRoot + "/Prefabs/Nature" });
        if (ids.Length == 0) return;
        GameObject tree = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(ids[0]));
        for (int i = 0; i < 34; i++)
        {
            float angle = i / 34f * Mathf.PI * 2f;
            float radius = 285f + (i % 4) * 12f;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius + 65f;
            GameObject instance = PrefabUtility.InstantiatePrefab(tree, parent) as GameObject;
            instance.name = "Outer_Tree_" + i;
            instance.transform.SetPositionAndRotation(new Vector3(x, Ground(terrain, x, z), z), Quaternion.Euler(0f, i * 37f, 0f));
            instance.transform.localScale = Vector3.one * (1.1f + (i % 3) * .2f);
        }
    }

    static Vector3 HighestHouseRooftop(Transform city, Vector3 fallback)
    {
        Renderer[] renderers = city.GetComponentsInChildren<Renderer>(true);
        Bounds highestHouse = new Bounds();
        float highestTop = float.NegativeInfinity;
        bool found = false;
        var houseBounds = new Dictionary<Transform, Bounds>();
        foreach (Renderer renderer in renderers)
        {
            Transform house = null;
            foreach (Transform ancestor in renderer.transform.GetComponentsInParent<Transform>(true))
                if (ancestor.name.StartsWith("Trost_House_")) { house = ancestor; break; }
            if (house == null) continue;
            if (!houseBounds.ContainsKey(house)) houseBounds[house] = renderer.bounds;
            else { Bounds combined = houseBounds[house]; combined.Encapsulate(renderer.bounds); houseBounds[house] = combined; }
        }
        foreach (KeyValuePair<Transform, Bounds> pair in houseBounds)
        {
            if (pair.Value.max.y <= highestTop) continue;
            highestTop = pair.Value.max.y;
            highestHouse = pair.Value;
            found = true;
        }
        if (!found) return fallback;
        // Stand just above the tallest roof, centered over the building so the
        // player can immediately choose a hook target in either direction.
        return new Vector3(highestHouse.center.x, highestHouse.max.y + 1.35f, highestHouse.center.z);
    }

    static void MakeHookable(GameObject root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = Hookable;

        // Remove imported/detail colliders. Some source houses contain dozens
        // of overlapping colliders for windows, beams and all three LODs.
        foreach (Collider oldCollider in root.GetComponentsInChildren<Collider>(true))
            UnityEngine.Object.DestroyImmediate(oldCollider);

        var structuralMeshes = new List<MeshFilter>();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            string hierarchyName = filter.name;
            foreach (Transform ancestor in filter.GetComponentsInParent<Transform>(true))
                hierarchyName += " " + ancestor.name;
            string lower = hierarchyName.ToLowerInvariant();
            if (lower.Contains("lod1") || lower.Contains("lod2")
                || lower.Contains("window") || lower.Contains("shutter")
                || lower.Contains("bolt") || lower.Contains("prop"))
                continue;
            structuralMeshes.Add(filter);
        }

        structuralMeshes.Sort((a, b) => RendererVolume(b).CompareTo(RendererVolume(a)));
        float largestVolume = structuralMeshes.Count > 0 ? RendererVolume(structuralMeshes[0]) : 0f;
        int meshColliderCount = 0;
        for (int i = 0; i < structuralMeshes.Count && meshColliderCount < 4; i++)
        {
            MeshFilter filter = structuralMeshes[i];
            float volume = RendererVolume(filter);
            if (i > 0 && volume < largestVolume * 0.08f) break;
            MeshCollider meshCollider = filter.gameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = filter.sharedMesh;
            meshCollider.convex = false;
            meshColliderCount++;
        }

        if (meshColliderCount > 0) return;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        BoxCollider fallbackCollider = root.AddComponent<BoxCollider>();
        fallbackCollider.center = root.transform.InverseTransformPoint(bounds.center);
        fallbackCollider.size = root.transform.InverseTransformVector(bounds.size);
    }

    static float RendererVolume(MeshFilter filter)
    {
        Renderer renderer = filter != null ? filter.GetComponent<Renderer>() : null;
        if (renderer == null) return 0f;
        Vector3 size = renderer.bounds.size;
        return Mathf.Max(0.0001f, size.x * size.y * size.z);
    }

    static void KeepVisibleAtDistance(GameObject root)
    {
        foreach (LODGroup group in root.GetComponentsInChildren<LODGroup>(true))
        {
            Renderer[] renderers = HighestDetailRenderers(group);
            if (renderers.Length == 0) continue;
            // Keep only the authored highest-detail renderers. Putting LOD0,
            // LOD1 and LOD2 into one LOD renders duplicate geometry (notably
            // floating stair fragments) at the same time.
            group.SetLODs(new[] { new LOD(0.001f, renderers) });
            group.RecalculateBounds();
        }
    }

    static Renderer[] HighestDetailRenderers(LODGroup group)
    {
        var renderers = new List<Renderer>();
        foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>(true))
        {
            // Prefabs can contain nested LODGroups. A renderer owned by a
            // child LODGroup must not also be assigned to this parent group,
            // otherwise Unity reports that it is registered with multiple
            // LODGroups and LOD selection becomes undefined.
            LODGroup[] ownerGroups = renderer.GetComponentsInParent<LODGroup>(true);
            if (ownerGroups.Length == 0 || ownerGroups[0] != group) continue;

            string name = renderer.name.ToLowerInvariant();
            if (name.Contains("lod1") || name.Contains("lod2") || name.Contains("lod3")) continue;
            renderers.Add(renderer);
        }

        return renderers.ToArray();
    }

    static void SnapBottomToGround(GameObject root, float groundY)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        root.transform.position += Vector3.up * (groundY - bounds.min.y);
    }

    static Transform Marker(Transform parent, string name, Vector3 position)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent);
        marker.transform.position = position;
        return marker.transform;
    }

    static float Ground(Terrain terrain, float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;

    static Material MaterialAsset(string name, Color color)
    {
        string path = RootPath + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        return material;
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, int layer, bool withCollider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = size;
        go.layer = layer;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!withCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void MovePlayer(ODMController player, Vector3 position)
    {
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.SetPositionAndRotation(position, Quaternion.identity);
        if (controller != null) controller.enabled = true;
    }

    static void SetBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            if (scene.path != "Assets/Scenes/SampleScene.unity" && scene.path != ScenePath) scenes.Add(scene);
        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
