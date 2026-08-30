// Assets/_Project/Scripts/RoleSystem/ToolWheelController.cs
using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction.Locomotion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// GTA-style radial tool-select menu. Hold the left controller's Y button to pop the
/// wheel open in front of the player; tilt the RIGHT thumbstick toward a wedge to
/// highlight it, release Y to equip it into the right hand. While the wheel is open,
/// normal locomotion is suspended - walking (left stick) and turning (right stick,
/// which the wheel is now using for selection) both pause so they can't fight the
/// menu. Segments are built fresh from PlayerToolRegistry.AllTools every time the
/// wheel opens, so adding a new PlayerTool to the scene just makes it show up here -
/// nothing on this script needs to change. Picking the tool already in-hand holsters
/// it instead (toggle-off, empty hands); a dedicated "Empty Hands" wedge does the same
/// regardless of which tool is currently equipped.
/// </summary>
public class ToolWheelController : MonoBehaviour
{
    private const float WheelDiameter = 900f;
    private const float BadgeDiameter = 140f;
    private const float Deadzone = 0.35f;

    [SerializeField] private Color dimWedgeColor = new Color(0.08f, 0.08f, 0.09f, 0.72f);
    [SerializeField] private Color hoverWedgeColor = new Color(0.16f, 0.55f, 0.85f, 0.92f);
    [SerializeField] private Color dimBadgeColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color hoverBadgeColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("Badge tint for roles with no PlayerTool built/placed yet - shown so the wheel previews every role, but visually muted since picking one does nothing yet.")]
    [SerializeField] private Color placeholderBadgeColor = new Color(1f, 1f, 1f, 0.15f);

    [System.Serializable]
    private class RoleIcon
    {
        public RoleId role;
        public Sprite icon;
    }

    [Tooltip("Optional icon per role, shown in the badge instead of the monogram fallback. Roles left unassigned here just keep the generated two-letter monogram.")]
    [SerializeField] private List<RoleIcon> roleIcons = new();

    private class WheelSegmentView
    {
        public RoleId Role;
        public string DisplayName;
        public bool HasTool;
        public Image Wedge;
        public Image Badge;
    }

    private InputAction openAction;
    private InputAction selectStickAction;

    private OVRCameraRig cameraRig;
    private readonly List<GameObject> suspendedLocomotionObjects = new();

    private GameObject wheelRoot;
    private RectTransform segmentsRoot;
    private TMP_Text centerTitle;
    private TMP_Text centerSubtitle;
    private Sprite circleSprite;

    private readonly List<WheelSegmentView> segmentViews = new();

    private bool isOpen;
    private int hoveredIndex;

    private void Awake()
    {
        BuildUI();
        wheelRoot.SetActive(false);

        // Y on the left controller opens the wheel; the right stick (normally
        // turning) selects from it instead while it's open.
        openAction = new InputAction("ToolWheel_Open", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
        selectStickAction = new InputAction("ToolWheel_SelectStick", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
    }

    private void OnEnable()
    {
        openAction.Enable();
        selectStickAction.Enable();
    }

    private void OnDisable()
    {
        openAction.Disable();
        selectStickAction.Disable();
    }

    private void OnDestroy()
    {
        openAction?.Dispose();
        selectStickAction?.Dispose();
    }

    private void Update()
    {
        if (!isOpen)
        {
            if (openAction.WasPressedThisFrame())
            {
                OpenWheel();
            }
            return;
        }

        UpdateHoveredSegment();

        if (openAction.WasReleasedThisFrame())
        {
            CloseWheel();
        }
    }

    private void OpenWheel()
    {
        EnsureCameraRig();
        if (cameraRig == null)
        {
            Debug.LogWarning("[ToolWheelController] No OVRCameraRig found in scene; can't resolve a hand anchor to equip tools to.");
            return;
        }

        SuspendLocomotion();

        BuildSegments();
        hoveredIndex = IndexOfCurrentlyEquipped();
        UpdateHighlight();

        isOpen = true;
        wheelRoot.SetActive(true);
    }

    private void CloseWheel()
    {
        isOpen = false;
        wheelRoot.SetActive(false);
        ResumeLocomotion();

        if (segmentViews.Count == 0)
        {
            return;
        }

        var selected = segmentViews[hoveredIndex];
        if (selected.Role == RoleId.None)
        {
            PlayerToolRegistry.HolsterCurrent();
            return;
        }

        // Roles previewed on the wheel before their PlayerTool exists yet - nothing
        // to equip, so picking one is silently a no-op rather than a warning spam.
        if (!selected.HasTool)
        {
            return;
        }

        PlayerToolRegistry.ToggleEquip(selected.Role, cameraRig.rightHandAnchor);
    }

    private void EnsureCameraRig()
    {
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
        }
    }

    // Turning normally reads the same right stick the wheel now uses for selection,
    // and walking would otherwise keep dragging the player around a menu they're
    // trying to browse - both pause for as long as the wheel is open. Found by type
    // rather than by scene path so this keeps working if the rig's hand assignment
    // ever changes; only components active right now (i.e. actually in use) match.
    private void SuspendLocomotion()
    {
        suspendedLocomotionObjects.Clear();

        foreach (var turner in FindObjectsByType<LocomotionAxisTurnerInteractor>(FindObjectsInactive.Exclude))
        {
            suspendedLocomotionObjects.Add(turner.gameObject);
        }
        foreach (var slider in FindObjectsByType<SlideLocomotionBroadcaster>(FindObjectsInactive.Exclude))
        {
            suspendedLocomotionObjects.Add(slider.gameObject);
        }

        foreach (var go in suspendedLocomotionObjects)
        {
            go.SetActive(false);
        }
    }

    private void ResumeLocomotion()
    {
        foreach (var go in suspendedLocomotionObjects)
        {
            if (go != null)
            {
                go.SetActive(true);
            }
        }
        suspendedLocomotionObjects.Clear();
    }

    private void UpdateHoveredSegment()
    {
        Vector2 stick = selectStickAction.ReadValue<Vector2>();

        if (stick.sqrMagnitude < Deadzone * Deadzone)
        {
            return;
        }

        float angleDeg = Mathf.Atan2(stick.x, stick.y) * Mathf.Rad2Deg;
        if (angleDeg < 0f)
        {
            angleDeg += 360f;
        }

        int count = segmentViews.Count;
        int index = Mathf.Clamp(Mathf.FloorToInt(angleDeg / (360f / count)), 0, count - 1);
        if (index != hoveredIndex)
        {
            hoveredIndex = index;
            UpdateHighlight();
        }
    }

    private int IndexOfCurrentlyEquipped()
    {
        for (int i = 0; i < segmentViews.Count; i++)
        {
            if (segmentViews[i].Role == PlayerToolRegistry.VirtuallyEquippedRole)
            {
                return i;
            }
        }
        return 0;
    }

    private void UpdateHighlight()
    {
        for (int i = 0; i < segmentViews.Count; i++)
        {
            bool hovered = i == hoveredIndex;
            segmentViews[i].Wedge.color = hovered ? hoverWedgeColor : dimWedgeColor;
            segmentViews[i].Badge.color = hovered
                ? hoverBadgeColor
                : (segmentViews[i].HasTool ? dimBadgeColor : placeholderBadgeColor);
        }

        var current = segmentViews[hoveredIndex];
        bool isHolsterEntry = current.Role == RoleId.None;
        bool isCurrentlyEquipped = current.Role == PlayerToolRegistry.VirtuallyEquippedRole;

        centerTitle.text = isHolsterEntry ? "Empty Hands" : current.DisplayName;
        centerSubtitle.text = !isHolsterEntry && !current.HasTool
            ? "Not available yet"
            : (isHolsterEntry || !isCurrentlyEquipped ? "Release to equip" : "Release to holster");
    }

    // --- UI construction -------------------------------------------------

    private void BuildUI()
    {
        wheelRoot = new GameObject("ToolWheelCanvas", typeof(RectTransform), typeof(Canvas));
        wheelRoot.transform.SetParent(transform, false);

        var rootRect = wheelRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(WheelDiameter, WheelDiameter);
        wheelRoot.transform.localScale = Vector3.one * 0.0006f;

        var canvas = wheelRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;

        var hud = wheelRoot.AddComponent<HudFollowCamera>();
        hud.Distance = 0.65f;

        circleSprite = CreateCircleSprite(256);

        var backdrop = CreateImageChild(rootRect, "Backdrop", new Vector2(WheelDiameter, WheelDiameter), Vector2.zero);
        backdrop.sprite = circleSprite;
        backdrop.color = new Color(0f, 0f, 0f, 0.55f);

        segmentsRoot = new GameObject("Segments", typeof(RectTransform)).GetComponent<RectTransform>();
        segmentsRoot.SetParent(rootRect, false);
        segmentsRoot.anchorMin = segmentsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        segmentsRoot.pivot = new Vector2(0.5f, 0.5f);
        segmentsRoot.sizeDelta = new Vector2(WheelDiameter, WheelDiameter);
        segmentsRoot.anchoredPosition = Vector2.zero;

        var hub = CreateImageChild(rootRect, "CenterHub", new Vector2(WheelDiameter * 0.4f, WheelDiameter * 0.4f), Vector2.zero);
        hub.sprite = circleSprite;
        hub.color = new Color(0.03f, 0.03f, 0.04f, 0.9f);

        centerTitle = CreateLabel(hub.rectTransform, "Title", new Vector2(0f, 18f), new Vector2(WheelDiameter * 0.34f, 60f), 34f, FontStyles.Bold);
        centerSubtitle = CreateLabel(hub.rectTransform, "Subtitle", new Vector2(0f, -28f), new Vector2(WheelDiameter * 0.34f, 40f), 20f, FontStyles.Normal);
        centerSubtitle.color = new Color(1f, 1f, 1f, 0.7f);
    }

    private void BuildSegments()
    {
        for (int i = segmentsRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(segmentsRoot.GetChild(i).gameObject);
        }
        segmentViews.Clear();

        // Every role gets a wedge, not just ones with a PlayerTool placed in the scene
        // yet - roles without one just preview their icon with no function for now
        // (see HasTool below), so the wheel reads as the full roster from day one.
        var entries = new List<(RoleId role, string name, bool hasTool)> { (RoleId.None, "Empty Hands", true) };
        foreach (RoleId role in System.Enum.GetValues(typeof(RoleId)))
        {
            if (role == RoleId.None)
            {
                continue;
            }
            entries.Add((role, ToDisplayName(role), PlayerToolRegistry.GetTool(role) != null));
        }

        int count = entries.Count;
        float segmentAngle = 360f / count;
        float gapAngle = Mathf.Min(6f, segmentAngle * 0.15f);

        for (int i = 0; i < count; i++)
        {
            CreateWedge(i, segmentAngle, gapAngle, entries[i].role, entries[i].name, entries[i].hasTool);
        }
    }

    private void CreateWedge(int index, float segmentAngle, float gapAngle, RoleId role, string displayName, bool hasTool)
    {
        float startAngle = index * segmentAngle + gapAngle * 0.5f;
        float visibleAngle = segmentAngle - gapAngle;

        var wedge = CreateImageChild(segmentsRoot, $"Wedge_{displayName}", new Vector2(WheelDiameter, WheelDiameter), Vector2.zero);
        wedge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -startAngle);
        wedge.sprite = circleSprite;
        wedge.type = Image.Type.Filled;
        wedge.fillMethod = Image.FillMethod.Radial360;
        wedge.fillOrigin = (int)Image.Origin360.Top;
        wedge.fillClockwise = true;
        wedge.fillAmount = visibleAngle / 360f;
        wedge.color = dimWedgeColor;

        float midAngleRad = (index * segmentAngle + segmentAngle * 0.5f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(midAngleRad), Mathf.Cos(midAngleRad));
        Vector2 badgePos = dir * (WheelDiameter * 0.30f);
        Vector2 labelPos = dir * (WheelDiameter * 0.30f + 95f);

        var badge = CreateImageChild(segmentsRoot, "Badge", new Vector2(BadgeDiameter, BadgeDiameter), badgePos);
        badge.sprite = circleSprite;
        badge.color = hasTool ? dimBadgeColor : placeholderBadgeColor;

        var icon = FindIcon(role);
        if (icon != null)
        {
            var iconImage = CreateImageChild(badge.rectTransform, "Icon", new Vector2(BadgeDiameter * 0.62f, BadgeDiameter * 0.62f), Vector2.zero);
            iconImage.sprite = icon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
        }
        else
        {
            var monogram = CreateLabel(badge.rectTransform, "Monogram", Vector2.zero, new Vector2(BadgeDiameter, BadgeDiameter), 48f, FontStyles.Bold);
            monogram.text = Monogram(role, displayName);
        }

        var label = CreateLabel(segmentsRoot, "Label", labelPos, new Vector2(220f, 50f), 26f, FontStyles.Normal);
        label.text = displayName;

        segmentViews.Add(new WheelSegmentView { Role = role, DisplayName = displayName, HasTool = hasTool, Wedge = wedge, Badge = badge });
    }

    private static Image CreateImageChild(RectTransform parent, string name, Vector2 size, Vector2 anchoredPosition)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        return go.GetComponent<Image>();
    }

    private static TMP_Text CreateLabel(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    // A soft-edged circle drawn into a texture at runtime, reused (via Image.Type.Filled
    // for wedges, plain Simple for badges/backdrop) instead of shipping sprite assets.
    private static Sprite CreateCircleSprite(int diameter)
    {
        var texture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float radius = diameter / 2f;
        var pixels = new Color32[diameter * diameter];
        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01((radius - dist) / 2f);
                pixels[y * diameter + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f), 100f);
    }

    // The wheel names the physical item in your hand, not the job title that carries
    // it - roles without a defined piece of equipment yet just fall back to their role
    // name until one exists.
    private static string ToDisplayName(RoleId role) => role switch
    {
        RoleId.Photographer => "Camera",
        RoleId.Sketcher => "Sketchpad",
        RoleId.EvidenceCollector => "Magnifying Glass",
        RoleId.Recorder => "Recorder",
        RoleId.IOC => "Flashlight",
        RoleId.TeamLeader => "Team Leader",
        RoleId.CaseAnalyst => "Case Analyst",
        _ => role.ToString(),
    };

    private Sprite FindIcon(RoleId role)
    {
        foreach (var entry in roleIcons)
        {
            if (entry.role == role)
            {
                return entry.icon;
            }
        }
        return null;
    }

    private static string Monogram(RoleId role, string displayName)
    {
        if (role == RoleId.None)
        {
            return "-";
        }

        var upper = displayName.Where(char.IsUpper).ToArray();
        if (upper.Length >= 2)
        {
            return $"{upper[0]}{upper[1]}";
        }

        return displayName.Length >= 2 ? displayName.Substring(0, 2).ToUpperInvariant() : displayName.ToUpperInvariant();
    }
}
