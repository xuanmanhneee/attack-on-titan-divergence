using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Phát từng đoạn của một clip đấm (Mixamo "Punching") trên model Humanoid, không cần Animator Controller.
// Các đầu vào trộn với nhau: tư thế thủ (một khung cố định của clip đấm), clip chạy lặp (nếu có)
// và đoạn đấm đang phát. TitanForm gọi Play(start, end) cho mỗi cú đấm, đặt Locomotion/RunSpeed
// mỗi khung và tự kiểm tra thời điểm trúng qua CurrentTime.
//
// Graph:
//   layer 0: mixer(guard, run, actionFull)   -- toàn thân
//   layer 1: actionUpper (mask nửa thân trên) -- đấm khi đang chạy: chân vẫn chạy, tay vẫn đấm
public class TitanPunchAnimator : MonoBehaviour
{
    public float blendIn = 0.06f;
    public float blendOut = 0.18f;
    public float locomotionBlend = 6f;   // tốc độ chuyển giữa đứng thủ thế và chạy (1/giây)

    PlayableGraph graph;
    AnimationLayerMixerPlayable layers;
    AnimationMixerPlayable baseMixer;
    AnimationClipPlayable guard, action, actionUpper, run;
    Animator animator;
    bool hasRun;
    float start, end, time, speed, weight, runWeight;
    bool playing, ready;

    public bool Playing => playing;
    public float CurrentTime => time;
    public float ActionWeight => weight; // 0 = đang thủ thế, 1 = đang phát cú đấm
    public float RunWeight => runWeight; // 0 = đứng, 1 = đang phát clip chạy
    public bool HasRun => hasRun;
    public Animator Animator => animator;

    // Do TitanForm đặt mỗi khung: 0..1 mức độ đang chạy và tốc độ phát clip chạy.
    public float Locomotion { get; set; }
    public float RunSpeed { get; set; } = 1f;

    public static TitanPunchAnimator TryCreate(GameObject model, AnimationClip clip, float guardTime,
                                               AnimationClip runClip = null)
    {
        var animator = model.GetComponentInChildren<Animator>();
        if (clip == null || animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            return null;

        var p = model.AddComponent<TitanPunchAnimator>();
        p.Build(animator, clip, guardTime, runClip);
        return p;
    }

    void Build(Animator anim, AnimationClip clip, float guardTime, AnimationClip runClip)
    {
        animator = anim;
        animator.applyRootMotion = false;
        animator.runtimeAnimatorController = null;
        // Camera bám xương đầu: không được để Animator ngừng cập nhật khi thân bị cull.
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        graph = PlayableGraph.Create("TitanPunch_" + name);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "Anim", animator);

        layers = AnimationLayerMixerPlayable.Create(graph, 2);
        output.SetSourcePlayable(layers);
        baseMixer = AnimationMixerPlayable.Create(graph, 3);

        // Thời gian của guard/action do code đặt, không để graph tự chạy.
        guard = CreateFrozen(clip, guardTime);
        action = CreateFrozen(clip, guardTime);
        actionUpper = CreateFrozen(clip, guardTime);

        hasRun = runClip != null && runClip.humanMotion;
        if (hasRun)
        {
            // Clip chạy lặp tự chạy theo thời gian game (clip đã bật Loop Time khi import).
            run = AnimationClipPlayable.Create(graph, runClip);
            run.SetApplyFootIK(true);
            run.SetSpeed(1);
        }
        else
        {
            run = CreateFrozen(clip, guardTime); // giữ chỗ cho input 1, trọng số luôn 0
        }

        graph.Connect(guard, 0, baseMixer, 0);
        graph.Connect(run, 0, baseMixer, 1);
        graph.Connect(action, 0, baseMixer, 2);
        graph.Connect(baseMixer, 0, layers, 0);
        graph.Connect(actionUpper, 0, layers, 1);
        layers.SetInputWeight(0, 1f);
        layers.SetInputWeight(1, 0f);
        layers.SetLayerMaskFromAvatarMask(1, UpperBodyMask());

        ApplyWeights();
        graph.Play();
        graph.Evaluate(0f); // có tư thế thủ ngay khung đầu (TitanForm cần vị trí đầu để đặt camera)
        ready = true;
    }

    AnimationClipPlayable CreateFrozen(AnimationClip clip, float t)
    {
        var p = AnimationClipPlayable.Create(graph, clip);
        p.SetApplyFootIK(false);
        p.SetSpeed(0);
        p.SetTime(t);
        return p;
    }

    static AvatarMask UpperBodyMask()
    {
        var mask = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
        return mask;
    }

    // Phát đoạn [startSec, endSec] của clip với tốc độ playSpeed.
    public void Play(float startSec, float endSec, float playSpeed)
    {
        if (!ready) return;
        start = startSec;
        end = endSec;
        speed = Mathf.Max(0.01f, playSpeed);
        time = start;
        playing = true;
        SetActionTime(time);
        SetActionTime(time); // gọi hai lần để Unity không nội suy từ thời điểm cũ
    }

    void SetActionTime(float t)
    {
        action.SetTime(t);
        actionUpper.SetTime(t);
    }

    void Update()
    {
        if (!ready) return;
        float dt = Time.deltaTime;

        if (playing)
        {
            time += dt * speed;
            if (time >= end) { time = end; playing = false; }
            SetActionTime(time);
        }

        float goal = playing ? 1f : 0f;
        float rate = playing ? 1f / Mathf.Max(0.01f, blendIn) : 1f / Mathf.Max(0.01f, blendOut);
        weight = Mathf.MoveTowards(weight, goal, rate * dt);

        float runGoal = hasRun ? Mathf.Clamp01(Locomotion) : 0f;
        runWeight = Mathf.MoveTowards(runWeight, runGoal, locomotionBlend * dt);
        if (hasRun) run.SetSpeed(Mathf.Max(0f, RunSpeed));

        ApplyWeights();
    }

    void ApplyWeights()
    {
        // Đứng yên: guard <-> đấm toàn thân. Đang chạy: clip chạy, nửa thân trên đè bằng cú đấm.
        float r = runWeight, w = weight;
        baseMixer.SetInputWeight(0, (1f - w) * (1f - r));
        baseMixer.SetInputWeight(1, r);
        baseMixer.SetInputWeight(2, w * (1f - r));
        layers.SetInputWeight(1, w * r);
    }

    void OnDestroy()
    {
        if (ready) graph.Destroy();
        ready = false;
    }
}
