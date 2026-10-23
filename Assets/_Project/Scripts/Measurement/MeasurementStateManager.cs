// Assets/_Project/Scripts/Measurement/MeasurementStateManager.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum MeasurementProgress
{
    NotStarted = 0,
    Partial = 1,
    Complete = 2
}

public enum MeasurementRecordResult
{
    /// <summary>Recorded; the requirement is not complete yet (other side missing, out of tolerance, or late under Block).</summary>
    Recorded = 0,

    /// <summary>Recorded, and this completed the requirement.</summary>
    Completed = 1,

    /// <summary>The requirement was already complete - nothing recorded.</summary>
    AlreadyComplete = 2,

    UnknownRequirement = 3,
    UnknownReference = 4,

    /// <summary>The reference exists but is not one of this requirement's two references.</summary>
    WrongReference = 5,

    InvalidDistance = 6,
    PrerequisiteBlocked = 7
}

/// <summary>Runtime state of one MeasurementRequirement. Never persisted to assets.</summary>
public class MeasurementRecord
{
    public MeasurementRequirement requirement;
    public MeasurementProgress progress = MeasurementProgress.NotStarted;

    public bool hasA;
    public float measuredA;
    public float errorA;
    public bool withinToleranceA;
    /// <summary>Time.time when side A was last measured, or -1.</summary>
    public float measuredAtA = -1f;

    public bool hasB;
    public float measuredB;
    public float errorB;
    public bool withinToleranceB;
    public float measuredAtB = -1f;

    /// <summary>True once any side was recorded after the item was disturbed (see MeasurementRequirement.disturbanceStatus).</summary>
    public bool takenAfterDisturbance;

    /// <summary>Time.time when the requirement completed, or -1.</summary>
    public float completedAtSessionTime = -1f;

    public bool IsComplete => progress == MeasurementProgress.Complete;
    public bool TakenBeforeDisturbance => !takenAfterDisturbance;
    public bool WithinTolerance => hasA && hasB && withinToleranceA && withinToleranceB;
}

/// <summary>
/// Tracks evidence-position measurements against the scene's MeasurementRequirementSet.
/// External procedure state, like ScenePhotographyManager: no EvidenceStatus, and the
/// evidence lifecycle never waits on it unless an item's stepRequirements explicitly
/// reference RequirementType.MeasurementCompleted.
///
/// RecordMeasurement is the single entry point a future measuring tool will call with
/// (requirementId, referenceId, measured distance). A requirement completes only when
/// both of its references have a recorded distance, within tolerance when validated, and
/// - under Block enforcement - taken before the item was disturbed. Partial state (one
/// side only, or a side out of tolerance) is kept; re-measuring a side replaces it.
///
/// Two layers: these fixed-reference measurements, and evidence-to-evidence pair
/// distances (MeasurementStateManager.Pairs.cs) - together the scene measurements the
/// Recorder waits for (RequirementType.SceneMeasurementsComplete).
/// </summary>
public partial class MeasurementStateManager : MonoBehaviour
{
    public static MeasurementStateManager Instance { get; private set; }

    /// <summary>Fired (requirementId) after any recorded measurement - lets requirement-driven managers re-evaluate RequirementType.MeasurementCompleted.</summary>
    public static event Action<string> OnMeasurementProgress;

    [SerializeField] private MeasurementRequirementSet requirementSet;
    [Tooltip("Registered server geometry profile, independent of deduction rules. Empty identifies the historical house layout.")]
    [SerializeField] private string measurementLayoutId = "";
    public string MeasurementLayoutId => measurementLayoutId;

#if UNITY_EDITOR
    [Tooltip("Editor-only: draw reference -> evidence lines while this object is selected. Never drawn in builds.")]
    [SerializeField] private bool showMeasurementGizmos = true;
#endif

    private readonly Dictionary<string, MeasurementRecord> records = new();

    public MeasurementRequirementSet RequirementSet => requirementSet;

    public IEnumerable<MeasurementRecord> AllRecords => records.Values;

    private void Awake()
    {
        Instance = this;
        BuildRecords();
        BuildPairs();
    }

    /// <summary>
    /// Clears every fixed-reference and evidence-pair measurement - a fresh investigation.
    /// A new session normally reloads the scene (all state is per-instance); this is the
    /// same reset in place, for tests and any future in-scene restart.
    /// </summary>
    public void ResetSession()
    {
        BuildRecords();
        BuildPairs();
        OnMeasurementProgress?.Invoke(string.Empty);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void BuildRecords()
    {
        records.Clear();
        if (requirementSet == null || requirementSet.requirements == null)
        {
            return;
        }

        foreach (var requirement in requirementSet.requirements)
        {
            if (requirement == null || string.IsNullOrEmpty(requirement.requirementId))
            {
                continue;
            }
            if (records.ContainsKey(requirement.requirementId))
            {
                Debug.LogWarning($"[MeasurementStateManager] Duplicate requirementId '{requirement.requirementId}' - only the first is tracked.", this);
                continue;
            }
            records[requirement.requirementId] = new MeasurementRecord { requirement = requirement };
        }
    }

    // --- Queries ---------------------------------------------------------

    public MeasurementRecord GetRecord(string requirementId)
    {
        return !string.IsNullOrEmpty(requirementId) && records.TryGetValue(requirementId, out var record) ? record : null;
    }

    public bool IsMeasurementComplete(string requirementId)
    {
        var record = GetRecord(requirementId);
        return record != null && record.IsComplete;
    }

    /// <summary>
    /// Whether the requirement's evidence counts as disturbed right now: lifecycle
    /// at/after disturbanceStatus, no longer in scene custody, or - for an item whose
    /// swab sample IS its collection (EvidenceDefinition.collectedBySwab, e.g. the blood
    /// spatter) - that sample has been taken, since sampling alters the stain itself.
    /// A trace swabbed off another object (the knife's blade) doesn't move that object.
    /// </summary>
    public bool IsDisturbed(MeasurementRequirement requirement)
    {
        var esm = EvidenceStateManager.Instance;
        var evidence = esm != null && requirement != null ? esm.GetRecord(requirement.targetEvidenceId) : null;
        if (evidence == null)
        {
            return false;
        }
        return esm.IsAtOrPastStatus(requirement.targetEvidenceId, requirement.disturbanceStatus)
            || evidence.custodyLocation != EvidenceCustodyLocation.Scene
            || (evidence.definition != null && evidence.definition.collectedBySwab && evidence.swabbingDone);
    }

    /// <summary>
    /// The true distance in the scene right now from referenceId to the requirement's
    /// evidence, by the requirement's own method (floor plan or 3D). For a future
    /// measuring tool and tests - never written back to the requirement.
    /// </summary>
    public bool TryGetSceneDistance(string requirementId, string referenceId, out float distance)
    {
        distance = 0f;
        var requirement = requirementSet != null ? requirementSet.Find(requirementId) : null;
        var reference = SceneReferencePoint.Find(referenceId);
        if (requirement == null || reference == null
            || !MeasurementGeometry.TryGetEvidenceAnchor(requirement.targetEvidenceId, out var target))
        {
            return false;
        }
        distance = MeasurementGeometry.DistanceToReference(reference, target, requirement.measureOnFloorPlan);
        return true;
    }

    /// <summary>
    /// The upload's measurement section: every required requirement, in authoring order,
    /// read from the live records. Display/analytics only - the server scores from the
    /// logged Measurement* events against its own expected distances.
    /// </summary>
    public MeasurementAssessmentResult BuildAssessment()
    {
        var items = new List<MeasurementItemResult>();
        if (requirementSet != null && requirementSet.requirements != null)
        {
            foreach (var requirement in requirementSet.requirements)
            {
                var record = requirement != null ? GetRecord(requirement.requirementId) : null;
                if (record != null && record.requirement == requirement && requirement.required)
                {
                    items.Add(MeasurementScoring.BuildItem(record));
                }
            }
        }
        var result = MeasurementScoring.Aggregate(items);
        var scene = GetSceneProgress(false);
        result.fixedSidesRequired = scene.fixedRequired;
        result.fixedSidesCompleted = scene.fixedCompleted;
        result.pairRequiredCount = scene.pairRequired;
        result.pairCompletedCount = scene.pairCompleted;
        result.sceneMeasurementsComplete = scene.IsComplete;
        result.pairs = BuildPairResults();
        foreach (var p in result.pairs) if (p.late) result.pairLateCount++;
        return result;
    }

    // --- Recording -------------------------------------------------------

    /// <summary>Body-only order gate; other evidence keeps its existing measurement rules.</summary>
    public static string GetBodyMeasurementBlockReason(string evidenceId)
    {
        var esm = EvidenceStateManager.Instance;
        var record = esm != null ? esm.GetRecord(evidenceId) : null;
        if (record?.definition == null || esm.IsAtOrPastStatus(evidenceId,EvidenceStatus.PhotographedFinal)) return null;
        if (record.definition.requiresBodyRemoval) return "Complete the final body photograph before recording body measurements.";
        return record.definition.measurementAfterFinalPhoto ? "Complete the final evidence photograph before recording its measurements." : null;
    }

    public MeasurementRecordResult RecordMeasurement(string requirementId, string referenceId, float measuredDistance)
    {
        if (ProcedureInterlock.BlocksUnrelatedActions) return MeasurementRecordResult.PrerequisiteBlocked;
        var record = GetRecord(requirementId);
        if (record == null)
        {
            Debug.LogWarning($"[MeasurementStateManager] Unknown measurement requirement '{requirementId}'.");
            return MeasurementRecordResult.UnknownRequirement;
        }

        var requirement = record.requirement;
        if (GetBodyMeasurementBlockReason(requirement.targetEvidenceId) != null)
            return MeasurementRecordResult.PrerequisiteBlocked;
        if (SceneReferencePoint.Find(referenceId) == null)
        {
            Debug.LogWarning($"[MeasurementStateManager] Unknown reference point '{referenceId}' for '{requirementId}'.");
            return MeasurementRecordResult.UnknownReference;
        }
        if (!requirement.UsesReference(referenceId))
        {
            Debug.LogWarning($"[MeasurementStateManager] '{referenceId}' is not a reference of '{requirementId}'.");
            return MeasurementRecordResult.WrongReference;
        }
        if (float.IsNaN(measuredDistance) || float.IsInfinity(measuredDistance) || measuredDistance < 0f)
        {
            return MeasurementRecordResult.InvalidDistance;
        }
        if (record.IsComplete)
        {
            return MeasurementRecordResult.AlreadyComplete;
        }

        if (record.progress == MeasurementProgress.NotStarted)
        {
            Log(SessionEventType.MeasurementStarted, requirement, referenceId, null, null);
        }

        bool isA = referenceId == requirement.referenceAId;
        float expected = isA ? requirement.expectedDistanceA : requirement.expectedDistanceB;
        float error = measuredDistance - expected;
        bool within = Mathf.Abs(error) <= requirement.tolerance;

        if (isA)
        {
            record.hasA = true;
            record.measuredA = measuredDistance;
            record.errorA = error;
            record.withinToleranceA = within;
            record.measuredAtA = Time.time;
        }
        else
        {
            record.hasB = true;
            record.measuredB = measuredDistance;
            record.errorB = error;
            record.withinToleranceB = within;
            record.measuredAtB = Time.time;
        }

        bool late = requirement.mustPrecedeDisturbance && IsDisturbed(requirement);
        if (late)
        {
            record.takenAfterDisturbance = true;
        }

        var measuredPayload = new Dictionary<string, string>
        {
            { "measuredDistance", Format(measuredDistance) },
            { "expectedDistance", Format(expected) },
            { "error", Format(error) },
            { "tolerance", Format(requirement.tolerance) },
            { "withinTolerance", within.ToString() },
            { "afterDisturbance", late.ToString() }
        };
        Log(SessionEventType.MeasurementTaken, requirement, referenceId, measuredPayload, null);
        if (requirement.validateTolerance && !within)
        {
            Log(SessionEventType.MeasurementOutOfTolerance, requirement, referenceId, measuredPayload, null);
        }
        if (late)
        {
            Log(SessionEventType.MeasurementAfterDisturbance, requirement, referenceId, measuredPayload,
                requirement.enforcement == ProcedureEnforcement.Block ? "blocked" : "warned");
        }

        bool bothSides = record.hasA && record.hasB;
        bool toleranceOk = !requirement.validateTolerance || (record.withinToleranceA && record.withinToleranceB);
        bool timingOk = !(record.takenAfterDisturbance && requirement.enforcement == ProcedureEnforcement.Block);

        MeasurementRecordResult result;
        if (bothSides && toleranceOk && timingOk)
        {
            record.progress = MeasurementProgress.Complete;
            record.completedAtSessionTime = Time.time;
            Log(SessionEventType.MeasurementCompleted, requirement, null, new Dictionary<string, string>
            {
                { "measuredDistanceA", Format(record.measuredA) },
                { "measuredDistanceB", Format(record.measuredB) },
                { "errorA", Format(record.errorA) },
                { "errorB", Format(record.errorB) },
                { "withinTolerance", record.WithinTolerance.ToString() },
                { "takenBeforeDisturbance", record.TakenBeforeDisturbance.ToString() }
            }, null);
            result = MeasurementRecordResult.Completed;
        }
        else
        {
            record.progress = MeasurementProgress.Partial;
            result = MeasurementRecordResult.Recorded;
        }

        OnMeasurementProgress?.Invoke(requirementId);
        return result;
    }

    private static void Log(SessionEventType type, MeasurementRequirement requirement, string referenceId, Dictionary<string, string> extra, string enforcementResult)
    {
        if (SessionLogger.Instance == null)
        {
            return;
        }

        var payload = new Dictionary<string, string>
        {
            { "requirementId", requirement.requirementId },
            { "evidenceId", requirement.targetEvidenceId ?? string.Empty },
            { "sessionTimeSeconds", Format(Time.time) }
        };
        if (Instance != null && !string.IsNullOrEmpty(Instance.measurementLayoutId))
            payload["layoutId"] = Instance.measurementLayoutId;
        if (!string.IsNullOrEmpty(referenceId))
        {
            payload["referenceId"] = referenceId;
        }
        if (extra != null)
        {
            foreach (var kv in extra)
            {
                payload[kv.Key] = kv.Value;
            }
        }
        if (enforcementResult != null)
        {
            payload["enforcement"] = enforcementResult;
        }

        SessionLogger.Instance.LogEvent(type, requirement.targetEvidenceId, payload);
    }

    private static string Format(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);

#if UNITY_EDITOR
    // Editor-only, while this object is selected: per item, its measurement point, a line to
    // each of ITS two references (perpendicular foot for a wall), the distances, the
    // reference names, a right-angle mark at the item, and the angle check.
    private void OnDrawGizmosSelected()
    {
        if (!showMeasurementGizmos || requirementSet == null)
        {
            return;
        }

        foreach (var requirement in requirementSet.requirements)
        {
            if (requirement == null || !MeasurementGeometry.TryGetEvidenceAnchor(requirement.targetEvidenceId, out var target))
            {
                continue;
            }

            var a = SceneReferencePoint.Find(requirement.referenceAId);
            var b = SceneReferencePoint.Find(requirement.referenceBId);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(target, 0.05f);
            DrawLeg(a, target, new Color(1f, 0.55f, 0f), requirement.measureOnFloorPlan);
            DrawLeg(b, target, new Color(0.2f, 0.7f, 1f), requirement.measureOnFloorPlan);

            string label = requirement.targetEvidenceId;
            if (a != null && b != null)
            {
                float angle = MeasurementGeometry.ReferenceAngle(target, a, b);
                DrawRightAngle(target, MeasurementGeometry.ReferenceFoot(a, target), MeasurementGeometry.ReferenceFoot(b, target));
                label += $"  {angle:0}°" + (MeasurementGeometry.ReferenceAngleWarning(requirement.targetEvidenceId, angle) != null ? "  CHECK" : "");
            }
            UnityEditor.Handles.Label(target + Vector3.up * 0.3f, label);
        }
    }

    private static void DrawLeg(SceneReferencePoint reference, Vector3 target, Color color, bool onFloorPlan)
    {
        if (reference == null)
        {
            return;
        }
        Vector3 foot = MeasurementGeometry.ReferenceFoot(reference, target);
        Vector3 from = new Vector3(foot.x, target.y, foot.z);
        Gizmos.color = color;
        Gizmos.DrawLine(from, target);
        Gizmos.DrawWireSphere(from, 0.04f);
        UnityEditor.Handles.Label((from + target) * 0.5f,
            $"{reference.displayName}: {MeasurementGeometry.DistanceToReference(reference, target, onFloorPlan):0.00} m");
    }

    // A small square corner at the item between the two legs (the implied rectangle).
    private static void DrawRightAngle(Vector3 target, Vector3 footA, Vector3 footB)
    {
        Vector3 da = new Vector3(footA.x - target.x, 0f, footA.z - target.z);
        Vector3 db = new Vector3(footB.x - target.x, 0f, footB.z - target.z);
        if (da.sqrMagnitude < 1e-6f || db.sqrMagnitude < 1e-6f)
        {
            return;
        }
        da = da.normalized * 0.15f;
        db = db.normalized * 0.15f;
        Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
        Gizmos.DrawLine(target + da, target + da + db);
        Gizmos.DrawLine(target + db, target + da + db);
    }
#endif
}
