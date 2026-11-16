using System.Collections.Generic;
using UnityEngine;

// VFX hoá titan: vài tia sét nhỏ chớp quanh người kèm sấm xa, rồi một cú sét lớn đánh xuống
// và loá trắng màn hình. Toàn bộ dựng bằng code (LineRenderer + âm thanh tổng hợp), không cần asset.
// TitanForm gọi TransformLightning.Play(...) lúc bắt đầu biến hình.
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

    public System.Action onBigStrike; // gọi đúng lúc sét lớn đánh xuống (TitanForm đổi nhạc ở đây)

    float total;
    float time;
    float screenFlash;          // 0..1, vẽ trắng toàn màn hình
    float nextSmall;
    int smallCount;
    bool bigDone;
    float bigAt;
    Light flashLight;
    AudioSource audioSrc;
    Material boltMat;
    readonly List<Bolt> bolts = new List<Bolt>();

    class Bolt
    {
        public LineRenderer core, glow;
        public float life, maxLife;
        public Vector3[] basePts;
        public float width;
    }

    void Awake()
    {
        boltMat = new Material(Shader.Find("Sprites/Default"));
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.spatialBlend = 0f;
        audioSrc.playOnAwake = false;

        var lg = new GameObject("FlashLight");
        lg.transform.SetParent(transform, false);
        flashLight = lg.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.range = 80f;
        flashLight.color = new Color(0.8f, 0.9f, 1f);
        flashLight.intensity = 0f;
        flashLight.shadows = LightShadows.None;
    }

    void Start()
    {
        bigAt = total * 0.62f;
        nextSmall = 0.05f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        time += dt;

        // Sét nhỏ: chớp dồn dần trước cú đánh lớn.
        if (!bigDone && time >= nextSmall && time < bigAt - 0.1f)
        {
            SmallStrike();
            smallCount++;
            nextSmall = time + Mathf.Lerp(0.28f, 0.12f, Mathf.Clamp01(time / bigAt)) * Random.Range(0.7f, 1.3f);
        }

        if (!bigDone && time >= bigAt)
        {
            bigDone = true;
            BigStrike();
        }

        // Cập nhật tia: chớp tắt chập chờn rồi biến mất.
        for (int i = bolts.Count - 1; i >= 0; i--)
        {
            Bolt b = bolts[i];
            b.life -= dt;
            if (b.life <= 0f) { Destroy(b.core.gameObject); bolts.RemoveAt(i); continue; }
            float k = b.life / b.maxLife;
            float flicker = Random.value < 0.35f ? 0.25f : 1f;
            float a = Mathf.Clamp01(k * 1.6f) * flicker;
            SetBoltAlpha(b, a);
            if (Random.value < 0.5f) Jitter(b); // tia run nhẹ
        }

        screenFlash = Mathf.Max(0f, screenFlash - dt * 1.1f);
        if (screenFlash > 0.01f && GameHud.Current != null) GameHud.Current.Flash(new Color(0.92f, 0.95f, 1f), screenFlash); // loá trắng màn hình (canvas FX của GameHud)
        flashLight.intensity = Mathf.Max(0f, flashLight.intensity - dt * 120f);

        if (bigDone && time > bigAt + 2.5f && bolts.Count == 0 && !audioSrc.isPlaying) Destroy(gameObject);
    }

    // ================= SÉT =================
    void SmallStrike()
    {
        float ang = Random.value * Mathf.PI * 2f;
        float dist = Random.Range(25f, 55f);
        Vector3 ground = transform.position + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
        ground.y = transform.position.y - 5f;
        SpawnBolt(ground, Random.Range(60f, 90f), 0.35f, 0.18f, 3);

        float v = Random.Range(0.25f, 0.5f);
        screenFlash = Mathf.Max(screenFlash, Random.Range(0.12f, 0.3f));
        flashLight.intensity = 40f;
        flashLight.transform.position = ground + Vector3.up * 20f;
        PlayThunder(v, Random.Range(0.8f, 1.25f), 0.8f + dist * 0.01f, false);
    }

    void BigStrike()
    {
        Vector3 fwd = Camera.main != null ? Camera.main.transform.forward : transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        Vector3 ground = transform.position + fwd.normalized * 5f;
        ground.y = transform.position.y - 5f;

        SpawnBolt(ground, 110f, 0.9f, 0.9f, 6);
        // Hai nhánh phụ cho dày.
        SpawnBolt(ground + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f)), 100f, 0.6f, 0.6f, 4);
        SpawnBolt(ground + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f)), 100f, 0.5f, 0.5f, 4);

        screenFlash = 1.6f;                       // >1 để trắng xoá lâu hơn một chút trước khi mờ
        flashLight.transform.position = ground + Vector3.up * 8f;
        flashLight.intensity = 400f;
        PlayThunder(1f, 0.7f, 0.05f, true);
        onBigStrike?.Invoke();
    }

    void SpawnBolt(Vector3 ground, float height, float width, float life, int detail)
    {
        Vector3 top = ground + Vector3.up * height + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
        var pts = new List<Vector3> { top, ground };
        float offset = height * 0.18f;
        for (int pass = 0; pass < detail + 3; pass++)
        {
            var next = new List<Vector3>(pts.Count * 2);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 mid = (a + b) * 0.5f;
                Vector3 dir = (b - a).normalized;
                Vector3 perp = Vector3.Cross(dir, Random.onUnitSphere).normalized;
                mid += perp * Random.Range(-offset, offset);
                next.Add(a); next.Add(mid);
            }
            next.Add(pts[pts.Count - 1]);
            pts = next;
            offset *= 0.5f;
        }

        var bolt = new Bolt { maxLife = life, life = life, basePts = pts.ToArray(), width = width };
        bolt.glow = MakeLine("Glow", bolt.basePts, width * 4f);
        bolt.core = MakeLine("Core", bolt.basePts, width);
        bolt.core.transform.SetParent(bolt.glow.transform, false);
        SetBoltAlpha(bolt, 1f);
        bolts.Add(bolt);
    }

    LineRenderer MakeLine(string n, Vector3[] pts, float width)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = boltMat;
        lr.useWorldSpace = true;
        lr.positionCount = pts.Length;
        lr.SetPositions(pts);
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, width), new Keyframe(1f, width * 0.35f));
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    void SetBoltAlpha(Bolt b, float a)
    {
        Color glow = new Color(0.45f, 0.6f, 1f, 0.35f * a);
        Color core = new Color(0.95f, 0.97f, 1f, a);
        b.glow.startColor = b.glow.endColor = glow;
        b.core.startColor = b.core.endColor = core;
    }

    void Jitter(Bolt b)
    {
        var p = new Vector3[b.basePts.Length];
        for (int i = 0; i < p.Length; i++)
            p[i] = b.basePts[i] + (i == 0 || i == p.Length - 1 ? Vector3.zero : Random.insideUnitSphere * 0.35f);
        b.glow.SetPositions(p);
        b.core.SetPositions(p);
    }

    // ================= ÂM THANH (tổng hợp) =================
    void PlayThunder(float volume, float pitch, float delay, bool big)
    {
        AudioClip clip = MakeThunder(big);
        var src = new GameObject("Thunder").AddComponent<AudioSource>();
        src.transform.SetParent(transform, false);
        src.clip = clip;
        src.volume = volume;
        src.pitch = pitch;
        src.spatialBlend = 0f;
        src.PlayDelayed(delay * 0.3f);
        Destroy(src.gameObject, clip.length / pitch + 1f);
    }

    static AudioClip MakeThunder(bool big)
    {
        const int rate = 44100;
        float len = big ? 3.2f : 1.8f;
        int n = (int)(rate * len);
        var data = new float[n];
        var rng = new System.Random(big ? 7 : Random.Range(0, 99999));
        float lp1 = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            // Rền: nhiễu lọc thấp, tắt chậm, nhấp nhô.
            lp1 += (noise - lp1) * 0.025f;
            lp2 += (lp1 - lp2) * 0.15f;
            float rumble = lp2 * 9f * Mathf.Exp(-t * (big ? 0.9f : 1.7f)) * (0.75f + 0.25f * Mathf.Sin(t * 11f + lp2 * 30f));
            // Nứt: nhiễu thô, tắt rất nhanh.
            float crack = noise * Mathf.Exp(-t * (big ? 22f : 35f)) * (big ? 1.0f : 0.5f);
            // Bụp trầm lúc mở đầu.
            float boom = Mathf.Sin(2f * Mathf.PI * 48f * t) * Mathf.Exp(-t * 7f) * (big ? 0.9f : 0.3f);
            float attack = Mathf.Clamp01(t / 0.004f);
            data[i] = Mathf.Clamp((rumble + crack + boom) * attack, -1f, 1f) * 0.9f;
        }
        var clip = AudioClip.Create(big ? "ThunderBig" : "ThunderSmall", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void OnDestroy()
    {
        if (boltMat != null) Destroy(boltMat);
    }
}
