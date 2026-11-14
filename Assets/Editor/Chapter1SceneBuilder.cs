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

    // Single source of truth for the city grid. Roads, house blocks, walls,
    // floor and mission markers are all derived from these values so they
    // cannot drift apart.  Roads cross at x = 0 so the central avenue lines up
    // with the breached gate; house blocks sit in the 46 m cells between roads.
    const float RoadWidth = 14f;
    const float BlockPitch = 60f;
    const float WallInnerX = 251f;      // inner face of Wall_Left / Wall_Right
    const float WallSouthZ = -220f;
    const float WallNorthZ = 350f;
    const float WallThickness = 18f;
    const float WallHeight = 50f;
    const float GateWidth = 42f;
    const float FloorTopY = -1.88f;
    // Deliberately authored rooftop start: the player spawns on top of this
    // house.  Rebuilding the city keeps the spawn on the same house.
    const string SpawnHouse = "Trost_House_20";

    static readonly float[] RoadXs = Steps(-180f, 180f, BlockPitch);
    static readonly float[] RoadZs = Steps(-165f, 315f, BlockPitch);
    static readonly float[] BlockXs = Centres(RoadXs, true);    // -210 .. 210 (8 cells)
    static readonly float[] BlockZs = Centres(RoadZs, false);   // -135 .. 285 (8 cells)

    static float[] Steps(float from, float to, float step)
    {
        var list = new List<float>();
        for (float v = from; v <= to + 0.01f; v += step) list.Add(v);
        return list.ToArray();
    }

    // Cell centres between consecutive roads, plus one cell beyond each end
    // when includeOuter is set (the outermost cells next to the side walls).
    static float[] Centres(float[] roads, bool includeOuter)
    {
        var list = new List<float>();
        if (includeOuter) list.Add(roads[0] - BlockPitch * 0.5f);
        for (int i = 0; i + 1 < roads.Length; i++) list.Add((roads[i] + roads[i + 1]) * 0.5f);
        if (includeOuter) list.Add(roads[roads.Length - 1] + BlockPitch * 0.5f);
        return list.ToArray();
    }

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

        Terrain terrain = CreateTerrain();
        Transform root = new GameObject("Chapter1_Trost").transform;
        Transform city = new GameObject("Baked_City").transform;
        city.SetParent(root);
        Transform markers = new GameObject("Mission_Markers").transform;
        markers.SetParent(root);
        PopulateCity(city, terrain);

        ODMController player = UnityEngine.Object.FindAnyObjectByType<ODMController>();
        if (player == null) throw new InvalidOperationException("SampleScene has no ODMController player.");
        ConfigurePlayer(player);
        PlaceOnSpawnRooftop(player, city);

        TrostMapAuthoring map = root.gameObject.AddComponent<TrostMapAuthoring>();
        ConfigureMap(map, markers, terrain, player.transform.position);

        EditorSceneManager.SaveScene(scene, ScenePath, false);
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = root.gameObject;
        Debug.Log("[Chapter 1] Baked Trost scene at " + ScenePath);
    }

    // Re-lays the city on the unified grid inside the EXISTING Chapter 1
    // scene.  Unlike Build() it does not start from SampleScene, so the player,
    // HUD, lighting and any hand edits outside Baked_City are left untouched.
    [MenuItem("Attack on Titan/Realign Chapter 1 City (keep scene)")]
    public static void RealignFromMenu() => Realign();

    public static void RealignBatch()
    {
        Realign();
        EditorApplication.Exit(0);
    }

    static void Realign()
    {
        if (!AssetDatabase.IsValidFolder(PackageRoot))
            throw new InvalidOperationException("Slavic World Free has not been imported yet.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject rootObject = GameObject.Find("Chapter1_Trost");
        Terrain terrain = UnityEngine.Object.FindAnyObjectByType<Terrain>();
        ODMController player = UnityEngine.Object.FindAnyObjectByType<ODMController>();
        if (rootObject == null || terrain == null || player == null)
            throw new InvalidOperationException("Chapter 1 scene is missing Chapter1_Trost, a Terrain or the player.");

        Transform root = rootObject.transform;
        Transform city = root.Find("Baked_City");
        if (city == null) { city = new GameObject("Baked_City").transform; city.SetParent(root); }
        Transform markers = root.Find("Mission_Markers");
        if (markers == null) { markers = new GameObject("Mission_Markers").transform; markers.SetParent(root); }

        for (int i = city.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(city.GetChild(i).gameObject);

        PopulateCity(city, terrain);
        PlaceOnSpawnRooftop(player, city);
        TrostMapAuthoring map = rootObject.GetComponent<TrostMapAuthoring>();
        if (map == null) map = rootObject.AddComponent<TrostMapAuthoring>();
        ConfigureMap(map, markers, terrain, player.transform.position);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Chapter 1] City realigned on the unified grid; player re-seated on " + SpawnHouse + ".");
    }

    static void PopulateCity(Transform city, Terrain terrain)
    {
        Material stone = MaterialAsset("TrostWall", new Color(0.47f, 0.43f, 0.37f));
        Material road = MaterialAsset("TrostRoad", new Color(0.25f, 0.22f, 0.18f));
        Material ground = MaterialAsset("TrostGround", new Color(0.31f, 0.36f, 0.18f));

        // Collision safety floor under the whole walled district, out to the
        // outer faces of the side/south/north walls so nothing ends on a void
        // edge.  Only the decorative road slabs are non-colliding.
        float floorMinZ = WallSouthZ - WallThickness * 0.5f;
        float floorMaxZ = WallNorthZ + WallThickness * 0.5f;
        float floorHalfX = WallInnerX + WallThickness;
        Box(city, "Trost_Ground", new Vector3(0f, FloorTopY - .09f, (floorMinZ + floorMaxZ) * .5f),
            new Vector3(floorHalfX * 2f, .18f, floorMaxZ - floorMinZ), ground, 0);
        BuildRoads(city, road);
        BuildHouses(city, terrain);
        BuildWalls(city, terrain, stone);
        BuildDressing(city, terrain);
    }

    static void ConfigurePlayer(ODMController player)
    {
        player.groundLayer = ~0;
        player.hookableLayer = 1 << Hookable;
        player.maxHookRange = 105f;
    }

    // Every marker sits on the road grid: three crossings of the central
    // avenue (x = 0), three depots on side-street crossings, and the gate at
    // the north end of the avenue.
    static void ConfigureMap(TrostMapAuthoring map, Transform markers, Terrain terrain, Vector3 playerStart)
    {
        float gateZ = WallNorthZ - 20f;
        float sealZ = gateZ - 27f;
        float floorMinZ = WallSouthZ - WallThickness * 0.5f;
        float floorMaxZ = WallNorthZ + WallThickness * 0.5f;
        float floorHalfX = WallInnerX + WallThickness;

        map.buildingLayer = Hookable;
        map.groundY = terrain.transform.position.y;
        map.floorRect = new Rect(-floorHalfX, floorMinZ, floorHalfX * 2f, floorMaxZ - floorMinZ);
        map.playerStart = Marker(markers, "Player_Start", playerStart);
        map.gate = Marker(markers, "Breached_Gate", OnGround(terrain, 0f, gateZ));
        map.sealPoint = Marker(markers, "Seal_Point", OnGround(terrain, 0f, sealZ));
        map.depots = new[]
        {
            Marker(markers, "Gas_Depot_West", OnGround(terrain, RoadXs[1], RoadZs[2])),   // (-120, -45)
            Marker(markers, "Gas_Depot_East", OnGround(terrain, RoadXs[5], RoadZs[5])),   // ( 120, 135)
            Marker(markers, "Gas_Depot_North", OnGround(terrain, RoadXs[2], RoadZs[7]))   // ( -60, 255)
        };
        map.intersections = new[]
        {
            Marker(markers, "Intersection_South", OnGround(terrain, 0f, RoadZs[2])),     // (0, -45)
            Marker(markers, "Intersection_Centre", OnGround(terrain, 0f, RoadZs[4])),    // (0,  75)
            Marker(markers, "Intersection_North", OnGround(terrain, 0f, RoadZs[6]))      // (0, 195)
        };
        EditorUtility.SetDirty(map);
    }

    static Vector3 OnGround(Terrain terrain, float x, float z) => new Vector3(x, Ground(terrain, x, z), z);

    static void PlaceOnSpawnRooftop(ODMController player, Transform city)
    {
        Transform house = city.Find(SpawnHouse);
        if (house == null)
        {
            Debug.LogWarning("[Chapter 1] Spawn house '" + SpawnHouse + "' not found; leaving the player where it is.");
            return;
        }

        Renderer[] renderers = house.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        Vector3 above = new Vector3(bounds.center.x, bounds.max.y + 2f, bounds.center.z);
        Physics.SyncTransforms();
        Vector3 spawn = above;
        if (Physics.Raycast(above, Vector3.down, out RaycastHit hit, bounds.size.y + 10f))
            spawn = hit.point + Vector3.up * 1.2f;
        MovePlayer(player, spawn);
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
        float minZ = WallSouthZ + WallThickness * 0.5f;
        float maxZ = WallNorthZ - WallThickness * 0.5f;
        foreach (float z in RoadZs)
            Box(parent, "Cross_Road", new Vector3(0f, -1.86f, z), new Vector3(WallInnerX * 2f, .25f, RoadWidth), material, 0, false);
        foreach (float x in RoadXs)
            Box(parent, "Main_Road", new Vector3(x, -1.86f, (minZ + maxZ) * .5f), new Vector3(RoadWidth, .25f, maxZ - minZ), material, 0, false);
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
        // 60 m road pitch gives the player a fresh hook target before the
        // previous swing runs out, while the 14 m roads leave clear recovery
        // corridors.  Houses are centred in the 46 m cells between roads, two
        // per cell, so no house straddles a road.
        foreach (float x in BlockXs)
            foreach (float z in BlockZs)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    float houseX = x + side * 11f;
                    float houseZ = z + ((index % 2 == 0) ? 4f : -4f);
                    Vector3 position = new Vector3(houseX, Ground(terrain, houseX, houseZ), houseZ);
                    GameObject instance = PrefabUtility.InstantiatePrefab(prefabs[index % prefabs.Count], parent) as GameObject;
                    instance.name = "Trost_House_" + index.ToString("D2");
                    float groundY = position.y;
                    instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f));
                    // The source assets use aggressive LOD culling.  Trost
                    // needs readable skyline landmarks, so preserve the full
                    // house silhouette at distance.
                    KeepVisibleAtDistance(instance);
                    instance.transform.localScale = Vector3.one * (1.45f + (index % 3) * .10f);
                    FitHouseToCell(instance, x, houseZ, side, groundY);
                    MakeHookable(instance);
                    TrostMapAuthoring.EnsureSolidHouseBody(instance.transform, Hookable);
                    index++;
                }
            }
    }

    static void BuildWalls(Transform parent, Terrain terrain, Material material)
    {
        const float z = WallNorthZ;
        const float height = WallHeight;
        const float halfGate = GateWidth * .5f;
        // The north wall runs from the inner face of each side wall to the
        // edge of the gate opening, leaving exactly GateWidth between the two
        // halves.  (The old values left a 63 m gap instead of 42 m.)
        float segmentLength = WallInnerX - halfGate;
        float segmentCentre = halfGate + segmentLength * .5f;
        float sideZMin = WallSouthZ - WallThickness * .5f;
        float sideZMax = WallNorthZ + WallThickness * .5f;
        float sideLength = sideZMax - sideZMin;
        float sideCentreZ = (sideZMin + sideZMax) * .5f;
        float sideCentreX = WallInnerX + WallThickness * .5f;
        Box(parent, "Wall_West", new Vector3(-segmentCentre, Ground(terrain, -segmentCentre, z) + height * .5f, z), new Vector3(segmentLength, height, WallThickness), material, Hookable);
        Box(parent, "Wall_East", new Vector3(segmentCentre, Ground(terrain, segmentCentre, z) + height * .5f, z), new Vector3(segmentLength, height, WallThickness), material, Hookable);
        Box(parent, "Wall_South", new Vector3(0f, Ground(terrain, 0f, WallSouthZ) + height * .5f, WallSouthZ), new Vector3(sideCentreX * 2f, height, WallThickness), material, Hookable);
        Box(parent, "Wall_Left", new Vector3(-sideCentreX, Ground(terrain, -sideCentreX, sideCentreZ) + height * .5f, sideCentreZ), new Vector3(WallThickness, height, sideLength), material, Hookable);
        Box(parent, "Wall_Right", new Vector3(sideCentreX, Ground(terrain, sideCentreX, sideCentreZ) + height * .5f, sideCentreZ), new Vector3(WallThickness, height, sideLength), material, Hookable);
        for (int i = -3; i <= 3; i++)
            Box(parent, "Gate_Rubble", new Vector3(i * 6f, Ground(terrain, i * 6f, z - 13f) + 2f, z - 13f - Mathf.Abs(i) * 2f), new Vector3(7f, 4f + Mathf.Abs(i), 6f), material, Hookable);
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

    // Cell is BlockPitch - RoadWidth = 46 m wide.  Keep 3 m clear of each road
    // edge and 3 m between the two houses, which leaves 18.5 m per house.
    const float CellHalfWidth = (BlockPitch - RoadWidth) * 0.5f;
    const float RoadClearance = 3f;
    const float MaxHouseWidth = (CellHalfWidth * 2f - RoadClearance * 2f - 3f) * 0.5f;

    // The source prefabs have very different footprints, so a fixed offset
    // makes some straddle roads and others collide.  Rotate so the shorter
    // footprint side lies across the cell, shrink anything that is still too
    // wide, then push each house against its own side of the cell.
    static void FitHouseToCell(GameObject house, float cellX, float centreZ, int side, float groundY)
    {
        Bounds bounds = RendererBounds(house);
        if (bounds.size.x > bounds.size.z)
        {
            house.transform.RotateAround(bounds.center, Vector3.up, 90f);
            bounds = RendererBounds(house);
        }
        if (bounds.size.x > MaxHouseWidth)
        {
            house.transform.localScale *= MaxHouseWidth / bounds.size.x;
            bounds = RendererBounds(house);
        }

        float inner = CellHalfWidth - RoadClearance;
        float targetX = side < 0 ? cellX - inner + bounds.extents.x : cellX + inner - bounds.extents.x;
        Vector3 delta = new Vector3(targetX - bounds.center.x, 0f, centreZ - bounds.center.z);
        house.transform.position += delta;
        SnapBottomToGround(house, groundY);
    }

    static Bounds RendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
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
        // Reuse an existing marker so Realign keeps references and hierarchy.
        Transform marker = parent.Find(name);
        if (marker == null)
        {
            marker = new GameObject(name).transform;
            marker.SetParent(parent);
        }
        marker.position = position;
        return marker;
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
