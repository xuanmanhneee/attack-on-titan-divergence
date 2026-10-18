using System;
using System.Collections.Generic;
using UnityEngine;

// Animation bằng code cho titan có rig Humanoid (không cần file animation).
// Dùng HumanPoseHandler + "muscle" nên không phụ thuộc trục xoay của từng xương:
// đi bộ (chân và tay đánh ngược pha), hạ tay xuống khỏi tư thế chữ T, giơ tay đập khi tấn công.
// Chiều Front-Back của cơ có thể ngược tùy model; nếu thấy ngược thì đổi dấu legSwing / armSwing.
public class TitanProceduralAnim : MonoBehaviour
{
    public float walkPhase;     // Titan cập nhật mỗi frame
    public float moveAmount;    // 0 = đứng, 1 = đang đi
    public float attackRaise;   // 0..1: giơ tay lên
    public float attackSlam;    // 0..1: vừa đập xuống, đang thu về

    public float legSwing = 0.55f;
    public float armSwing = 0.3f;

    HumanPoseHandler handler;
    HumanPose basePose;
    HumanPose pose;
    float[] baseMuscles;
    float[] buf;
    readonly Dictionary<string, int> index = new Dictionary<string, int>();
    bool ready;
    float smoothMove;

    public static TitanProceduralAnim TryCreate(GameObject model)
    {
        var animator = model.GetComponentInChildren<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            Debug.Log("[Titan] " + model.name + ": không có Humanoid avatar hợp lệ, dùng hiệu ứng lắc nhẹ. " +
                      "(Chọn FBX > Rig > Animation Type = Humanoid > Apply, xem có báo lỗi map xương không)");
            return null;
        }

        var p = model.AddComponent<TitanProceduralAnim>();
        p.Init(animator);
        return p;
    }

    void Init(Animator animator)
    {
        handler = new HumanPoseHandler(animator.avatar, animator.transform);
        handler.GetHumanPose(ref basePose);
        baseMuscles = (float[])basePose.muscles.Clone();
        buf = new float[baseMuscles.Length];

        var names = HumanTrait.MuscleName;
        for (int i = 0; i < names.Length; i++) index[names[i]] = i;

        animator.enabled = false; // chỉ dùng HumanPoseHandler, tránh Animator ghi đè
        ready = true;
    }

    void Set(string muscle, float value)
    {
        if (index.TryGetValue(muscle, out int i)) buf[i] = Mathf.Clamp(value, -1f, 1f);
    }

    void LateUpdate()
    {
        if (!ready) return;

        smoothMove = Mathf.MoveTowards(smoothMove, moveAmount, 4f * Time.deltaTime);
        Array.Copy(baseMuscles, buf, buf.Length);

        float s = Mathf.Sin(walkPhase);
        float m = smoothMove;
        float raise = attackRaise;
        float slam = attackSlam;

        // Chân đánh ngược pha
        Set("Left Upper Leg Front-Back", legSwing * s * m);
        Set("Right Upper Leg Front-Back", -legSwing * s * m);

        // Tay: hạ xuống khỏi chữ T; đi thì đánh ngược chân; tấn công thì giơ cao rồi đập xuống
        float downUp = Mathf.Lerp(-0.95f, 0.95f, raise);
        downUp = Mathf.Lerp(downUp, -0.3f, slam);
        float swing = armSwing * s * m * (1f - Mathf.Max(raise, slam));
        Set("Left Arm Down-Up", downUp);
        Set("Right Arm Down-Up", downUp);
        Set("Left Arm Front-Back", -swing);
        Set("Right Arm Front-Back", swing);

        // Thân hơi ngả khi đập và thở nhẹ khi đứng
        Set("Spine Front-Back", 0.25f * (slam - raise * 0.6f));
        Set("Chest Front-Back", 0.04f * Mathf.Sin(Time.time * 1.5f));

        pose.bodyPosition = basePose.bodyPosition;
        pose.bodyRotation = basePose.bodyRotation;
        pose.muscles = buf;
        handler.SetHumanPose(ref pose);
    }

    void OnDestroy()
    {
        if (handler != null) handler.Dispose();
    }
}
