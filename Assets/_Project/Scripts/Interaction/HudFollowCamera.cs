// Assets/_Project/Scripts/Interaction/HudFollowCamera.cs
using UnityEngine;

/// <summary>
/// Positions a world-space canvas relative to the HMD. Most existing HUDs retain
/// their original close-follow behavior; reading panels can opt into a stable,
/// obstacle-aware placement profile so they remain on the player's side of walls.
/// </summary>
[DefaultExecutionOrder(20000)]
public partial class HudFollowCamera : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField, Min(0.1f)] private float distance = 0.4f;
    [SerializeField] private Camera targetCamera;
    [Tooltip("Camera-local offset: X right, Y up/down, Z additional distance.")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float smoothTime = 0.05f;
    [SerializeField, Min(0f)] private float rotationSmoothing = 25f;

    [Header("Obstacle-aware reading panels")]
    [Tooltip("Enable for dialogue/question panels. Other HUDs keep their existing placement by default.")]
    [SerializeField] private bool obstacleAwarePlacement;
    [Tooltip("Closest comfortable panel distance from the headset.")]
    [SerializeField, Min(0.25f)] private float minimumDistance = 0.82f;
    [Tooltip("Keep the panel this far in front of a detected surface.")]
    [SerializeField, Min(0.02f)] private float wallClearance = 0.18f;
    [Tooltip("Layers containing solid environment geometry. Triggers are always ignored.")]
    [SerializeField] private LayerMask environmentMask = 1 << 0; // Default: this project has no dedicated Environment layer.
    [Tooltip("Approximate panel center clearance used by sphere casts; increase slightly for wider panels.")]
    [SerializeField, Range(0.01f, 0.2f)] private float obstacleCastRadius = 0.07f;
    [Tooltip("Lateral movement attempted if a wall leaves less than the minimum reading distance.")]
    [SerializeField, Range(0.1f, 0.8f)] private float lateralFallback = 0.32f;
    [Tooltip("Ignore tiny headset position changes while the panel is open.")]
    [SerializeField, Range(0f, 0.2f)] private float positionReplanThreshold = 0.07f;
    [Tooltip("Ignore tiny headset rotation changes while the panel is open.")]
    [SerializeField, Range(0f, 15f)] private float rotationReplanThreshold = 4f;

    private Vector3 positionVelocity;
    private bool hasValidPose;
    private bool hasPlacementTarget;
    private Vector3 placementTarget;
    private Quaternion placementRotation;
    private Vector3 lastHeadPosition;
    private Quaternion lastHeadRotation;
    private int selectedFallbackSide;

    public float Distance { get => distance; set => distance = Mathf.Max(0.1f, value); }
    public Vector3 LocalOffset { get => localOffset; set => localOffset = value; }
    public float MinimumDistance { get => minimumDistance; set => minimumDistance = Mathf.Max(0.25f, value); }
    public float WallClearance { get => wallClearance; set => wallClearance = Mathf.Max(0.02f, value); }
    public LayerMask EnvironmentMask { get => environmentMask; set => environmentMask = value; }
    public bool ObstacleAwarePlacement { get => obstacleAwarePlacement; set => obstacleAwarePlacement = value; }
    public float SmoothTime { get => smoothTime; set => smoothTime = Mathf.Max(0f, value); }
    public float RotationSmoothing { get => rotationSmoothing; set => rotationSmoothing = Mathf.Max(0f, value); }

    private void LateUpdate()
    {
        if (sharedLayout) { UpdateSharedLayout(); return; }
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        Transform head = targetCamera.transform;
        Vector3 desiredPosition = head.position + head.forward * distance + head.rotation * localOffset;
        Quaternion desiredRotation = head.rotation;

        if (obstacleAwarePlacement)
        {
            bool shouldReplan = !hasPlacementTarget
                || Vector3.Distance(head.position, lastHeadPosition) >= positionReplanThreshold
                || Quaternion.Angle(head.rotation, lastHeadRotation) >= rotationReplanThreshold;
            if (shouldReplan)
            {
                placementTarget = FindSafePosition(head, desiredPosition);
                placementRotation = desiredRotation;
                lastHeadPosition = head.position;
                lastHeadRotation = head.rotation;
                hasPlacementTarget = true;
            }
            desiredPosition = placementTarget;
            desiredRotation = placementRotation;
        }

        if (!hasValidPose || smoothTime <= 0f)
        {
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            positionVelocity = Vector3.zero;
            hasValidPose = true;
            return;
        }

        Vector3 smoothedPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity,
            smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        if (obstacleAwarePlacement && HasBlocker(targetCamera.transform.position, smoothedPosition))
        {
            // A smooth interpolation can otherwise cut across a wall while the
            // target moves around it. Preserve the safe endpoint immediately.
            smoothedPosition = desiredPosition;
            positionVelocity = Vector3.zero;
        }
        Quaternion smoothedRotation = rotationSmoothing > 0f
            ? Quaternion.Slerp(transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSmoothing * Time.unscaledDeltaTime))
            : desiredRotation;
        transform.SetPositionAndRotation(smoothedPosition, smoothedRotation);
    }

    private Vector3 FindSafePosition(Transform head, Vector3 desiredPosition)
    {
        Vector3 origin = head.position;
        Vector3 toDesired = desiredPosition - origin;
        Vector3 direction = toDesired.sqrMagnitude > 0.0001f ? toDesired.normalized : head.forward;
        float desiredDistance = Mathf.Max(minimumDistance, toDesired.magnitude);

        if (!TryNearestBlocker(origin, direction, desiredDistance, out RaycastHit hit))
        {
            return desiredPosition;
        }

        float clearDistance = hit.distance - wallClearance;
        if (clearDistance >= minimumDistance)
        {
            return origin + direction * clearDistance;
        }

        // When the player is very close to a wall, test modest lateral placements.
        // Keep the previous side for this open panel whenever it remains clear.
        float forwardDistance = minimumDistance;
        if (selectedFallbackSide != 0)
        {
            Vector3 retained = BuildFallback(origin, head, direction, forwardDistance, selectedFallbackSide);
            if (!HasBlocker(origin, retained)) return retained;
        }

        Vector3 left = BuildFallback(origin, head, direction, forwardDistance, -1);
        Vector3 right = BuildFallback(origin, head, direction, forwardDistance, 1);
        bool leftClear = !HasBlocker(origin, left);
        bool rightClear = !HasBlocker(origin, right);
        if (leftClear || rightClear)
        {
            // Prefer the candidate requiring the smallest translation from the
            // ordinary forward location; deterministic tie-break prevents flicker.
            float leftCost = leftClear ? Vector3.Distance(left, desiredPosition) : float.PositiveInfinity;
            float rightCost = rightClear ? Vector3.Distance(right, desiredPosition) : float.PositiveInfinity;
            selectedFallbackSide = leftCost <= rightCost ? -1 : 1;
            return selectedFallbackSide < 0 ? left : right;
        }

        // No comfortable clear location exists in front. Stay on this side of the
        // nearest surface; a short distance is safer than deliberately clipping it.
        selectedFallbackSide = 0;
        return origin + direction * Mathf.Max(0.12f, clearDistance);
    }

    private Vector3 BuildFallback(Vector3 origin, Transform head, Vector3 direction, float forwardDistance, int side)
    {
        Vector3 horizontalRight = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
        if (horizontalRight.sqrMagnitude < 0.5f) horizontalRight = head.right;
        return origin + direction * forwardDistance + horizontalRight * lateralFallback * side;
    }

    private bool HasBlocker(Vector3 origin, Vector3 candidate)
    {
        Vector3 delta = candidate - origin;
        float length = delta.magnitude;
        return length > 0.01f && TryNearestBlocker(origin, delta / length, length, out _);
    }

    private bool TryNearestBlocker(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit nearest)
    {
        nearest = default;
        RaycastHit[] hits = Physics.SphereCastAll(origin, obstacleCastRadius, direction, maxDistance,
            environmentMask, QueryTriggerInteraction.Ignore);
        bool found = false;
        float nearestDistance = maxDistance;
        foreach (RaycastHit candidate in hits)
        {
            Collider collider = candidate.collider;
            if (collider == null || IsExcludedCollider(collider)) continue;
            if (candidate.distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = candidate.distance;
                found = true;
            }
        }
        return found;
    }

    private bool IsExcludedCollider(Collider collider)
    {
        Transform hitTransform = collider.transform;
        if (hitTransform == transform || hitTransform.IsChildOf(transform)) return true;
        if (targetCamera != null && (hitTransform == targetCamera.transform || hitTransform.IsChildOf(targetCamera.transform))) return true;
        // Reading panels should react to room geometry, not forensic props or
        // interaction tools that happen to sit between the headset and the panel.
        if (collider.GetComponentInParent<PlayerTool>() != null
            || collider.GetComponentInParent<EvidenceProp>() != null
            || collider.GetComponentInParent<EvidenceFingerprintMarker>() != null
            || collider.GetComponentInParent<EvidenceTentPickup>() != null) return true;
        if (collider.tag == "Player" || collider.tag == "PlayerRig") return true;
        if (targetCamera != null && hitTransform.root == targetCamera.transform.root) return true;
        return false;
    }

    private void OnEnable()
    {
        RegisterSharedPanel();
        hasValidPose = false;
        hasPlacementTarget = false;
        selectedFallbackSide = 0;
    }

    private void OnDisable() { UnregisterSharedPanel(); }
}
