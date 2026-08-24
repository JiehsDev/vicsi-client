// Assets/_Project/Scripts/Deduction/EvidenceNode.cs
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// One draggable card on the IOC evidence board - either a piece of evidence
/// or a conclusion (e.g. "Murder weapon"). If nodeId matches a known
/// evidence item its label is pulled live from EvidenceStateManager;
/// otherwise it falls back to the manually authored displayText (used for
/// conclusion and red-herring cards that have no EvidenceDefinition).
/// Dragging is driven by the same Meta ISDK ray/poke -> uGUI event pipeline
/// as the lobby menu buttons (PointableCanvasModule), so this just
/// implements the standard drag/drop handler interfaces.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class EvidenceNode : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Tooltip("Stable ID used to match connections - an EvidenceDefinition.evidenceId for evidence cards, or a made-up id like \"conclusion_murder_weapon\" for conclusion/red-herring cards.")]
    public string nodeId;

    [Tooltip("Fallback label for nodes with no matching EvidenceDefinition (conclusions, red herrings).")]
    [SerializeField] private string displayText;

    [SerializeField] private TMP_Text label;

    private RectTransform rectTransform;
    private EvidenceBoardController board;

    public RectTransform RectTransform => rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        board = GetComponentInParent<EvidenceBoardController>();

        string text = displayText;
        var record = EvidenceStateManager.Instance != null ? EvidenceStateManager.Instance.GetRecord(nodeId) : null;
        if (record != null && record.definition != null)
        {
            text = record.definition.displayName;
        }

        if (label != null && !string.IsNullOrEmpty(text))
        {
            label.text = text;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        board?.BeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform.parent is not RectTransform parent)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var localPoint);
        rectTransform.anchoredPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        board?.EndDrag(this);
    }

    public void OnDrop(PointerEventData eventData)
    {
        var draggedNode = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<EvidenceNode>() : null;
        if (draggedNode != null && draggedNode != this)
        {
            board?.TryConnect(draggedNode, this);
        }
    }
}
