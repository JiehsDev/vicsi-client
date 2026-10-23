using System;
using System.Collections.Generic;
using UnityEngine;

public partial class HudFollowCamera
{
    [Header("Shared VR layout")]
    [SerializeField] private bool sharedLayout;
    [SerializeField] private int placementPriority = PlayerUIGate.InformationPriority;
    [Tooltip("Companions share the primary's safe anchor and occupy measured slots above it.")]
    [SerializeField] private bool companionPanel;
    [SerializeField, Min(0.02f)] private float companionGap = 0.06f;
    [Tooltip("Use a measured slot below the primary instead of the dialogue slot above it.")]
    [SerializeField] private bool companionBelowPrimary;
    [Tooltip("Keep the opening world pose instead of following head turns (Notes, sketch, checklist).")]
    [SerializeField] private bool anchoredPlacement;
    private object layoutOwner;
    private Func<bool> visibleWhen;
    private Func<bool> deferWhen;
    private Canvas layoutCanvas;
    private bool suppressedByLayout;
    private bool hasAnchor;
    private Vector3 anchorPosition;
    private Quaternion anchorRotation;
    private static readonly List<HudFollowCamera> activePanels = new();
    private static readonly List<HudFollowCamera> group = new();
    private static readonly List<Vector3> slots = new();
    private static int layoutFrame = -1, groupSignature;
    private static Vector3 groupPosition, groupVelocity, groupTarget;
    private static Quaternion groupRotation;
    private static bool groupPoseValid;
    public bool AnchoredPlacement => anchoredPlacement;
    public bool SharedLayout => sharedLayout;
    public bool IsLayoutSuppressed => suppressedByLayout;
    public int FallbackSide => selectedFallbackSide;
    public int PlacementPriority { get => placementPriority; set => placementPriority = value; }
    public static bool IsOwnerSuppressed(object owner)
    {
        foreach (var panel in activePanels)
            if (panel != null && panel.isActiveAndEnabled && Equals(panel.layoutOwner, owner) && panel.suppressedByLayout) return true;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLayout()
    {
        activePanels.Clear(); group.Clear(); slots.Clear(); layoutFrame = -1;
        groupSignature = 0; groupPoseValid = false;
    }
    public static HudFollowCamera ConfigureShared(GameObject panel, object owner, int priority,
        bool companion = false, Func<bool> visible = null)
    {
        if (panel == null) return null;
        // The lobby already owns one fixed, desk-height canvas and its internal rows.
        if (panel.GetComponentInParent<LobbyCanvasPlacement>() != null) return panel.GetComponent<HudFollowCamera>();
        var follow = panel.GetComponent<HudFollowCamera>();
        if (follow == null) follow = panel.AddComponent<HudFollowCamera>();
        bool wasAware = follow.obstacleAwarePlacement;
        follow.sharedLayout = true; follow.obstacleAwarePlacement = true;
        follow.layoutOwner = owner; follow.placementPriority = priority;
        follow.companionPanel = companion; follow.visibleWhen = visible;
        follow.deferWhen = null;
        if (!wasAware)
        {
            // Retain each panel's authored distance/size/offset, including closer tool reports.
            follow.minimumDistance = Mathf.Min(0.82f, follow.distance);
            follow.smoothTime = 0.12f; follow.rotationSmoothing = 8f;
        }
        follow.RegisterSharedPanel();
        return follow;
    }
    public void SetWorldAnchor(Vector3 position, Quaternion rotation)
    {
        anchoredPlacement = true; hasAnchor = true;
        anchorPosition = position; anchorRotation = rotation;
        hasPlacementTarget = false; groupPoseValid = false;
    }
    /// <summary>Non-interactive, compact floating feedback; collision queries stay in this shared solver.</summary>
    public static HudFollowCamera ConfigureTemporaryFeedback(GameObject panel, object owner, Func<bool> visible)
    {
        var follow = ConfigureShared(panel, owner, PlayerUIGate.ProceduralFeedbackPriority, true, visible);
        if (follow == null || panel.GetComponentInParent<LobbyCanvasPlacement>() != null) return follow;
        follow.distance = .90f; follow.localOffset = new Vector3(0, -.18f, 0);
        follow.minimumDistance = .60f; follow.wallClearance = .12f;
        follow.obstacleCastRadius = .035f; follow.lateralFallback = .32f;
        follow.smoothTime = .10f; follow.rotationSmoothing = 8f;
        follow.positionReplanThreshold = .05f; follow.rotationReplanThreshold = 3f;
        follow.companionGap = .05f; follow.companionBelowPrimary = true;
        follow.deferWhen = () => PlayerUIGate.IsBlocked;
        return follow;
    }
    private void RegisterSharedPanel()
    {
        if (!activePanels.Contains(this)) activePanels.Add(this);
    }
    private void UnregisterSharedPanel()
    {
        activePanels.Remove(this); SetLayoutSuppressed(false); hasAnchor = false;
    }
    private bool WantsLayout()
    {
        if (!sharedLayout || !isActiveAndEnabled) return false;
        if (visibleWhen != null) return visibleWhen();
        var cg = GetComponent<CanvasGroup>();
        return cg == null || cg.alpha > 0.001f;
    }
    private void SetLayoutSuppressed(bool value)
    {
        if (value == suppressedByLayout) return;
        if (layoutCanvas == null) layoutCanvas = GetComponent<Canvas>();
        // Never turn a Canvas back on unless this layout actually turned it off.
        if (value && layoutCanvas != null && !layoutCanvas.enabled) return;
        suppressedByLayout = value;
        if (layoutCanvas != null) layoutCanvas.enabled = !value;
    }
    private Vector2 PanelHalfSize()
    {
        var rect = transform as RectTransform;
        if (rect == null) return Vector2.one * obstacleCastRadius;
        return Vector2.Scale(rect.rect.size, new Vector2(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y))) * 0.5f;
    }
    private Vector3 PivotToCenter()
    {
        var rect = transform as RectTransform;
        if (rect == null) return Vector3.zero;
        return Vector3.Scale(rect.rect.center, transform.lossyScale);
    }
    private void UpdateSharedLayout()
    {
        if (layoutFrame == Time.frameCount) return;
        layoutFrame = Time.frameCount;
        group.Clear(); slots.Clear();
        HudFollowCamera primary = null;
        foreach (var panel in activePanels)
        {
            if (panel == null || !panel.WantsLayout()) { if (panel != null) panel.SetLayoutSuppressed(false); continue; }
            if (panel.deferWhen?.Invoke() == true) { panel.SetLayoutSuppressed(true); continue; }
            if (primary == null || (!panel.companionPanel && primary.companionPanel)
                || (panel.companionPanel == primary.companionPanel && (panel.placementPriority > primary.placementPriority
                || (panel.placementPriority == primary.placementPriority && panel.GetEntityId().GetHashCode() < primary.GetEntityId().GetHashCode())))) primary = panel;
        }
        if (primary == null) { groupPoseValid = false; groupSignature = 0; return; }
        group.Add(primary); slots.Add(Vector3.zero);
        foreach (var panel in activePanels)
        {
            if (panel == null || panel == primary || !panel.WantsLayout() || panel.deferWhen?.Invoke() == true) continue;
            if (!panel.companionPanel) panel.SetLayoutSuppressed(true);
            else group.Add(panel);
        }
        // Deterministic companion order, independent of Update/enable order.
        if (group.Count > 2) group.Sort(1, group.Count - 1, Comparer<HudFollowCamera>.Create((a,b) =>
            a.placementPriority != b.placementPriority ? b.placementPriority.CompareTo(a.placementPriority) : a.GetEntityId().GetHashCode().CompareTo(b.GetEntityId().GetHashCode())));
        float top = primary.PivotToCenter().y + primary.PanelHalfSize().y;
        float bottom = primary.PivotToCenter().y - primary.PanelHalfSize().y;
        int signature = primary.GetEntityId().GetHashCode();
        for (int i = 1; i < group.Count; i++)
        {
            var panel = group[i];
            float y = panel.companionBelowPrimary
                ? bottom - panel.companionGap - panel.PanelHalfSize().y - panel.PivotToCenter().y
                : top + panel.companionGap + panel.PanelHalfSize().y - panel.PivotToCenter().y;
            slots.Add(new Vector3(0, y, 0));
            if (panel.companionBelowPrimary) bottom = y + panel.PivotToCenter().y - panel.PanelHalfSize().y;
            else top = y + panel.PivotToCenter().y + panel.PanelHalfSize().y;
            signature = unchecked(signature * 31 + panel.GetEntityId().GetHashCode());
        }
        if (signature != groupSignature)
        {
            groupSignature = signature; groupPoseValid = false;
            primary.selectedFallbackSide = 0; primary.hasPlacementTarget = false;
        }
        if (primary.targetCamera == null) primary.targetCamera = Camera.main;
        if (primary.targetCamera == null) return;
        var head = primary.targetCamera.transform;
        foreach (var panel in group) panel.targetCamera = primary.targetCamera;
        if (primary.anchoredPlacement && !primary.hasAnchor)
        { primary.anchorPosition = primary.transform.position; primary.anchorRotation = primary.transform.rotation; primary.hasAnchor = true; }
        Vector3 desired = primary.anchoredPlacement ? primary.anchorPosition
            : head.position + head.forward * primary.distance + head.rotation * primary.localOffset;
        Quaternion rotation = primary.anchoredPlacement ? primary.anchorRotation : head.rotation;
        bool replan = !groupPoseValid || !primary.hasPlacementTarget
            || Vector3.Distance(head.position, primary.lastHeadPosition) >= primary.positionReplanThreshold
            || (!primary.anchoredPlacement && Quaternion.Angle(head.rotation, primary.lastHeadRotation) >= primary.rotationReplanThreshold)
            || !GroupFits(head.position, groupTarget, primary.placementRotation);
        if (replan)
        {
            bool safe = primary.FindGroupPose(head, desired, ref rotation, out groupTarget);
            // Space may fit the modal but not its companion. Defer companions first.
            if (!safe && group.Count > 1)
            {
                for (int i = 1; i < group.Count; i++) group[i].SetLayoutSuppressed(true);
                group.RemoveRange(1, group.Count - 1); slots.RemoveRange(1, slots.Count - 1);
                safe = primary.FindGroupPose(head, desired, ref rotation, out groupTarget);
            }
            if (!safe)
            {
                primary.SetLayoutSuppressed(true); groupPoseValid = false; return;
            }
            primary.placementRotation = rotation; primary.hasPlacementTarget = true;
            primary.lastHeadPosition = head.position; primary.lastHeadRotation = head.rotation;
        }
        if (!groupPoseValid || primary.smoothTime <= 0)
        { groupPosition = groupTarget; groupRotation = primary.placementRotation; groupVelocity = Vector3.zero; }
        else
        {
            groupPosition = Vector3.SmoothDamp(groupPosition, groupTarget, ref groupVelocity, primary.smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            groupRotation = Quaternion.Slerp(groupRotation, primary.placementRotation, 1 - Mathf.Exp(-primary.rotationSmoothing * Time.unscaledDeltaTime));
            if (!GroupFits(head.position, groupPosition, groupRotation))
            { groupPosition = groupTarget; groupRotation = primary.placementRotation; groupVelocity = Vector3.zero; }
        }
        groupPoseValid = true;
        for (int i = 0; i < group.Count; i++)
        {
            var panel = group[i]; panel.SetLayoutSuppressed(false);
            panel.selectedFallbackSide = primary.selectedFallbackSide;
            panel.transform.SetPositionAndRotation(groupPosition + groupRotation * slots[i], groupRotation);
        }
    }
    private bool FindGroupPose(Transform head, Vector3 desired, ref Quaternion rotation, out Vector3 safe)
    {
        Vector3 preferredDelta = desired - head.position;
        Vector3 preferredDirection = preferredDelta.sqrMagnitude > .001f ? preferredDelta.normalized : head.forward;
        float readableDistance = preferredDelta.magnitude;
        for (int i = 0; i < group.Count; i++)
        {
            Vector3 offset = rotation * slots[i];
            float projection = Vector3.Dot(preferredDirection, offset);
            float discriminant = projection * projection + group[i].minimumDistance * group[i].minimumDistance - offset.sqrMagnitude;
            if (discriminant > 0f) readableDistance = Mathf.Max(readableDistance, -projection + Mathf.Sqrt(discriminant));
        }
        desired = head.position + preferredDirection * readableDistance;
        safe = desired;
        // The original shared center-cast solver provides the first wall-limited candidate.
        int retainedSide = selectedFallbackSide;
        Vector3 centerSafe = FindSafePosition(head, desired);
        int centerSide = selectedFallbackSide;
        selectedFallbackSide = retainedSide;
        if (GroupFits(head.position, desired, rotation)) return true;
        if (retainedSide == 0 && GroupFits(head.position, centerSafe, rotation)
            && Vector3.Distance(head.position, centerSafe) >= minimumDistance)
        { selectedFallbackSide = centerSide; safe = centerSafe; return true; }
        Vector3 delta = desired - head.position;
        Vector3 direction = delta.sqrMagnitude > 0.001f ? delta.normalized : head.forward;
        float preferred = delta.magnitude;
        for (float d = preferred; d >= minimumDistance - .001f; d -= .05f)
        {
            Vector3 candidate = head.position + direction * d;
            if (GroupFits(head.position, candidate, rotation)) { safe = candidate; return true; }
        }
        // Choose a side once per visible group, then retain it even if temporarily obstructed.
        for (int pass = 0; pass < (retainedSide == 0 ? 2 : 1); pass++)
        {
            int side = retainedSide != 0 ? retainedSide : (pass == 0 ? -1 : 1);
            for (int step = 1; step <= 4; step++)
            {
                Vector3 candidate = head.position + direction * minimumDistance + head.right * (lateralFallback * step * side);
                if (!GroupFits(head.position, candidate, rotation)) continue;
                selectedFallbackSide = side; safe = candidate; return true;
            }
            // For a broad wall close to the face, turn the whole readable group toward
            // a side opening. Radial distance stays readable and companions turn together.
            foreach (float angle in new[] { 35f, 60f, 85f })
            {
                Quaternion turn = Quaternion.AngleAxis(angle * side, Vector3.up);
                Vector3 candidate = head.position + turn * direction * Mathf.Max(minimumDistance, preferred);
                Quaternion turned = turn * rotation;
                if (!GroupFits(head.position, candidate, turned)) continue;
                selectedFallbackSide = side; safe = candidate; rotation = turned; return true;
            }
        }
        // A very tight enclosure may have no readable slot. Defer instead of crossing geometry.
        return false;
    }
    private static bool GroupFits(Vector3 origin, Vector3 position, Quaternion rotation)
    {
        for (int i = 0; i < group.Count; i++)
        {
            Vector3 panelPosition = position + rotation * slots[i];
            if (Vector3.Distance(origin, panelPosition) < group[i].minimumDistance - .001f
                || !group[i].PanelFits(origin, panelPosition, rotation)) return false;
        }
        return true;
    }
    public bool PanelFits(Vector3 origin, Vector3 position, Quaternion rotation)
    {
        Vector2 half = PanelHalfSize();
        Vector3 center = position + rotation * PivotToCenter();
        // Sweep the full rectangle, not just its center. Also test endpoints because casts
        // starting inside geometry can miss an overlap. Both reuse the same mask/exclusions.
        Vector3 extents = new Vector3(half.x + .015f, half.y + .015f, wallClearance);
        Vector3 delta = center - origin;
        foreach (var hit in Physics.BoxCastAll(origin, extents, delta.normalized, rotation, delta.magnitude,
            environmentMask, QueryTriggerInteraction.Ignore))
            if (hit.collider != null && !IsExcludedCollider(hit.collider)
                && (hit.distance > .001f || OriginOverlapsCollider(hit.collider, origin))) return false;
        // The sweep starts with a full-sized rectangle at the eyes. A wide, turned
        // rectangle can overlap a nearby wall there even when the actual panel and
        // every sightline fit. Ignore that artificial initial overlap, but still
        // reject an eye inside solid geometry; endpoint overlaps and rays below
        // remain authoritative for the real panel and its visibility.
        foreach (var collider in Physics.OverlapBox(center, extents, rotation, environmentMask, QueryTriggerInteraction.Ignore))
            if (!IsExcludedCollider(collider)) return false;
        // Rays protect against a wall intersecting the swept box at its starting position.
        for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
            if (HasBlocker(origin, center + rotation * new Vector3(half.x * x, half.y * y, 0))) return false;
        return true;
    }
    private bool OriginOverlapsCollider(Collider collider, Vector3 origin)
    {
        // Unity rejects ClosestPoint for non-convex mesh colliders. Architectural mesh
        // surfaces use a supported tiny-volume overlap at the eyes; endpoint overlaps
        // and visibility sweeps above still test the complete panel rectangle.
        if (collider is MeshCollider mesh && !mesh.convex)
        {
            foreach (var overlap in Physics.OverlapSphere(origin, .002f, environmentMask, QueryTriggerInteraction.Ignore))
                if (overlap == collider) return true;
            return false;
        }
        return (collider.ClosestPoint(origin) - origin).sqrMagnitude < .000001f;
    }
}
