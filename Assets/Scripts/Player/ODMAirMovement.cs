using UnityEngine;

// Chuyển động trên không và vật lý dây: trọng lực, gas, thu dây, ràng buộc độ dài dây.
// Thông số vẫn nằm trên ODMController (giữ giá trị trong scene); lớp này chỉ đọc chúng.
// velocity và gas thuộc ODMController nên được truyền bằng ref.
public class ODMAirMovement : MonoBehaviour
{
    ODMController odm;
    HookState left, right;

    public void Init(ODMController odm, HookState left, HookState right)
    {
        this.odm = odm;
        this.left = left;
        this.right = right;
    }

    public void LoosenRope(HookState hook)
    {
        if (!hook.active) return;

        float dist = Vector3.Distance(transform.position, hook.anchorPoint);
        if (dist > odm.maxHookRange)
        {
            hook.active = false;
            return;
        }
        hook.ropeLength = Mathf.Max(hook.ropeLength, dist);
    }

    public void Move(float dt, bool hooked, Vector3 wish, ref Vector3 velocity, ref float gas)
    {
        bool dualHooked = left.active && right.active;
        float speedLimit = odm.maxSpeed * (dualHooked ? odm.dualHookMaxSpeedMultiplier : 1f);
        bool boosting = GameInput.GetKey(KeyCode.LeftShift) && gas > 0f && !odm.Exhausted;

        velocity.y += odm.gravity * dt;

        if ((!odm.airControlRequiresGas || boosting) && wish.sqrMagnitude > 0.0001f)
        {
            Vector3 wishDir = wish.normalized;
            float along = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), wishDir);
            float add = Mathf.Clamp(odm.airControlMaxSpeed - along, 0f, odm.airControl * dt);
            velocity += wishDir * add;
        }

        if (boosting)
        {
            // Gas đẩy theo hướng camera; dây giới hạn bán kính và tạo cung đu.
            Vector3 gasDirection = odm.cameraTransform.forward.normalized;
            float speedBeforeSteer = velocity.magnitude;
            if (odm.steerRate > 0f && speedBeforeSteer > 0.5f)
            {
                velocity = Vector3.RotateTowards(velocity,
                                                 gasDirection * speedBeforeSteer,
                                                 odm.steerRate * Mathf.Deg2Rad * dt,
                                                 0f);
            }

            float gasMultiplier = dualHooked ? odm.dualHookGasMultiplier : 1f;
            velocity += gasDirection * odm.gasForce * gasMultiplier * dt;
            gas = Mathf.Max(0f, gas - odm.gasDrain * (hooked ? odm.hookedGasDrainFactor : 1f) * dt);

            if (velocity.magnitude > speedLimit)
                velocity = velocity.normalized * speedLimit;
        }

        if (!hooked) return;

        if (odm.autoTakeUpSlack)
        {
            TakeUpSlack(left, dt);
            TakeUpSlack(right, dt);
        }

        bool strong = GameInput.GetKey(KeyCode.Space);
        float reelMultiplier = dualHooked ? odm.dualHookReelMultiplier : 1f;
        ReelPull(left, dt, strong, reelMultiplier, ref velocity);
        ReelPull(right, dt, strong, reelMultiplier, ref velocity);

        if (velocity.magnitude > speedLimit)
            velocity = velocity.normalized * speedLimit;

        float speedBeforeConstraint = velocity.magnitude;
        Vector3 predicted = transform.position + velocity * dt;
        bool constrained = false;
        for (int i = 0; i < odm.constraintIterations; i++)
        {
            if ((i & 1) == 0)
            {
                if (left.active) constrained |= ApplyRopeConstraint(left, ref predicted);
                if (right.active) constrained |= ApplyRopeConstraint(right, ref predicted);
            }
            else
            {
                if (right.active) constrained |= ApplyRopeConstraint(right, ref predicted);
                if (left.active) constrained |= ApplyRopeConstraint(left, ref predicted);
            }
        }

        if (dt > 0f)
            velocity = (predicted - transform.position) / dt;

        if (constrained && odm.cornerMomentumRetention > 0f && velocity.sqrMagnitude > 0.0001f)
        {
            float currentSpeed = velocity.magnitude;
            float retainedSpeed = Mathf.Lerp(currentSpeed,
                                             Mathf.Max(currentSpeed, speedBeforeConstraint),
                                             odm.cornerMomentumRetention);
            velocity *= Mathf.Min(retainedSpeed, speedLimit) / currentSpeed;
        }

        velocity *= Mathf.Pow(odm.ropeDamping, dt * 60f);

        if (velocity.magnitude > speedLimit)
            velocity = velocity.normalized * speedLimit;
    }

    void TakeUpSlack(HookState hook, float dt)
    {
        if (!hook.active || hook.attached) return;
        float dist = Vector3.Distance(transform.position, hook.anchorPoint);
        float target = Mathf.Max(odm.attachDistance, dist);
        if (hook.ropeLength > target)
            hook.ropeLength = Mathf.MoveTowards(hook.ropeLength, target, odm.slackTakeUpSpeed * dt);
    }

    void ReelPull(HookState hook, float dt, bool strong, float forceMultiplier, ref Vector3 velocity)
    {
        if (!hook.active || hook.attached) return;

        Vector3 toAnchor = hook.anchorPoint - transform.position;
        float dist = toAnchor.magnitude;
        Vector3 dir = toAnchor / Mathf.Max(dist, 0.0001f);

        float intensity = strong ? 1f : 0f;
        if (!strong && odm.autoReel)
        {
            float look = Vector3.Dot(odm.cameraTransform.forward, dir);
            intensity = Mathf.InverseLerp(odm.autoReelLookDot, 0.97f, look) * odm.autoReelStrength;
        }
        if (odm.reelOnlyWhenApproaching && Vector3.Dot(velocity, dir) < -odm.reelAwayThreshold)
            intensity = 0f;

        if (intensity <= 0f) return;

        if (dist <= odm.attachDistance)
        {
            hook.ropeLength = odm.attachDistance;
            return;
        }

        if (strong)
            hook.ropeLength = Mathf.Max(odm.attachDistance, hook.ropeLength - odm.reelSpeed * dt);

        float approachSpeed = Vector3.Dot(velocity, dir);
        float cap = odm.reelMaxSpeed * forceMultiplier * Mathf.Max(0.5f, intensity);
        float add = Mathf.Min(odm.reelForce * forceMultiplier * intensity * dt,
                              Mathf.Max(0f, cap - approachSpeed));
        velocity += dir * add;
    }

    bool ApplyRopeConstraint(HookState hook, ref Vector3 predicted)
    {
        Vector3 toPos = predicted - hook.anchorPoint;
        float dist = toPos.magnitude;
        if (dist > hook.ropeLength && dist > 0.0001f)
        {
            predicted = hook.anchorPoint + toPos / dist * hook.ropeLength;
            return true;
        }

        return false;
    }
}
