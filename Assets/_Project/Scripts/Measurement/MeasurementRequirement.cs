// Assets/_Project/Scripts/Measurement/MeasurementRequirement.cs
using System;
using UnityEngine;

/// <summary>
/// One required evidence-position measurement: the distance from each of two fixed
/// SceneReferencePoints to the evidence item. The pair lets the item's original
/// position be reconstructed (two circles around fixed points intersect at it).
/// References are stored as stable ids, never Transforms - resolved at runtime via
/// SceneReferencePoint.Find. Expected distances are authoring-time reference values for
/// validating a future player measurement, baked from the scene with the editor helper
/// (MeasurementRequirementSetEditor) - never overwritten at runtime.
/// </summary>
[Serializable]
public class MeasurementRequirement
{
    [Tooltip("Stable id, e.g. \"MEAS-EVD-014\". Logged on every measurement event and referenced by RequirementType.MeasurementCompleted - never derive it from list position.")]
    public string requirementId;

    [Tooltip("The evidenceId whose position is being fixed. Its EvidenceProp transform is the measured point.")]
    public string targetEvidenceId;

    [Tooltip("referenceId of the first SceneReferencePoint.")]
    public string referenceAId;

    [Tooltip("referenceId of the second SceneReferencePoint. Choose it so the angle at the evidence between A and B is well away from 0/180 degrees, or the reconstruction is ambiguous.")]
    public string referenceBId;

    [Min(0f)] public float expectedDistanceA;
    [Min(0f)] public float expectedDistanceB;

    [Tooltip("Allowed absolute error per side, in metres.")]
    [Min(0f)] public float tolerance = 0.10f;

    [Tooltip("When false, any recorded value counts (errors are still logged).")]
    public bool validateTolerance = true;

    [Tooltip("Measure on the floor plan (horizontal distance, height ignored) - how scene sketches record positions, and what lets a wall corner act as a vertical reference edge. When false, straight-line 3D distance.")]
    public bool measureOnFloorPlan = true;

    public bool required = true;

    [Tooltip("The measurement belongs before the item is disturbed - see disturbanceStatus.")]
    public bool mustPrecedeDisturbance = true;

    [Tooltip("The item counts as disturbed once its lifecycle reaches this status (default Collected), or once it leaves the scene in custody.")]
    public EvidenceStatus disturbanceStatus = EvidenceStatus.Collected;

    [Tooltip("For a measurement taken after disturbance: Warn still completes it (flagged late, MeasurementAfterDisturbance logged); Block records it but never completes.")]
    public ProcedureEnforcement enforcement = ProcedureEnforcement.Warn;

    [Min(0f)] public float scoreWeight = 1f;

    [Tooltip("This item is part of the scene roster for evidence-to-evidence distances: every pair of required, participating items must be measured once. Untick to keep an item out of pair measurement (its fixed references still apply).")]
    public bool participatesInScenePairMeasurement = true;

    [TextArea]
    public string instructionText;

    public bool UsesReference(string referenceId) =>
        !string.IsNullOrEmpty(referenceId) && (referenceId == referenceAId || referenceId == referenceBId);
}
