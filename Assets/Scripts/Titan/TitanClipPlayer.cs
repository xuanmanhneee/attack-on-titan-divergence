using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Phát animation có sẵn trong file FBX của titan, không cần Animator Controller.
// Chọn clip theo tên (idle/stand, walk/run/move, attack/punch/smash). Không tìm thấy clip nào thì trả null
// và Titan dùng hiệu ứng lắc nhẹ thay thế. Tên các clip tìm được được in ra Console để bạn kiểm tra.
public class TitanClipPlayer : MonoBehaviour
{
    public enum Role { Idle = 0, Walk = 1, Attack = 2 }

    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[3];
    readonly AnimationClip[] clips = new AnimationClip[3];
    readonly float[] weights = new float[3];
    Role target = Role.Idle;
    Role last = Role.Idle;
    bool ready;

    static readonly string[][] Keywords =
    {
        new[] { "idle", "stand", "wait", "breath" },
        new[] { "walk", "run", "move" },
        new[] { "attack", "punch", "smash", "slam", "swipe", "slash" },
    };

    // Quy ước: bỏ file animation (FBX Mixamo) vào Assets/Resources/Titans/Animations/ và đặt tên
    // Idle, Walk (hoặc Run), Attack. Chỉ dùng được với model có Humanoid avatar.
    static AnimationClip LoadExternal(string fileName)
    {
        foreach (var c in Resources.LoadAll<AnimationClip>("Titans/Animations/" + fileName))
            if (c != null && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    static AnimationClip Find(List<AnimationClip> pool, string[] keywords)
    {
        foreach (var c in pool)
        {
            string n = c.name.ToLowerInvariant();
            foreach (var kw in keywords) if (n.Contains(kw)) return c;
        }
        return null;
    }

    public static TitanClipPlayer TryCreate(GameObject model, string resourcePath)
    {
        var animator = model.GetComponentInChildren<Animator>();
        bool human = animator != null && animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman;

        var clips = new AnimationClip[3];
        if (human)
        {
            clips[0] = LoadExternal("Idle");
            clips[1] = LoadExternal("Walk") ?? LoadExternal("Run");
            clips[2] = LoadExternal("Attack");
        }

        // Clip có sẵn trong FBX, chọn theo tên (attack trước để "Standing Attack" không bị nhận là idle)
        var pool = new List<AnimationClip>();
        foreach (var c in Resources.LoadAll<AnimationClip>(resourcePath))
            if (c != null && !c.name.StartsWith("__preview__")) pool.Add(c);
        if (pool.Count > 0)
            Debug.Log("[Titan] " + model.name + " có " + pool.Count + " clip riêng: " + string.Join(", ", pool.ConvertAll(c => c.name)));

        for (int role = 2; role >= 0; role--)
        {
            if (clips[role] != null) continue;
            var found = Find(pool, Keywords[role]);
            if (found != null) { clips[role] = found; pool.Remove(found); }
        }

        // Thiếu vai trò nào thì mượn clip gần nhất
        if (clips[0] == null) clips[0] = clips[1];
        if (clips[1] == null) clips[1] = clips[0];
        if (clips[2] == null) clips[2] = clips[1] != null ? clips[1] : clips[0];
        if (clips[0] == null) return null;

        var player = model.AddComponent<TitanClipPlayer>();
        for (int i = 0; i < 3; i++) player.clips[i] = clips[i];
        Debug.Log("[Titan] " + model.name + " dùng clip - Idle: " + clips[0].name + ", Walk: " + clips[1].name + ", Attack: " + clips[2].name);
        player.Build(model);
        return player;
    }

    void Build(GameObject model)
    {
        var animator = model.GetComponentInChildren<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.applyRootMotion = false;

        graph = PlayableGraph.Create("TitanClips_" + model.name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime); // theo Time.timeScale (hợp với làm chậm thời gian)
        var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
        mixer = AnimationMixerPlayable.Create(graph, 3);
        output.SetSourcePlayable(mixer);

        for (int i = 0; i < 3; i++)
        {
            playables[i] = AnimationClipPlayable.Create(graph, clips[i]);
            graph.Connect(playables[i], 0, mixer, i);
            mixer.SetInputWeight(i, i == 0 ? 1f : 0f);
            weights[i] = i == 0 ? 1f : 0f;
        }
        graph.Play();
        ready = true;
    }

    public void SetRole(Role role)
    {
        if (!ready) return;
        if (role == Role.Attack && last != Role.Attack) playables[2].SetTime(0);
        last = target = role;
    }

    void Update()
    {
        if (!ready) return;
        for (int i = 0; i < 3; i++)
        {
            float goal = (int)target == i ? 1f : 0f;
            weights[i] = Mathf.MoveTowards(weights[i], goal, 4f * Time.deltaTime);
            mixer.SetInputWeight(i, weights[i]);

            // Lặp clip thủ công (idle và walk lặp mãi; attack giữ ở cuối rồi Titan sẽ restart khi tấn công lại)
            var clip = clips[i];
            if (clip != null && i != 2 && playables[i].GetTime() >= clip.length)
                playables[i].SetTime(0);
        }
    }

    public void Stop()
    {
        if (ready) { graph.Destroy(); ready = false; }
    }

    void OnDestroy()
    {
        Stop();
    }
}
