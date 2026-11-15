using UnityEngine;

// Hiệu ứng hạt tự dựng bằng code (không cần prefab / asset):
//  - Steam: hơi nước bốc lên từ xác titan
//  - Burst: một tràng hạt ngắn (bụi khi móc trúng tường, tia máu hơi khi chém gáy)
// Dùng shader Sprites/Default (luôn có trong bản build, chạy được trên URP).
public static class GameVfx
{
    static Material mat;
    static Texture2D soft;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { mat = null; soft = null; }

    static Texture2D Soft()
    {
        if (soft != null) return soft;
        const int s = 64;
        var pix = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                pix[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        soft = new Texture2D(s, s, TextureFormat.RGBA32, false);
        soft.wrapMode = TextureWrapMode.Clamp;
        soft.SetPixels32(pix);
        soft.Apply();
        return soft;
    }

    static Material Mat()
    {
        if (mat != null) return mat;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) return null;
        mat = new Material(sh);
        mat.mainTexture = Soft();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Soft());
        return mat;
    }

    static ParticleSystem NewSystem(string name, Vector3 pos, out ParticleSystemRenderer rend)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        rend = go.GetComponent<ParticleSystemRenderer>();
        Material m = Mat();
        if (m != null) rend.sharedMaterial = m;
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        return ps;
    }

    static Gradient FadeGradient(Color c)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(c.a, 0.15f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // Hơi nước bốc lên quanh `pos` trong `seconds` giây. size = bán kính vùng phát (m).
    public static void Steam(Vector3 pos, float size, float seconds)
    {
        ParticleSystemRenderer rend;
        var ps = NewSystem("Steam", pos, out rend);

        var main = ps.main;
        main.loop = false;
        main.duration = Mathf.Max(0.1f, seconds);
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 1.1f);
        main.startColor = new Color(1f, 1f, 1f, 0.55f);
        main.gravityModifier = -0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;

        var em = ps.emission;
        em.rateOverTime = 45f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.2f, size);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeGradient(new Color(1f, 1f, 1f, 0.55f)));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));

        ps.Play();
        Object.Destroy(ps.gameObject, seconds + 6f);
    }

    // Một tràng hạt tỏa ra rồi tan: count hạt, tốc độ speed (m/s), kích thước size (m), sống life giây.
    public static void Burst(Vector3 pos, Color color, int count, float speed, float size, float life)
    {
        ParticleSystemRenderer rend;
        var ps = NewSystem("Burst", pos, out rend);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = color;
        main.gravityModifier = 0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(8, count);

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 200)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeGradient(color));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 1.8f));

        ps.Play();
        Object.Destroy(ps.gameObject, life + 1f);
    }
}
