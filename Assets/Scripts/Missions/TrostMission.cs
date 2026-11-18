using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Nhiệm vụ "Trận Trost" (3 giai đoạn):
//  1) Dọn titan đã lọt vào khu phố.
//  2) Titan tràn qua lỗ hổng trên tường (có một Pure Titan khổng lồ).
//  3) Hoá titan, nhấc đá và giữ thăng bằng trên đường tới lỗ hổng.
// Gas không tự hồi: phải chạy tới điểm tiếp tế (cột sáng xanh) để nạp gas và kiếm.
public class TrostMission : MonoBehaviour
{
    public enum Phase { Clear, Reinforce, Approach, Carry, Done }

    public struct Marker { public Vector3 pos; public string label; public Color color; }

    public Phase Current { get; private set; } = Phase.Clear;
    public string ObjectiveText { get; private set; } = "";
    public readonly List<Marker> Markers = new List<Marker>();

    [Header("Số lượng và thời gian")]
    public int clearTitans = 6;
    public int reinforceTitans = 6;
    public float phaseDelay = 4f;
    public float minSpawnDistance = 90f;

    [Header("Bê đá")]
    public float pickupRadius = 12f;
    public float gateRadius = 12f;
    public float rockDiameter = 11f;
    public float balanceForce = 1.25f;
    public float driftAcceleration = 0.7f;
    public float movingDrift = 0.8f;
    public float stoppedDrift = 0.35f;
    public float stoppedRecovery = 0.38f;
    public float failGrace = 0.5f;
    public float Balance { get; private set; }

    [Header("Tiếp tế")]
    public float depotStock = 600f;
    public float depotFillRate = 250f;
    public float depotRegen = 30f;
    public float depotRadius = 9f;

    ODMController odm;
    PlayerCombat combat;
    GameManager gm;
    TrostMap map;
    TitanForm form;

    Health playerHealth;
    float refillSfxTimer;
    public float depotHealRate = 15f;
    float delayTimer;
    bool waiting;
    bool announcedDepot;
    GameObject rock;
    SphereCollider rockCollider;
    float balanceVelocity;
    float driftTarget;
    float driftTimer;
    float outOfBalanceTime;
    Vector3 carryStart;
    bool resetting;
    bool finishing;
    bool rockDropped;

    class Depot { public Vector3 pos; public float stock; }
    readonly List<Depot> depots = new List<Depot>();

    public void Init(GameManager manager, ODMController player, PlayerCombat pc, TrostMap trostMap)
    {
        gm = manager; odm = player; combat = pc; map = trostMap;
        form = odm.GetComponent<TitanForm>();
        depots.Clear();
        foreach (var p in map.depots) depots.Add(new Depot { pos = p, stock = depotStock });

        SubscribeTitanEvents();
        CreateRock();
        SpawnClearWave();
        gm.Say("Trost: titan đã lọt vào khu phố! Gas cạn thì chạy tới cột sáng xanh để tiếp tế.");
    }

    void OnEnable() { SubscribeTitanEvents(); }
    void OnDisable() { Titan.Killed -= OnKilled; }

    void SubscribeTitanEvents()
    {
        Titan.Killed -= OnKilled;
        Titan.Killed += OnKilled;
    }

    bool RestoreRuntimeReferences()
    {
        if (gm == null) gm = FindAnyObjectByType<GameManager>();
        if (odm == null) odm = FindAnyObjectByType<ODMController>();
        if (odm != null && combat == null) combat = odm.GetComponent<PlayerCombat>();
        if (odm != null && form == null) form = odm.GetComponent<TitanForm>();

        if (map == null && odm != null)
        {
            TrostMapAuthoring authored = FindAnyObjectByType<TrostMapAuthoring>();
            if (authored != null)
            {
                map = authored.CreateRuntimeMap(odm.transform.position);
                depots.Clear();
                foreach (Vector3 position in map.depots)
                    depots.Add(new Depot { pos = position, stock = depotStock });
                SubscribeTitanEvents();
                Debug.Log("[TrostMission] Restored runtime map references after script reload.", this);
            }
        }

        return gm != null && odm != null && combat != null && form != null && map != null;
    }

    // ================= SINH TITAN =================
    Titan Spawn(Vector3 pos, TitanModelDef def, float height)
    {
        var t = Titan.Create(pos, height, odm.transform, def);
        t.detectRange = 70f; // trong thành phố, tầm nhìn xa hơn mặc định nhưng vẫn bị nhà chắn và có góc nhìn
        t.loseRange = 200f;
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
        // Compiling scripts during Play Mode clears non-serialized runtime
        // references while this component survives. Rebind from the baked
        // scene metadata instead of throwing once per frame.
        if (!RestoreRuntimeReferences()) return;
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
                else if (Titan.All.Count == 0) Advance(Phase.Approach, "Hoá Titan và tới tảng đá!");
                break;

            case Phase.Approach:
                UpdateApproach();
                break;

            case Phase.Carry:
                UpdateCarry(dt);
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
        if (next == Phase.Approach)
        {
            waiting = false;
            form.PrepareCarryMission();
        }
    }

    void UpdateApproach()
    {
        form.PrepareCarryMission();
        Vector3 d = odm.transform.position - map.rockStart;
        d.y = 0f;
        ObjectiveText = "3/3  Hoá Titan và tới tảng đá (" + Mathf.RoundToInt(d.magnitude) + " m)";
        if (form.IsTitan && d.magnitude <= pickupRadius && form.BeginCarry(map.gate))
        {
            Current = Phase.Carry;
            carryStart = odm.transform.position;
            carryStart.y = map.GroundAt(carryStart);
            Balance = balanceVelocity = outOfBalanceTime = 0f;
            PickNewDrift();
            rockCollider.enabled = false;
            gm.Say("Giữ W để tiến, dùng A/D để giữ thăng bằng!");
        }
    }

    void UpdateCarry(float dt)
    {
        if (resetting || finishing) return;

        bool moving = form.CarryForwardHeld;
        driftTimer -= dt;
        if (driftTimer <= 0f) PickNewDrift();
        float target = driftTarget * (moving ? movingDrift : stoppedDrift);
        balanceVelocity = Mathf.MoveTowards(balanceVelocity, target, driftAcceleration * dt);
        float steer = (GameInput.GetKey(KeyCode.D) || GameInput.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                    - (GameInput.GetKey(KeyCode.A) || GameInput.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        balanceVelocity += steer * balanceForce * dt;
        if (!moving)
        {
            balanceVelocity = Mathf.MoveTowards(balanceVelocity, 0f, driftAcceleration * dt);
            Balance = Mathf.MoveTowards(Balance, 0f, stoppedRecovery * dt);
        }
        Balance = Mathf.Clamp(Balance + balanceVelocity * dt, -1f, 1f);
        Debug.Assert(Balance >= -1f && Balance <= 1f, "Balance must stay in [-1, 1]");
        form.SetCarryBalance(Balance);
        UpdateCarriedRock();

        outOfBalanceTime = Mathf.Abs(Balance) >= 0.999f ? outOfBalanceTime + dt : 0f;
        if (outOfBalanceTime > failGrace) { StartCoroutine(ResetCarry()); return; }

        Vector3 d = odm.transform.position - map.gate;
        d.y = 0f;
        ObjectiveText = "3/3  Bê đá tới cổng (" + Mathf.RoundToInt(d.magnitude) + " m)";
        if (d.magnitude <= gateRadius) StartCoroutine(FinishCarry());
    }

    void PickNewDrift()
    {
        float sign = Random.value < 0.5f ? -1f : 1f;
        driftTarget = sign * Random.Range(0.45f, 1f);
        driftTimer = Random.Range(0.6f, 1.25f);
    }

    IEnumerator ResetCarry()
    {
        resetting = true;
        rockDropped = true;
        form.PauseCarry(true);
        form.AddCarryImpact(1f);
        GameAudio.Play(Sfx.Thud, rock.transform.position, 1f, 0.8f);
        rock.transform.position = new Vector3(rock.transform.position.x, map.GroundAt(rock.transform.position) + rockDiameter * 0.5f, rock.transform.position.z);

        const float fadeTime = 0.65f;
        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            if (GameHud.Current != null) GameHud.Current.Flash(Color.black, Mathf.Sin(t / fadeTime * Mathf.PI));
            yield return null;
        }

        form.ResetCarryPosition(carryStart);
        Balance = balanceVelocity = outOfBalanceTime = 0f;
        form.SetCarryBalance(0f);
        PickNewDrift();
        rockDropped = false;
        UpdateCarriedRock();
        form.PauseCarry(false);
        resetting = false;
    }

    IEnumerator FinishCarry()
    {
        finishing = true;
        form.PauseCarry(true);
        rock.transform.position = map.gate + Vector3.up * (rockDiameter * 0.5f);
        rock.transform.rotation = Quaternion.identity;
        rockCollider.enabled = true;
        GameAudio.Play(Sfx.Thud, map.gate, 1f, 0.7f);
        GameVfx.Burst(map.gate + Vector3.up, new Color(0.55f, 0.5f, 0.43f, 0.85f), 80, 12f, 1.2f, 1.5f);
        form.AddCarryImpact(1f);
        ObjectiveText = "Đã lấp lỗ hổng Trost!";
        yield return new WaitForSeconds(0.65f);
        form.EndCarry();
        Current = Phase.Done;
        gm.EndGame(true);
    }

    void UpdateCarriedRock()
    {
        if (rockDropped) return;
        Transform t = odm.transform;
        rock.transform.position = t.position + t.forward * (rockDiameter * 0.4f) + Vector3.up * (rockDiameter * 0.85f);
        rock.transform.rotation = t.rotation * Quaternion.Euler(0f, 0f, -Balance * 5f);
    }

    void CreateRock()
    {
        rock = new GameObject("MissionBoulder");
        rock.transform.SetParent(map.root != null ? map.root.transform : null, true);
        rock.transform.position = map.rockStart + Vector3.up * (rockDiameter * 0.5f);
        GameObject visual;
        if (map.rockPrefab != null)
        {
            visual = Instantiate(map.rockPrefab, rock.transform);
            visual.name = "RockVisual";
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "RockVisual_Fallback";
            visual.transform.SetParent(rock.transform, false);
            Destroy(visual.GetComponent<Collider>());
            visual.transform.localScale = new Vector3(1f, 0.9f, 1.08f);
            Renderer fallbackRenderer = visual.GetComponent<Renderer>();
            fallbackRenderer.material.color = new Color(0.38f, 0.35f, 0.33f);
        }

        foreach (Transform child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = false;
        foreach (Collider childCollider in visual.GetComponentsInChildren<Collider>(true)) Destroy(childCollider);

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            visual.transform.localScale *= rockDiameter / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            visual.transform.position += rock.transform.position - bounds.center;
        }
        rockCollider = rock.AddComponent<SphereCollider>();
        rockCollider.radius = rockDiameter * 0.5f;
    }

    // ================= TIẾP TẾ =================
    void UpdateDepots(float dt)
    {
        foreach (var dep in depots)
        {
            dep.stock = Mathf.Min(depotStock, dep.stock + depotRegen * dt);

            Vector3 d = odm.transform.position - dep.pos; d.y = 0f;
            bool near = d.magnitude <= depotRadius && Mathf.Abs(odm.transform.position.y - dep.pos.y) < 30f;
            if (!near) continue;

            float need = (1f - odm.GasNormalized) * odm.maxGas;
            float give = Mathf.Min(dep.stock, need, depotFillRate * dt);
            if (give > 0f)
            {
                odm.RefillGas(give);
                dep.stock -= give;
                if (!announcedDepot) { announcedDepot = true; gm.Say("Đang tiếp tế gas..."); }
                refillSfxTimer -= dt;
                if (refillSfxTimer <= 0f) { refillSfxTimer = 0.6f; GameAudio.Play2D(Sfx.Refill, 0.6f); }
            }

            // Điểm tiếp tế cũng băng bó: hồi dần máu khi đứng trong vùng
            if (playerHealth == null) playerHealth = odm.GetComponent<Health>();
            if (playerHealth != null && playerHealth.Current < playerHealth.maxHealth) playerHealth.Heal(depotHealRate * dt);
        }
    }

    void BuildMarkers()
    {
        Markers.Clear();
        foreach (var dep in depots)
        {
            if (Current == Phase.Carry) break;
            int pct = Mathf.RoundToInt(dep.stock / depotStock * 100f);
            Markers.Add(new Marker { pos = dep.pos, label = "TIẾP TẾ " + pct + "%", color = new Color(0.3f, 1f, 0.6f) });
        }
        if (Current == Phase.Approach)
            Markers.Add(new Marker { pos = map.rockStart, label = "TẢNG ĐÁ", color = new Color(1f, 0.72f, 0.2f) });
        else if (Current == Phase.Carry)
            Markers.Add(new Marker { pos = map.gate, label = "CỔNG", color = new Color(1f, 0.6f, 0.15f) });
    }
}
