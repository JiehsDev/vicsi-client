// Assets/_Project/Scripts/UI/DialogueHistoryUI.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Read-only "Dialogue History" panel: every line DialogueHistoryManager recorded this
/// session, oldest at the top, newest at the bottom, each as "source" over "text".
/// Opened from the Utility Menu's "Dialogue History" entry. Same panel conventions as
/// InsightsUI - runtime-built world-space canvas that follows the head, deferential Show()
/// (refuses while PlayerUIGate is held by something else), PlayerUIGate and
/// LocomotionSuspender held while open, right B closes.
///
/// Right thumbstick up/down scrolls (raw InputAction, like every panel here - no mouse or
/// ray dependency). Opening jumps to the newest line. While open it also takes
/// ToolInputLock, so the trigger can't fire the tool in hand at evidence behind the
/// panel; what is held or equipped is untouched. Purely a view: nothing here replays a
/// line, plays audio or changes any game state.
/// </summary>
public class DialogueHistoryUI : MonoBehaviour
{
    public static DialogueHistoryUI Instance { get; private set; }

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(900f, 760f);
    [SerializeField] private float panelScale = 0.0007f;
    [SerializeField] private float followDistance = 0.75f;

    [Header("Text")]
    [SerializeField] private float titleFontSize = 30f;
    [SerializeField] private float sourceFontSize = 20f;
    [SerializeField] private float lineFontSize = 23f;
    [SerializeField] private Color sourceColor = new Color(0.45f, 0.85f, 1f, 1f);

    [Header("Scrolling")]
    [Tooltip("Scroll speed (canvas units per second) at full thumbstick deflection.")]
    [SerializeField] private float scrollSpeed = 900f;
    [SerializeField, Range(0f, 0.9f)] private float stickDeadzone = 0.2f;

    public bool IsShowing { get; private set; }

    /// <summary>Number of entry rows currently built in the list (tests / diagnostics).</summary>
    public int RowCount => rows.Count;

    /// <summary>0 = scrolled to the newest (bottom) line, 1 = oldest (top).</summary>
    public float VerticalPosition => scrollRect != null ? scrollRect.verticalNormalizedPosition : 0f;

    private GameObject panelRoot;
    private ScrollRect scrollRect;
    private RectTransform content;
    private TMP_Text emptyLabel;
    private readonly List<GameObject> rows = new();
    private DialogueHistoryManager subscribedManager;

    private InputAction closeAction;
    private InputAction scrollAction;

    private void Awake()
    {
        Instance = this;
        closeAction = new InputAction("DialogueHistory_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        scrollAction = new InputAction("DialogueHistory_Scroll", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        BuildRuntimeUI();
        panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        closeAction.Enable();
        scrollAction.Enable();
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        closeAction.Disable();
        scrollAction.Disable();
        Unsubscribe();

        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
    }

    // Same Quest device-reconnect fix the other panels' loose InputActions apply.
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected)
        {
            return;
        }
        closeAction.Disable();
        closeAction.Enable();
        scrollAction.Disable();
        scrollAction.Enable();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        closeAction?.Dispose();
        scrollAction?.Dispose();
    }

    private void Update()
    {
        if (!IsShowing)
        {
            return;
        }

        if (closeAction.WasPressedThisFrame())
        {
            Hide();
            return;
        }

        float y = scrollAction.ReadValue<Vector2>().y;
        if (Mathf.Abs(y) > stickDeadzone)
        {
            ScrollBy(y * scrollSpeed * Time.unscaledDeltaTime);
        }
    }

    public void Toggle()
    {
        if (IsShowing)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void Show()
    {
        if (IsShowing)
        {
            return;
        }
        if (PlayerUIGate.IsBlocked)
        {
            return;
        }

        LocomotionSuspender.Suspend();
        PlayerUIGate.Enter(this);
        ToolInputLock.Enter(this);
        IsShowing = true;

        HudFollowCamera.ConfigureShared(panelRoot, this, 300);
        panelRoot.SetActive(true);
        Subscribe();
        Rebuild();
        ScrollToNewest();
        InteractionFeedback.Confirm(panelRoot.transform);
    }

    public void Hide()
    {
        panelRoot.SetActive(false);
        Unsubscribe();
        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
    }

    /// <summary>Scrolls by canvas units; positive = toward older lines (up).</summary>
    public void ScrollBy(float units)
    {
        if (scrollRect == null || content == null)
        {
            return;
        }
        float overflow = content.rect.height - scrollRect.viewport.rect.height;
        if (overflow <= 0f)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            return;
        }
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + units / overflow);
    }

    public void ScrollToNewest()
    {
        if (scrollRect == null)
        {
            return;
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scrollRect.verticalNormalizedPosition = 0f;
    }

    private void ReleaseHolds()
    {
        LocomotionSuspender.Resume();
        PlayerUIGate.Exit(this);
        ToolInputLock.Exit(this);
    }

    private void Subscribe()
    {
        var manager = DialogueHistoryManager.Instance;
        if (manager == subscribedManager)
        {
            return;
        }
        Unsubscribe();
        subscribedManager = manager;
        if (manager != null)
        {
            manager.OnEntryAdded += HandleEntryAdded;
            manager.OnCleared += Rebuild;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedManager != null)
        {
            subscribedManager.OnEntryAdded -= HandleEntryAdded;
            subscribedManager.OnCleared -= Rebuild;
            subscribedManager = null;
        }
    }

    // A line shown while the panel is open appears live; stay pinned to the bottom only
    // if the reader was already there.
    private void HandleEntryAdded(DialogueHistoryEntry entry)
    {
        bool atBottom = scrollRect.verticalNormalizedPosition <= 0.01f;
        Rebuild();
        if (atBottom)
        {
            ScrollToNewest();
        }
    }

    private void Rebuild()
    {
        foreach (var row in rows)
        {
            if (row != null)
            {
                row.transform.SetParent(null, false);
                Destroy(row);
            }
        }
        rows.Clear();

        var manager = DialogueHistoryManager.Instance;
        if (manager != null)
        {
            foreach (var entry in manager.Entries)
            {
                rows.Add(CreateRow(entry));
            }
        }
        emptyLabel.gameObject.SetActive(rows.Count == 0);
    }

    private GameObject CreateRow(DialogueHistoryEntry entry)
    {
        var row = EditableSceneVisuals.Create("Entry_" + entry.sequence, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        row.transform.SetParent(content, false);
        row.GetComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 1f);
        var layout = row.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(16, 16, 10, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var source = CreateText(row.transform, "Source", sourceFontSize, sourceColor, FontStyles.Bold);
        int minutes = Mathf.FloorToInt(entry.sessionTimeSeconds / 60f);
        int seconds = Mathf.FloorToInt(entry.sessionTimeSeconds % 60f);
        source.text = $"{EvidenceDisplay.Text(entry.SourceLabel)}  <color=#8a8f99><size=80%>{minutes:00}:{seconds:00}</size></color>";

        var line = CreateText(row.transform, "Line", lineFontSize, Color.white, FontStyles.Normal);
        line.text = EvidenceDisplay.Text(entry.text);
        return row;
    }

    private static TMP_Text CreateText(Transform parent, string name, float size, Color color, FontStyles style)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = EditableSceneVisuals.Component<TextMeshProUGUI>(go);
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.richText = true;
        return text;
    }

    // --- Runtime UI ------------------------------------------------------------

    [EditableSceneBuilder]
    private void BuildRuntimeUI()
    {
        using (EditableSceneVisuals.Begin(this,"BuildRuntimeUI"))
        {
        var canvasGo = EditableSceneVisuals.Create("DialogueHistoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(HudFollowCamera));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue - 4;
        var canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = panelSize;
        canvasGo.transform.localScale = Vector3.one * panelScale;
        canvasGo.GetComponent<HudFollowCamera>().Distance = followDistance;

        var background = NewImage(canvasRect, "Background", new Color(0.06f, 0.06f, 0.08f, 0.96f));
        Stretch(background.rectTransform, 0f, 0f, 0f, 0f);

        var title = CreateText(canvasRect, "Title", titleFontSize, Color.white, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.Center;
        title.text = "Dialogue History";
        Stretch(title.rectTransform, 0f, 0f, 0f, 0f);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.offsetMin = new Vector2(20f, -70f);
        title.rectTransform.offsetMax = new Vector2(-20f, -14f);

        // Scroll view: viewport (masked) + content (grows with entries) + slim scrollbar.
        var viewportImage = NewImage(canvasRect, "Viewport", new Color(1f, 1f, 1f, 0.06f));
        var viewport = viewportImage.rectTransform;
        Stretch(viewport, 24f, 70f, 44f, 80f);
        EditableSceneVisuals.Component<RectMask2D>(viewport.gameObject);

        var contentGo = EditableSceneVisuals.Create("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var contentLayout = contentGo.GetComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 10f;
        contentLayout.padding = new RectOffset(12, 12, 12, 12);
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollbarGo = EditableSceneVisuals.Create("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        scrollbarGo.transform.SetParent(canvasRect, false);
        var scrollbarRect = scrollbarGo.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-38f, 80f);
        scrollbarRect.offsetMax = new Vector2(-24f, -70f);
        scrollbarGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        var handleArea = EditableSceneVisuals.Create("SlidingArea", typeof(RectTransform));
        handleArea.transform.SetParent(scrollbarRect, false);
        Stretch(handleArea.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
        var handle = NewImage(handleArea.GetComponent<RectTransform>(), "Handle", new Color(0.45f, 0.85f, 1f, 0.7f));
        Stretch(handle.rectTransform, 0f, 0f, 0f, 0f);
        var scrollbar = scrollbarGo.GetComponent<Scrollbar>();
        scrollbar.handleRect = handle.rectTransform;
        scrollbar.targetGraphic = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.interactable = false; // display only; the thumbstick scrolls

        scrollRect = EditableSceneVisuals.Component<ScrollRect>(canvasGo);
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        emptyLabel = CreateText(canvasRect, "EmptyLabel", 22f, new Color(0.7f, 0.7f, 0.75f, 1f), FontStyles.Italic);
        emptyLabel.alignment = TextAlignmentOptions.Center;
        emptyLabel.text = "No dialogue yet.";
        Stretch(emptyLabel.rectTransform, 24f, 70f, 44f, 80f);

        var hint = CreateText(canvasRect, "Hint", 18f, new Color(0.7f, 0.7f, 0.75f, 1f), FontStyles.Normal);
        hint.alignment = TextAlignmentOptions.Center;
        hint.text = "Right stick: scroll    B: close";
        Stretch(hint.rectTransform, 20f, 0f, 20f, 0f);
        hint.rectTransform.anchorMax = new Vector2(1f, 0f);
        hint.rectTransform.offsetMin = new Vector2(20f, 22f);
        hint.rectTransform.offsetMax = new Vector2(-20f, 62f);

        panelRoot = canvasGo;
            }
    }

    private static Image NewImage(RectTransform parent, string name, Color color)
    {
        var go = EditableSceneVisuals.Create(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    // Stretch to the parent with insets (left, top, right, bottom).
    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
