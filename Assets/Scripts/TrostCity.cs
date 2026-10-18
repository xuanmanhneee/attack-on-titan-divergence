using System.Collections.Generic;
using UnityEngine;

// Dữ liệu bản đồ Trost sau khi dựng
public class TrostMap
{
    public GameObject root;
    public Vector3 playerStart;
    public Vector3 gate;        // tâm lỗ hổng trên tường
    public Vector3 sealPoint;   // điểm cần tới để chặn lỗ hổng
    public readonly List<Vector3> depots = new List<Vector3>();
    public readonly List<Vector3> intersections = new List<Vector3>();
    public int buildingLayer;
    public float halfWidth, length;

    public float GroundY(float x, float z, float fallback)
    {
        var t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y : fallback;
    }
}

// Dựng khu phố kiểu Trost bằng code (hộp màu): lưới nhà cao thấp xen kẽ, tường lớn ở phía bắc
// có lỗ hổng (cổng bị phá), tường bao hai bên, 3 điểm tiếp tế gas. Mọi công trình nằm ở layer "hookable".
// Khi có asset nhà thật, thay phần Building() bằng prefab.
public static class TrostCity
{
    const int Nx = 8, Nz = 9;
    const float Pitch = 48f, Block = 34f, StartZ = 60f;
    const float WallHeight = 85f, WallThickness = 26f, GateWidth = 30f;

    static readonly Color[] WallColors =
    {
        new Color(0.86f, 0.80f, 0.70f), new Color(0.80f, 0.74f, 0.64f),
        new Color(0.90f, 0.86f, 0.78f), new Color(0.74f, 0.68f, 0.60f),
    };
    static readonly Color[] RoofColors =
    {
        new Color(0.55f, 0.25f, 0.20f), new Color(0.42f, 0.22f, 0.18f), new Color(0.50f, 0.30f, 0.22f),
    };

    static Material[] wallMats, roofMats;
    static Material stoneMat, rubbleMat;

    static Material Mat(Color c, bool unlit = false)
    {
        Shader sh = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        return m;
    }

    static GameObject Box(Transform parent, Vector3 center, Vector3 size, Material mat, int layer, bool collider = true, PrimitiveType type = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.layer = layer;
        if (!collider) Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    public static TrostMap Build(Vector3 origin, int layer, int seed = 1234)
    {
        var rng = new System.Random(seed);
        var map = new TrostMap { buildingLayer = layer, playerStart = origin };
        var root = new GameObject("TrostCity");
        map.root = root;
        Transform tr = root.transform;

        wallMats = new Material[WallColors.Length];
        for (int i = 0; i < wallMats.Length; i++) wallMats[i] = Mat(WallColors[i]);
        roofMats = new Material[RoofColors.Length];
        for (int i = 0; i < roofMats.Length; i++) roofMats[i] = Mat(RoofColors[i]);
        stoneMat = Mat(new Color(0.55f, 0.55f, 0.57f));
        rubbleMat = Mat(new Color(0.38f, 0.35f, 0.33f));

        float fb = origin.y - 1f; // độ cao dự phòng nếu không có Terrain

        // ----- Nhà -----
        for (int i = 0; i < Nx; i++)
            for (int j = 0; j < Nz; j++)
            {
                float cx = origin.x + (i - (Nx - 1) / 2f) * Pitch;
                float cz = origin.z + StartZ + j * Pitch;
                int mode = rng.Next(0, 4); // 1 nhà lớn / 2 nhà ngang / 2 nhà dọc / 4 nhà nhỏ
                float half = Block / 2f;
                var cells = new List<Vector4>(); // x,z,w,d (tâm và kích thước)
                if (mode == 0) cells.Add(new Vector4(cx, cz, Block - 4f, Block - 4f));
                else if (mode == 1) { cells.Add(new Vector4(cx - half / 2f, cz, half - 2f, Block - 4f)); cells.Add(new Vector4(cx + half / 2f, cz, half - 2f, Block - 4f)); }
                else if (mode == 2) { cells.Add(new Vector4(cx, cz - half / 2f, Block - 4f, half - 2f)); cells.Add(new Vector4(cx, cz + half / 2f, Block - 4f, half - 2f)); }
                else
                {
                    for (int a = -1; a <= 1; a += 2)
                        for (int b = -1; b <= 1; b += 2)
                            cells.Add(new Vector4(cx + a * half / 2f, cz + b * half / 2f, half - 2f, half - 2f));
                }
                foreach (var c in cells)
                {
                    float h = 14f + (float)rng.NextDouble() * 22f;
                    if (rng.NextDouble() < 0.12) h += 16f; // tháp cao
                    Building(tr, c.x, c.y, c.z, c.w, h, layer, rng, map, fb);
                }
            }

        float totalW = Nx * Pitch + 80f;
        float wallZ = origin.z + StartZ + (Nz - 1) * Pitch + Block / 2f + 36f;

        // ----- Tường bắc có lỗ hổng -----
        float segW = (totalW - GateWidth) / 2f;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = origin.x + side * (GateWidth / 2f + segW / 2f);
            float g = map.GroundY(x, wallZ, fb);
            Box(tr, new Vector3(x, g + WallHeight / 2f - 3f, wallZ), new Vector3(segW, WallHeight + 3f, WallThickness), stoneMat, layer);
        }
        // Tường bao hai bên và phía nam (giữ người chơi và titan trong khu phố)
        float len = wallZ - (origin.z - 40f);
        float midZ = (wallZ + (origin.z - 40f)) / 2f;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = origin.x + side * (totalW / 2f + 10f);
            float g = map.GroundY(x, midZ, fb);
            Box(tr, new Vector3(x, g + 30f, midZ), new Vector3(20f, 66f, len), stoneMat, layer);
        }
        {
            float z = origin.z - 40f;
            float g = map.GroundY(origin.x, z, fb);
            Box(tr, new Vector3(origin.x, g + 30f, z), new Vector3(totalW + 40f, 66f, 20f), stoneMat, layer);
        }

        // Đống đổ nát quanh lỗ hổng
        for (int k = 0; k < 10; k++)
        {
            float x = origin.x + (float)(rng.NextDouble() * 2 - 1) * (GateWidth * 0.9f);
            float z = wallZ - 6f - (float)rng.NextDouble() * 40f;
            float g = map.GroundY(x, z, fb);
            float s = 3f + (float)rng.NextDouble() * 6f;
            var rb = Box(tr, new Vector3(x, g + s * 0.3f, z), new Vector3(s, s * 0.7f, s), rubbleMat, layer);
            rb.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 90f, (float)rng.NextDouble() * 20f);
        }

        float gateY = map.GroundY(origin.x, wallZ, fb);
        map.gate = new Vector3(origin.x, gateY, wallZ);
        map.sealPoint = new Vector3(origin.x, map.GroundY(origin.x, wallZ - 18f, fb), wallZ - 18f);
        map.halfWidth = totalW / 2f;
        map.length = len;

        // ----- Giao lộ (điểm sinh titan) và điểm tiếp tế -----
        for (int k = 0; k <= Nx; k++)
            for (int m = 0; m <= Nz; m++)
            {
                float x = origin.x + (k - Nx / 2f) * Pitch;
                float z = origin.z + StartZ + (m - 0.5f) * Pitch;
                map.intersections.Add(new Vector3(x, map.GroundY(x, z, fb), z));
            }

        int[,] depotCells = { { 2, 2 }, { 6, 5 }, { 3, 8 } };
        for (int d = 0; d < depotCells.GetLength(0); d++)
        {
            float x = origin.x + (depotCells[d, 0] - Nx / 2f) * Pitch;
            float z = origin.z + StartZ + (depotCells[d, 1] - 0.5f) * Pitch;
            Vector3 p = new Vector3(x, map.GroundY(x, z, fb), z);
            map.depots.Add(p);
            Depot(tr, p);
        }

        return map;
    }

    static void Building(Transform parent, float x, float z, float w, float d, float h, int layer, System.Random rng, TrostMap map, float fb)
    {
        float g = map.GroundY(x, z, fb);
        var body = Box(parent, new Vector3(x, g + h / 2f - 0.75f, z), new Vector3(w, h + 1.5f, d), wallMats[rng.Next(wallMats.Length)], layer);
        body.name = "Building";
        Box(parent, new Vector3(x, g + h + 0.5f, z), new Vector3(w + 1.4f, 1f, d + 1.4f), roofMats[rng.Next(roofMats.Length)], layer, false).name = "Roof";
    }

    // Điểm tiếp tế: bệ xanh trên mặt đất và cột sáng cao để nhìn thấy từ xa
    static void Depot(Transform parent, Vector3 p)
    {
        var pad = Box(parent, p + Vector3.up * 0.1f, new Vector3(16f, 0.1f, 16f), Mat(new Color(0.2f, 0.9f, 0.4f), true), 0, false, PrimitiveType.Cylinder);
        pad.name = "SupplyPad";
        var beam = Box(parent, p + Vector3.up * 50f, new Vector3(1.4f, 50f, 1.4f), Mat(new Color(0.4f, 1f, 0.7f), true), 0, false, PrimitiveType.Cylinder);
        beam.name = "SupplyBeam";
    }
}
