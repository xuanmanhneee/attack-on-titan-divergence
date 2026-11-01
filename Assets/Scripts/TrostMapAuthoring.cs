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

    static void AddPositions(List<Vector3> target, Transform[] points)
    {
        if (points == null) return;
        foreach (Transform point in points)
            if (point != null) target.Add(point.position);
    }
}
