using UnityEngine;

// Đi bộ / chạy gas trên đất và mái nhà: kiểm tra chạm đất, nhận diện mái dốc, trượt mái.
// Thông số (groundLayer, walkSpeed, roofSlide...) vẫn nằm trên ODMController để giữ giá trị trong scene;
// lớp này chỉ đọc chúng. velocity và gas thuộc ODMController nên được truyền bằng ref.
public class ODMGroundMovement : MonoBehaviour
{
    ODMController odm;
    CharacterController controller;

    readonly Collider[] groundHits = new Collider[16];
    readonly RaycastHit[] roofHits = new RaycastHit[16];
    RaycastHit currentGroundSurface;
    bool hasCurrentGroundSurface;

    public void Init(ODMController odm, CharacterController controller)
    {
        this.odm = odm;
        this.controller = controller;
    }

    // Trả về true nếu đang chạm đất.
    public bool UpdateGrounded()
    {
        hasCurrentGroundSurface = false;
        Vector3 scale = transform.lossyScale;
        float worldHeight = controller.height * Mathf.Abs(scale.y);
        float worldRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        Vector3 feet = transform.TransformPoint(controller.center)
                     + Vector3.down * (worldHeight * 0.5f - worldRadius + 0.1f);
        bool sphereGrounded = false;
        int count = Physics.OverlapSphereNonAlloc(feet, worldRadius * 0.95f, groundHits,
                                                  odm.groundLayer, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = groundHits[i];
            if (hit != null && !hit.transform.IsChildOf(transform))
            {
                sphereGrounded = true;
                break;
            }
        }
        bool grounded = sphereGrounded || controller.isGrounded;

        // A sloped MeshCollider can sit between the controller's lower
        // hemisphere and the overlap volume for one frame.  A short downward
        // ray makes roof contact deterministic instead of switching to air
        // movement and appearing stuck.
        Vector3 rayOrigin = transform.TransformPoint(controller.center)
                          + Vector3.up * 0.2f;
        float rayLength = worldHeight * 0.5f + 0.45f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit,
                            rayLength, odm.groundLayer, QueryTriggerInteraction.Ignore)
            && !groundHit.transform.IsChildOf(transform)
            && groundHit.normal.y > 0.05f)
        {
            currentGroundSurface = groundHit;
            hasCurrentGroundSurface = true;
            grounded = true;
        }
        return grounded;
    }

    public bool HasCurrentGroundSurface => hasCurrentGroundSurface;

    public void Move(float dt, Vector3 wishDir, ref Vector3 velocity, ref float gas)
    {
        bool boosting = GameInput.GetKey(KeyCode.LeftShift) && gas > 0f && !odm.Exhausted && wishDir.sqrMagnitude > 0.0001f;
        bool slidingRoof = TryGetSteepGround(out Vector3 roofNormal, out Vector3 downhill);
        float speed = boosting ? odm.groundBoostSpeed : (slidingRoof ? 0f : odm.walkSpeed);

        // Normal walking handles ordinary roof pitches.  A steeper roof only
        // becomes climbable while gas is held, which keeps rooftops useful
        // without letting the player casually walk up walls.
        controller.slopeLimit = boosting ? odm.gasSlopeLimit : 89f;

        if (boosting && TryFindRoofSurface(wishDir, out RaycastHit roof))
        {
            Vector3 alongRoof = Vector3.ProjectOnPlane(wishDir, roof.normal);
            if (alongRoof.sqrMagnitude > 0.0001f)
            {
                alongRoof.Normalize();
                velocity = Vector3.MoveTowards(velocity, alongRoof * speed, odm.groundAccel * dt);
                gas = Mathf.Max(0f, gas - odm.gasDrain * odm.groundGasDrainFactor * dt);
                return;
            }
        }

        if (slidingRoof && !boosting)
        {
            // Gravity projected onto the roof gives the real downhill
            // direction. Player input cannot create walking traction here;
            // use gas or reel the rope to get off the slope.
            if (downhill.sqrMagnitude > 0.0001f)
            {
                downhill.Normalize();
                Vector3 surfaceVelocity = Vector3.ProjectOnPlane(velocity, roofNormal);
                surfaceVelocity += downhill * odm.roofSlideAcceleration * dt;
                surfaceVelocity = Vector3.ClampMagnitude(surfaceVelocity, odm.roofSlideMaxSpeed);
                velocity = surfaceVelocity - roofNormal * odm.roofSurfaceGrip;
                return;
            }
        }

        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        flat = Vector3.MoveTowards(flat, wishDir * speed, odm.groundAccel * dt);
        velocity.x = flat.x;
        velocity.z = flat.z;

        velocity.y = -2f;

        if (boosting)
            gas = Mathf.Max(0f, gas - odm.gasDrain * odm.groundGasDrainFactor * dt);
    }

    bool TryFindRoofSurface(Vector3 wishDir, out RaycastHit best)
    {
        best = default;
        Vector3 forward = wishDir.normalized;
        if (forward.sqrMagnitude < 0.0001f) return false;
        Vector3 origin = transform.position + forward * odm.gasRoofProbeDistance + Vector3.up * odm.gasRoofProbeHeight;

        int count = Physics.RaycastNonAlloc(origin, Vector3.down, roofHits,
                                            odm.gasRoofProbeHeight + controller.height + 2f,
                                            odm.groundLayer, QueryTriggerInteraction.Ignore);
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = roofHits[i];
            if (candidate.collider == null || candidate.transform.IsChildOf(transform)) continue;
            float slope = candidate.normal.y;
            if (slope < 0.08f || slope > 0.995f) continue;
            Vector3 along = Vector3.ProjectOnPlane(forward, candidate.normal);
            if (along.sqrMagnitude < 0.0001f) continue;
            float ahead = Vector3.Dot(along.normalized, forward);
            if (ahead < 0.25f) continue;
            float score = ahead * 10f + candidate.point.y;
            if (score <= bestScore) continue;
            bestScore = score;
            best = candidate;
        }

        if (bestScore == float.NegativeInfinity) return false;
        float angle = Vector3.Angle(best.normal, Vector3.up);
        return angle > 2f && angle <= odm.gasSlopeLimit;
    }

    bool TryGetSteepGround(out Vector3 surfaceNormal, out Vector3 downhill)
    {
        RaycastHit ground;
        if (hasCurrentGroundSurface)
            ground = currentGroundSurface;
        else
        {
            Vector3 origin = transform.position + Vector3.up * 1.25f;
            if (!Physics.Raycast(origin, Vector3.down, out ground, 3.5f, odm.groundLayer,
                                 QueryTriggerInteraction.Ignore)
                || ground.transform.IsChildOf(transform))
            {
                surfaceNormal = Vector3.up;
                downhill = Vector3.zero;
                return false;
            }
        }

        // Do not trust a single imported MeshCollider triangle: this roof pack
        // has hidden ridge/seam triangles whose normals do not match what the
        // player sees. Estimate the visible surface from nearby hit heights.
        float radius = Mathf.Max(controller.radius * 1.25f, 0.55f);
        float sumXX = 0f, sumZZ = 0f, sumXH = 0f, sumZH = 0f;
        SampleGroundGradient(ground, new Vector3(radius, 0f, 0f), ref sumXX, ref sumZZ, ref sumXH, ref sumZH);
        SampleGroundGradient(ground, new Vector3(-radius, 0f, 0f), ref sumXX, ref sumZZ, ref sumXH, ref sumZH);
        SampleGroundGradient(ground, new Vector3(0f, 0f, radius), ref sumXX, ref sumZZ, ref sumXH, ref sumZH);
        SampleGroundGradient(ground, new Vector3(0f, 0f, -radius), ref sumXX, ref sumZZ, ref sumXH, ref sumZH);

        float gradientX = sumXX > 0.0001f ? sumXH / sumXX : 0f;
        float gradientZ = sumZZ > 0.0001f ? sumZH / sumZZ : 0f;
        float gradient = Mathf.Sqrt(gradientX * gradientX + gradientZ * gradientZ);
        float angle = Mathf.Atan(gradient) * Mathf.Rad2Deg;

        surfaceNormal = new Vector3(-gradientX, 1f, -gradientZ).normalized;
        downhill = new Vector3(-gradientX, -gradient * gradient, -gradientZ);
        if (downhill.sqrMagnitude > 0.0001f) downhill.Normalize();
        return angle >= odm.roofSlideMinAngle;
    }

    void SampleGroundGradient(RaycastHit center, Vector3 offset,
                              ref float sumXX, ref float sumZZ,
                              ref float sumXH, ref float sumZH)
    {
        Vector3 origin = center.point + offset + Vector3.up * 2f;
        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit sample, 4f,
                             odm.groundLayer, QueryTriggerInteraction.Ignore))
            return;
        if (sample.collider != center.collider) return;

        float heightDelta = sample.point.y - center.point.y;
        if (Mathf.Abs(heightDelta) > 1.5f) return;

        sumXX += offset.x * offset.x;
        sumZZ += offset.z * offset.z;
        sumXH += offset.x * heightDelta;
        sumZH += offset.z * heightDelta;
    }
}
