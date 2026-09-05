// Assets/_Project/Scripts/RoleSystem/ToolWheelController.cs
using System.Collections.Generic;
using System.Linq;
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
///
/// Visual style is high-contrast black/white (see reference: black ring, wedges flip
/// to solid white on hover, white icons flip to black on hover). Segment names are not
/// shown next to the wedges - the hovered wedge's name and description are shown in
/// the center hub instead, same as the reference mock.
/// </summary>
public class ToolWheelController : MonoBehaviour
{
    private const float WheelDiameter = 900f;
    private const float IconDiameter = 120f;
    private const float Deadzone = 0.35f;

    // The wheel is a ring, not a filled disc - wedges only occupy the band from
    // RingInnerRadiusRatio out to the edge, so the world (or, in VR, whatever's behind
    // the wheel) stays visible through the middle. Icons sit at the midpoint of that
    // band.
    private const float RingInnerRadiusRatio = 0.42f;
    private const float IconRadiusRatio = 0.355f;

    // Hover "lift" feedback: the hovered wedge's icon/monogram pops outward and scales
    // up, animated toward its target each frame rather than snapping, so it reads as
    // being physically raised off the wheel instead of just recoloring.
    private const float HoverLiftOffset = 26f;
    private const float HoverLiftScale = 1.3f;
    private const float HoverLiftSpeed = 12f;

    // Rendered on a world-space canvas pinned in front of the camera, but pushed to
    // the very top of the UI sort order so it always draws above every other canvas
    // in the scene instead of fighting for a z-index against HUDs/menus.
    private const int WheelSortingOrder = short.MaxValue;

    [SerializeField] private Color dimWedgeColor = new Color(0.02f, 0.02f, 0.02f, 0.97f);
    [SerializeField] private Color hoverWedgeColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("Icon/monogram opacity for roles with no PlayerTool built/placed yet - shown so the wheel previews every role, but visually muted since picking one does nothing yet.")]
    [SerializeField] private float placeholderIconAlpha = 0.3f;

    [System.Serializable]
    private class WheelIconEntry
    {
        public ToolType role;
        [Tooltip("Shown by default (dim/unhovered wedge, black background).")]
        public Sprite iconWhite;
        [Tooltip("Shown while this wedge is hovered (wedge flips to a white background).")]
        public Sprite iconBlack;
        [TextArea]
        [Tooltip("Shown in the center hub while this wedge is hovered. Leave blank to use the built-in default description.")]
        public string description;
    }

    [Tooltip("Optional icon pair + description per role. Roles left unassigned here fall back to a generated two-letter monogram and a default description.")]
    [SerializeField] private List<WheelIconEntry> roleIcons = new();

    [Header("Editor-Authored UI (optional)")]
    [Tooltip("Leave every field below blank for a fully auto-generated wheel (zero setup). To hand-tweak layout/fonts/sprites instead, right-click this component's header and run \"Build/Rebuild Wheel UI (Editor)\" - it scaffolds a real, persistent child hierarchy and wires these fields to it. Once assigned, the wheel uses exactly what you've authored (and Awake never regenerates it); edit the WedgeTemplate child to change how every wedge looks, and the rest of the hierarchy directly for the backdrop/hub/labels.")]
    [SerializeField] private RectTransform wheelRootRect;
    [SerializeField] private Image backdropImage;
    [SerializeField] private RectTransform segmentsRootRect;
    [SerializeField] private Image centerHubImage;
    [SerializeField] private TMP_Text centerTitleText;
    [SerializeField] private TMP_Text centerSubtitleText;
    [SerializeField] private ToolWheelWedgeTemplate wedgeTemplate;

    private class WheelSegmentView
    {
        public ToolType Role;
        public string DisplayName;
        public string Description;
        public bool HasTool;
        public Image Wedge;
        public Image Icon;
        public Sprite IconWhite;
        public Sprite IconBlack;
        public TMP_Text Monogram;

        // Whichever of Icon/Monogram exists, cached here so the hover-lift animation
        // has one thing to move/scale without branching every frame.
        public RectTransform VisualRect;
        public Vector2 BaseAnchoredPosition;
        public Vector2 HoverDirection;
        public float LiftT;
    }

    private InputAction openAction;
    private InputAction selectStickAction;

    private OVRCameraRig cameraRig;

    private GameObject wheelRoot;
    private RectTransform segmentsRoot;
    private TMP_Text centerTitle;
    private TMP_Text centerSubtitle;
    private Sprite circleSprite;
    private Sprite ringSprite;

    private readonly List<WheelSegmentView> segmentViews = new();

    private bool isOpen;
    private int hoveredIndex;

    private void Awake()
    {
        EnsureUI();
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
        PlayerUIGate.Exit(this);
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
        AnimateHoverLift();

        if (openAction.WasReleasedThisFrame())
        {
            CloseWheel();
        }
    }

    private void OpenWheel()
    {
        // Defers to whatever else already owns the screen right now (the
        // camera viewfinder, the utility menu, ...) instead of stacking this
        // wheel on top of it.
        if (PlayerUIGate.IsBlocked)
        {
            return;
        }

        EnsureCameraRig();
        if (cameraRig == null)
        {
            Debug.LogWarning("[ToolWheelController] No OVRCameraRig found in scene; can't resolve a hand anchor to equip tools to.");
            return;
        }

        LocomotionSuspender.Suspend();

        BuildSegments();
        hoveredIndex = IndexOfCurrentlyEquipped();
        UpdateHighlight();

        isOpen = true;
        wheelRoot.SetActive(true);
        PlayerUIGate.Enter(this);
    }

    private void CloseWheel()
    {
        isOpen = false;
        wheelRoot.SetActive(false);
        LocomotionSuspender.Resume();
        PlayerUIGate.Exit(this);

        if (segmentViews.Count == 0)
        {
            return;
        }

        var selected = segmentViews[hoveredIndex];
        if (selected.Role == ToolType.None)
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

        // Every tool defaults to the right hand; a tool that must occupy a specific
        // hand (e.g. the evidence bag, which needs the right hand free for grabbing
        // evidence) says so itself via PlayerTool.PreferredHand, rather than this
        // wheel special-casing individual ToolTypes.
        var tool = PlayerToolRegistry.GetTool(selected.Role);
        Transform handAnchor = tool != null && tool.PreferredHand == PlayerTool.Hand.Left
            ? cameraRig.leftHandAnchor
            : cameraRig.rightHandAnchor;

        PlayerToolRegistry.ToggleEquip(selected.Role, handAnchor);
    }

    private void EnsureCameraRig()
    {
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
        }
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
            var view = segmentViews[i];
            bool hovered = i == hoveredIndex;
            float alpha = view.HasTool ? 1f : placeholderIconAlpha;

            view.Wedge.color = hovered ? hoverWedgeColor : dimWedgeColor;

            if (view.Icon != null)
            {
                // Default look is the white icon on the black wedge; hovering flips the
                // wedge to white, so the icon flips to its black counterpart to stay
                // legible (falls back to the white icon if no black variant was set).
                view.Icon.sprite = hovered && view.IconBlack != null ? view.IconBlack : view.IconWhite;
                view.Icon.color = new Color(1f, 1f, 1f, alpha);
            }
            else if (view.Monogram != null)
            {
                view.Monogram.color = hovered ? new Color(0f, 0f, 0f, alpha) : new Color(1f, 1f, 1f, alpha);
            }
        }

        var current = segmentViews[hoveredIndex];
        bool isHolsterEntry = current.Role == ToolType.None;

        centerTitle.text = isHolsterEntry ? "Empty Hands" : current.DisplayName.ToUpperInvariant();
        centerSubtitle.text = current.Description;
    }

    // Eases each wedge's icon/monogram toward its hover target every frame the wheel
    // is open, so the hovered one visibly rises and grows instead of popping instantly.
    private void AnimateHoverLift()
    {
        for (int i = 0; i < segmentViews.Count; i++)
        {
            var view = segmentViews[i];
            if (view.VisualRect == null)
            {
                continue;
            }

            float target = i == hoveredIndex ? 1f : 0f;
            view.LiftT = Mathf.MoveTowards(view.LiftT, target, Time.deltaTime * HoverLiftSpeed);

            view.VisualRect.anchoredPosition = view.BaseAnchoredPosition + view.HoverDirection * (HoverLiftOffset * view.LiftT);
            float scale = Mathf.Lerp(1f, HoverLiftScale, view.LiftT);
            view.VisualRect.localScale = new Vector3(scale, scale, scale);
        }
    }

    // --- UI construction -------------------------------------------------

    // If the wheel has been hand-authored via BuildEditableWheelUI() (and saved with
    // the scene), use exactly that hierarchy. Otherwise fall back to the fully
    // generated wheel, unchanged, so dropping this component into a scene with no
    // setup still works.
    private void EnsureUI()
    {
        if (wheelRootRect != null && segmentsRootRect != null && centerTitleText != null && centerSubtitleText != null)
        {
            wheelRoot = wheelRootRect.gameObject;
            segmentsRoot = segmentsRootRect;
            centerTitle = centerTitleText;
            centerSubtitle = centerSubtitleText;
            circleSprite = CreateCircleSprite(256);
            ringSprite = CreateRingSprite(256, RingInnerRadiusRatio);
            return;
        }

        BuildUI();
    }

    // Scaffolds a real, persistent wheel hierarchy as children of this GameObject and
    // wires the Editor-Authored UI fields above to it, so it's just normal editable
    // scene content from then on instead of something only ToolWheelController itself
    // can see. Re-run any time to reset back to the default look. Only meaningful in
    // Edit mode - anything built while playing is thrown away like usual Play-mode
    // changes, since there'd be nothing to save it into.
    [ContextMenu("Build/Rebuild Wheel UI (Editor)")]
    private void BuildEditableWheelUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        var root = new GameObject("ToolWheelCanvas", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(WheelDiameter, WheelDiameter);
        root.transform.localScale = Vector3.one * 0.0006f;

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = WheelSortingOrder;

        var hud = root.AddComponent<HudFollowCamera>();
        hud.Distance = 0.65f;

        var circle = CreateCircleSprite(256);
        var ring = CreateRingSprite(256, RingInnerRadiusRatio);

        // A thin outline at the outer edge - not a filled backdrop. The center and the
        // gaps between wedges stay transparent, so whatever's behind the wheel (the
        // world, in VR) reads straight through it.
        var backdrop = CreateImageChild(rootRect, "Backdrop", new Vector2(WheelDiameter, WheelDiameter), Vector2.zero);
        backdrop.sprite = CreateRingSprite(256, 0.97f);
        backdrop.color = new Color(1f, 1f, 1f, 0.5f);

        var segRoot = new GameObject("Segments", typeof(RectTransform)).GetComponent<RectTransform>();
        segRoot.SetParent(rootRect, false);
        segRoot.anchorMin = segRoot.anchorMax = new Vector2(0.5f, 0.5f);
        segRoot.pivot = new Vector2(0.5f, 0.5f);
        segRoot.sizeDelta = new Vector2(WheelDiameter, WheelDiameter);
        segRoot.anchoredPosition = Vector2.zero;

        // No solid hub - just a positioning anchor for the title/subtitle text, which
        // sits directly over whatever's visible through the ring's open center.
        var hub = CreateImageChild(rootRect, "CenterHub", new Vector2(WheelDiameter * 0.4f, WheelDiameter * 0.4f), Vector2.zero);
        hub.sprite = circle;
        hub.color = new Color(0f, 0f, 0f, 0f);

        var title = CreateLabel(hub.rectTransform, "Title", new Vector2(0f, 18f), new Vector2(WheelDiameter * 0.34f, 60f), 30f, FontStyles.Bold);
        title.characterSpacing = 2f;
        var subtitle = CreateLabel(hub.rectTransform, "Subtitle", new Vector2(0f, -30f), new Vector2(WheelDiameter * 0.32f, 90f), 18f, FontStyles.Normal);
        subtitle.color = new Color(1f, 1f, 1f, 0.75f);
        subtitle.textWrappingMode = TextWrappingModes.Normal;

        var bottomHint = CreateLabel(rootRect, "BottomHint", new Vector2(0f, -(WheelDiameter * 0.5f + 50f)), new Vector2(WheelDiameter * 0.6f, 40f), 18f, FontStyles.Normal);
        bottomHint.text = "RELEASE TO SELECT";
        bottomHint.characterSpacing = 3f;
        bottomHint.color = new Color(1f, 1f, 1f, 0.7f);

        // One example wedge, left inactive and cloned per role at runtime. Lives beside
        // Segments (not inside it) so BuildSegments()'s destroy-and-rebuild each time
        // the wheel opens never touches it. Wedge/Icon/Monogram are siblings under an
        // unrotated container - only Wedge itself gets spun to its wedge's angle, so
        // the icon/monogram stay upright regardless. Tweak size, font, sprite, or fill
        // style however you like; CreateWedge() only ever repositions, recolors, and
        // re-texts whatever it finds here.
        var templateRoot = new GameObject("WedgeTemplate", typeof(RectTransform));
        templateRoot.transform.SetParent(rootRect, false);
        var templateRect = templateRoot.GetComponent<RectTransform>();
        templateRect.anchorMin = templateRect.anchorMax = new Vector2(0.5f, 0.5f);
        templateRect.pivot = new Vector2(0.5f, 0.5f);
        templateRect.sizeDelta = new Vector2(WheelDiameter, WheelDiameter);
        templateRect.anchoredPosition = Vector2.zero;

        var templateWedge = CreateImageChild(templateRect, "Wedge", new Vector2(WheelDiameter, WheelDiameter), Vector2.zero);
        templateWedge.sprite = ring;
        templateWedge.type = Image.Type.Filled;
        templateWedge.fillMethod = Image.FillMethod.Radial360;
        templateWedge.fillOrigin = (int)Image.Origin360.Top;
        templateWedge.fillClockwise = true;
        templateWedge.color = dimWedgeColor;

        var templateIcon = CreateImageChild(templateRect, "Icon", new Vector2(IconDiameter, IconDiameter), Vector2.zero);
        templateIcon.preserveAspect = true;
        templateIcon.raycastTarget = false;
        templateIcon.color = Color.white;

        var templateMonogram = CreateLabel(templateRect, "Monogram", Vector2.zero, new Vector2(IconDiameter, IconDiameter), 44f, FontStyles.Bold);

        var templateMarker = templateRoot.AddComponent<ToolWheelWedgeTemplate>();
        templateMarker.wedge = templateWedge;
        templateMarker.icon = templateIcon;
        templateMarker.monogram = templateMonogram;
        templateRoot.SetActive(false);

        wheelRootRect = rootRect;
        backdropImage = backdrop;
        segmentsRootRect = segRoot;
        centerHubImage = hub;
        centerTitleText = title;
        centerSubtitleText = subtitle;
        wedgeTemplate = templateMarker;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void BuildUI()
    {
        wheelRoot = new GameObject("ToolWheelCanvas", typeof(RectTransform), typeof(Canvas));
        wheelRoot.transform.SetParent(transform, false);

        var rootRect = wheelRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(WheelDiameter, WheelDiameter);
        wheelRoot.transform.localScale = Vector3.one * 0.0006f;

        var canvas = wheelRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = WheelSortingOrder;

        var hud = wheelRoot.AddComponent<HudFollowCamera>();
        hud.Distance = 0.65f;

        circleSprite = CreateCircleSprite(256);
        ringSprite = CreateRingSprite(256, RingInnerRadiusRatio);

        var backdrop = CreateImageChild(rootRect, "Backdrop", new Vector2(WheelDiameter, WheelDiameter), Vector2.zero);
        backdrop.sprite = CreateRingSprite(256, 0.97f);
        backdrop.color = new Color(1f, 1f, 1f, 0.5f);

        segmentsRoot = new GameObject("Segments", typeof(RectTransform)).GetComponent<RectTransform>();
        segmentsRoot.SetParent(rootRect, false);
        segmentsRoot.anchorMin = segmentsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        segmentsRoot.pivot = new Vector2(0.5f, 0.5f);
        segmentsRoot.sizeDelta = new Vector2(WheelDiameter, WheelDiameter);
        segmentsRoot.anchoredPosition = Vector2.zero;

        var hub = CreateImageChild(rootRect, "CenterHub", new Vector2(WheelDiameter * 0.4f, WheelDiameter * 0.4f), Vector2.zero);
        hub.sprite = circleSprite;
        hub.color = new Color(0f, 0f, 0f, 0f);

        centerTitle = CreateLabel(hub.rectTransform, "Title", new Vector2(0f, 18f), new Vector2(WheelDiameter * 0.34f, 60f), 30f, FontStyles.Bold);
        centerTitle.characterSpacing = 2f;
        centerSubtitle = CreateLabel(hub.rectTransform, "Subtitle", new Vector2(0f, -30f), new Vector2(WheelDiameter * 0.32f, 90f), 18f, FontStyles.Normal);
        centerSubtitle.color = new Color(1f, 1f, 1f, 0.75f);
        centerSubtitle.textWrappingMode = TextWrappingModes.Normal;

        var bottomHint = CreateLabel(rootRect, "BottomHint", new Vector2(0f, -(WheelDiameter * 0.5f + 50f)), new Vector2(WheelDiameter * 0.6f, 40f), 18f, FontStyles.Normal);
        bottomHint.text = "RELEASE TO SELECT";
        bottomHint.characterSpacing = 3f;
        bottomHint.color = new Color(1f, 1f, 1f, 0.7f);
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
        var entries = new List<(ToolType role, string name, string description, bool hasTool)>
        {
            (ToolType.None, "Empty Hands", ResolveDescription(ToolType.None), true)
        };
        foreach (ToolType role in System.Enum.GetValues(typeof(ToolType)))
        {
            if (role == ToolType.None)
            {
                continue;
            }
            entries.Add((role, ToDisplayName(role), ResolveDescription(role), PlayerToolRegistry.GetTool(role) != null));
        }

        int count = entries.Count;
        float segmentAngle = 360f / count;
        float gapAngle = Mathf.Min(6f, segmentAngle * 0.15f);

        for (int i = 0; i < count; i++)
        {
            CreateWedge(i, segmentAngle, gapAngle, entries[i].role, entries[i].name, entries[i].description, entries[i].hasTool);
        }
    }

    private void CreateWedge(int index, float segmentAngle, float gapAngle, ToolType role, string displayName, string description, bool hasTool)
    {
        float startAngle = index * segmentAngle + gapAngle * 0.5f;
        float visibleAngle = segmentAngle - gapAngle;

        Image wedge;
        Image iconSlot;
        TMP_Text monogramSlot;

        if (wedgeTemplate != null)
        {
            // Editor-authored path: clone the hand-tweaked template instead of
            // building a wedge from scratch. Instantiate() remaps the clone's
            // ToolWheelWedgeTemplate references onto its own children automatically.
            var clone = Instantiate(wedgeTemplate, segmentsRoot);
            clone.name = $"Wedge_{displayName}";
            clone.gameObject.SetActive(true);
            clone.transform.localRotation = Quaternion.identity;
            wedge = clone.wedge;
            iconSlot = clone.icon;
            monogramSlot = clone.monogram;
        }
        else
        {
            wedge = CreateImageChild(segmentsRoot, $"Wedge_{displayName}", new Vector2(WheelDiameter, WheelDiameter), Vector2.zero);
            wedge.sprite = ringSprite;
            wedge.type = Image.Type.Filled;
            wedge.fillMethod = Image.FillMethod.Radial360;
            wedge.fillOrigin = (int)Image.Origin360.Top;
            wedge.fillClockwise = true;
            iconSlot = null;
            monogramSlot = null;
        }

        wedge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -startAngle);
        wedge.fillAmount = visibleAngle / 360f;
        wedge.color = dimWedgeColor;

        float midAngleRad = (index * segmentAngle + segmentAngle * 0.5f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(midAngleRad), Mathf.Cos(midAngleRad));
        Vector2 iconPos = dir * (WheelDiameter * IconRadiusRatio);

        var (iconWhite, iconBlack) = FindIcons(role);

        Image iconImage = null;
        TMP_Text monogram = null;

        if (iconWhite != null)
        {
            iconImage = iconSlot != null ? iconSlot : CreateImageChild(segmentsRoot, "Icon", new Vector2(IconDiameter, IconDiameter), Vector2.zero);
            iconImage.gameObject.SetActive(true);
            iconImage.rectTransform.anchoredPosition = iconPos;
            iconImage.sprite = iconWhite;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            if (monogramSlot != null)
            {
                monogramSlot.gameObject.SetActive(false);
            }
        }
        else
        {
            monogram = monogramSlot != null ? monogramSlot : CreateLabel(segmentsRoot, "Monogram", Vector2.zero, new Vector2(IconDiameter, IconDiameter), 44f, FontStyles.Bold);
            monogram.gameObject.SetActive(true);
            monogram.rectTransform.anchoredPosition = iconPos;
            monogram.text = Monogram(role, displayName);
            if (iconSlot != null)
            {
                iconSlot.gameObject.SetActive(false);
            }
        }

        var visualRect = iconImage != null ? iconImage.rectTransform : monogram.rectTransform;

        segmentViews.Add(new WheelSegmentView
        {
            Role = role,
            DisplayName = displayName,
            Description = description,
            HasTool = hasTool,
            Wedge = wedge,
            Icon = iconImage,
            IconWhite = iconWhite,
            IconBlack = iconBlack,
            Monogram = monogram,
            VisualRect = visualRect,
            BaseAnchoredPosition = iconPos,
            HoverDirection = dir
        });
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
    // for wedges, plain Simple for the backdrop/hub) instead of shipping sprite assets.
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

    // Same soft-edged disc as CreateCircleSprite, but with the middle cut out - this is
    // what makes the wheel a ring instead of a filled pie. Used as the source sprite
    // for wedges (Image.Type.Filled just masks a portion of whatever it's given, so
    // radial-filling a donut still produces a ring-shaped wedge) and for the thin outer
    // outline.
    private static Sprite CreateRingSprite(int diameter, float innerRadiusRatio)
    {
        var texture = new Texture2D(diameter, diameter, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float radius = diameter / 2f;
        float innerRadius = radius * innerRadiusRatio;
        var pixels = new Color32[diameter * diameter];
        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float outerAlpha = Mathf.Clamp01((radius - dist) / 2f);
                float innerAlpha = Mathf.Clamp01((dist - innerRadius) / 2f);
                pixels[y * diameter + x] = new Color(1f, 1f, 1f, Mathf.Min(outerAlpha, innerAlpha));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, diameter, diameter), new Vector2(0.5f, 0.5f), 100f);
    }

    // The wheel names the physical item in your hand, not the job title that carries
    // it - roles without a defined piece of equipment yet just fall back to their role
    // name until one exists.
    private static string ToDisplayName(ToolType role) => role switch
    {
        ToolType.Photographer => "Camera",
        ToolType.Sketcher => "Sketchpad",
        ToolType.EvidenceCollector => "Evidence Bag",
        ToolType.Recorder => "Recorder",
        ToolType.IOC => "Flashlight",
        ToolType.EvidenceMarker => "Evidence Tent",
        _ => role.ToString(),
    };

    // Shown in the center hub while a wedge is hovered. An inspector override (see
    // WheelIconEntry.description) always wins; this is just the fallback so the wheel
    // still reads sensibly before anyone fills those in.
    private static string DefaultDescription(ToolType role) => role switch
    {
        ToolType.None => "Put away your current tool.",
        ToolType.Photographer => "Captures photographic evidence at the scene.",
        ToolType.IOC => "Illuminates dark areas to reveal hidden evidence.",
        ToolType.Sketcher => "Sketches the crime scene layout by hand.",
        ToolType.EvidenceCollector => "Bags and seals collected evidence.",
        ToolType.Recorder => "Records verbal notes and witness statements.",
        ToolType.EvidenceMarker => "Places numbered evidence tents at the scene.",
        _ => string.Empty,
    };

    private string ResolveDescription(ToolType role)
    {
        foreach (var entry in roleIcons)
        {
            if (entry.role == role && !string.IsNullOrWhiteSpace(entry.description))
            {
                return entry.description;
            }
        }
        return DefaultDescription(role);
    }

    private (Sprite white, Sprite black) FindIcons(ToolType role)
    {
        foreach (var entry in roleIcons)
        {
            if (entry.role == role)
            {
                return (entry.iconWhite, entry.iconBlack);
            }
        }
        return (null, null);
    }

    private static string Monogram(ToolType role, string displayName)
    {
        if (role == ToolType.None)
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
