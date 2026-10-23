// Assets/_Project/Scripts/UI/EvidenceProcedureUI.cs
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Evidence / Procedure panel - the first panel in ViCSI's forensic world-space style
/// (ForensicUIStyle). One wide landscape panel:
///
///   header   ViCSI · Evidence / Procedure                              [X]
///   left     the evidence the player has legitimately recorded (selectable)
///   centre   the selected item's procedure as a vertical checklist (one scroll area)
///   right    its fixed-reference measurements and evidence-to-evidence distances
///   footer   NEXT: the one action to take now
///
/// Purely a view over the authoritative state; it changes nothing. Steps and the single
/// Current step come from EvidenceProcedureMapResolver; measurements from
/// MeasurementStateManager. Opened from the Utility Menu's "Evidence Procedure" entry.
///
/// Placement: placed once in front of the player (~1.35 m, a little below eye level,
/// facing them) when opened, then left still - not head-locked, not on the wrist.
/// Input (raw InputActions only): right-controller ray + trigger selects evidence and the
/// close button (WorldPanelPointer); right stick up/down scrolls the procedure,
/// left/right switches item; right B closes. While open it holds PlayerUIGate,
/// LocomotionSuspender and ToolInputLock, so a trigger pull aimed at the panel can never
/// fire a tool at the scene behind it.
///
/// EVIDENCE-LEAK RULE (see project notes - do not "improve" this): only items the player
/// has already photographed (status at or after PhotographedRaw) can be listed or shown,
/// and measurement counts/rows only involve those items. Found is reached by proximity
/// alone, so listing Found items would tell the player which objects are evidence; from
/// the first photo on, the existing EvidenceNotifier already names the item, so the panel
/// reveals nothing new. Focus follows the player's own actions on items that are already
/// eligible - it never jumps to an item the player hasn't photographed.
/// </summary>
public class EvidenceProcedureUI : MonoBehaviour
{
    public static EvidenceProcedureUI Instance { get; private set; }

    // Layout (canvas units; the canvas is scaled so 1 unit = metresPerUnit metres).
    private const float PanelWidth = 1200f;
    private const float PanelHeight = 680f;
    private const float HeaderHeight = 72f;
    private const float FooterHeight = 88f;
    private const float ListWidth = 320f;
    private const float ListRowSlot = 108f;                // numbered labels can wrap to three lines
    private const float ListRowVisual = 100f;
    private const float RightX = ListWidth + 32f;          // 352
    private const float ProcedureWidth = 400f;
    private const float DetailsX = RightX + ProcedureWidth + 36f; // 788
    private const float DetailsWidth = PanelWidth - DetailsX - 32f;
    private const float SectionTop = HeaderHeight + 78f;   // below the item heading
    private const float StepRowHeight = 44f;
    private const float StepDetailHeight = 36f;
    private const float ValueRowHeight = 40f;

    [Header("Placement")]
    [Tooltip("Metres per canvas unit. 0.001 = 1 mm per unit, so the panel is 1.16 x 0.68 m and body text is 29 mm per em.")]
    [SerializeField] private float metresPerUnit = 0.001f;
    [SerializeField] private float openDistance = WorldPanelPlacement.DefaultDistance;
    [SerializeField] private float openDrop = WorldPanelPlacement.DefaultDrop;

    [Header("Input")]
    [SerializeField] private float scrollSpeed = 900f;
    [SerializeField, Range(0f, 0.9f)] private float stickDeadzone = 0.25f;
    [SerializeField, Range(0.3f, 0.95f)] private float switchThreshold = 0.7f;
    [Tooltip("Safety refresh while open (seconds) - picks up sub-step progress that has no event of its own (dust loaded, print brushed, sample sealed).")]
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.5f;

    public bool IsShowing { get; private set; }

    /// <summary>The item currently selected, or null.</summary>
    public string FocusedEvidenceId { get; private set; }

    /// <summary>The steps currently rendered (tests / diagnostics).</summary>
    public IReadOnlyList<ProcedureMapStep> CurrentSteps => steps;

    /// <summary>True while the selected item's procedure is complete (every step done).</summary>
    public bool ShowingProcedureComplete { get; private set; }

    /// <summary>0 = bottom of the procedure list, 1 = top.</summary>
    public float VerticalPosition => scrollRect != null ? scrollRect.verticalNormalizedPosition : 1f;

    /// <summary>The listed (selectable) evidence ids, in roster order.</summary>
    public IReadOnlyList<string> ListedEvidenceIds => eligibleOrder;

    /// <summary>The one instruction in the footer (without the NEXT label).</summary>
    public string NextActionText => nextText != null ? nextText.text : string.Empty;

    /// <summary>Rendered fixed-reference rows ("label|value") for the selected item.</summary>
    public IReadOnlyList<string> FixedReferenceRows => fixedRows;

    /// <summary>Rendered evidence-distance rows ("label|value") for the selected item.</summary>
    public IReadOnlyList<string> EvidenceDistanceRows => distanceRows;

    /// <summary>Rendered sub-detail lines under the Current step ("label|value"), e.g. measurement counts.</summary>
    public IReadOnlyList<string> CurrentStepDetailRows => detailRows;

    /// <summary>How far the procedure content exceeds its viewport (canvas units; 0 = fits).</summary>
    public float ProcedureOverflow => content != null && scrollRect != null ? Mathf.Max(0f, content.rect.height - scrollRect.viewport.rect.height) : 0f;

    /// <summary>The ray pointer (tests aim rays through it).</summary>
    public WorldPanelPointer Pointer => pointer;

    /// <summary>The panel's world-space root (placement checks).</summary>
    public Transform PanelTransform => panelRoot != null ? panelRoot.transform : null;

    /// <summary>World-space panel size in metres.</summary>
    public Vector2 PanelSizeMetres => new Vector2(PanelWidth, PanelHeight) * metresPerUnit;

    private List<ProcedureMapStep> steps = new();
    private readonly List<string> eligibleOrder = new();
    private readonly List<string> fixedRows = new();
    private readonly List<string> distanceRows = new();
    private readonly List<string> detailRows = new();
    private readonly Dictionary<string, ListRow> listRows = new();

    private class ListRow
    {
        public RectTransform slot;
        public Image fill;
        public Image bar;
        public TMP_Text label;
        public bool hovered;
    }

    private GameObject panelRoot;
    private RectTransform canvasRect;
    private RectTransform listRoot;
    private TMP_Text listEmpty;
    private TMP_Text itemHeading;
    private TMP_Text procedureHeading;
    private ScrollRect scrollRect;
    private RectTransform content;
    private RectTransform scrollTrack;
    private RectTransform scrollThumb;
    private RectTransform details;
    private Image footerBar;
    private TMP_Text nextLabel;
    private TMP_Text nextText;
    private Image closeFill;
    private WorldPanelPointer pointer;
    private RectTransform focusRow;

    private InputAction closeAction;
    private InputAction stickAction;
    private bool stickCentred = true;
    private bool dirty = true;
    private float nextRefresh;
    private string procedureSignature = string.Empty;
    private string detailsSignature = string.Empty;
    private string listSignature = string.Empty;

    // Presentation names for the resolver's stable step ids; anything else uses the
    // resolver's own displayName, so new steps still render.
    private static readonly Dictionary<string, string> StepLabels = new()
    {
        { EvidenceProcedureMapResolver.SceneStepId, "Scene Photography" },
        { "initial_photo", "Initial Photograph" },
        { "final_photo", "Final Photograph" },
    };

    private void Awake()
    {
        Instance = this;
        closeAction = new InputAction("EvidenceProcedure_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        stickAction = new InputAction("EvidenceProcedure_Stick", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        BuildRuntimeUI();
        panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        closeAction.Enable();
        stickAction.Enable();
        InputSystem.onDeviceChange += OnDeviceChange;
        EvidenceStateManager.OnEvidenceStatusChanged += HandleStatusChanged;
        EvidenceStateManager.OnEvidenceSwabbed += HandleItemProgress;
        EvidenceStateManager.OnBodyProcedureChanged += HandleItemProgress;
        EvidenceStateManager.OnEvidenceFingerprinted += HandleItemProgress;
        EvidenceStateManager.OnFingerprintLabStatusChanged += HandleLabStatus;
        MeasurementStateManager.OnMeasurementProgress += HandleMeasurementProgress;
        ScenePhotographyManager.OnScenePhotoProgress += HandleSceneProgress;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleStatusChanged;
        EvidenceStateManager.OnEvidenceSwabbed -= HandleItemProgress;
        EvidenceStateManager.OnBodyProcedureChanged -= HandleItemProgress;
        EvidenceStateManager.OnEvidenceFingerprinted -= HandleItemProgress;
        EvidenceStateManager.OnFingerprintLabStatusChanged -= HandleLabStatus;
        MeasurementStateManager.OnMeasurementProgress -= HandleMeasurementProgress;
        ScenePhotographyManager.OnScenePhotoProgress -= HandleSceneProgress;
        closeAction.Disable();
        stickAction.Disable();
        pointer?.Disable();
        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected)
        {
            return;
        }
        closeAction.Disable();
        closeAction.Enable();
        stickAction.Disable();
        stickAction.Enable();
        if (IsShowing)
        {
            pointer.Rebind();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        closeAction?.Dispose();
        stickAction?.Dispose();
        pointer?.Dispose();
        if (panelRoot != null)
        {
            Destroy(panelRoot);
        }
    }

    // --- Authoritative progress events -> refresh (and focus the item worked on) ---

    private void HandleStatusChanged(string evidenceId, EvidenceStatus status)
    {
        Focus(evidenceId);
        dirty = true;
    }

    private void HandleItemProgress(string evidenceId)
    {
        Focus(evidenceId);
        dirty = true;
    }

    private void HandleLabStatus(string evidenceId, FingerprintLabStatus status)
    {
        Focus(evidenceId);
        dirty = true;
    }

    private void HandleMeasurementProgress(string requirementId)
    {
        var record = MeasurementStateManager.Instance != null ? MeasurementStateManager.Instance.GetRecord(requirementId) : null;
        if (record != null)
        {
            Focus(record.requirement.targetEvidenceId);
        }
        dirty = true;
    }

    private void HandleSceneProgress(string photoPointId)
    {
        dirty = true;
    }

    // Follows the player's own work, but only onto items they have already photographed.
    private void Focus(string evidenceId)
    {
        if (IsEligible(evidenceId))
        {
            FocusedEvidenceId = evidenceId;
        }
    }

    public static bool IsEligible(string evidenceId)
    {
        var esm = EvidenceStateManager.Instance;
        return esm != null && !string.IsNullOrEmpty(evidenceId) && esm.GetRecord(evidenceId) != null
            && esm.IsAtOrPastStatus(evidenceId, EvidenceStatus.PhotographedRaw);
    }

    // --- Open / close --------------------------------------------------------------

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
        if (SceneTurnover.IsInvestigationBlocked || IsShowing || PlayerUIGate.IsBlocked)
        {
            return;
        }
        LocomotionSuspender.Suspend();
        PlayerUIGate.Enter(this);
        ToolInputLock.Enter(this);
        IsShowing = true;
        WorldPanelPlacement.PlaceInFront(panelRoot.transform, openDistance, openDrop, this);
        panelRoot.SetActive(true);
        pointer.Enable();
        if (!IsEligible(FocusedEvidenceId))
        {
            RefreshEligible();
            FocusedEvidenceId = eligibleOrder.Count > 0 ? eligibleOrder[eligibleOrder.Count - 1] : null;
        }
        Rebuild();
        InteractionFeedback.Confirm(panelRoot.transform);
    }

    public void Hide()
    {
        pointer.Disable();
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
        if (IsShowing)
        {
            ReleaseHolds();
            IsShowing = false;
        }
    }

    private void ReleaseHolds()
    {
        LocomotionSuspender.Resume();
        PlayerUIGate.Exit(this);
        ToolInputLock.Exit(this);
    }

    /// <summary>Shows a specific item (if eligible). For tests and future "Procedure" buttons on item screens.</summary>
    public void ShowFor(string evidenceId)
    {
        Focus(evidenceId);
        dirty = true;
        if (!IsShowing)
        {
            Show();
        }
        else
        {
            Rebuild();
        }
    }

    /// <summary>Selects a listed item (the evidence list's click). Ignores items that may not be shown.</summary>
    public bool SelectEvidence(string evidenceId)
    {
        if (!IsEligible(evidenceId))
        {
            return false;
        }
        bool changed = FocusedEvidenceId != evidenceId;
        FocusedEvidenceId = evidenceId;
        Rebuild();
        if (changed)
        {
            InteractionFeedback.Confirm(panelRoot.transform);
        }
        return true;
    }

    /// <summary>Moves to the previous (-1) or next (+1) photographed item.</summary>
    public void SwitchItem(int direction)
    {
        RefreshEligible();
        if (eligibleOrder.Count == 0)
        {
            return;
        }
        int index = eligibleOrder.IndexOf(FocusedEvidenceId);
        index = index < 0 ? 0 : (index + direction + eligibleOrder.Count) % eligibleOrder.Count;
        FocusedEvidenceId = eligibleOrder[index];
        Rebuild();
    }

    /// <summary>Closes the panel and forgets the selection (in-scene session restart / tests).</summary>
    public void ResetSession()
    {
        Hide();
        FocusedEvidenceId = null;
        steps = new List<ProcedureMapStep>();
        procedureSignature = detailsSignature = listSignature = string.Empty;
        dirty = true;
    }

    private void RefreshEligible()
    {
        eligibleOrder.Clear();
        var esm = EvidenceStateManager.Instance;
        if (esm == null)
        {
            return;
        }
        foreach (var record in esm.AllRecords)
        {
            if (record?.definition != null && IsEligible(record.definition.evidenceId))
            {
                eligibleOrder.Add(record.definition.evidenceId);
            }
        }
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

        Vector2 stick = stickAction.ReadValue<Vector2>();
        if (Mathf.Abs(stick.x) >= switchThreshold && Mathf.Abs(stick.x) > Mathf.Abs(stick.y))
        {
            if (stickCentred)
            {
                stickCentred = false;
                SwitchItem(stick.x > 0f ? 1 : -1);
            }
        }
        else if (Mathf.Abs(stick.x) < stickDeadzone)
        {
            stickCentred = true;
        }
        if (Mathf.Abs(stick.y) > stickDeadzone && Mathf.Abs(stick.y) > Mathf.Abs(stick.x))
        {
            ScrollBy(stick.y * scrollSpeed * Time.unscaledDeltaTime);
        }

        if (dirty || Time.unscaledTime >= nextRefresh)
        {
            Rebuild();
        }

        pointer.Tick();
        if (!IsShowing)
        {
            return; // the close button was clicked this frame
        }
        UpdateScrollIndicator();
    }

    public void ScrollBy(float units)
    {
        float overflow = ProcedureOverflow;
        if (overflow <= 0f)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            return;
        }
        scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition + units / overflow);
    }

    // --- Rendering -------------------------------------------------------------------

    /// <summary>Re-resolves and redraws now (also called on every progress event while open).</summary>
    public void Rebuild()
    {
        dirty = false;
        nextRefresh = Time.unscaledTime + refreshInterval;

        RefreshEligible();
        if (!IsEligible(FocusedEvidenceId))
        {
            FocusedEvidenceId = eligibleOrder.Count > 0 ? eligibleOrder[eligibleOrder.Count - 1] : null;
        }

        var record = !string.IsNullOrEmpty(FocusedEvidenceId) && EvidenceStateManager.Instance != null
            ? EvidenceStateManager.Instance.GetRecord(FocusedEvidenceId) : null;
        var map = IsEligible(FocusedEvidenceId) ? EvidenceProcedureMapResolver.ResolveMap(FocusedEvidenceId) : new ProcedureMapResult();
        steps = map.steps;
        ShowingProcedureComplete = map.isComplete;
        string name = record?.definition != null && steps.Count > 0 ? record.definition.PlayerDisplayName : string.Empty;

        RebuildList();
        itemHeading.text = name;
        procedureHeading.gameObject.SetActive(steps.Count > 0);

        var measurement = MeasurementStatusFor(map);
        RebuildProcedure(map, measurement);
        RebuildDetails(steps.Count > 0 ? FocusedEvidenceId : null);
        RenderNextAction(map, measurement, name);
    }

    private EvidenceMeasurementStatus? MeasurementStatusFor(ProcedureMapResult map)
    {
        if (map.current == null || map.current.stepId != "measurement" || MeasurementStateManager.Instance == null)
        {
            return null;
        }
        return MeasurementStateManager.Instance.GetEvidenceMeasurementStatus(FocusedEvidenceId);
    }

    // --- Evidence list ---

    private void RebuildList()
    {
        var sb = new StringBuilder(FocusedEvidenceId).Append('#');
        foreach (var id in eligibleOrder)
        {
            sb.Append(id).Append(':').Append(EvidenceProcedureMapResolver.ResolveMap(id).isComplete ? 1 : 0).Append('|');
        }
        string signature = sb.ToString();
        if (signature == listSignature)
        {
            return;
        }
        listSignature = signature;

        pointer.Unregister("evidence:");
        foreach (Transform child in listRoot)
        {
            Destroy(child.gameObject);
        }
        listRoot.DetachChildren();
        listRows.Clear();
        listEmpty.gameObject.SetActive(eligibleOrder.Count == 0);

        var esm = EvidenceStateManager.Instance;
        for (int i = 0; i < eligibleOrder.Count; i++)
        {
            string id = eligibleOrder[i];
            var definition = esm.GetRecord(id).definition;

            // The slot is the hit target: the visual row plus a 4-unit margin above and below,
            // so neighbouring targets touch but never overlap.
            var slot = ForensicUIStyle.PlaceTopLeft(ForensicUIStyle.Group(listRoot, "Evidence_" + i), 0f, i * ListRowSlot, ListWidth - 32f, ListRowSlot);
            var visual = ForensicUIStyle.Group(slot, "Row");
            ForensicUIStyle.Stretch(visual, 0f, (ListRowSlot - ListRowVisual) * 0.5f, 0f, (ListRowSlot - ListRowVisual) * 0.5f);
            var fill = ForensicUIStyle.Rect(visual, "Fill", Color.clear);
            ForensicUIStyle.Stretch(fill.rectTransform);
            var bar = ForensicUIStyle.Rect(visual, "SelectedBar", ForensicUIStyle.Interactive);
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(5f, 0f);
            // Numbered names wrap within the three-line row rather than being cut off.
            var label = ForensicUIStyle.Text(visual, "Label", ForensicUIStyle.ListItemSize, ForensicUIStyle.TextSecondary);
            ForensicUIStyle.Stretch(label.rectTransform, 22f, 2f, 44f, 2f);
            label.lineSpacing = -12f;
            label.text = definition.PlayerDisplayName;

            if (EvidenceProcedureMapResolver.ResolveMap(id).isComplete)
            {
                var check = ForensicUIStyle.CheckMark(visual, ForensicUIStyle.Success, 20f, 4f);
                check.anchorMin = check.anchorMax = new Vector2(1f, 0.5f);
                check.anchoredPosition = new Vector2(-20f, 0f);
            }

            var row = new ListRow { slot = slot, fill = fill, bar = bar, label = label };
            listRows[id] = row;
            StyleListRow(id, row);
            string captured = id;
            pointer.Register("evidence:" + id, visual, new Vector2(0f, (ListRowSlot - ListRowVisual) * 0.5f),
                () => SelectEvidence(captured),
                hover => { row.hovered = hover; StyleListRow(captured, row); });
        }
    }

    private void StyleListRow(string id, ListRow row)
    {
        bool selected = id == FocusedEvidenceId;
        row.fill.color = selected ? ForensicUIStyle.SelectedFill : row.hovered ? ForensicUIStyle.HoverFill : Color.clear;
        row.bar.enabled = selected;
        row.label.color = selected ? ForensicUIStyle.TextPrimary : ForensicUIStyle.TextSecondary;
    }

    // --- Procedure checklist ---

    private void RebuildProcedure(ProcedureMapResult map, EvidenceMeasurementStatus? measurement)
    {
        var lines = new List<string>();
        if (measurement.HasValue)
        {
            var m = measurement.Value;
            lines.Add($"Fixed references|{m.fixedCompleted} / {m.fixedRequired}");
            if (m.pairsAvailable > 0)
            {
                lines.Add($"Evidence distances|{m.pairsCompleted} / {m.pairsAvailable}");
            }
        }
        detailRows.Clear();
        detailRows.AddRange(lines);

        var sb = new StringBuilder(FocusedEvidenceId).Append('#');
        foreach (var s in steps)
        {
            sb.Append(s.stepId).Append(':').Append((int)s.state).Append('|');
        }
        sb.Append(string.Join(";", lines));
        string signature = sb.ToString();
        if (signature == procedureSignature && content.childCount > 0)
        {
            return; // nothing visible changed - keep the scroll position and avoid churn
        }
        bool sameItem = procedureSignature.StartsWith(FocusedEvidenceId + "#", System.StringComparison.Ordinal);
        procedureSignature = signature;

        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
        content.DetachChildren();
        focusRow = null;

        foreach (var step in steps)
        {
            var row = CreateStepRow(step);
            if (step.state == ProcedureMapStepState.Current)
            {
                focusRow = row;
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    ForensicUIStyle.ValueRow(content, "Detail", parts[0], parts[1], StepDetailHeight,
                        ForensicUIStyle.MetaSize, ForensicUIStyle.TextSecondary, ForensicUIStyle.TextPrimary, 48f, 100f);
                }
            }
        }
        if (map.isComplete)
        {
            focusRow = CreateCompleteRow();
        }
        KeepFocusVisible(!sameItem);
    }

    private RectTransform CreateStepRow(ProcedureMapStep step)
    {
        var row = ForensicUIStyle.Group(content, "Step_" + step.stepId);
        EditableSceneVisuals.Component<LayoutElement>(row.gameObject).preferredHeight = StepRowHeight;
        bool current = step.state == ProcedureMapStepState.Current;

        if (current)
        {
            ForensicUIStyle.Stretch(ForensicUIStyle.Rect(row, "CurrentFill", ForensicUIStyle.AccentFill).rectTransform);
            var bar = ForensicUIStyle.Rect(row, "CurrentBar", ForensicUIStyle.Accent).rectTransform;
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.sizeDelta = new Vector2(5f, 0f);
        }

        RectTransform marker;
        switch (step.state)
        {
            case ProcedureMapStepState.Completed:
                marker = ForensicUIStyle.CheckMark(row, ForensicUIStyle.Success, 22f, 4f);
                break;
            case ProcedureMapStepState.Current:
                marker = ForensicUIStyle.Circle(row, "Marker", false, ForensicUIStyle.Accent, 16f).rectTransform;
                break;
            case ProcedureMapStepState.Blocked:
                marker = ForensicUIStyle.Circle(row, "Marker", true, ForensicUIStyle.Warning, 16f).rectTransform;
                break;
            default:
                marker = ForensicUIStyle.Circle(row, "Marker", true, ForensicUIStyle.TextMuted, 16f).rectTransform;
                break;
        }
        marker.anchorMin = marker.anchorMax = new Vector2(0f, 0.5f);
        marker.anchoredPosition = new Vector2(26f, 0f);

        var color = step.state switch
        {
            ProcedureMapStepState.Completed => ForensicUIStyle.TextSecondary,
            ProcedureMapStepState.Current => ForensicUIStyle.TextPrimary,
            _ => ForensicUIStyle.TextMuted
        };
        // Emphasis comes from the gold marker, fill and bar - not bold, which would also
        // push long names ("Lab Fingerprint Process") past the column.
        var label = ForensicUIStyle.Text(row, "Label", ForensicUIStyle.BodySize, color);
        ForensicUIStyle.Stretch(label.rectTransform, 48f, 0f, 12f, 0f);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.text = StepLabel(step);
        return row;
    }

    public static string StepLabel(ProcedureMapStep step) =>
        EvidenceDisplay.Text(StepLabels.TryGetValue(step.stepId, out var label) ? label : step.displayName);

    private RectTransform CreateCompleteRow()
    {
        var row = ForensicUIStyle.Group(content, "ProcedureComplete");
        EditableSceneVisuals.Component<LayoutElement>(row.gameObject).preferredHeight = StepRowHeight + 12f;
        var top = ForensicUIStyle.SeparatorLine(row).rectTransform;
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = new Vector2(1f, 1f);
        top.anchoredPosition = new Vector2(0f, -4f);
        var label = ForensicUIStyle.Text(row, "Label", ForensicUIStyle.BodySize, ForensicUIStyle.Success);
        ForensicUIStyle.Stretch(label.rectTransform, 48f, 8f, 12f, 0f);
        label.text = "Procedure complete";
        return row;
    }

    private void KeepFocusVisible(bool resetToFocus)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float overflow = ProcedureOverflow;
        if (focusRow == null || overflow <= 0f)
        {
            scrollRect.verticalNormalizedPosition = 1f;
            return;
        }
        // Same item, progress only: keep the player's scroll unless the Current row left the view.
        float rowTop = -focusRow.anchoredPosition.y - focusRow.rect.height * (1f - focusRow.pivot.y);
        float rowBottom = rowTop + focusRow.rect.height + (detailRows.Count * StepDetailHeight);
        float viewTop = (1f - scrollRect.verticalNormalizedPosition) * overflow;
        float viewHeight = scrollRect.viewport.rect.height;
        if (!resetToFocus && rowTop >= viewTop && rowBottom <= viewTop + viewHeight)
        {
            return;
        }
        // Centre the Current row (and its detail lines) in the viewport where possible.
        float target = (rowTop + rowBottom) * 0.5f - viewHeight * 0.5f;
        scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(target / overflow);
    }

    private void UpdateScrollIndicator()
    {
        float overflow = ProcedureOverflow;
        scrollTrack.gameObject.SetActive(overflow > 0f);
        if (overflow <= 0f)
        {
            return;
        }
        float viewHeight = scrollRect.viewport.rect.height;
        float trackHeight = scrollTrack.rect.height;
        float thumbHeight = Mathf.Max(40f, trackHeight * viewHeight / (viewHeight + overflow));
        scrollThumb.sizeDelta = new Vector2(0f, thumbHeight);
        scrollThumb.anchoredPosition = new Vector2(0f, -(1f - scrollRect.verticalNormalizedPosition) * (trackHeight - thumbHeight));
    }

    // --- Measurements (fixed references, evidence distances) ---

    private void RebuildDetails(string evidenceId)
    {
        fixedRows.Clear();
        distanceRows.Clear();
        var m = MeasurementStateManager.Instance;
        if (m != null && !string.IsNullOrEmpty(evidenceId))
        {
            var requirement = m.RequirementSet != null && m.RequirementSet.requirements != null
                ? m.RequirementSet.requirements.Find(r => r != null && r.required && r.targetEvidenceId == evidenceId) : null;
            if (requirement != null)
            {
                var record = m.GetRecord(requirement.requirementId);
                fixedRows.Add($"{ReferenceName(requirement.referenceAId)}|{(record != null && record.hasA ? MeasurementStateManager.FormatDisplay(record.measuredA) : Unmeasured)}");
                fixedRows.Add($"{ReferenceName(requirement.referenceBId)}|{(record != null && record.hasB ? MeasurementStateManager.FormatDisplay(record.measuredB) : Unmeasured)}");
            }
            // Only pairs whose two items are both discovered (EVIDENCE-LEAK RULE); one row
            // per unordered pair, named by the other item.
            foreach (var pair in m.GetAvailablePairsFor(evidenceId))
            {
                distanceRows.Add($"{MeasurementStateManager.DisplayName(pair.pair.Other(evidenceId))}|{(pair.isComplete ? pair.DisplayDistance : Unmeasured)}");
            }
        }

        string signature = string.Join(";", fixedRows) + "#" + string.Join(";", distanceRows);
        if (signature == detailsSignature && details.childCount > 0)
        {
            return;
        }
        detailsSignature = signature;
        foreach (Transform child in details)
        {
            Destroy(child.gameObject);
        }
        details.DetachChildren();

        AddDetailSection("Fixed references", fixedRows);
        if (fixedRows.Count > 0 && distanceRows.Count > 0)
        {
            EditableSceneVisuals.Component<LayoutElement>(ForensicUIStyle.Group(details, "Gap").gameObject).preferredHeight = 22f;
        }
        AddDetailSection("Evidence distances", distanceRows);
    }

    private const string Unmeasured = "–"; // en dash: "known, not measured yet"

    private void AddDetailSection(string heading, List<string> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }
        var h = ForensicUIStyle.SectionHeading(details, heading);
        EditableSceneVisuals.Component<LayoutElement>(h.gameObject).preferredHeight = 34f;
        foreach (var row in rows)
        {
            var parts = row.Split('|');
            bool measured = parts[1] != Unmeasured;
            ForensicUIStyle.ValueRow(details, "Row", parts[0], parts[1], ValueRowHeight, ForensicUIStyle.MetaSize,
                ForensicUIStyle.TextSecondary, measured ? ForensicUIStyle.TextPrimary : ForensicUIStyle.TextMuted);
        }
    }

    private static string ReferenceName(string referenceId)
    {
        var reference = SceneReferencePoint.Find(referenceId);
        return reference != null && !string.IsNullOrEmpty(reference.displayName) ? reference.displayName : "Fixed reference";
    }

    // --- Next action ---

    private void RenderNextAction(ProcedureMapResult map, EvidenceMeasurementStatus? measurement, string itemName)
    {
        bool done = map.isComplete;
        footerBar.color = done ? ForensicUIStyle.Success : ForensicUIStyle.Accent;
        nextLabel.color = done ? ForensicUIStyle.Success : ForensicUIStyle.Accent;
        nextLabel.text = done ? "Done" : "Next";

        string text;
        if (steps.Count == 0)
        {
            text = "Photograph an item of evidence to begin its record.";
        }
        else if (done)
        {
            text = $"Procedure complete for {itemName}.";
        }
        else if (measurement.HasValue && !string.IsNullOrEmpty(measurement.Value.nextSuggestion))
        {
            text = measurement.Value.nextSuggestion;
        }
        else
        {
            text = !string.IsNullOrEmpty(map.subStep) ? map.subStep : map.instruction ?? string.Empty;
        }
        if (text.Length > 0 && !text.EndsWith(".", System.StringComparison.Ordinal))
        {
            text += ".";
        }
        nextText.text = EvidenceDisplay.Text(text);
    }

    // --- Runtime UI --------------------------------------------------------------------

    [EditableSceneBuilder]
    private void BuildRuntimeUI()
    {
        using (EditableSceneVisuals.Begin(this,"BuildRuntimeUI"))
        {
        // A scene-root object, not a child of this component: a parent (e.g. under the player
        // rig) would drag the panel when the player moves and scale it with the rig. Placed
        // once per open, it then stays exactly where it was put.
        var canvasGo = EditableSceneVisuals.Create("EvidenceProcedureCanvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(null, false);
        canvasGo.transform.localScale = Vector3.one;
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue - 4;
        canvasRect = canvasGo.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        canvasGo.transform.localScale = Vector3.one * metresPerUnit;

        ForensicUIStyle.Stretch(ForensicUIStyle.Rect(canvasRect, "Background", ForensicUIStyle.PanelBackground).rectTransform);

        // Header
        var header = ForensicUIStyle.PlaceTopLeft(ForensicUIStyle.Rect(canvasRect, "Header", ForensicUIStyle.HeaderBackground).rectTransform, 0f, 0f, PanelWidth, HeaderHeight);
        var title = ForensicUIStyle.Text(header, "Title", ForensicUIStyle.TitleSize, ForensicUIStyle.TextPrimary, FontStyles.Bold);
        ForensicUIStyle.PlaceTopLeft(title.rectTransform, 32f, 0f, 120f, HeaderHeight);
        title.text = "ViCSI";
        var divider = ForensicUIStyle.Rect(header, "TitleDivider", ForensicUIStyle.Separator).rectTransform;
        ForensicUIStyle.PlaceTopLeft(divider, 150f, 20f, 2f, HeaderHeight - 40f);
        var context = ForensicUIStyle.Text(header, "Context", 26f, ForensicUIStyle.TextSecondary);
        ForensicUIStyle.PlaceTopLeft(context.rectTransform, 172f, 0f, 420f, HeaderHeight);
        context.text = "Evidence / Procedure";
        var hint = ForensicUIStyle.Text(header, "Hint", ForensicUIStyle.HintSize, ForensicUIStyle.TextMuted, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        ForensicUIStyle.PlaceTopLeft(hint.rectTransform, PanelWidth - 560f, 0f, 460f, HeaderHeight);
        hint.text = "Trigger: select   ·   Stick: scroll / switch   ·   B: close";

        var close = ForensicUIStyle.PlaceTopLeft(ForensicUIStyle.Group(header, "Close"), PanelWidth - 76f, 14f, 44f, 44f);
        closeFill = ForensicUIStyle.Rect(close, "Fill", Color.clear);
        ForensicUIStyle.Stretch(closeFill.rectTransform);
        ForensicUIStyle.Cross(close, ForensicUIStyle.TextSecondary, 20f, 3f);
        ForensicUIStyle.SeparatorLine(header);

        // Evidence list (fixed)
        var listHeading = ForensicUIStyle.SectionHeading(canvasRect, "Evidence");
        ForensicUIStyle.PlaceTopLeft(listHeading.rectTransform, 32f, HeaderHeight + 20f, ListWidth - 48f, 30f);
        listRoot = ForensicUIStyle.PlaceTopLeft(ForensicUIStyle.Group(canvasRect, "EvidenceList"), 16f, HeaderHeight + 62f, ListWidth - 32f, PanelHeight - HeaderHeight - FooterHeight - 70f);
        // Sibling of the list (not a child): the list's rows are cleared on every rebuild.
        listEmpty = ForensicUIStyle.Text(canvasRect, "EvidenceListEmpty", ForensicUIStyle.MetaSize, ForensicUIStyle.TextMuted);
        ForensicUIStyle.PlaceTopLeft(listEmpty.rectTransform, 36f, HeaderHeight + 62f, ListWidth - 72f, 100f);
        listEmpty.alignment = TextAlignmentOptions.TopLeft;
        listEmpty.text = "No evidence recorded yet.";
        var listDivider = ForensicUIStyle.Rect(canvasRect, "ListDivider", ForensicUIStyle.Separator).rectTransform;
        ForensicUIStyle.PlaceTopLeft(listDivider, ListWidth, HeaderHeight, 2f, PanelHeight - HeaderHeight - FooterHeight);

        // Selected item
        itemHeading = ForensicUIStyle.Text(canvasRect, "ItemHeading", ForensicUIStyle.ItemHeadingSize, ForensicUIStyle.TextPrimary, FontStyles.Bold);
        ForensicUIStyle.PlaceTopLeft(itemHeading.rectTransform, RightX, HeaderHeight + 14f, PanelWidth - RightX - 32f, 50f);
        itemHeading.textWrappingMode = TextWrappingModes.NoWrap;

        procedureHeading = ForensicUIStyle.SectionHeading(canvasRect, "Procedure");
        ForensicUIStyle.PlaceTopLeft(procedureHeading.rectTransform, RightX, SectionTop, ProcedureWidth, 30f);

        // The one scroll area: the procedure checklist.
        float viewportTop = SectionTop + 40f;
        float viewportHeight = PanelHeight - FooterHeight - 16f - viewportTop;
        var viewport = ForensicUIStyle.PlaceTopLeft(ForensicUIStyle.Group(canvasRect, "ProcedureViewport"), RightX - 8f, viewportTop, ProcedureWidth + 8f, viewportHeight);
        // Soft top/bottom edges: a partly scrolled row fades instead of being cut, and hints there is more.
        EditableSceneVisuals.Component<RectMask2D>(viewport.gameObject).softness = new Vector2Int(0, 18);
        content = ForensicUIStyle.Group(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        ForensicUIStyle.Stack(content);
        EditableSceneVisuals.Component<ContentSizeFitter>(content.gameObject).verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect = EditableSceneVisuals.Component<ScrollRect>(viewport.gameObject);
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;

        scrollTrack = ForensicUIStyle.PlaceTopLeft(ForensicUIStyle.Rect(canvasRect, "ScrollTrack", new Color(1f, 1f, 1f, 0.05f)).rectTransform,
            RightX + ProcedureWidth + 8f, viewportTop, 4f, viewportHeight);
        scrollThumb = ForensicUIStyle.Rect(scrollTrack, "Thumb", new Color(1f, 1f, 1f, 0.30f)).rectTransform;
        scrollThumb.anchorMin = new Vector2(0f, 1f);
        scrollThumb.anchorMax = new Vector2(1f, 1f);
        scrollThumb.pivot = new Vector2(0.5f, 1f);
        scrollTrack.gameObject.SetActive(false);

        // Measurements (fixed column)
        details = ForensicUIStyle.PlaceTopLeft(ForensicUIStyle.Group(canvasRect, "Measurements"), DetailsX, SectionTop, DetailsWidth, PanelHeight - FooterHeight - 16f - SectionTop);
        ForensicUIStyle.Stack(details);

        // Footer: the one next action
        var footer = ForensicUIStyle.PlaceTopLeft(ForensicUIStyle.Rect(canvasRect, "NextAction", ForensicUIStyle.FooterBackground).rectTransform, 0f, PanelHeight - FooterHeight, PanelWidth, FooterHeight);
        var footerTop = ForensicUIStyle.SeparatorLine(footer).rectTransform;
        footerTop.anchorMin = new Vector2(0f, 1f);
        footerTop.anchorMax = new Vector2(1f, 1f);
        footerBar = ForensicUIStyle.Rect(footer, "AccentBar", ForensicUIStyle.Accent);
        footerBar.rectTransform.anchorMin = new Vector2(0f, 0f);
        footerBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        footerBar.rectTransform.pivot = new Vector2(0f, 0.5f);
        footerBar.rectTransform.sizeDelta = new Vector2(6f, 0f);
        nextLabel = ForensicUIStyle.SectionHeading(footer, "Next");
        ForensicUIStyle.PlaceTopLeft(nextLabel.rectTransform, 32f, 0f, 100f, FooterHeight);
        nextText = ForensicUIStyle.Text(footer, "Instruction", ForensicUIStyle.NextActionSize, ForensicUIStyle.TextPrimary);
        ForensicUIStyle.PlaceTopLeft(nextText.rectTransform, 140f, 0f, PanelWidth - 172f, FooterHeight);

        panelRoot = canvasGo;

        pointer = new WorldPanelPointer(canvasRect, "EvidenceProcedure", this);
        // Close: 44-unit visual, 72-unit target (14 each side), inside the header strip.
        pointer.Register("close", close, new Vector2(14f, 14f), Hide,
            hover => closeFill.color = hover ? ForensicUIStyle.HoverFill : Color.clear);
            }
    }
}
