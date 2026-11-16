using UnityEngine;

// Chống kẹt trong mesh nhà/mái (single-sided). Nếu capsule lọt vào sau mặt tường/mái thì
// CharacterController.Move trả về 0 theo mọi hướng => người chơi "dính" vào nhà.
// ODMController gọi Track mỗi bước mô phỏng; khi Track trả về true, người chơi đã được đưa
// về vị trí an toàn và ODMController phải xử lý phần còn lại (velocity, độ dài dây).
public class ODMAntiStuck : MonoBehaviour
{
    const int EmbeddedStepsBeforeRecover = 3;
    static readonly Vector3[] InsideProbeDirections =
    {
        Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.left, Vector3.right
    };

    readonly Collider[] overlapHits = new Collider[32];
    readonly RaycastHit[] insideProbeHits = new RaycastHit[16];
    readonly Vector3[] safeHistory = new Vector3[12];
    int safeHistoryHead;
    int safeHistoryCount;
    float nextSafeRecordTime;
    int embeddedSteps;

    CharacterController controller;
    LayerMask collisionMask;
    bool logDebug;

    public void Init(CharacterController controller, LayerMask collisionMask, bool logDebug)
    {
        this.controller = controller;
        this.collisionMask = collisionMask;
        this.logDebug = logDebug;
    }

    public void ResetHistory()
    {
        safeHistoryCount = 0;
        embeddedSteps = 0;
    }

    // Trả về true nếu vừa đưa người chơi ra khỏi chỗ kẹt (vị trí transform đã đổi).
    public bool Track(Vector3 requested, Vector3 actual, bool isGrounded)
    {
        if (controller == null || !controller.enabled) return false;

        // Đứng trên đất luôn có lực ghì xuống (-2 m/s) mà không di chuyển: bỏ thành phần đó.
        if (isGrounded)
        {
            if (requested.y < 0f) requested.y = 0f;
            if (actual.y < 0f) actual.y = 0f;
        }

        float req = requested.magnitude;
        float moved = actual.magnitude;
        float now = Time.time;

        if (req < 0.003f || moved >= req * 0.15f)
        {
            embeddedSteps = 0;
            if (req < 0.003f || moved >= req * 0.5f)
                RecordSafePosition(now);
            return false;
        }

        // Không đi được theo hướng mong muốn. Áp sát tường thì vẫn lùi ra được;
        // bị kẹt trong mesh thì hướng nào cũng bị chặn.
        Vector3 dir = requested / req;
        if (ProbeMove(-dir) || ProbeMove(Vector3.up) || ProbeMove(Vector3.down))
        {
            embeddedSteps = 0;
            return false;
        }

        embeddedSteps++;
        if (embeddedSteps >= EmbeddedStepsBeforeRecover)
        {
            embeddedSteps = 0;
            return RecoverFromEmbedding(dir);
        }
        return false;
    }

    bool ProbeMove(Vector3 dir)
    {
        const float probeDistance = 0.04f;
        Vector3 start = transform.position;
        controller.Move(dir * probeDistance);
        float moved = (transform.position - start).magnitude;
        controller.enabled = false;
        transform.position = start;
        controller.enabled = true;
        return moved >= probeDistance * 0.3f;
    }

    void RecordSafePosition(float now)
    {
        if (now < nextSafeRecordTime) return;
        Vector3 center = transform.TransformPoint(controller.center);
        if (IsInsideGeometry(center)) return;
        nextSafeRecordTime = now + 0.1f;
        safeHistory[safeHistoryHead] = transform.position;
        safeHistoryHead = (safeHistoryHead + 1) % safeHistory.Length;
        safeHistoryCount = Mathf.Min(safeHistoryCount + 1, safeHistory.Length);
    }

    bool RecoverFromEmbedding(Vector3 stuckDir)
    {
        Vector3 from = transform.position;
        Vector3 offset = transform.TransformPoint(controller.center) - from;

        bool found = false;
        Vector3 target = from;

        // 1) Vị trí an toàn gần đây nhất (mới -> cũ).
        for (int i = 1; i <= safeHistoryCount && !found; i++)
        {
            int idx = (safeHistoryHead - i + safeHistory.Length) % safeHistory.Length;
            Vector3 p = safeHistory[idx];
            if ((p - from).sqrMagnitude < 0.0025f) continue;
            if (IsFreeSpot(p + offset)) { target = p; found = true; }
        }

        // 2) Không có lịch sử hợp lệ: tìm chỗ trống quanh người chơi.
        if (!found)
        {
            Vector3 back = -stuckDir;
            Vector3[] dirs =
            {
                Vector3.up, back, new Vector3(back.x, 0f, back.z).normalized,
                Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                (Vector3.forward + Vector3.left).normalized, (Vector3.forward + Vector3.right).normalized,
                (Vector3.back + Vector3.left).normalized, (Vector3.back + Vector3.right).normalized,
                Vector3.down
            };
            float[] distances = { 0.5f, 1f, 1.5f, 2.5f, 4f };
            for (int d = 0; d < distances.Length && !found; d++)
                for (int k = 0; k < dirs.Length && !found; k++)
                {
                    if (dirs[k].sqrMagnitude < 0.0001f) continue;
                    Vector3 p = from + dirs[k] * distances[d];
                    if (IsFreeSpot(p + offset)) { target = p; found = true; }
                }
        }

        if (!found)
        {
            Debug.LogWarning($"[ODM] Kẹt trong collider tại {from} nhưng không tìm được chỗ thoát.", this);
            return false;
        }

        controller.enabled = false;
        transform.position = target;
        controller.enabled = true;

        if (logDebug)
            Debug.Log($"[ODM] Thoát kẹt: {from} -> {target}", this);
        return true;
    }

    bool IsFreeSpot(Vector3 worldCenter)
    {
        Vector3 scale = transform.lossyScale;
        float worldHeight = controller.height * Mathf.Abs(scale.y);
        float worldRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float r = worldRadius + controller.skinWidth;
        float halfSegment = Mathf.Max(0f, worldHeight * 0.5f - worldRadius);
        int count = Physics.OverlapCapsuleNonAlloc(worldCenter - Vector3.up * halfSegment,
                                                   worldCenter + Vector3.up * halfSegment, r,
                                                   overlapHits, collisionMask,
                                                   QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider other = overlapHits[i];
            if (other == null || other == controller || other.transform.IsChildOf(transform)) continue;
            return false;
        }
        return !IsInsideGeometry(worldCenter);
    }

    // Ở trong một mesh kín (single-sided) thì tia bắn ra sẽ chạm MẶT SAU của tam giác.
    // Từ 2 hướng trở lên thấy mặt sau => đang nằm bên trong nhà.
    public bool IsInsideGeometry(Vector3 point)
    {
        bool previous = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        int backfaces = 0;
        for (int d = 0; d < InsideProbeDirections.Length && backfaces < 2; d++)
        {
            Vector3 dir = InsideProbeDirections[d];
            int count = Physics.RaycastNonAlloc(point, dir, insideProbeHits, 6f,
                                                collisionMask, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            Vector3 nearestNormal = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = insideProbeHits[i];
                if (h.collider == null || h.collider == controller || h.transform.IsChildOf(transform)) continue;
                if (h.distance <= 0f) continue;
                if (h.distance < nearest) { nearest = h.distance; nearestNormal = h.normal; }
            }
            if (nearest < float.PositiveInfinity && Vector3.Dot(nearestNormal, dir) > 0.05f)
                backfaces++;
        }
        Physics.queriesHitBackfaces = previous;
        return backfaces >= 2;
    }
}
