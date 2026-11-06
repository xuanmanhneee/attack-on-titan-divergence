using System.Collections.Generic;
using UnityEngine;

// Metadata authored together with the Chapter 1 scene.  GameManager converts
// this into the lightweight data object consumed by the mission at runtime.
// The city geometry itself is deliberately stored in the scene hierarchy.
public class TrostMapAuthoring : MonoBehaviour
{
    public Transform playerStart;
    public Transform gate;
    public Transform sealPoint;
    public Transform[] depots;
    public Transform[] intersections;
    public int buildingLayer = 3;
    public float groundY;
    public Rect floorRect;

    public TrostMap CreateRuntimeMap(Vector3 fallbackPlayerStart)
    {
        EnsureSolidHouseBodies();

        var map = new TrostMap
        {
            root = gameObject,
            playerStart = playerStart != null ? playerStart.position : fallbackPlayerStart,
            gate = gate != null ? gate.position : transform.position,
            sealPoint = sealPoint != null ? sealPoint.position : transform.position,
            buildingLayer = buildingLayer,
            groundY = groundY,
            floorRect = floorRect
        };

        AddPositions(map.depots, depots);
        AddPositions(map.intersections, intersections);
        return map;
    }

    void EnsureSolidHouseBodies()
    {
        int created = 0;
        foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
        {
            if (!candidate.name.StartsWith("Trost_House_")) continue;
            NormalizeHouseLods(candidate);
            if (EnsureSolidHouseBody(candidate, buildingLayer)) created++;
        }
        Debug.Log($"[TrostCollision] solid house bodies created={created}", this);
    }

    static void NormalizeHouseLods(Transform house)
    {
        foreach (LODGroup group in house.GetComponentsInChildren<LODGroup>(true))
        {
            var highest = new List<Renderer>();
            foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>(true))
            {
                string name = renderer.name.ToLowerInvariant();
                if (name.Contains("lod1") || name.Contains("lod2") || name.Contains("lod3")) continue;
                highest.Add(renderer);
            }

            if (highest.Count == 0)
            {
                LOD[] existing = group.GetLODs();
                if (existing.Length > 0) highest.AddRange(existing[0].renderers);
            }
            if (highest.Count == 0) continue;
            group.SetLODs(new[] { new LOD(0.001f, highest.ToArray()) });
            group.RecalculateBounds();
        }
    }

    public static bool EnsureSolidHouseBody(Transform house, int layer)
    {
        if (house == null || house.Find("SolidHouseBody") != null) return false;

        Renderer[] renderers = house.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return false;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        // A closed body collider prevents high-speed movement from finding
        // holes in decorative wall/window meshes. Stop below the eaves so the
        // authored sloped MeshCollider remains the actual rooftop surface.
        float bodyHeight = Mathf.Max(2f, bounds.size.y * 0.62f);
        Vector3 worldCenter = new Vector3(bounds.center.x, bounds.min.y + bodyHeight * 0.5f, bounds.center.z);
        Vector3 worldSize = new Vector3(bounds.size.x * 0.94f, bodyHeight, bounds.size.z * 0.94f);

        var solid = new GameObject("SolidHouseBody");
        solid.layer = layer;
        solid.transform.SetParent(house, false);
        BoxCollider box = solid.AddComponent<BoxCollider>();
        box.center = house.InverseTransformPoint(worldCenter);
        Vector3 localSize = house.InverseTransformVector(worldSize);
        box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        return true;
    }

    static void AddPositions(List<Vector3> target, Transform[] points)
    {
        if (points == null) return;
        foreach (Transform point in points)
            if (point != null) target.Add(point.position);
    }
}
