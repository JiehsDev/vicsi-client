// Assets/_Project/Scripts/Deduction/EvidenceNode.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One card on the IOC evidence board - either a piece of evidence or a
/// conclusion/statement (e.g. "The Wife"). If nodeId matches a known evidence
/// item its label is pulled live from EvidenceStateManager; otherwise it
/// falls back to the manually authored displayText (used for conclusion and
/// statement cards that have no EvidenceDefinition).
///
/// Selection/connection is driven entirely by EvidenceBoardController's raw
/// InputAction navigation (right thumbstick to move focus, right A to
/// pick/connect) - NOT uGUI pointer/drag events. The Meta ISDK ray ->
/// PointableCanvasModule -> uGUI drag/drop pipeline this class originally
/// used was never validated against this project's own confirmed finding
/// elsewhere (see RecorderReportUI's class comment) that the same pipeline
/// fails to reliably reach world-space canvases built/placed the same way
/// this project's other panels are - so this follows the proven
/// raw-InputAction pattern every other working VR panel in this project
/// already uses instead. This class no longer implements any
/// IBeginDragHandler/IDragHandler/IEndDragHandler/IDropHandler interface.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EvidenceNode : MonoBehaviour
{
    [Tooltip("Stable ID used to match connections - an EvidenceDefinition.evidenceId for evidence cards, or a made-up id like \"suspect_wife\" for conclusion/statement cards.")]
    public string nodeId;

    [Tooltip("Fallback label for nodes with no matching EvidenceDefinition (conclusions, statements). Ignored for a nodeId that resolves to a live EvidenceDefinition - see RefreshLabel().")]
    [SerializeField] private string displayText;

    [SerializeField] private TMP_Text label;

    [Tooltip("Optional availability rule for an authored statement or conclusion card. Evidence-backed cards continue to use their evidence discovery state.")]
    [SerializeField] private RequirementNode visibilityRequirement = new RequirementNode();

    [Tooltip("Card background - tinted to reflect focus/pending state by EvidenceBoardController. Optional; visual-only, never read by connection logic.")]
    [SerializeField] private Image background;

    [Header("Visual state colors")]
    [SerializeField] private Color normalColor = new Color(0.16f, 0.16f, 0.18f, 0.95f);
    [SerializeField] private Color focusedColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    [SerializeField] private Color pendingColor = new Color(0.85f, 0.65f, 0.2f, 1f);
    [SerializeField] private Color normalTextColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private Color focusedTextColor = new Color(0.05f, 0.05f, 0.06f, 1f);
    [SerializeField] private Color pendingTextColor = new Color(0.05f, 0.05f, 0.06f, 1f);

    private RectTransform rectTransform;

    public RectTransform RectTransform => rectTransform;

    /// <summary>True if this node currently resolves to a real registered EvidenceDefinition (an evidence card) rather than being a conclusion/statement card with only authored displayText.</summary>
    public bool IsEvidenceBacked { get; private set; }

    /// <summary>Read-only authored statement availability for other case-review views.</summary>
    public bool IsStatementAvailable => RequirementEvaluator.IsSatisfied(visibilityRequirement);

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        EnsureFallbackLabel();
    }

    /// <summary>Lets authored statement cards omit a dedicated text child while still
    /// rendering their fallback text. Existing cards retain their authored labels.</summary>
    private void EnsureFallbackLabel()
    {
        if (label != null) return;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -8f);

        label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 16f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = true;
        label.color = normalTextColor;
        label.raycastTarget = false;
    }

    private void Start()
    {
        RefreshLabel();
    }

    /// <summary>
    /// Re-reads the live EvidenceDefinition (if any) for this nodeId. Safe to call
    /// repeatedly (called from RefreshVisibility every refresh) - read-only, never
    /// mutates EvidenceStateManager.
    /// </summary>
    public void RefreshLabel()
    {
        string text = displayText;
        var record = EvidenceStateManager.Instance != null ? EvidenceStateManager.Instance.GetRecord(nodeId) : null;
        IsEvidenceBacked = record != null && record.definition != null;

        if (IsEvidenceBacked)
        {
            text = record.definition.PlayerDisplayName;
        }

        if (label != null && !string.IsNullOrEmpty(text))
        {
            label.text = text;
        }
    }

    /// <summary>
    /// Shows/hides this card based on live EvidenceStateManager state only - never
    /// mutates evidence state, EvidenceStateManager remains the sole authority.
    /// Evidence-backed nodes are visible once their item has reached at least
    /// EvidenceStatus.Found. Authored statement and conclusion cards can optionally
    /// use a requirement to avoid revealing a later statement before its supporting
    /// evidence is available; an empty requirement keeps the existing always-visible
    /// behaviour.
    /// </summary>
    public void RefreshVisibility()
    {
        RefreshLabel();

        if (!IsEvidenceBacked)
        {
            gameObject.SetActive(RequirementEvaluator.IsSatisfied(visibilityRequirement));
            return;
        }

        bool discovered = EvidenceStateManager.Instance != null
            && EvidenceStateManager.Instance.IsAtOrPastStatus(nodeId, EvidenceStatus.Found);
        gameObject.SetActive(discovered);
    }

    /// <summary>Purely visual - tints the card to reflect whether it currently has input focus and/or is the pending connection anchor. Never affects connection/relationship logic.</summary>
    public void SetVisualState(bool focused, bool pending)
    {
        if (background != null)
        {
            background.color = pending ? pendingColor : (focused ? focusedColor : normalColor);
        }
        if (label != null)
        {
            label.color = pending ? pendingTextColor : (focused ? focusedTextColor : normalTextColor);
        }
    }
}
