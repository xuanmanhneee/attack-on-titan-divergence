using System.Collections.Generic;
using UnityEngine;

// Giữ chân titan luôn chạm mặt đất, dù animation làm hông cao hơn / thấp hơn tư thế chữ T lúc đo kích thước.
// Mỗi frame: tìm xương chân thấp nhất (tính trong hệ tọa độ của Titan), dịch node "Visual" lên/xuống sao cho đế chân nằm đúng y = 0.
// Chỉ dùng được với rig Humanoid (cần biết xương bàn chân).
public class TitanFootGrounder : MonoBehaviour
{
    const float MaxShift = 4f;   // đơn vị local của Titan (titan cao 10) - giới hạn để không bao giờ lệch quá xa

    Transform root;
    Transform visual;
    readonly List<Transform> feet = new List<Transform>();
    float sole;      // khoảng cách từ xương chân thấp nhất tới đế giày lúc chưa animation
    float current;   // độ dịch hiện tại của Visual (local y)

    public float Shift => current;

    public static TitanFootGrounder TryCreate(GameObject model, Transform root, Transform visual)
    {
        var animator = model.GetComponentInChildren<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) return null;

        var g = model.AddComponent<TitanFootGrounder>();
        g.root = root;
        g.visual = visual;

        HumanBodyBones[] bones = { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes };
        foreach (var b in bones)
        {
            var t = animator.GetBoneTransform(b);
            if (t != null) g.feet.Add(t);
        }
        if (g.feet.Count == 0) { Destroy(g); return null; }

        // Lúc này model đang ở tư thế gốc và đã được đặt để điểm thấp nhất của lưới nằm ở y = 0
        g.sole = Mathf.Max(0f, g.LowestFootY());
        return g;
    }

    float LowestFootY()
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < feet.Count; i++)
        {
            if (feet[i] == null) continue;
            float y = root.InverseTransformPoint(feet[i].position).y;
            if (y < lowest) lowest = y;
        }
        return lowest;
    }

    void LateUpdate()
    {
        if (root == null || visual == null) return;

        float lowest = LowestFootY();
        if (lowest == float.MaxValue) return;

        float raw = lowest - current;                       // độ cao xương chân nếu Visual chưa bị dịch
        float want = Mathf.Clamp(sole - raw, -MaxShift, MaxShift);
        float t = 1f - Mathf.Exp(-10f * Time.deltaTime);
        current = Mathf.Lerp(current, want, t);

        visual.localPosition = new Vector3(0f, current, 0f);
    }
}
