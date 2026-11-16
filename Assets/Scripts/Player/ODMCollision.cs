using UnityEngine;

// Va chạm của ODM: đẩy capsule ra khỏi geometry chồng lấn, di chuyển có capsule-cast
// (trượt dọc tường thay vì dính), xử lý vận tốc khi chạm bề mặt và log debug mái nhà.
// Thông số nằm trên ODMController (movementCollisionMask, wallGlideAssist, ...).
public class ODMCollision : MonoBehaviour
{
    ODMController odm;
    CharacterController controller;
    ODMAntiStuck antiStuck;
    ODMGroundMovement groundMove;
    HookState hookLeft, hookRight;

    readonly Collider[] overlapHits = new Collider[32];
    Collider lastContactCollider;
    Vector3 lastContactPoint;
    Vector3 lastContactNormal;
    Vector3 lastContactMoveDirection;
    float lastContactTime = -10f;
    float nextRoofDebugLog;
    bool loggedRoofContactSample;

    public void Init(ODMController odm, CharacterController controller, ODMAntiStuck antiStuck,
                     ODMGroundMovement groundMove, HookState left, HookState right)
    {
        this.odm = odm;
        this.controller = controller;
        this.antiStuck = antiStuck;
        this.groundMove = groundMove;
        hookLeft = left;
        hookRight = right;
    }

    public void ResolveOverlap()
    {
        if (controller == null || !controller.enabled) return;

        Vector3 scale = transform.lossyScale;
        float worldHeight = controller.height * Mathf.Abs(scale.y);
        float worldRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        Vector3 center = transform.TransformPoint(controller.center);
        float halfSegment = Mathf.Max(0f, worldHeight * 0.5f - worldRadius);
        Vector3 top = center + Vector3.up * halfSegment;
        Vector3 bottom = center - Vector3.up * halfSegment;
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, worldRadius,
                                                   overlapHits, odm.groundLayer,
                                                   QueryTriggerInteraction.Ignore);
        Vector3 correction = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider other = overlapHits[i];
            if (other == null || other == controller || other.transform.IsChildOf(transform)) continue;
            if (Physics.ComputePenetration(controller, transform.position, transform.rotation,
                                           other, other.transform.position, other.transform.rotation,
                                           out Vector3 direction, out float distance))
                correction += direction * distance;
        }

        if (correction.sqrMagnitude < 0.000001f) return;
        // ComputePenetration against a single-sided (non-convex) house mesh can
        // point to the back side of a facade. A large teleport along that
        // direction drops the capsule inside the building, where the
        // CharacterController can no longer move in any direction. Keep the
        // push small and refuse one that would end inside geometry.
        correction = Vector3.ClampMagnitude(correction, worldRadius * 0.5f);
        Vector3 target = transform.position + correction + correction.normalized * 0.015f;
        Vector3 targetCenter = center + (target - transform.position);
        if (antiStuck.IsInsideGeometry(targetCenter) && !antiStuck.IsInsideGeometry(center)) return;

        controller.enabled = false;
        transform.position = target;
        controller.enabled = true;
        odm.Velocity = Vector3.ProjectOnPlane(odm.Velocity, correction.normalized);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        lastContactCollider = hit.collider;
        lastContactPoint = hit.point;
        lastContactNormal = hit.normal;
        lastContactMoveDirection = hit.moveDirection;
        lastContactTime = Time.time;
        ApplyWallContact(hit.normal);
    }

    public void Move(Vector3 displacement)
    {
        if (displacement.sqrMagnitude < 0.0000001f)
        {
            controller.Move(displacement);
            return;
        }

        Vector3 scale = transform.lossyScale;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = controller.height * Mathf.Abs(scale.y);
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 center = transform.TransformPoint(controller.center);
        Vector3 top = center + Vector3.up * halfSegment;
        Vector3 bottom = center - Vector3.up * halfSegment;
        Vector3 direction = displacement.normalized;
        float distance = displacement.magnitude;
        float skin = odm.wallCollisionSkin;
        float castRadius = Mathf.Max(0.01f, radius - skin);

        if (Physics.CapsuleCast(bottom, top, castRadius, direction, out RaycastHit hit,
                                distance + skin, odm.movementCollisionMask,
                                QueryTriggerInteraction.Ignore)
            && !hit.transform.IsChildOf(transform))
        {
            // Let CharacterController resolve real roof slopes.  The custom
            // wall slide below is for vertical walls; applying it to a roof
            // would erase the uphill component and trap the player in place.
            if (odm.IsGrounded && hit.normal.y > 0.55f)
            {
                controller.Move(displacement);
                GameAudio.Play(Sfx.Impact, hit.point, 0.5f);
                return;
            }

            float safeDistance = Mathf.Max(0f, hit.distance - skin);
            if (safeDistance < distance)
            {
                controller.Move(direction * safeDistance);

                // Preserve movement that runs along the wall, while discarding
                // the component that would push the controller through it.
                Vector3 remaining = displacement - direction * safeDistance;
                Vector3 slide = Vector3.ProjectOnPlane(remaining, hit.normal);
                if (slide.sqrMagnitude > 0.0000001f)
                    controller.Move(slide);

                ApplyWallContact(hit.normal);
                return;
            }
        }

        controller.Move(displacement);
    }

    void ApplyWallContact(Vector3 normal)
    {
        if (odm.IsGrounded && normal.y > 0.1f) return;

        Vector3 velocity = odm.Velocity;
        float into = Vector3.Dot(velocity, normal); // < 0: đang lao vào bề mặt
        if (into >= 0f) return;

        Vector3 incoming = velocity;
        Vector3 alongWall = Vector3.ProjectOnPlane(incoming, normal);
        bool isWall = Mathf.Abs(normal.y) < 0.5f;
        bool hooked = hookLeft.active || hookRight.active;

        if (odm.wallGlideAssist && hooked && isWall)
        {
            // Đổi một phần cú đâm trực diện thành chuyển động theo mặt tường về hướng camera.
            // Người chơi vẫn phải nhìn/chọn hướng cua, nhưng va quệt không làm mất sạch đà.
            Vector3 desired = Vector3.ProjectOnPlane(odm.cameraTransform.forward, normal);
            if (desired.sqrMagnitude > 0.0001f)
            {
                desired.Normalize();
                float targetAlong = incoming.magnitude * odm.wallGlideMomentum;
                float currentAlong = Vector3.Dot(alongWall, desired);
                if (currentAlong < targetAlong)
                    alongWall += desired * (targetAlong - currentAlong);
            }

            alongWall += normal * odm.wallSeparationSpeed;
        }

        float speedLimit = odm.maxSpeed * (odm.ActiveHookCount >= 2 ? odm.dualHookMaxSpeedMultiplier : 1f);
        odm.Velocity = Vector3.ClampMagnitude(alongWall, speedLimit);
    }

    public void LogRoofContact(Vector3 frameStartPosition)
    {
        if (!odm.logRoofCollisionDebug || Time.time < nextRoofDebugLog) return;

        float input = new Vector2(GameInput.GetAxis("Horizontal"), GameInput.GetAxis("Vertical")).magnitude;
        bool tryingToMove = input > 0.2f || GameInput.GetKey(KeyCode.LeftShift)
                           || (hookLeft.active || hookRight.active) && GameInput.GetKey(KeyCode.Space);
        float moved = Vector3.Distance(frameStartPosition, transform.position);
        bool stuck = tryingToMove && moved <= 0.025f;
        Vector3 scale = transform.lossyScale;
        float worldHeight = controller.height * Mathf.Abs(scale.y);
        float worldRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        Vector3 worldCenter = transform.TransformPoint(controller.center);
        float capsuleBottomY = worldCenter.y - worldHeight * 0.5f;

        string groundInfo = "groundHit=none";
        float groundAngle = 0f;
        if (Physics.Raycast(worldCenter + Vector3.up * 0.2f, Vector3.down,
                            out RaycastHit ground, worldHeight + 2f, odm.groundLayer,
                            QueryTriggerInteraction.Ignore)
            && !ground.transform.IsChildOf(transform))
        {
            groundAngle = Vector3.Angle(ground.normal, Vector3.up);
            groundInfo = $"ground={ground.collider.name}, pointY={ground.point.y:F3}, "
                       + $"bottomY={capsuleBottomY:F3}, gap={capsuleBottomY - ground.point.y:F3}, "
                       + $"normal={ground.normal}, angle={groundAngle:F1}";
            Debug.DrawRay(ground.point, ground.normal * 3f, Color.cyan, 1f);
        }

        // Keep normal gameplay logs quiet. Sample the first surface under the
        // player even when the collider reports it as flat; imported roof
        // meshes can contain hidden horizontal triangles around the ridge.
        if (!stuck && loggedRoofContactSample) return;
        if (!stuck) loggedRoofContactSample = true;
        nextRoofDebugLog = Time.time + 0.75f;

        string contactInfo = Time.time - lastContactTime < 0.75f && lastContactCollider != null
            ? $"contact={lastContactCollider.name}, point={lastContactPoint}, normal={lastContactNormal}, "
              + $"angle={Vector3.Angle(lastContactNormal, Vector3.up):F1}, moveDir={lastContactMoveDirection}"
            : "contact=none";

        string message = $"[RoofCollision] {(stuck ? "STUCK" : "CONTACT")} pos={transform.position}, "
                       + $"moved={moved:F4}, velocity={odm.Velocity}, "
                       + $"grounded={odm.IsGrounded}/{controller.isGrounded}, "
                       + $"slopeLimit={controller.slopeLimit:F1}, slideMin={odm.roofSlideMinAngle:F1}, "
                       + $"cachedGround={groundMove.HasCurrentGroundSurface}, radius={worldRadius:F2}; "
                       + groundInfo + "; " + contactInfo;
        if (stuck) Debug.LogWarning(message, odm);
        else Debug.Log(message, odm);
    }
}
