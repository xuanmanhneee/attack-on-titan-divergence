using System.Collections.Generic;
using UnityEngine;

// Khai báo các model titan thật (nằm trong Assets/Resources/Titans/...).
// Thêm titan mới: bỏ FBX + texture vào Resources/Titans/<Tên>/ rồi thêm một dòng vào All.
// modelPath là đường dẫn trong Resources, KHÔNG có đuôi file.
// Nếu titan đi lùi hoặc gáy (chấm đỏ) nằm ở phía trước mặt thì đổi yawOffset thành 180.
[System.Serializable]
public class TitanModelDef
{
    public string name;
    public string modelPath;
    public string bodyTex, faceTex, hairTex, weaponTex; // đường dẫn texture trong Resources (có thể để trống)
    public float yawOffset = 0f;       // xoay model cho đúng hướng mặt (độ)
    public float napeHeight = 0.85f;   // độ cao gáy so với chiều cao titan (0..1)
    public float napeBack = 0.55f;     // gáy lùi ra sau lưng bao nhiêu (đơn vị, titan cao 10)
    public float healthMultiplier = 1f;
    public float heightMultiplier = 1f;
    public int spawnWeight = 1;
}

public static class TitanModels
{
    public static readonly TitanModelDef[] All =
    {
        new TitanModelDef
        {
            name = "RealisticTitan",
            modelPath = "Titans/RealisticTitan/Attack_sketchfab",
            bodyTex = "Titans/RealisticTitan/Body_color",
            spawnWeight = 5,
        },
        new TitanModelDef
        {
            name = "ArmoredTitan",
            modelPath = "Titans/ArmoredTitan/role_kaizhijuren_skin",
            bodyTex = "Titans/ArmoredTitan/role_kaizhijuren_body_tex",
            faceTex = "Titans/ArmoredTitan/role_kaizhijuren_face_tex",
            hairTex = "Titans/ArmoredTitan/role_kaizhijuren_hair_tex",
            healthMultiplier = 1.8f,
            heightMultiplier = 1.1f,
            spawnWeight = 2,
        },
        new TitanModelDef
        {
            name = "WarhammerTitan",
            modelPath = "Titans/WarhammerTitan/role_lala_cj_skin",
            bodyTex = "Titans/WarhammerTitan/role_zhanchuijuren_body_tex",
            faceTex = "Titans/WarhammerTitan/role_zhanchuijuren_head_tex",
            weaponTex = "Titans/WarhammerTitan/role_zhanchuijuren_wiqi02_tex",
            healthMultiplier = 1.5f,
            spawnWeight = 2,
        },
    };

    static readonly Dictionary<string, bool> available = new Dictionary<string, bool>();

    static bool IsAvailable(TitanModelDef d)
    {
        if (!available.TryGetValue(d.modelPath, out bool ok))
        {
            ok = Resources.Load<GameObject>(d.modelPath) != null;
            available[d.modelPath] = ok;
            if (!ok) Debug.LogWarning("[Titan] Không tải được model '" + d.modelPath + "' (kiểm tra Assets/Resources).");
        }
        return ok;
    }

    // Chọn ngẫu nhiên theo trọng số trong các model tải được. Trả null nếu không có model nào (dùng placeholder).
    public static TitanModelDef PickRandom()
    {
        int total = 0;
        foreach (var d in All) if (IsAvailable(d)) total += Mathf.Max(1, d.spawnWeight);
        if (total == 0) return null;

        int r = Random.Range(0, total);
        foreach (var d in All)
        {
            if (!IsAvailable(d)) continue;
            r -= Mathf.Max(1, d.spawnWeight);
            if (r < 0) return d;
        }
        return null;
    }
}
