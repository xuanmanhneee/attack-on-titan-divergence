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
        new[] { "idle", "stand", "wait" },
        new[] { "walk", "run", "move" },
        new[] { "attack", "punch", "smash", "hit" },
    };

    public static TitanClipPlayer TryCreate(GameObject model, string resourcePath)
    {
        var all = Resources.LoadAll<AnimationClip>(resourcePath);
        var usable = new List<AnimationClip>();
        foreach (var c in all)
            if (c != null && !c.name.StartsWith("__preview__")) usable.Add(c);

        if (usable.Count == 0) return null;
        Debug.Log("[Titan] " + model.name + " có " + usable.Count + " clip: " + string.Join(", ", usable.ConvertAll(c => c.name)));

        var player = model.AddComponent<TitanClipPlayer>();
        for (int role = 0; role < 3; role++)
            foreach (var c in usable)
            {
                string n = c.name.ToLowerInvariant();
                foreach (var kw in Keywords[role])
                    if (n.Contains(kw)) { player.clips[role] = c; break; }
                if (player.clips[role] != null) break;
            }

        // Thiếu vai trò nào thì mượn clip của vai trò gần nhất
        if (player.clips[0] == null) player.clips[0] = player.clips[1];
        if (player.clips[1] == null) player.clips[1] = player.clips[0];
        if (player.clips[2] == null) player.clips[2] = player.clips[1] != null ? player.clips[1] : player.clips[0];

        if (player.clips[0] == null && player.clips[1] == null)
        {
            Destroy(player);
            return null; // có clip nhưng không rõ là clip gì: không dùng
        }

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
