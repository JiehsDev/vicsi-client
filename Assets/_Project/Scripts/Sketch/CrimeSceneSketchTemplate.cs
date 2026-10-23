// Assets/_Project/Scripts/Sketch/CrimeSceneSketchTemplate.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The authored drawing sheet for one scenario's Crime Scene Sketch: a RectTransform board
/// (size = definition.canvasSize, pivot centred) whose children are the permanent
/// architecture (SketchFixedElement), the invisible evidence drop zones
/// (SketchEvidenceAnchor) and the measurement slots (SketchMeasurementSlot), all keyed by
/// the ids in the CrimeSceneSketchDefinition. It lives in the scenario scene on its own
/// world-space canvas so it can be laid out by dragging in the Scene view; at runtime
/// CrimeSceneSketchUI adopts it as the board of the sketch panel.
///
/// Board coordinates are the board's local space (origin at the centre, +y up = north).
/// worldMinXZ / worldMaxXZ say which floor-plan rectangle (world X/Z, +Z north) the board
/// covers; they are only used by the editor "Align ... To Scene" helper, never at runtime.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class CrimeSceneSketchTemplate : MonoBehaviour
{
    public CrimeSceneSketchDefinition definition;

    [Header("Floor-plan mapping (authoring only)")]
    [Tooltip("World X/Z at the board's bottom-left corner.")]
    public Vector2 worldMinXZ;
    [Tooltip("World X/Z at the board's top-right corner (+Z is up/north on the sheet).")]
    public Vector2 worldMaxXZ;

    [Tooltip("Optional detail rectangle within the board. World mapping and evidence placement use this area; orientation plans can occupy the rest of the sheet.")]
    public RectTransform mappedArea;

    [Tooltip("Editor only: label fixed elements with their ids in the Scene view (anchors and slots are always labelled by their gizmos).")]
    public bool showEditorLabels = true;

    public RectTransform Board => (RectTransform)transform;

    // In Play mode the sheet is only ever shown inside the sketch panel - never left
    // floating at its authoring spot in the scene.
    private void Awake()
    {
        if (Application.isPlaying && GetComponentInParent<CrimeSceneSketchUI>(true) == null)
        {
            gameObject.SetActive(false);
        }
    }

    public IEnumerable<SketchEvidenceAnchor> Anchors => GetComponentsInChildren<SketchEvidenceAnchor>(true);
    public IEnumerable<SketchMeasurementSlot> Slots => GetComponentsInChildren<SketchMeasurementSlot>(true);
    public IEnumerable<SketchFixedElement> FixedElements => GetComponentsInChildren<SketchFixedElement>(true);

    public SketchEvidenceAnchor FindAnchor(string anchorId)
    {
        if (string.IsNullOrEmpty(anchorId)) return null;
        foreach (var a in Anchors)
        {
            if (a.anchorId == anchorId) return a;
        }
        return null;
    }

    public SketchMeasurementSlot FindSlot(string slotId)
    {
        if (string.IsNullOrEmpty(slotId)) return null;
        foreach (var s in Slots)
        {
            if (s.slotId == slotId) return s;
        }
        return null;
    }

    public bool HasFixedElement(SketchFixedElementKind kind)
    {
        foreach (var f in FixedElements)
        {
            if (f.kind == kind) return true;
        }
        return false;
    }

    /// <summary>Any transform's position in board coordinates.</summary>
    public Vector2 ToBoard(Transform t) => Board.InverseTransformPoint(t.position);

    [Tooltip("Multiplies every evidence anchor's snapRadius when judging a drop. A stick-driven cursor can't hit a small circle precisely; the correct anchor is always tested first, so widening never lets one item land on another's spot. 1 = authored radii.")]
    [SerializeField, Min(1f)] private float evidenceDropForgiveness = 2f;

    /// <summary>The radius (board units) a drop on this anchor is accepted within.</summary>
    public float EffectiveSnapRadius(SketchEvidenceAnchor anchor) => anchor != null ? anchor.snapRadius * evidenceDropForgiveness : 0f;

    /// <summary>
    /// Which anchor an evidence drop at boardPosition lands on. Correct if it is inside the
    /// element's own anchor radius (even where radii overlap - the body and the spatter
    /// beneath it are close on this plan); otherwise the nearest anchor whose radius
    /// contains the point; null for open board. The caller treats every non-correct
    /// result identically, so the response never tells the student where another item's
    /// anchor is.
    /// </summary>
    public string ResolveEvidenceDrop(string correctAnchorId, Vector2 boardPosition)
    {
        var correct = FindAnchor(correctAnchorId);
        if (correct != null && Vector2.Distance(ToBoard(correct.transform), boardPosition) <= EffectiveSnapRadius(correct))
        {
            return correct.anchorId;
        }
        string best = null;
        float bestDistance = float.MaxValue;
        foreach (var a in Anchors)
        {
            float d = Vector2.Distance(ToBoard(a.transform), boardPosition);
            if (d <= EffectiveSnapRadius(a) && d < bestDistance)
            {
                best = a.anchorId;
                bestDistance = d;
            }
        }
        return best;
    }

    /// <summary>Slot under boardPosition among those isTarget accepts (nearest centre wins), or null.</summary>
    public SketchMeasurementSlot ResolveSlotDrop(Vector2 boardPosition, System.Func<SketchMeasurementSlot, bool> isTarget)
    {
        SketchMeasurementSlot best = null;
        float bestDistance = float.MaxValue;
        foreach (var s in Slots)
        {
            if (isTarget != null && !isTarget(s)) continue;
            Vector2 centre = ToBoard(s.transform);
            Rect r = s.RectTransform.rect;
            Vector2 half = r.size * 0.5f + Vector2.one * s.dropMargin;
            Vector2 d = boardPosition - centre;
            if (Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.y) <= half.y && d.sqrMagnitude < bestDistance)
            {
                best = s;
                bestDistance = d.sqrMagnitude;
            }
        }
        return best;
    }

    /// <summary>World position of the evidence anchor that slotId's measurement line starts from (editor gizmos).</summary>
    public Vector3? AnchorWorldPositionForSlot(string slotId)
    {
        var slot = definition != null ? definition.FindSlot(slotId) : null;
        var element = slot != null ? definition.FindElement(slot.evidenceId) : null;
        var anchor = element != null ? FindAnchor(element.correctAnchorId) : null;
        return anchor != null ? anchor.transform.position : (Vector3?)null;
    }

    /// <summary>Floor-plan world point -> board coordinates, using worldMinXZ/worldMaxXZ.</summary>
    public Vector2 WorldToBoard(Vector3 world)
    {
        RectTransform area = mappedArea != null ? mappedArea : Board;
        Vector2 size = area.rect.size;
        float u = Mathf.InverseLerp(worldMinXZ.x, worldMaxXZ.x, world.x);
        float v = Mathf.InverseLerp(worldMinXZ.y, worldMaxXZ.y, world.z);
        Vector2 local = new Vector2((u - 0.5f) * size.x, (v - 0.5f) * size.y);
        return mappedArea != null ? ToBoard(area) + local : local;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Re-derives every evidence anchor (from the evidence's measurement anchor) and every
    /// slot's reference foot (perpendicular foot on its reference) from the live scene,
    /// through worldMinXZ/worldMaxXZ. Slots are moved to the middle of their measurement
    /// line, nudged to one side. Use after moving props or references; walls and labels
    /// are left alone. Generic - driven entirely by the definition's ids.
    /// </summary>
    [ContextMenu("Align Anchors And Reference Feet To Scene")]
    private void AlignToScene()
    {
        if (definition == null)
        {
            Debug.LogWarning("[CrimeSceneSketchTemplate] No definition assigned.", this);
            return;
        }
        foreach (var element in definition.evidenceElements)
        {
            var anchor = FindAnchor(element.correctAnchorId);
            if (anchor == null || !MeasurementGeometry.TryGetEvidenceAnchor(element.evidenceId, out var world)) continue;
            UnityEditor.Undo.RecordObject(anchor.transform, "Align sketch anchor");
            SetBoardPosition(anchor.transform, WorldToBoard(world));
        }
        foreach (var slotDef in definition.measurementSlots)
        {
            var slot = FindSlot(slotDef.slotId);
            var reference = SceneReferencePoint.Find(slotDef.referenceId);
            if (slot == null || reference == null || !MeasurementGeometry.TryGetEvidenceAnchor(slotDef.evidenceId, out var world)) continue;
            Vector2 from = WorldToBoard(world);
            Vector2 foot = WorldToBoard(MeasurementGeometry.ReferenceFoot(reference, world));
            Vector2 along = foot - from;
            Vector2 side = along.sqrMagnitude > 1e-4f ? new Vector2(-along.y, along.x).normalized : Vector2.up;
            UnityEditor.Undo.RecordObject(slot.transform, "Align sketch slot");
            Vector2 half = slot.RectTransform.rect.size * 0.5f;
            float clearance = Mathf.Abs(side.x) * half.x + Mathf.Abs(side.y) * half.y + 14f;
            SetBoardPosition(slot.transform, (from + foot) * 0.5f + side * clearance);
            // The foot is usually the slot's child, so place it after the slot has moved.
            if (slot.referenceFoot != null)
            {
                UnityEditor.Undo.RecordObject(slot.referenceFoot, "Align sketch reference foot");
                SetBoardPosition(slot.referenceFoot, foot);
            }
        }
        Debug.Log("[CrimeSceneSketchTemplate] Anchors and reference feet aligned to the scene.", this);
    }

    private void SetBoardPosition(Transform t, Vector2 boardPosition)
    {
        t.position = Board.TransformPoint(boardPosition);
        var lp = t.localPosition;
        lp.z = 0f;
        t.localPosition = lp;
    }

    private void OnDrawGizmos()
    {
        var corners = new Vector3[4];
        Board.GetWorldCorners(corners);
        UnityEditor.Handles.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);
        UnityEditor.Handles.DrawPolyLine(corners[0], corners[1], corners[2], corners[3], corners[0]);
        UnityEditor.Handles.Label(corners[1], definition != null ? $"Sketch: {definition.sketchId}" : "Sketch (no definition)");
    }
#endif
}
