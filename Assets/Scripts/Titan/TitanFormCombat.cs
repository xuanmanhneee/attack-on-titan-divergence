using UnityEngine;

// Đấm của người chơi khi là titan: đấm nhanh/mạnh, khung sát thương theo clip, tính trúng. Tách từ TitanForm.
// Không phải component: TitanForm tạo và gọi Update mỗi khung ở dạng titan; số liệu đấm vẫn là field public của TitanForm.
class TitanFormCombat
{
    readonly TitanForm f;
    public TitanFormCombat(TitanForm form) { f = form; }

    // Các đoạn trong clip Mixamo "Punching" (30 fps): khung bắt đầu, khung trúng đích, khung kết thúc.
    // Cú đấm đơn luân phiên: trái - phải - trái - phải. Chuột phải phát chuỗi 4 cú liên hoàn.
    const float ClipFps = 30f;
    static readonly Vector3Int[] lightSegments =
    {
        new Vector3Int(8, 14, 19),   // trái
        new Vector3Int(29, 34, 39),  // phải
        new Vector3Int(50, 55, 59),  // trái
        new Vector3Int(39, 43, 49),  // phải
    };
    const int comboStart = 50, comboEnd = 90;
    static readonly int[] comboHits = { 55, 62, 69, 76 };
    int lightIndex;
    float[] pendingHits = new float[0];
    int nextHit;
    bool clipHeavy;

    float punchT = 1f;
    float punchLen;
    bool punchHeavy;
    bool punchRightArm;
    bool punchHitDone;
    float punchCooldownLeft;

    public void Reset()
    {
        punchT = 1f;
        punchCooldownLeft = 0f;
        pendingHits = new float[0];
        nextHit = 0;
    }

    public void Update(float dt)
    {
        if (f.visual.punchAnim != null) { ClipPunch(); return; }
        punchCooldownLeft -= dt;

        if (punchT < 1f)
        {
            punchT = Mathf.Min(1f, punchT + dt / punchLen);
            if (!punchHitDone && punchT >= 0.4f)
            {
                punchHitDone = true;
                ResolvePunch(f.punchDamage * (punchHeavy ? f.heavyMultiplier : 1f), f.punchReach * (punchHeavy ? 1.15f : 1f));
            }
        }
        else if (punchCooldownLeft <= 0f)
        {
            bool light = GameInput.GetMouseButtonDown(0) || GameInput.GetKeyDown(KeyCode.F);
            bool heavy = GameInput.GetMouseButtonDown(1);
            if (light || heavy) StartPunch(heavy);
        }

        AnimateFists();
    }

    void StartPunch(bool heavy)
    {
        punchHeavy = heavy;
        punchLen = heavy ? f.heavyPunchTime : f.punchTime;
        punchT = 0f;
        punchHitDone = false;
        punchCooldownLeft = punchLen + f.punchCooldown;
        if (!heavy) punchRightArm = !punchRightArm;
    }

    // Đấm bằng clip: gây sát thương đúng khung nắm đấm duỗi hết. Cú đơn được nối ngay sau khi trúng
    // (bấm liên tục ra combo trái-phải), chuỗi liên hoàn thì phải đánh hết mới ra đòn mới.
    void ClipPunch()
    {
        while (nextHit < pendingHits.Length && f.visual.punchAnim.CurrentTime >= pendingHits[nextHit])
        {
            nextHit++;
            if (clipHeavy) ResolvePunch(f.punchDamage * f.heavyMultiplier * 0.5f, f.punchReach * 1.1f);
            else ResolvePunch(f.punchDamage, f.punchReach);
        }

        bool free = !f.visual.punchAnim.Playing || (!clipHeavy && nextHit >= pendingHits.Length);
        if (!free) return;

        if (GameInput.GetMouseButtonDown(1))
        {
            clipHeavy = true;
            pendingHits = new float[comboHits.Length];
            for (int i = 0; i < comboHits.Length; i++) pendingHits[i] = comboHits[i] / ClipFps;
            nextHit = 0;
            f.visual.punchAnim.Play(comboStart / ClipFps, comboEnd / ClipFps, f.comboAnimSpeed);
        }
        else if (GameInput.GetMouseButtonDown(0) || GameInput.GetKeyDown(KeyCode.F))
        {
            Vector3Int seg = lightSegments[lightIndex];
            lightIndex = (lightIndex + 1) % lightSegments.Length;
            clipHeavy = false;
            pendingHits = new[] { seg.y / ClipFps };
            nextHit = 0;
            f.visual.punchAnim.Play(seg.x / ClipFps, seg.z / ClipFps, f.punchAnimSpeed);
        }
    }

    void ResolvePunch(float damage, float reach)
    {
        bool hitAny = false;

        foreach (Titan t in Titan.All.ToArray())
        {
            if (t == null || t.IsDead) continue;

            // Gáy: đòn chí mạng
            Vector3 toNape = t.Nape.position - f.cam.position;
            if (toNape.magnitude <= reach + 1.5f && Vector3.Angle(f.cam.forward, toNape) <= f.punchAngle * 0.6f)
            {
                t.Damage(damage * f.napeMultiplier);
                hitAny = true;
                continue;
            }

            foreach (Collider col in t.BodyColliders)
            {
                if (col == null) continue;
                Vector3 to = col.ClosestPoint(f.cam.position) - f.cam.position;
                if (to.magnitude <= reach && Vector3.Angle(f.cam.forward, to) <= f.punchAngle)
                {
                    t.Damage(damage);
                    hitAny = true;
                    break;
                }
            }
        }

        if (hitAny) f.flash = Mathf.Max(f.flash, 0.12f);
    }

    // Tay đấm bằng animation Humanoid của model: giơ tay rồi đập xuống, luân phiên trái/phải.
    void AnimateFists()
    {
        if (f.visual.proc == null) return;

        float raise = 0f, slam = 0f;
        if (punchT < 1f)
        {
            if (punchT < 0.4f) raise = punchT / 0.4f;
            else slam = 1f - (punchT - 0.4f) / 0.6f;
        }
        f.visual.proc.attackRaise = raise;
        f.visual.proc.attackSlam = slam;
        f.visual.proc.attackMaskL = punchHeavy || !punchRightArm ? 1f : 0f;
        f.visual.proc.attackMaskR = punchHeavy || punchRightArm ? 1f : 0f;
    }
}
