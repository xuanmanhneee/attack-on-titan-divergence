using System.Collections.Generic;
using UnityEngine;

// Nhiệm vụ "Trận Trost" (3 giai đoạn):
//  1) Dọn titan đã lọt vào khu phố.
//  2) Titan tràn qua lỗ hổng trên tường (có một Pure Titan khổng lồ).
//  3) Tới cổng, trụ lại trong vùng cổng đủ lâu để chặn lỗ hổng trong khi titan tiếp tục kéo tới.
// Gas không tự hồi: phải chạy tới điểm tiếp tế (cột sáng xanh) để nạp gas và kiếm.
public class TrostMission : MonoBehaviour
{
    public enum Phase { Clear, Reinforce, Seal, Done }

    public struct Marker { public Vector3 pos; public string label; public Color color; }

    public Phase Current { get; private set; } = Phase.Clear;
    public string ObjectiveText { get; private set; } = "";
    public readonly List<Marker> Markers = new List<Marker>();

    [Header("Số lượng và thời gian")]
    public int clearTitans = 6;
    public int reinforceTitans = 6;
    public float sealSeconds = 15f;
    public float sealRadius = 28f;
    public float phaseDelay = 4f;
    public float minSpawnDistance = 90f;

    [Header("Tiếp tế")]
    public float depotStock = 600f;
    public float depotFillRate = 250f;
    public float depotRegen = 30f;
    public float depotRadius = 9f;

    ODMController odm;
    PlayerCombat combat;
    GameManager gm;
    TrostMap map;

    float delayTimer;
    bool waiting;
    float sealProgress;
    float respawnTimer;
    bool announcedDepot;

    class Depot { public Vector3 pos; public float stock; public bool restocked; }
    readonly List<Depot> depots = new List<Depot>();

    public void Init(GameManager manager, ODMController player, PlayerCombat pc, TrostMap trostMap)
    {
        gm = manager; odm = player; combat = pc; map = trostMap;
        foreach (var p in map.depots) depots.Add(new Depot { pos = p, stock = depotStock });

        Titan.Killed += OnKilled;
        SpawnClearWave();
        gm.Say("Trost: titan đã lọt vào khu phố! Gas cạn thì chạy tới cột sáng xanh để tiếp tế.");
    }

    void OnDestroy() { Titan.Killed -= OnKilled; }

    // ================= SINH TITAN =================
    Titan Spawn(Vector3 pos, TitanModelDef def, float height)
    {
        var t = Titan.Create(pos, height, odm.transform, def);
        t.detectRange = 220f; // trong thành phố, titan phát hiện người chơi từ xa
        t.loseRange = 400f;
        return t;
    }

    void SpawnClearWave()
    {
        var pts = new List<Vector3>(map.intersections);
        for (int i = pts.Count - 1; i > 0; i--) { int r = Random.Range(0, i + 1); (pts[i], pts[r]) = (pts[r], pts[i]); }

        int spawned = 0;
        foreach (var p in pts)
        {
            if (spawned >= clearTitans) break;
            Vector3 d = p - odm.transform.position; d.y = 0f;
            if (d.magnitude < minSpawnDistance) continue;
            Spawn(p, TitanModels.PickRandom(), Random.Range(10f, 15f));
            spawned++;
        }
    }

    // Điểm sinh titan gần cổng: chọn ngã tư (luôn là đường trống) cách cổng 25–130 m, tuyệt đối không sinh trong nhà.
    Vector3 GatePoint()
    {
        var cand = new List<Vector3>();
        foreach (var ip in map.intersections)
        {
            float dz = map.sealPoint.z - ip.z;
            if (dz < 25f || dz > 130f) continue;
            Vector3 d = ip - odm.transform.position; d.y = 0f;
            if (d.magnitude < 40f) continue;   // đừng sinh ngay sát người chơi
            cand.Add(ip);
        }
        if (cand.Count == 0) cand.AddRange(map.intersections);

        Vector3 p = cand[Random.Range(0, cand.Count)];
        p.y = map.GroundAt(p);
        return p;
    }

    void SpawnReinforcements()
    {
        var abnormal = TitanModels.Get("MeshyGluttonousTitan");
        for (int i = 0; i < reinforceTitans; i++)
        {
            bool boss = (i == 0 && abnormal != null);
            Spawn(GatePoint(), boss ? abnormal : TitanModels.PickRandom(), boss ? 15f : Random.Range(9f, 15f));
        }
    }

    // ================= TIẾN TRÌNH =================
    void OnKilled(Titan t)
    {
        if (Current == Phase.Done) return;
        int left = Titan.All.Count;
        if (left > 0) gm.Say("Còn " + left + " titan");
    }

    void Update()
    {
        if (gm.Current != GameManager.State.Playing) return;
        float dt = Time.deltaTime;

        UpdateDepots(dt);

        switch (Current)
        {
            case Phase.Clear:
                ObjectiveText = "1/3  Dọn sạch titan trong khu phố (còn " + Titan.All.Count + ")";
                if (Titan.All.Count == 0) Advance(Phase.Reinforce, "Titan đang tràn qua lỗ hổng trên tường!");
                break;

            case Phase.Reinforce:
                ObjectiveText = "2/3  Hạ gục đợt titan tràn qua cổng (còn " + Titan.All.Count + ")";
                if (waiting) { delayTimer -= dt; if (delayTimer <= 0f) { waiting = false; SpawnReinforcements(); } }
                else if (Titan.All.Count == 0) Advance(Phase.Seal, "Tới cổng và chặn lỗ hổng!");
                break;

            case Phase.Seal:
                UpdateSeal(dt);
                break;
        }

        BuildMarkers();
    }

    void Advance(Phase next, string message)
    {
        Current = next;
        waiting = true;
        delayTimer = phaseDelay;
        gm.Say(message);
        if (next == Phase.Seal) { waiting = false; respawnTimer = 6f; }
    }

    void UpdateSeal(float dt)
    {
        Vector3 d = odm.transform.position - map.sealPoint; d.y = 0f;
        bool inside = d.magnitude <= sealRadius;

        sealProgress = inside ? sealProgress + dt : Mathf.Max(0f, sealProgress - dt * 0.5f);
        ObjectiveText = inside
            ? "3/3  Giữ vững vùng cổng: " + sealProgress.ToString("F0") + "/" + sealSeconds.ToString("F0") + " giây"
            : "3/3  Tới cổng (" + Mathf.RoundToInt(d.magnitude) + " m) rồi trụ lại " + sealSeconds.ToString("F0") + " giây";

        respawnTimer -= dt;
        if (respawnTimer <= 0f && Titan.All.Count < 5)
        {
            respawnTimer = 10f;
            for (int i = 0; i < 2; i++) Spawn(GatePoint(), TitanModels.PickRandom(), Random.Range(10f, 15f));
        }

        if (sealProgress >= sealSeconds)
        {
            Current = Phase.Done;
            ObjectiveText = "Đã chặn được lỗ hổng!";
            gm.EndGame(true);
        }
    }

    // ================= TIẾP TẾ =================
    void UpdateDepots(float dt)
    {
        foreach (var dep in depots)
        {
            dep.stock = Mathf.Min(depotStock, dep.stock + depotRegen * dt);

            Vector3 d = odm.transform.position - dep.pos; d.y = 0f;
            bool near = d.magnitude <= depotRadius && Mathf.Abs(odm.transform.position.y - dep.pos.y) < 30f;
            if (!near) { dep.restocked = false; continue; }

            float need = (1f - odm.GasNormalized) * odm.maxGas;
            float give = Mathf.Min(dep.stock, need, depotFillRate * dt);
            if (give > 0f)
            {
                odm.RefillGas(give);
                dep.stock -= give;
                if (!announcedDepot) { announcedDepot = true; gm.Say("Đang tiếp tế gas..."); }
            }
            if (!dep.restocked && dep.stock > 1f)
            {
                combat.Restock();
                dep.restocked = true;
            }
        }
    }

    void BuildMarkers()
    {
        Markers.Clear();
        foreach (var dep in depots)
        {
            int pct = Mathf.RoundToInt(dep.stock / depotStock * 100f);
            Markers.Add(new Marker { pos = dep.pos, label = "TIẾP TẾ " + pct + "%", color = new Color(0.3f, 1f, 0.6f) });
        }
        if (Current == Phase.Seal)
            Markers.Add(new Marker { pos = map.sealPoint, label = "CỔNG", color = new Color(1f, 0.6f, 0.15f) });
    }
}
