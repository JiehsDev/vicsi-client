// Assets/_Project/Scripts/UI/UtilityMenuController.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Radial menu for the player's utility screens (Notes, Item Logs, the Photo
/// Album, ...) - visually and mechanically the same GTA-style wheel as
/// ToolWheelController (ring wedges, hover-lift, "release to select"), but
/// generic: entries are a plain list of name/description/UnityEvent instead
/// of being tied to ToolType/PlayerToolRegistry, since these are UI screens to
/// open, not tools to equip. Hold the LEFT controller's X button to open it,
/// tilt the RIGHT thumbstick toward an entry to highlight it, release X to
/// open that entry's screen. This shares the left X button with
/// PhotographTool's aim toggle, and both defer to PlayerUIGate, so holding X
/// while the camera is already up (or this menu itself is already open) only
/// ever triggers one of them.
/// An entry with no listener wired yet (e.g. Notes/Item Logs before they
/// exist) still shows up, just dimmed and inert, exactly like an unbuilt
/// role on the tool wheel - add its OnSelect listener later and it lights up
/// automatically.
/// </summary>
public class UtilityMenuController : MonoBehaviour
{
    private const float WheelDiameter = 700f;
    private const float RingInnerRadiusRatio = 0.42f;
    private const float LabelRadiusRatio = 0.72f;
    private const float Deadzone = 0.35f;

    private const float HoverLiftOffset = 20f;
    private const float HoverLiftScale = 1.15f;
    private const float HoverLiftSpeed = 12f;

    private const int WheelSortingOrder = short.MaxValue;

    [SerializeField] private Color dimWedgeColor = new Color(0.02f, 0.02f, 0.02f, 0.97f);
    [SerializeField] private Color hoverWedgeColor = new Color(1f, 1f, 1f, 1f);
    [Tooltip("Text opacity for entries with no OnSelect listener wired yet - shown so the menu previews every planned screen, but visually muted since picking one does nothing yet.")]
    [SerializeField] private float placeholderAlpha = 0.35f;

    [System.Serializable]
    public class MenuEntry
    {
        public string displayName;
        [TextArea]
        public string description;
        [Tooltip("What happens when this entry is selected - e.g. PhotoAlbumUI.Show for the Album entry. Leave empty for a placeholder entry (shown dimmed, does nothing when picked).")]
        public UnityEvent onSelect;
    }

    [Tooltip("Every screen this menu can open, in wheel order.")]
    [SerializeField] private List<MenuEntry> entries = new();

    private class WheelSegmentView
    {
        public MenuEntry Entry;
        public bool HasListener;
        public Image Wedge;
        public TMP_Text Label;
        public RectTransform VisualRect;
        public Vector2 BaseAnchoredPosition;
        public Vector2 HoverDirection;
        public float LiftT;
    }

    private InputAction openAction;
    private InputAction selectStickAction;

    private readonly List<WheelSegmentView> segmentViews = new();

    private GameObject wheelRoot;
    private RectTransform segmentsRoot;
    private Sprite ringSprite;

    private bool isOpen;
    private int hoveredIndex;

    private void Awake()
    {
        BuildUI();
        wheelRoot.SetActive(false);

        // The left controller's X button opens the menu; the right thumbstick
        // (otherwise turning) selects from it while open. The right
        // controller's B (secondary) button is reserved for closing whatever
        // screen the menu opened (see PhotoAlbumUI), not for this.
        openAction = new InputAction("UtilityMenu_Open", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
        selectStickAction = new InputAction("UtilityMenu_SelectStick", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
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
                OpenMenu();
            }
            return;
        }

        UpdateHoveredSegment();
        AnimateHoverLift();

        if (openAction.WasReleasedThisFrame())
        {
            CloseMenu();
        }
    }

    private void OpenMenu()
    {
        if (entries.Count == 0)
        {
            return;
        }

        // Defers to whatever else already owns the screen right now - most
        // often the camera viewfinder, since PhotographTool's aim toggle and
        // this menu's open button are both bound to the left controller's X.
        if (PlayerUIGate.IsBlocked)
        {
            return;
        }

        LocomotionSuspender.Suspend();
        BuildSegments();
        hoveredIndex = 0;
        UpdateHighlight();

        isOpen = true;
        wheelRoot.SetActive(true);
        PlayerUIGate.Enter(this);
    }

    private void CloseMenu()
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
        if (selected.HasListener)
        {
            selected.Entry.onSelect.Invoke();
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

    private void UpdateHighlight()
    {
        for (int i = 0; i < segmentViews.Count; i++)
        {
            var view = segmentViews[i];
            bool hovered = i == hoveredIndex;
            float alpha = view.HasListener ? 1f : placeholderAlpha;

            view.Wedge.color = hovered ? hoverWedgeColor : dimWedgeColor;
            view.Label.color = new Color(hovered ? 0f : 1f, hovered ? 0f : 1f, hovered ? 0f : 1f, alpha);
        }
    }

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

    private void BuildUI()
    {
        wheelRoot = new GameObject("UtilityMenuCanvas", typeof(RectTransform), typeof(Canvas));
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

        var bottomHint = CreateLabel(rootRect, "BottomHint", new Vector2(0f, -(WheelDiameter * 0.5f + 40f)), new Vector2(WheelDiameter * 0.7f, 40f), 16f, FontStyles.Normal);
        bottomHint.text = "RELEASE TO OPEN";
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

        int count = entries.Count;
        float segmentAngle = 360f / count;
        float gapAngle = Mathf.Min(6f, segmentAngle * 0.15f);

        for (int i = 0; i < count; i++)
        {
            CreateWedge(i, segmentAngle, gapAngle, entries[i]);
        }
    }

    private void CreateWedge(int index, float segmentAngle, float gapAngle, MenuEntry entry)
    {
        float startAngle = index * segmentAngle + gapAngle * 0.5f;
        float visibleAngle = segmentAngle - gapAngle;

        var wedge = CreateImageChild(segmentsRoot, $"Wedge_{entry.displayName}", new Vector2(WheelDiameter, WheelDiameter), Vector2.zero);
        wedge.sprite = ringSprite;
        wedge.type = Image.Type.Filled;
        wedge.fillMethod = Image.FillMethod.Radial360;
        wedge.fillOrigin = (int)Image.Origin360.Top;
        wedge.fillClockwise = true;
        wedge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -startAngle);
        wedge.fillAmount = visibleAngle / 360f;
        wedge.color = dimWedgeColor;

        float midAngleRad = (index * segmentAngle + segmentAngle * 0.5f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(midAngleRad), Mathf.Cos(midAngleRad));
        Vector2 labelPos = dir * (WheelDiameter * LabelRadiusRatio);

        var label = CreateLabel(segmentsRoot, "Label", labelPos, new Vector2(150f, 60f), 20f, FontStyles.Bold);
        label.text = entry.displayName;
        label.raycastTarget = false;

        bool hasListener = entry.onSelect != null && entry.onSelect.GetPersistentEventCount() > 0;

        segmentViews.Add(new WheelSegmentView
        {
            Entry = entry,
            HasListener = hasListener,
            Wedge = wedge,
            Label = label,
            VisualRect = label.rectTransform,
            BaseAnchoredPosition = labelPos,
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

    // Same soft-edged ring sprite generation as ToolWheelController - the middle
    // is cut out so the wheel reads as a ring, not a filled disc.
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
}
