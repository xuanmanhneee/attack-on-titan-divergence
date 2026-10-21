using System;
using UnityEngine;

// Chiến đấu bằng kiếm. F (hoặc chuột giữa) = chém. R = thay kiếm.
// Chém đúng gáy trong tầm và góc nhìn => hạ gục ngay, không phụ thuộc tốc độ bay.
// Chém thân chỉ gây sát thương nhỏ.
// Mỗi nhát chém làm mòn kiếm; hết độ bền phải thay kiếm (có số lượng kiếm dự phòng).
[RequireComponent(typeof(ODMController))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Tầm và góc chém")]
    public float napeReach = 10f;
    public float bodyReach = 5f;
    public float slashAngle = 80f;

    [Header("Sát thương")]
    public float bodyDamage = 8f;

    [Header("Kiếm")]
    public float slashCooldown = 0.45f;
    public float durabilityPerSlash = 12f;
    public float durabilityOnNapeKill = 5f;
    public int spareBlades = 3;
    public float reloadTime = 1.2f;

    public static event Action<string> Feedback;

    public float Durability { get; private set; } = 100f;
    public int Spare => spareBlades;
    public bool IsReloading { get; private set; }

    ODMController odm;
    float cooldown;
    float reloadTimer;

    void Awake()
    {
        odm = GetComponent<ODMController>();
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        float dt = Time.deltaTime;
        cooldown -= dt;

        if (IsReloading)
        {
            reloadTimer -= dt;
            if (reloadTimer <= 0f)
            {
                IsReloading = false;
                Durability = 100f;
                spareBlades--;
                Feedback?.Invoke("Đã thay kiếm mới");
            }
            return;
        }

        if (GameInput.GetKeyDown(KeyCode.R)) StartReload();
        if (GameInput.GetKeyDown(KeyCode.F) || GameInput.GetMouseButtonDown(2)) Slash();
    }

    // Gọi khi tới điểm tiếp tế: đầy kiếm và đủ kiếm dự phòng
    public void Restock()
    {
        Durability = 100f;
        spareBlades = 3;
        IsReloading = false;
    }

    void StartReload()
    {
        if (Durability >= 99f) return;
        if (spareBlades <= 0) { Feedback?.Invoke("Hết kiếm dự phòng!"); return; }
        IsReloading = true;
        reloadTimer = reloadTime;
    }

    void Slash()
    {
        if (cooldown > 0f) return;
        if (Durability <= 0f) { Feedback?.Invoke("Kiếm gãy! Nhấn R để thay"); return; }
        cooldown = slashCooldown;

        Transform cam = odm.cameraTransform;

        foreach (Titan t in Titan.All.ToArray())
        {
            if (t == null || t.IsDead) continue;

            // 1) Gáy
            Vector3 toNape = t.Nape.position - cam.position;
            if (toNape.magnitude <= napeReach && Vector3.Angle(cam.forward, toNape) <= slashAngle)
            {
                t.KillByNape();
                Durability -= durabilityOnNapeKill;
                Feedback?.Invoke("CHÉM GÁY! Hạ gục titan");
                continue;
            }

            // 2) Thân
            foreach (Collider col in t.BodyColliders)
            {
                if (col == null) continue;
                Vector3 p = col.ClosestPoint(cam.position);
                Vector3 to = p - cam.position;
                if (to.magnitude <= bodyReach && Vector3.Angle(cam.forward, to) <= slashAngle)
                {
                    t.Damage(bodyDamage);
                    Durability -= durabilityPerSlash;
                    break;
                }
            }
        }

        Durability = Mathf.Max(0f, Durability);
        if (Durability <= 0f) Feedback?.Invoke("Kiếm gãy! Nhấn R để thay");
    }

}
