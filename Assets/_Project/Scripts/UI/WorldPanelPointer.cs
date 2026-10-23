// Assets/_Project/Scripts/UI/WorldPanelPointer.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Controller-ray clicking for one world-space panel, built on a raw InputAction (right
/// trigger) and plain geometry: the right controller's ray is intersected with the
/// canvas plane and tested against registered hit rectangles. No EventSystem,
/// GraphicRaycaster or ISDK PointableCanvas - that chain is confirmed not to reach this
/// project's runtime-built world-space canvases (see RecorderReportUI), so panels keep
/// their thumbstick/button InputActions and gain this pointer on top.
///
/// Each target's hit rectangle is its visual RectTransform grown by a padding, so the ray
/// target is a little larger than what is drawn. The owner lays targets out so the grown
/// rectangles don't overlap; if they ever did, the first registered target wins.
///
/// Owned and ticked by the panel (Tick while open). Draws a thin ray and a cursor dot
/// only while the controller points at the panel.
/// </summary>
public class WorldPanelPointer
{
    public class Target
    {
        public string id;
        public RectTransform rect;
        public Vector2 padding;
        public Action onClick;
        public Action<bool> onHover;
        public bool enabled = true;
    }

    private readonly RectTransform canvasRect;
    private readonly List<Target> targets = new();
    private readonly InputAction clickAction;
    private readonly RectTransform cursor;
    private readonly LineRenderer line;
    private Target hovered;
    private OVRCameraRig rig;
    private float ignoreClicksUntil;
    private readonly object owner;
    private bool waitForTriggerRelease;
    private static int clickedFrame = -1;

    /// <summary>Overrides the controller ray (tests / editor without a headset). Null = use the right controller.</summary>
    public Func<Ray?> RayOverride;

    public float MaxDistance = 4f;

    /// <summary>The id of the target under the ray, or null.</summary>
    public string HoveredId => hovered?.id;

    public IReadOnlyList<Target> Targets => targets;

    public WorldPanelPointer(RectTransform canvasRect, string actionName, object owner = null)
    {
        this.owner = owner;
        this.canvasRect = canvasRect;
        clickAction = new InputAction(actionName + "_PointerClick", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");

        var dot = ForensicUIStyle.Circle(canvasRect, "PointerCursor", false, ForensicUIStyle.TextPrimary, 14f);
        cursor = dot.rectTransform;
        cursor.anchorMin = cursor.anchorMax = new Vector2(0.5f, 0.5f);
        cursor.gameObject.SetActive(false);

        var lineGo = EditableSceneVisuals.Create("PointerRay", typeof(LineRenderer));
        lineGo.transform.SetParent(canvasRect, false);
        line = lineGo.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = 0.0025f;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        var shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            line.material = new Material(shader);
        }
        line.startColor = new Color(0.93f, 0.93f, 0.90f, 0.05f);
        line.endColor = new Color(0.93f, 0.93f, 0.90f, 0.55f);
        line.enabled = false;
    }

    public Target Register(string id, RectTransform rect, Vector2 padding, Action onClick, Action<bool> onHover = null)
    {
        var target = new Target { id = id, rect = rect, padding = padding, onClick = onClick, onHover = onHover };
        targets.Add(target);
        return target;
    }

    public void Unregister(string idPrefix)
    {
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            if (targets[i].id.StartsWith(idPrefix, StringComparison.Ordinal))
            {
                if (hovered == targets[i]) SetHovered(null);
                targets.RemoveAt(i);
            }
        }
    }

    public void Enable()
    {
        clickAction.Enable();
        // A trigger already held when the panel opens (or the press that opened it) is not a click.
        ignoreClicksUntil = Time.unscaledTime + 0.25f;
        waitForTriggerRelease = true;
    }

    public void Disable()
    {
        clickAction.Disable();
        SetHovered(null);
        // The panel may already be destroyed (scene unload / leaving play mode).
        if (cursor != null) cursor.gameObject.SetActive(false);
        if (line != null) line.enabled = false;
    }

    public void Rebind()
    {
        clickAction.Disable();
        clickAction.Enable();
    }

    public void Dispose()
    {
        clickAction.Dispose();
    }

    public void Tick()
    {
        if (owner != null && !PlayerUIGate.CanUsePanel(owner))
        {
            SetHovered(null); cursor.gameObject.SetActive(false); line.enabled = false; return;
        }
        if (!clickAction.IsPressed()) waitForTriggerRelease = false;
        Ray? ray = RayOverride != null ? RayOverride() : ControllerRay();
        Target hit = null;
        Vector3 point = default;
        bool onPanel = ray.HasValue && TryHitPanel(ray.Value, out point);
        if (onPanel)
        {
            hit = HitTest(point);
        }
        SetHovered(hit);

        cursor.gameObject.SetActive(onPanel);
        line.enabled = onPanel && RayOverride == null;
        if (onPanel)
        {
            Vector3 local = canvasRect.InverseTransformPoint(point);
            cursor.anchoredPosition = new Vector2(local.x, local.y);
            cursor.SetAsLastSibling();
            line.SetPosition(0, ray.Value.origin);
            line.SetPosition(1, point);
        }

        if (hit != null && !waitForTriggerRelease && clickedFrame != Time.frameCount
            && clickAction.WasPressedThisFrame() && Time.unscaledTime >= ignoreClicksUntil)
        {
            clickedFrame = Time.frameCount;
            waitForTriggerRelease = true;
            InteractionSfx.Select();
            hit.onClick?.Invoke();
        }
    }

    /// <summary>Clicks whatever a world ray hits on this panel. Returns the target id, or null. (Tests.)</summary>
    public string ClickWithRay(Ray ray)
    {
        if (!TryHitPanel(ray, out var point))
        {
            return null;
        }
        var hit = HitTest(point);
        if(hit!=null) InteractionSfx.Select();
        hit?.onClick?.Invoke();
        return hit?.id;
    }

    /// <summary>The target whose grown hit rectangle contains a world point, or null.</summary>
    public Target HitTest(Vector3 worldPoint)
    {
        foreach (var target in targets)
        {
            if (target.enabled && target.rect != null && target.rect.gameObject.activeInHierarchy && Contains(target, worldPoint))
            {
                return target;
            }
        }
        return null;
    }

    public static bool Contains(Target target, Vector3 worldPoint)
    {
        // Clipped, offscreen rows must not intercept rays over a header/footer or another option.
        foreach (var mask in target.rect.GetComponentsInParent<RectMask2D>(false))
        {
            if (!mask.isActiveAndEnabled) continue;
            var rect = (RectTransform)mask.transform;
            var clippedPoint = rect.InverseTransformPoint(worldPoint);
            if (!rect.rect.Contains(new Vector2(clippedPoint.x, clippedPoint.y))) return false;
        }
        Vector3 local = target.rect.InverseTransformPoint(worldPoint);
        var r = target.rect.rect;
        return local.x >= r.xMin - target.padding.x && local.x <= r.xMax + target.padding.x
            && local.y >= r.yMin - target.padding.y && local.y <= r.yMax + target.padding.y;
    }

    /// <summary>World-space centre of a target (tests aim a ray at it).</summary>
    public Vector3 WorldCentre(string id)
    {
        foreach (var target in targets)
        {
            if (target.id == id)
            {
                return target.rect.TransformPoint(target.rect.rect.center);
            }
        }
        return canvasRect.position;
    }

    private bool TryHitPanel(Ray ray, out Vector3 point)
    {
        point = default;
        var plane = new Plane(-canvasRect.forward, canvasRect.position);
        if (!plane.Raycast(ray, out float distance) || distance > MaxDistance)
        {
            return false;
        }
        point = ray.GetPoint(distance);
        Vector3 local = canvasRect.InverseTransformPoint(point);
        return canvasRect.rect.Contains(new Vector2(local.x, local.y));
    }

    private Ray? ControllerRay()
    {
        if (rig == null)
        {
            rig = UnityEngine.Object.FindAnyObjectByType<OVRCameraRig>();
        }
        var hand = rig != null ? rig.rightHandAnchor : null;
        return hand != null ? new Ray(hand.position, hand.forward) : (Ray?)null;
    }

    private void SetHovered(Target target)
    {
        if (hovered == target)
        {
            return;
        }
        if (hovered != null && hovered.rect != null) hovered.onHover?.Invoke(false);
        hovered = target;
        hovered?.onHover?.Invoke(true);
    }
}

/// <summary>
/// One-shot placement for world-space panels: in front of the player at a comfortable
/// reading distance, a little below eye level, facing the eyes - then left alone. Not
/// head-locked, not wrist-attached: once open, the panel stays where it was placed.
/// </summary>
public static class WorldPanelPlacement
{
    public const float DefaultDistance = 1.35f;
    public const float DefaultDrop = 0.14f;

    public static void PlaceInFront(Transform panel, float distance = DefaultDistance, float drop = DefaultDrop, object owner = null)
    {
        var cam = Camera.main;
        if (cam == null || panel == null)
        {
            return;
        }
        Vector3 eye = cam.transform.position;
        // Yaw only: looking at the floor while opening must not put the panel on the floor.
        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f)
        {
            forward = Vector3.ProjectOnPlane(cam.transform.up, Vector3.up);
        }
        forward.Normalize();
        Vector3 position = eye + forward * distance + Vector3.down * drop;
        // Face the eyes (a slight upward tilt, since the panel sits below eye level).
        panel.SetPositionAndRotation(position, Quaternion.LookRotation(position - eye, Vector3.up));
        var follow = HudFollowCamera.ConfigureShared(panel.gameObject, owner, PlayerUIGate.InformationPriority);
        follow.Distance = distance; follow.MinimumDistance = Mathf.Min(0.82f, distance);
        follow.LocalOffset = Vector3.down * drop;
        follow.SetWorldAnchor(position, panel.rotation);
    }
}
