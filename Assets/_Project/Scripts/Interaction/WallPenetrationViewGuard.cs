using Oculus.Interaction;
using UnityEngine;

/// <summary>Wall comfort mask for actual head crossings, excluding interaction and procedure volumes.</summary>
[DefaultExecutionOrder(31000)] // After SceneCordon corrects a prohibited crossing.
[DisallowMultipleComponent]
public sealed class WallPenetrationViewGuard : MonoBehaviour
{
    [SerializeField] private Transform trackedHead;
    [SerializeField] private Transform logicalHead;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private TunnelingEffect effect;
    [SerializeField] private MeshRenderer maskRenderer;
    [SerializeField] private LayerMask wallLayers = 3; // Default + TransparentFX; no UI or Ignore Raycast.
    [SerializeField] private AnimationCurve penetrationFov = AnimationCurve.Linear(0, 360, .3f, 40);
    private RaycastHit[] hits = new RaycastHit[32];

    private void Awake() => ClearMask();
    private void OnDisable() => ClearMask();
    private void LateUpdate() => Refresh();

    public bool TryGetPenetration(Vector3 logical, Vector3 tracked, out float depth, out Collider obstruction)
    {
        depth = 0; obstruction = null;
        Vector3 delta = tracked - logical;
        float length = delta.magnitude;
        if (length < .001f) return false;
        int count;
        // Retry a saturated buffer: an unordered non-alloc result must not hide a real wall behind UI hits.
        while ((count = Physics.RaycastNonAlloc(logical, delta / length, hits, length, wallLayers, QueryTriggerInteraction.Ignore)) == hits.Length)
            hits = new RaycastHit[hits.Length * 2];
        float nearest = length;
        for (int i = 0; i < count; i++)
        {
            var collider = hits[i].collider;
            if (collider == null || collider.CompareTag("Player")
                || (playerRoot != null && collider.transform.IsChildOf(playerRoot))
                || collider.GetComponentInParent<Canvas>() != null
                || collider.transform is RectTransform
                || collider.GetComponentInParent<HousePhotographyEntryGate>() != null) continue;
            if (hits[i].distance >= nearest) continue;
            nearest = hits[i].distance; obstruction = collider;
        }
        if (obstruction == null) return false;
        depth = length - nearest;
        return true;
    }

    public void Refresh()
    {
        if (trackedHead == null || logicalHead == null || effect == null || maskRenderer == null
            || !TryGetPenetration(logicalHead.position, trackedHead.position, out float depth, out _))
        { ClearMask(); return; }
        float fov = penetrationFov.Evaluate(depth);
        if (fov >= 360f) { ClearMask(); return; }
        effect.UseAimingTarget = true;
        effect.AimingDirection = (logicalHead.position - trackedHead.position).normalized;
        effect.UserFOV = fov;
        effect.enabled = true;
        maskRenderer.enabled = true;
    }

    private void ClearMask()
    {
        if (effect != null) { effect.UserFOV = 360; effect.enabled = false; }
        // Explicitly clear even before the SDK's Start/OnDisable lifecycle has initialized its renderer.
        if (maskRenderer != null) maskRenderer.enabled = false;
    }
}
