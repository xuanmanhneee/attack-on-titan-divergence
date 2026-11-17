using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// VFX hoá titan: sét vàng toả ra từ chỗ đưa tay lên miệng, mạnh dần tới chói sáng che kín màn hình.
//   - Tia sét HDR (nhân sáng + viền mềm bằng texture) có nhánh con, kết hợp Bloom của URP nên phát sáng thật.
//   - Quả cầu năng lượng phình dần ở trước mặt, tia lửa bắn ra mọi hướng, camera rung và quang sai màu tăng theo.
//   - Cuối cùng trắng xoá toàn màn hình (canvas FX của GameHud), giữ một chút rồi mờ khi thế giới chạy lại.
// Không có âm thanh: tiếng do TitanForm phát (transform_sfx, nhạc titan, tiếng gầm).
// Vật liệu cộng sáng: Resources/Vfx/VfxAdditive (URP Particles/Unlit, additive). Texture mềm sinh bằng code.
// TitanForm gọi TransformLightning.Play(...) khi hết đoạn cắn tay; totalTime = thời gian tới lúc trắng xoá hoàn toàn.
public class TransformLightning : MonoBehaviour
{
    public static TransformLightning Play(Vector3 center, float totalTime)
    {
        var go = new GameObject("TransformLightning");
        go.transform.position = center;
        var t = go.AddComponent<TransformLightning>();
        t.total = Mathf.Max(0.8f, totalTime);
        return t;
    }

    public const float FullWhiteAt = 0.85f; // tỉ lệ đoạn sét mà màn hình trắng kín
    public static readonly Color WhiteColor = new Color(1f, 0.97f, 0.8f);
    static readonly Color GlowTint = new Color(1.6f, 0.8f, 0.1f);   // HDR: vàng cam, > 1 để Bloom bắt
    static readonly Color CoreTint = new Color(6f, 5f, 2.2f);         // lõi trắng vàng rất sáng
    static readonly Color SparkTint = new Color(5f, 3f, 0.6f);
    static readonly Color OrbTint = new Color(3.2f, 1.9f, 0.35f);

    float total;
    float time;
    float nextSpawn;
    float nextSky = 0.35f;
    float impact;              // chớp ánh sáng/rung do sét đánh trời xuống, tự giảm dần
    float spike;
    Light flashLight;
    Material coreMat, glowMat, sparkMat, orbMat;
    Texture2D lineTex, dotTex;
    ParticleSystem sparks;
    ParticleSystem.EmissionModule sparkEmission;
    ParticleSystem.ShapeModule sparkShape;
    Volume volume;
    VolumeProfile profile;
    Bloom bloom;
    ChromaticAberration chroma;
    Transform camT;
    Quaternion camBaseRot;
    bool shakeActive;
    readonly List<Bolt> bolts = new List<Bolt>();

    class Bolt
    {
        public LineRenderer core, glow;
        public float life, maxLife;
        public Vector3[] basePts;
    }

    void Awake()
    {
        camT = Camera.main != null ? Camera.main.transform : null;
        if (camT != null) { camBaseRot = camT.localRotation; shakeActive = true; }

        lineTex = MakeLineTexture();
        dotTex = MakeDotTexture();
        Material baseMat = Resources.Load<Material>("Vfx/VfxAdditive");
        coreMat = MakeMat(baseMat, lineTex, CoreTint);
        glowMat = MakeMat(baseMat, lineTex, GlowTint);
        sparkMat = MakeMat(baseMat, dotTex, SparkTint);
        orbMat = MakeMat(baseMat, dotTex, OrbTint);

        var lg = new GameObject("FlashLight");
        lg.transform.SetParent(transform, false);
        flashLight = lg.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.range = 70f;
        flashLight.color = new Color(1f, 0.75f, 0.2f);
        flashLight.intensity = 0f;
        flashLight.shadows = LightShadows.None;

        BuildSparks();
        BuildOrb();
        BuildPostProcess();
    }

    static Material MakeMat(Material template, Texture2D tex, Color hdr)
    {
        Material m;
        if (template != null) m = new Material(template);
        else
        {
            // Dự phòng nếu thiếu asset: shader cộng sáng có sẵn của Unity (không có HDR nên kém sáng hơn).
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
            m = new Material(sh);
        }
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", hdr);
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", hdr);
        m.color = hdr;
        return m;
    }

    // Mặt cắt ngang của tia: lõi sáng, mờ dần ra hai bên (u dọc tia, v ngang tia).
    static Texture2D MakeLineTexture()
    {
        const int h = 64;
        var tex = new Texture2D(2, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++)
        {
            float d = (y + 0.5f) / h * 2f - 1f;                 // -1..1
            float a = Mathf.Exp(-d * d * 5f) * Mathf.Clamp01((1f - Mathf.Abs(d)) * 4f);
            for (int x = 0; x < 2; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }

    static Texture2D MakeDotTexture()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r2 = dx * dx + dy * dy;
                float a = Mathf.Clamp01(1f - r2) * Mathf.Exp(-r2 * 2.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    // ---------- Hạt ----------
    void BuildSparks()
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(transform, false);
        sparks = go.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.playOnAwake = false;
        main.loop = true;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 30f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.16f);
        main.maxParticles = 800;
        sparkEmission = sparks.emission;
        sparkEmission.rateOverTime = 0f;
        sparkShape = sparks.shape;
        sparkShape.shapeType = ParticleSystemShapeType.Sphere;
        sparkShape.radius = 3.5f;
        var col = sparks.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.6f, 0.2f), 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var r = sparks.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 2.5f;
        r.sharedMaterial = sparkMat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        sparks.Play();
    }

    // Quả cầu năng lượng: một hạt lớn phình dần suốt đoạn sét.
    void BuildOrb()
    {
        var go = new GameObject("Orb");
        go.transform.SetParent(transform, false);
        go.transform.position = Origin();
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.useUnscaledTime = true;
        main.duration = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = total + 0.6f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.maxParticles = 1;
        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        var shape = ps.shape; shape.enabled = false;
        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.1f), new Keyframe(0.5f, 0.5f), new Keyframe(1f, 2.5f)));
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sharedMaterial = orbMat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        ps.Play();
    }

    // ---------- Hậu kỳ: Bloom + quang sai màu tăng dần (Volume toàn cục, chạy bằng code) ----------
    void BuildPostProcess()
    {
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0.25f);
        bloom.scatter.Override(0.75f);
        bloom.tint.Override(new Color(1f, 0.85f, 0.5f));
        chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0f);

        var go = new GameObject("TransformPost");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.sharedProfile = profile;
        volume.weight = 0f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        time += dt;
        float p = Mathf.Clamp01(time / total);
        float k = Mathf.Lerp(0.3f, 1f, Mathf.Pow(p, 1.2f)); // có sẵn khá nhiều từ đầu, dồn dập dần về cuối

        if (time < total && time >= nextSpawn)
        {
            int count = 2 + (int)(k * 7f);
            for (int i = 0; i < count; i++) SpawnRadialBolt(k);
            spike = Mathf.Max(spike, Mathf.Lerp(0.1f, 0.4f, k));
            nextSpawn = time + Mathf.Lerp(0.1f, 0.015f, k) * Random.Range(0.7f, 1.3f);
        }

        // Sét đánh từ trên trời xuống: to hơn, thưa hơn, dồn dần.
        if (time < total && time >= nextSky)
        {
            SpawnSkyBolt(k);
            nextSky = time + Mathf.Lerp(0.45f, 0.14f, k) * Random.Range(0.7f, 1.4f);
        }
        impact = Mathf.Max(0f, impact - dt * 4f);

        // Tia chớp tắt chập chờn rồi biến mất.
        for (int i = bolts.Count - 1; i >= 0; i--)
        {
            Bolt b = bolts[i];
            b.life -= dt;
            if (b.life <= 0f) { Destroy(b.glow.gameObject); bolts.RemoveAt(i); continue; }
            float a = Mathf.Clamp01(b.life / b.maxLife * 1.6f) * (Random.value < 0.3f ? 0.35f : 1f);
            SetBoltAlpha(b, a);
            if (Random.value < 0.5f) Jitter(b);
        }

        // Tia lửa bắn ra từ chỗ miệng, nhiều dần.
        if (sparks != null)
        {
            sparks.transform.position = camT != null ? camT.position : transform.position;
            sparkEmission.rateOverTime = time < total ? Mathf.Lerp(30f, 400f, k) : 0f;
        }

        // Hậu kỳ.
        if (volume != null)
        {
            float vw = time < total ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p * 1.6f)) : Mathf.Clamp01(1f - (time - total) * 4f);
            volume.weight = vw;
            bloom.intensity.value = Mathf.Lerp(0.25f, 3.5f, p * p);
            chroma.intensity.value = Mathf.Lerp(0f, 0.5f, k);
        }

        // Rung camera nhẹ tăng dần (ngoài trục Y vị trí vì TitanForm đang điều khiển).
        if (shakeActive && camT != null)
        {
            if (time < total)
            {
                float amp = Mathf.Lerp(0f, 1.6f, Mathf.InverseLerp(0.15f, 1f, k)) + 2.5f * impact;
                camT.localRotation = camBaseRot * Quaternion.Euler(Random.Range(-amp, amp), Random.Range(-amp, amp), Random.Range(-amp, amp) * 1.5f);
            }
            else { camT.localRotation = camBaseRot; shakeActive = false; }
        }

        // Chói sáng: từ 45% thời gian trắng dần, kín màn hình từ 85%; sau đó giữ chút rồi mờ khi thế giới chạy lại.
        float white = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, FullWhiteAt, p)); // kín từ FullWhiteAt tới hết (TitanForm hiện thân titan lúc này)
        if (time > total) white = Mathf.Clamp01(1.3f - (time - total) * 0.9f);
        spike = Mathf.Max(0f, spike - dt * 3f);
        float flash = Mathf.Max(white, spike, impact * 0.6f);
        if (flash > 0.01f && GameHud.Current != null) GameHud.Current.Flash(WhiteColor, flash);

        if (impact < 0.05f) flashLight.transform.position = LightPos();
        float target = 500f * k * (Random.value < 0.4f ? 0.5f : 1f) + 3000f * impact;
        // Hết đoạn sét là tắt hẳn đèn chớp: nếu để tắt dần thì thân titan (vừa hiện) bị rọi sáng ~1 giây.
        flashLight.intensity = time < total ? target : 0f;

        if (time > total + 2f && bolts.Count == 0) Destroy(gameObject);
    }

    // Gốc của tia: ngay trước mặt, chỗ bàn tay đưa lên miệng.
    Vector3 Origin()
    {
        Transform cam = camT != null ? camT : transform;
        return cam.position + cam.forward * 0.5f - cam.up * 0.25f;
    }

    Vector3 LightPos() => Origin() + (camT != null ? camT.forward : Vector3.forward) * 1.5f;

    // ---------- Tia ----------
    static Vector3[] BuildPath(Vector3 start, Vector3 end, int passes)
    {
        float length = (end - start).magnitude;
        var pts = new List<Vector3> { start, end };
        float offset = length * 0.14f;
        for (int pass = 0; pass < passes; pass++)
        {
            var next = new List<Vector3>(pts.Count * 2);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 perp = Vector3.Cross((b - a).normalized, Random.onUnitSphere).normalized;
                next.Add(a);
                next.Add((a + b) * 0.5f + perp * Random.Range(-offset, offset));
            }
            next.Add(pts[pts.Count - 1]);
            pts = next;
            offset *= 0.5f;
        }
        return pts.ToArray();
    }

    void SpawnRadialBolt(float k)
    {
        Camera cam = Camera.main;
        Transform ct = camT != null ? camT : transform;
        Vector3 start, dir;
        float length;
        if (cam == null || Random.value < 0.3f)
        {
            // Từ chỗ tay đưa lên miệng, toả rất rộng ra mọi hướng.
            start = Origin();
            dir = Vector3.Slerp(ct.forward, Random.onUnitSphere, Random.Range(0.5f, 1f)).normalized;
            length = Mathf.Lerp(5f, 30f, k) * Random.Range(0.6f, 1.2f);
        }
        else
        {
            // Rải khắp màn hình: điểm bắt đầu ở vị trí ngẫu nhiên trong khung nhìn, hướng chéo ngang tầm nhìn nên thấy rõ cả tia.
            start = cam.ViewportToWorldPoint(new Vector3(Random.Range(-0.1f, 1.1f), Random.Range(-0.1f, 1.1f), Random.Range(4f, 20f)));
            dir = Random.onUnitSphere;
            dir = (dir - ct.forward * Vector3.Dot(dir, ct.forward) * 0.7f).normalized;
            length = Mathf.Lerp(4f, 26f, k) * Random.Range(0.6f, 1.3f);
        }
        float width = Mathf.Lerp(0.05f, 0.28f, k) * Random.Range(0.7f, 1.3f);
        float life = Random.Range(0.1f, 0.22f);

        Vector3[] main = BuildPath(start, start + dir * length, 5);
        AddBolt(main, width, life);

        // Nhánh con rẽ ra từ giữa thân tia chính.
        int forks = k > 0.25f ? Random.Range(1, 3) : 0;
        for (int f = 0; f < forks; f++)
        {
            int at = Random.Range(main.Length / 5, main.Length * 4 / 5);
            Vector3 fdir = Vector3.Slerp(dir, Random.onUnitSphere, Random.Range(0.3f, 0.6f)).normalized;
            Vector3[] fork = BuildPath(main[at], main[at] + fdir * length * Random.Range(0.25f, 0.5f), 4);
            AddBolt(fork, width * 0.55f, life * 0.85f);
        }
    }

    // Sét đánh thẳng từ trời xuống đất trong tầm nhìn: cùng kiểu tia nhưng dày hơn nhiều, nhiều nhánh, có nổ tia lửa ở chỗ chạm đất.
    void SpawnSkyBolt(float k)
    {
        Transform ct = camT != null ? camT : transform;
        Vector3 fwd = ct.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        // Điểm đánh: trước mặt, lệch trái/phải trong khoảng nhìn, cách 18-80m.
        Vector3 flat = Quaternion.Euler(0f, Random.Range(-55f, 55f), 0f) * fwd * Random.Range(18f, 80f);
        Vector3 ground = ct.position + flat;
        RaycastHit hit;
        if (Physics.Raycast(ground + Vector3.up * 200f, Vector3.down, out hit, 400f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point;
        else ground.y = ct.position.y - 1.7f;

        Vector3 top = ground + Vector3.up * 160f + new Vector3(Random.Range(-18f, 18f), 0f, Random.Range(-18f, 18f));
        float width = Mathf.Lerp(0.5f, 1.5f, k) * Random.Range(0.8f, 1.2f);
        float life = Random.Range(0.22f, 0.4f);
        Vector3[] main = BuildPath(top, ground, 6);
        AddBolt(main, width, life);
        int forks = Random.Range(2, 5);
        for (int f = 0; f < forks; f++)
        {
            int at = Random.Range(main.Length / 6, main.Length * 5 / 6);
            Vector3 fdir = Vector3.Slerp(Vector3.down, Random.onUnitSphere, Random.Range(0.35f, 0.7f)).normalized;
            AddBolt(BuildPath(main[at], main[at] + fdir * Random.Range(15f, 45f), 4), width * 0.4f, life * 0.8f);
        }

        // Chạm đất: loé sáng, rung, bắn tia lửa.
        impact = Mathf.Max(impact, Mathf.Lerp(0.5f, 1f, k));
        flashLight.transform.position = ground + Vector3.up * 5f;
        if (sparks != null)
        {
            for (int i = 0; i < 30; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = ground + Vector3.up * 0.3f,
                    velocity = new Vector3(Random.Range(-12f, 12f), Random.Range(6f, 22f), Random.Range(-12f, 12f)),
                    startLifetime = Random.Range(0.4f, 0.9f),
                    startSize = Random.Range(0.1f, 0.3f)
                };
                sparks.Emit(ep, 1);
            }
        }
    }

    void AddBolt(Vector3[] pts, float width, float life)
    {
        var bolt = new Bolt { maxLife = life, life = life, basePts = pts };
        bolt.glow = MakeLine("Glow", pts, width * 2.2f, glowMat);
        bolt.core = MakeLine("Core", pts, width * 0.9f, coreMat);
        bolt.core.transform.SetParent(bolt.glow.transform, false);
        SetBoltAlpha(bolt, 1f);
        bolts.Add(bolt);
    }

    LineRenderer MakeLine(string n, Vector3[] pts, float width, Material mat)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = mat;
        lr.useWorldSpace = true;
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, width * 0.3f));
        lr.numCapVertices = 3;
        lr.numCornerVertices = 2;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    void SetBoltAlpha(Bolt b, float a)
    {
        Color c = new Color(1f, 1f, 1f, a);
        b.glow.startColor = b.glow.endColor = c;
        b.core.startColor = b.core.endColor = c;
    }

    void Jitter(Bolt b)
    {
        var p = new Vector3[b.basePts.Length];
        for (int i = 0; i < p.Length; i++)
            p[i] = b.basePts[i] + (i == 0 ? Vector3.zero : Random.insideUnitSphere * 0.3f);
        b.glow.SetPositions(p);
        b.core.SetPositions(p);
    }

    void OnDestroy()
    {
        if (shakeActive && camT != null) camT.localRotation = camBaseRot;
        if (coreMat != null) Destroy(coreMat);
        if (glowMat != null) Destroy(glowMat);
        if (sparkMat != null) Destroy(sparkMat);
        if (orbMat != null) Destroy(orbMat);
        if (lineTex != null) Destroy(lineTex);
        if (dotTex != null) Destroy(dotTex);
        if (profile != null) Destroy(profile);
    }
}
