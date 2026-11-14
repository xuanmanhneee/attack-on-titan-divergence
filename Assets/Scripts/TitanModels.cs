using System.Collections.Generic;
using UnityEngine;

// Danh sách ngoại hình Titan. Titan vô tri có thể dùng proceduralStyle để dựng
// trực tiếp bằng mesh nhẹ; Titan đặc biệt dùng FBX trong Resources chỉ khi được
// gọi riêng cho boss/biến hình, không nằm trong pool quái thường.
// modelPath là đường dẫn trong Resources, KHÔNG có đuôi file.
// Nếu titan đi lùi hoặc gáy (chấm đỏ) nằm ở phía trước mặt thì đổi yawOffset thành 180.
[System.Serializable]
public class TitanModelDef
{
    public string name;
    public string modelPath;
    public string proceduralStyle;       // Titan vô tri dựng từ mesh nhẹ ngay trong Unity
    public string bodyTex, faceTex, hairTex, weaponTex; // đường dẫn texture trong Resources (có thể để trống)
    public float yawOffset = 0f;       // xoay model cho đúng hướng mặt (độ)
    public float napeHeight = 0.85f;   // độ cao gáy so với chiều cao titan (0..1)
    public float napeBack = 0.55f;     // gáy lùi ra sau lưng bao nhiêu (đơn vị, titan cao 10)
    public float healthMultiplier = 1f;
    public float heightMultiplier = 1f;
    public int spawnWeight = 1;

    // Dạng titan của người chơi (TitanForm): vị trí mắt so với xương Head, tính theo đơn vị model chưa scale
    // (+Y lên, +Z trước mặt). Các renderer có tên chứa chuỗi trong hideInFirstPerson chỉ đổ bóng,
    // không vẽ ra camera người chơi (tóc/mắt/răng che tầm nhìn).
    public Vector3 eyeFromHead;
    public string[] hideInFirstPerson;
}

public static class TitanModels
{
    public static readonly TitanModelDef[] All =
    {
        new TitanModelDef
        {
            name = "MeshyGluttonousTitan",
            modelPath = "Titans/MeshyGluttonousTitan",
            napeHeight = 0.69f,
            napeBack = 0.85f,
            healthMultiplier = 1.2f,
            heightMultiplier = 1f,
            spawnWeight = 1,
        },
        // Titan Tiến Công (rig Mixamo + clip Punching): chỉ dùng cho dạng titan của người chơi (TitanForm),
        // không sinh làm quái.
        new TitanModelDef
        {
            name = "AttackTitan",
            modelPath = "Titans/AttackTitanRigged/AttackTitan_Punching",
            bodyTex = "Titans/RealisticTitan/Body_color",
            napeHeight = 0.89f,
            napeBack = 0.6f,
            spawnWeight = 0,
            eyeFromHead = new Vector3(0f, 1f, 3f), // trong đầu, ngang gò má: nắm đấm vươn ra giữa khung hình
            hideInFirstPerson = new[] { "Hair", "Eyes", "Teeth" },
        },
    };

    static readonly Dictionary<string, bool> available = new Dictionary<string, bool>();

    static bool IsAvailable(TitanModelDef d)
    {
        if (!string.IsNullOrEmpty(d.proceduralStyle)) return true;
        if (!available.TryGetValue(d.modelPath, out bool ok))
        {
            ok = Resources.Load<GameObject>(d.modelPath) != null;
            available[d.modelPath] = ok;
            if (!ok) Debug.LogWarning("[Titan] Không tải được model '" + d.modelPath + "' (kiểm tra Assets/Resources).");
        }
        return ok;
    }

    public static TitanModelDef Get(string name)
    {
        foreach (var d in All) if (d.name == name && IsAvailable(d)) return d;
        return null;
    }

    // Chọn ngẫu nhiên theo trọng số trong các model tải được. Trả null nếu không có model nào (dùng placeholder).
    // spawnWeight <= 0: model đặc biệt, không bao giờ được chọn ngẫu nhiên.
    public static TitanModelDef PickRandom()
    {
        int total = 0;
        foreach (var d in All) if (d.spawnWeight > 0 && IsAvailable(d)) total += d.spawnWeight;
        if (total == 0) return null;

        int r = Random.Range(0, total);
        foreach (var d in All)
        {
            if (d.spawnWeight <= 0 || !IsAvailable(d)) continue;
            r -= d.spawnWeight;
            if (r < 0) return d;
        }
        return null;
    }
}
