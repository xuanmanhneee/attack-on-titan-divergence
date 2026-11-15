using System;
using System.Collections.Generic;
using UnityEngine;

public enum Sfx
{
    HookFire, HookHit, Slash, SlashHit, Kill, Footstep, TitanSmash, Roar,
    PlayerHurt, GasBurst, LowGas, Reload, Refill, Impact, Thud
}

// Âm thanh trong trận: tất cả được tổng hợp bằng code lúc chạy nên không cần file.
// Muốn thay bằng âm thanh thật: bỏ file vào Assets/Resources/Audio/ với tên trùng tên enum Sfx
// (vd: HookFire.wav, Slash.ogg, Footstep.wav ...). File nào có thì dùng, không có thì dùng bản tổng hợp.
// Âm lượng theo cài đặt (Master x Sfx / Music).
public class GameAudio : MonoBehaviour
{
    const int Rate = 44100;
    const float TwoPi = 6.2831853f;

    static GameAudio inst;
    static AudioClip menuMusic, battleMusic;
    static readonly System.Random rng = new System.Random(12345);

    readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
    readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
    AudioSource[] pool;
    float[] busyUntil;
    int poolIdx;
    AudioSource gasSrc, windSrc, musicSrc;
    float gasTarget, windTarget, musicLevel, musicTarget;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { inst = null; menuMusic = null; battleMusic = null; }

    // ================= API =================
    public static void Play2D(Sfx s, float vol = 1f, float pitch = 1f) { Get().PlayInternal(s, false, Vector3.zero, vol, pitch, 0f); }

    public static void Play(Sfx s, Vector3 pos, float vol = 1f, float pitch = 1f, float delay = 0f)
    {
        Get().PlayInternal(s, true, pos, vol, pitch, delay);
    }

    // gasUse: 1 = đang xả gas (tiếng xì), 0 = không. speed01: tốc độ / tốc độ tối đa (tiếng gió).
    public static void SetLoops(float gasUse, float speed01)
    {
        if (inst == null && gasUse <= 0f && speed01 <= 0f) return;
        GameAudio g = Get();
        g.gasTarget = Mathf.Clamp01(gasUse);
        g.windTarget = Mathf.Clamp01((speed01 - 0.12f) / 0.55f);
    }

    // Gọi lúc màn hình chờ để việc tạo âm thanh không làm khựng lúc vào trận
    public static void Warmup()
    {
        Get();
        if (battleMusic == null) battleMusic = BuildMusic("BattleMusic", 84, BattleMelody(), 2.4f, 0.55f, 0.16f, 0.10f, 73.42f, 110f);
    }

    public static void Battle(bool on)
    {
        if (inst == null && !on) return;
        GameAudio g = Get();
        if (on)
        {
            if (battleMusic == null) battleMusic = BuildMusic("BattleMusic", 84, BattleMelody(), 2.4f, 0.55f, 0.16f, 0.10f, 73.42f, 110f);
            if (g.musicSrc.clip != battleMusic) g.musicSrc.clip = battleMusic;
            if (!g.musicSrc.isPlaying) g.musicSrc.Play();
            g.musicTarget = 1f;
        }
        else g.musicTarget = 0f;
    }

    // Tạm dừng: nhạc nhỏ lại
    public static void Duck(bool on)
    {
        if (inst == null) return;
        if (inst.musicTarget > 0f) inst.musicTarget = on ? 0.3f : 1f;
    }

    // Nhạc menu (MangaSfx dùng khi không có file menu_music)
    public static AudioClip MenuMusic()
    {
        if (menuMusic == null) menuMusic = BuildMusic("MenuMusic", 62, MenuMelody(), 1.1f, 0.35f, 0.22f, 0.13f, 73.42f, 110f);
        return menuMusic;
    }

    static GameAudio Get()
    {
        if (inst != null) return inst;
        var go = new GameObject("GameAudio");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<GameAudio>();
        return inst;
    }

    // ================= Khởi tạo =================
    void Awake()
    {
        pool = new AudioSource[16];
        busyUntil = new float[16];
        for (int i = 0; i < pool.Length; i++)
        {
            var g = new GameObject("Sfx" + i);
            g.transform.SetParent(transform, false);
            var a = g.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.rolloffMode = AudioRolloffMode.Logarithmic;
            a.minDistance = 6f;
            a.maxDistance = 300f;
            a.dopplerLevel = 0f;
            pool[i] = a;
        }

        foreach (Sfx s in Enum.GetValues(typeof(Sfx)))
        {
            AudioClip c = Resources.Load<AudioClip>("Audio/" + s);
            clips[s] = c != null ? c : Make(s);
        }

        gasSrc = MakeLoopSource("GasLoop", MakeNoiseLoop("GasLoop", 1.2f, 0.45f));
        windSrc = MakeLoopSource("WindLoop", MakeNoiseLoop("WindLoop", 2.0f, 0.06f));

        var mg = new GameObject("Music");
        mg.transform.SetParent(transform, false);
        musicSrc = mg.AddComponent<AudioSource>();
        musicSrc.playOnAwake = false;
        musicSrc.loop = true;
        musicSrc.spatialBlend = 0f;
        musicSrc.volume = 0f;
    }

    AudioSource MakeLoopSource(string name, AudioClip clip)
    {
        var g = new GameObject(name);
        g.transform.SetParent(transform, false);
        var a = g.AddComponent<AudioSource>();
        a.clip = clip;
        a.loop = true;
        a.playOnAwake = false;
        a.spatialBlend = 0f;
        a.volume = 0f;
        a.Play();
        return a;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float sfxVol = GameSettings.Sfx;

        gasSrc.volume = Mathf.MoveTowards(gasSrc.volume, gasTarget * 0.10f * sfxVol, dt * 5f);
        windSrc.volume = Mathf.MoveTowards(windSrc.volume, windTarget * 0.12f * sfxVol, dt * 3f);
        windSrc.pitch = 0.75f + windTarget * 0.7f;

        musicLevel = Mathf.MoveTowards(musicLevel, musicTarget, dt * 0.7f);
        musicSrc.volume = musicLevel * Mathf.Clamp01(GameSettings.Music) * 0.6f;
        if (musicLevel <= 0.001f && musicTarget <= 0f && musicSrc.isPlaying) musicSrc.Stop();
    }

    void PlayInternal(Sfx s, bool spatial, Vector3 pos, float vol, float pitch, float delay)
    {
        AudioClip c;
        if (!clips.TryGetValue(s, out c) || c == null) return;

        // giới hạn tần suất để không bị chồng âm khi nhiều titan cùng gầm / bước
        float now = Time.unscaledTime;
        float gap = s == Sfx.Roar ? 3.5f : (s == Sfx.Footstep ? 0.06f : 0.02f);
        float last;
        if (lastPlayed.TryGetValue(s, out last) && now - last < gap) return;
        lastPlayed[s] = now;

        AudioSource a = null;
        int slot = 0;
        for (int i = 0; i < pool.Length; i++)
        {
            int idx = (poolIdx + i) % pool.Length;
            // isPlaying có thể là false trong lúc chờ PlayDelayed -> dùng thêm mốc busyUntil
            if (!pool[idx].isPlaying && now >= busyUntil[idx]) { a = pool[idx]; poolIdx = (idx + 1) % pool.Length; slot = idx; break; }
        }
        if (a == null)
        {
            slot = poolIdx;
            a = pool[slot];
            poolIdx = (poolIdx + 1) % pool.Length;
            a.Stop();
        }
        busyUntil[slot] = now + delay + c.length / Mathf.Max(0.1f, pitch);

        a.clip = c;
        a.volume = Mathf.Clamp01(vol * GameSettings.Sfx);
        a.pitch = pitch;
        if (spatial) { a.spatialBlend = 1f; a.transform.position = pos; }
        else a.spatialBlend = 0f;

        if (delay > 0.001f) a.PlayDelayed(delay); else a.Play();
    }

    // ================= Tổng hợp tiếng =================
    static float Nz() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

    static AudioClip Synth(string name, float seconds, Func<float, float, float> f)
    {
        int n = Mathf.Max(1, (int)(seconds * Rate));
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, i / (float)n), -1f, 1f);
        var c = AudioClip.Create(name, n, 1, Rate, false);
        c.SetData(data, 0);
        return c;
    }

    static AudioClip MakeNoiseLoop(string name, float seconds, float coef)
    {
        int n = (int)(seconds * Rate);
        var data = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            lp += (Nz() - lp) * coef;
            data[i] = lp * (coef < 0.2f ? 3.2f : 1.4f);
        }
        int fade = 1500; // làm mờ hai đầu để chỗ nối vòng lặp không bị "tách"
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            data[i] *= k;
            data[n - 1 - i] *= k;
        }
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
        var c = AudioClip.Create(name, n, 1, Rate, false);
        c.SetData(data, 0);
        return c;
    }

    static AudioClip Make(Sfx s)
    {
        float lp = 0f, lp2 = 0f, ph = 0f;
        switch (s)
        {
            case Sfx.HookFire:
                return Synth("HookFire", 0.30f, (t, p) =>
                {
                    lp += (Nz() - lp) * (0.12f + 0.55f * p);
                    return lp * Mathf.Exp(-t * 7f) * 1.6f * Mathf.Min(1f, t * 60f)
                         + Mathf.Sin(TwoPi * (1400f - 900f * p) * t) * Mathf.Exp(-t * 35f) * 0.25f;
                });
            case Sfx.HookHit:
                return Synth("HookHit", 0.20f, (t, p) =>
                    Mathf.Sin(TwoPi * 95f * t) * Mathf.Exp(-t * 24f) * 0.9f + Nz() * Mathf.Exp(-t * 60f) * 0.5f);
            case Sfx.Slash:
                return Synth("Slash", 0.24f, (t, p) =>
                {
                    lp += (Nz() - lp) * 0.3f;
                    float hp = Nz() - lp;
                    float env = Mathf.Sin(Mathf.PI * p);
                    return hp * env * env * 0.8f;
                });
            case Sfx.SlashHit:
                return Synth("SlashHit", 0.28f, (t, p) =>
                    Nz() * Mathf.Exp(-t * 30f) * 0.6f + Mathf.Sin(TwoPi * 140f * t) * Mathf.Exp(-t * 18f) * 0.7f);
            case Sfx.Kill:
                return Synth("Kill", 1.0f, (t, p) =>
                {
                    lp += (Nz() - lp) * 0.35f;
                    float crack = Nz() * Mathf.Exp(-t * 70f) * 0.8f;
                    float body = Mathf.Sin(TwoPi * (95f - 55f * p) * t) * Mathf.Exp(-t * 5f) * 0.8f;
                    float steam = lp * Mathf.Min(1f, t * 20f) * Mathf.Exp(-t * 3f) * 0.9f;
                    return crack + body + steam;
                });
            case Sfx.Footstep:
                return Synth("Footstep", 0.40f, (t, p) =>
                {
                    lp += (Nz() - lp) * 0.08f;
                    return Mathf.Sin(TwoPi * (58f - 28f * p) * t) * Mathf.Exp(-t * 13f) * 1.0f + lp * Mathf.Exp(-t * 22f) * 1.6f;
                });
            case Sfx.TitanSmash:
                return Synth("TitanSmash", 0.7f, (t, p) =>
                {
                    lp += (Nz() - lp) * 0.1f;
                    return Mathf.Sin(TwoPi * (72f - 40f * p) * t) * Mathf.Exp(-t * 8f) * 1.0f + lp * Mathf.Exp(-t * 12f) * 2.0f;
                });
            case Sfx.Roar:
                return Synth("Roar", 1.3f, (t, p) =>
                {
                    float f0 = 78f + 22f * Mathf.Sin(TwoPi * 5.5f * t) + 35f * p;
                    ph += f0 / Rate;
                    float saw = 2f * (ph - Mathf.Floor(ph)) - 1f;
                    float x = saw * 0.6f + Nz() * 0.35f;
                    lp += (x - lp) * 0.16f;
                    lp2 += (lp - lp2) * 0.35f;
                    float env = Mathf.Pow(Mathf.Sin(Mathf.PI * p), 0.6f);
                    return (lp * 0.7f + lp2 * 0.6f) * env * 1.5f;
                });
            case Sfx.PlayerHurt:
                return Synth("PlayerHurt", 0.38f, (t, p) =>
                    Mathf.Sin(TwoPi * (230f - 150f * p) * t) * Mathf.Exp(-t * 10f) * 0.6f + Nz() * Mathf.Exp(-t * 40f) * 0.6f
                    + Mathf.Sin(TwoPi * 60f * t) * Mathf.Exp(-t * 14f) * 0.6f);
            case Sfx.GasBurst:
                return Synth("GasBurst", 0.45f, (t, p) =>
                {
                    lp += (Nz() - lp) * 0.4f;
                    return lp * Mathf.Min(1f, t * 80f) * Mathf.Exp(-t * 7f) * 1.8f;
                });
            case Sfx.LowGas:
                return Synth("LowGas", 0.30f, (t, p) =>
                {
                    float gate = Mathf.Repeat(t * 7f, 1f) < 0.55f ? 1f : 0f;
                    return Mathf.Sin(TwoPi * 760f * t) * gate * (1f - p) * 0.35f;
                });
            case Sfx.Reload:
                return Synth("Reload", 0.55f, (t, p) =>
                {
                    float sum = 0f;
                    float[] at = { 0f, 0.17f, 0.34f };
                    for (int i = 0; i < at.Length; i++)
                    {
                        float u = t - at[i];
                        if (u < 0f) continue;
                        sum += Mathf.Exp(-u * 70f) * Mathf.Sin(TwoPi * (1900f + 400f * i) * u) * 0.5f;
                    }
                    return sum;
                });
            case Sfx.Refill:
                return Synth("Refill", 0.55f, (t, p) =>
                {
                    lp += (Nz() - lp) * 0.3f;
                    float env = Mathf.Sin(Mathf.PI * p);
                    return lp * env * 0.9f + Mathf.Sin(TwoPi * (500f + 500f * p) * t) * env * 0.18f;
                });
            case Sfx.Impact:
                return Synth("Impact", 0.32f, (t, p) =>
                {
                    lp += (Nz() - lp) * 0.2f;
                    return Mathf.Sin(TwoPi * 70f * t) * Mathf.Exp(-t * 14f) * 0.9f + lp * Mathf.Exp(-t * 25f) * 1.2f;
                });
            default: // Thud: thân titan đập xuống đất
                return Synth("Thud", 0.8f, (t, p) =>
                {
                    lp += (Nz() - lp) * 0.07f;
                    return Mathf.Sin(TwoPi * (46f - 20f * p) * t) * Mathf.Exp(-t * 6f) * 1.0f + lp * Mathf.Exp(-t * 10f) * 2.2f;
                });
        }
    }

    // ================= Nhạc nền tổng hợp =================
    static float Hz(int semitonesFromA4) { return 440f * Mathf.Pow(2f, semitonesFromA4 / 12f); }

    // Rê thứ: D F A C | A F D F | G Bb D C | A F E A
    static float[] BattleMelody()
    {
        int[] s = { -7, -4, 0, 3, 0, -4, -7, -4, -2, 1, 5, 3, 0, -4, -5, -12 };
        var m = new float[s.Length];
        for (int i = 0; i < s.Length; i++) m[i] = Hz(s[i]);
        return m;
    }

    // 0 = nghỉ
    static float[] MenuMelody()
    {
        int[] s = { -7, 99, 99, -12, -4, 99, 99, 99, -2, 99, 0, 99 };
        var m = new float[s.Length];
        for (int i = 0; i < s.Length; i++) m[i] = s[i] == 99 ? 0f : Hz(s[i]);
        return m;
    }

    // Vòng lặp liền mạch: mọi tần số được làm tròn thành số chu kỳ nguyên trong cả vòng, đuôi nốt cuối nối sang nốt đầu.
    static AudioClip BuildMusic(string name, int bpm, float[] melody, float noteDecay, float drumGain,
                                float droneGain, float melGain, float drone1, float drone2)
    {
        float beat = 60f / bpm;
        int beats = melody.Length;
        int n = Mathf.RoundToInt(beat * beats * Rate);
        float loopLen = n / (float)Rate;
        var data = new float[n];

        float d1 = Mathf.Round(drone1 * loopLen) / loopLen;
        float d2 = Mathf.Round(drone2 * loopLen) / loopLen;
        float peak = 0.0001f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float s = 0f;

            float lfo = 0.65f + 0.35f * Mathf.Sin(TwoPi * t / loopLen);
            s += (Mathf.Sin(TwoPi * d1 * t) * 0.55f + Mathf.Sin(TwoPi * d2 * t) * 0.3f + Mathf.Sin(TwoPi * d1 * 2f * t) * 0.1f) * droneGain * lfo;

            int b0 = Mathf.Min(beats - 1, (int)(t / beat));
            float tb = t - b0 * beat;

            for (int k = 0; k < 3; k++)
            {
                int b = ((b0 - k) % beats + beats) % beats;
                float f = melody[b];
                if (f <= 0f) continue;
                float tau = tb + k * beat;
                float env = Mathf.Min(1f, tau * 80f) * Mathf.Exp(-tau * noteDecay);
                s += (Mathf.Sin(TwoPi * f * tau) + 0.35f * Mathf.Sin(TwoPi * f * 2f * tau)) * env * melGain;
            }

            if (drumGain > 0f)
            {
                float amp = (b0 % 4 == 0) ? 1f : ((b0 % 4 == 2) ? 0.6f : 0f);
                if (amp > 0f)
                    s += Mathf.Sin(TwoPi * (48f + 60f * Mathf.Exp(-tb * 25f)) * tb) * Mathf.Exp(-tb * 7f) * amp * drumGain;
            }

            data[i] = s;
            float a = Mathf.Abs(s);
            if (a > peak) peak = a;
        }

        float scale = 0.8f / peak;
        for (int i = 0; i < n; i++) data[i] *= scale;

        var c = AudioClip.Create(name, n, 1, Rate, false);
        c.SetData(data, 0);
        return c;
    }
}
