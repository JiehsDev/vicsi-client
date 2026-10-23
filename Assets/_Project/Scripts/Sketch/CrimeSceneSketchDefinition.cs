// Assets/_Project/Scripts/Sketch/CrimeSceneSketchDefinition.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public enum SketchSymbolShape
{
    Square,
    Diamond,
    Bar,
    BodyOutline
}

/// <summary>One evidence item the student can place on the sketch.</summary>
[Serializable]
public class SketchEvidenceElementDefinition
{
    [Tooltip("EvidenceDefinition.evidenceId this element stands for. It unlocks only once that item is legitimately discovered (see CrimeSceneSketchManager.IsEvidenceUnlocked).")]
    public string evidenceId;

    [Tooltip("Student-facing name on the palette card and legend. Never an internal id.")]
    public string displayName;

    [Tooltip("anchorId of the SketchEvidenceAnchor in the template that is this element's correct position. The anchor itself is invisible to the student.")]
    public string correctAnchorId;

    [Tooltip("Must be placed before the sketch can be submitted.")]
    public bool required = true;

    [Header("Symbol")]
    public SketchSymbolShape shape = SketchSymbolShape.Square;
    public Color color = new Color(0.85f, 0.2f, 0.2f, 1f);

    [Tooltip("Size of the placed symbol on the board, in board units (pixels).")]
    public Vector2 placedSize = new Vector2(34f, 34f);

    [Tooltip("Optional icon; when empty the symbol is drawn from Shape + Color.")]
    public Sprite icon;
}

/// <summary>
/// One distance the student files on the sketch: evidenceId's distance to referenceId, as
/// recorded by MeasurementStateManager for requirementId. Validation is by this identity
/// (requirementId + referenceId), never by the displayed number - two cards can read the
/// same "1.34 m".
/// </summary>
[Serializable]
public class SketchMeasurementSlotDefinition
{
    [Tooltip("Matches a SketchMeasurementSlot.slotId in the template.")]
    public string slotId;

    public string evidenceId;

    [Tooltip("SceneReferencePoint.referenceId - one of this evidence item's own two fixed references.")]
    public string referenceId;

    [Tooltip("MeasurementRequirement.requirementId whose recorded distance belongs here.")]
    public string requirementId;

    [Tooltip("Optional student-facing relationship text. Empty = \"to {reference display name}\".")]
    public string relationshipText;

    public bool required = true;
}

/// <summary>
/// Data for one scenario's Crime Scene Sketch: which evidence elements and measurement
/// slots exist, what each one's correct target is, and what completion requires. The
/// visual layout (walls, labels, north arrow, the invisible anchors and the slots) is
/// authored as RectTransforms on a CrimeSceneSketchTemplate in the scenario scene, keyed
/// by the ids here - so a developer drags things in the Scene view, never types
/// coordinates, and no scene position lives in C#.
///
/// A scene-level procedure, deliberately NOT part of EvidenceStatus.
/// </summary>
[CreateAssetMenu(menuName = "ViCSI/Sketch/Crime Scene Sketch Definition", fileName = "Sketch_")]
public class CrimeSceneSketchDefinition : ScriptableObject
{
    public string sketchId = "SKETCH-";
    public string scenarioId;
    public string title = "Crime Scene Sketch";

    [Tooltip("Board size in board units (pixels). The template's root RectTransform uses this size.")]
    public Vector2 canvasSize = new Vector2(1000f, 1000f);

    [Header("Background")]
    public Color paperColor = new Color(0.95f, 0.94f, 0.90f, 1f);
    public bool showGrid = true;
    [Min(10f)] public float gridSpacing = 50f;
    public Color gridColor = new Color(0.2f, 0.35f, 0.55f, 0.10f);

    [Header("Completion")]
    [Tooltip("The template must contain a SketchFixedElement of kind NorthArrow.")]
    public bool requireNorthArrow = true;

    public List<SketchEvidenceElementDefinition> evidenceElements = new();
    public List<SketchMeasurementSlotDefinition> measurementSlots = new();

    public SketchEvidenceElementDefinition FindElement(string evidenceId)
    {
        if (string.IsNullOrEmpty(evidenceId)) return null;
        foreach (var e in evidenceElements)
        {
            if (e != null && e.evidenceId == evidenceId) return e;
        }
        return null;
    }

    public SketchMeasurementSlotDefinition FindSlot(string slotId)
    {
        if (string.IsNullOrEmpty(slotId)) return null;
        foreach (var s in measurementSlots)
        {
            if (s != null && s.slotId == slotId) return s;
        }
        return null;
    }

    /// <summary>The slot that takes (requirementId, referenceId)'s recorded distance, or null.</summary>
    public SketchMeasurementSlotDefinition FindSlotFor(string requirementId, string referenceId)
    {
        foreach (var s in measurementSlots)
        {
            if (s != null && s.requirementId == requirementId && s.referenceId == referenceId) return s;
        }
        return null;
    }

    /// <summary>
    /// Authoring problems: duplicate or missing ids, slots that don't match a measurement
    /// requirement's own references, template objects missing for an id, no north arrow.
    /// Empty when the definition, template and measurement set agree. Pass null to skip a check.
    /// </summary>
    public List<string> Validate(CrimeSceneSketchTemplate template, MeasurementRequirementSet measurements)
    {
        var problems = new List<string>();
        var seenEvidence = new HashSet<string>();
        foreach (var e in evidenceElements)
        {
            if (e == null || string.IsNullOrEmpty(e.evidenceId))
            {
                problems.Add("Evidence element with no evidenceId.");
                continue;
            }
            if (!seenEvidence.Add(e.evidenceId)) problems.Add($"Duplicate evidence element '{e.evidenceId}'.");
            if (string.IsNullOrEmpty(e.displayName)) problems.Add($"Evidence element '{e.evidenceId}' has no display name.");
            if (string.IsNullOrEmpty(e.correctAnchorId)) problems.Add($"Evidence element '{e.evidenceId}' has no correct anchor.");
            else if (template != null && template.FindAnchor(e.correctAnchorId) == null)
                problems.Add($"Template has no anchor '{e.correctAnchorId}' (for {e.evidenceId}).");
        }

        var seenSlots = new HashSet<string>();
        var seenIdentity = new HashSet<string>();
        foreach (var s in measurementSlots)
        {
            if (s == null || string.IsNullOrEmpty(s.slotId))
            {
                problems.Add("Measurement slot with no slotId.");
                continue;
            }
            if (!seenSlots.Add(s.slotId)) problems.Add($"Duplicate slot '{s.slotId}'.");
            if (!seenIdentity.Add(s.requirementId + "|" + s.referenceId)) problems.Add($"Two slots take the same measurement ({s.requirementId}, {s.referenceId}).");
            if (FindElement(s.evidenceId) == null) problems.Add($"Slot '{s.slotId}' refers to evidence '{s.evidenceId}', which has no element.");
            if (template != null && template.FindSlot(s.slotId) == null) problems.Add($"Template has no slot '{s.slotId}'.");
            if (measurements != null)
            {
                var r = measurements.Find(s.requirementId);
                if (r == null) problems.Add($"Slot '{s.slotId}': no measurement requirement '{s.requirementId}'.");
                else
                {
                    if (r.targetEvidenceId != s.evidenceId) problems.Add($"Slot '{s.slotId}': requirement '{s.requirementId}' measures '{r.targetEvidenceId}', not '{s.evidenceId}'.");
                    if (r.referenceAId != s.referenceId && r.referenceBId != s.referenceId)
                        problems.Add($"Slot '{s.slotId}': '{s.referenceId}' is not one of {s.requirementId}'s own references.");
                }
            }
        }

        if (requireNorthArrow && template != null && !template.HasFixedElement(SketchFixedElementKind.NorthArrow))
        {
            problems.Add("Template has no north arrow.");
        }
        return problems;
    }
}
