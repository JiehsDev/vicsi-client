// Assets/_Project/Scripts/UI/EvidenceProcedureMapResolver.cs
using System.Collections.Generic;
using UnityEngine;

public enum ProcedureMapStepState
{
    Completed = 0,
    Current = 1,
    Pending = 2,
    /// <summary>Not produced any more: the map shows exactly one instructional priority (see Resolve).</summary>
    Blocked = 3
}

/// <summary>One box on the Evidence Procedure map.</summary>
public class ProcedureMapStep
{
    /// <summary>Stable presentation id (scene_photography, initial_photo, chalk, marker, measurement, ...).</summary>
    public string stepId;
    public string displayName;
    public ProcedureMapStepState state;
    /// <summary>Short instruction under the Current box (null otherwise).</summary>
    public string guidance;
    /// <summary>The single sub-action inside the Current box, e.g. "Apply fingerprint powder" (null otherwise).</summary>
    public string subStep;
    /// <summary>True for the global Overall Scene Photography box (not an evidence step).</summary>
    public bool isGlobal;
}

/// <summary>The whole map for one item: its steps plus the single instructional priority.</summary>
public class ProcedureMapResult
{
    public List<ProcedureMapStep> steps = new();
    /// <summary>The one Current step, or null when the procedure is complete (or the item is unknown).</summary>
    public ProcedureMapStep current;
    public string instruction;
    public string subStep;
    /// <summary>True when the item has steps and every one is Completed.</summary>
    public bool isComplete;
}

/// <summary>
/// Read-only interpretation of the existing procedure for one evidence item, for the
/// Evidence Procedure map. It changes nothing: every answer comes from the authoritative
/// systems - ScenePhotographyManager, EvidenceStateManager (status, swab/sample, print,
/// lab, block reasons), MeasurementStateManager, the fingerprint kit/print marker.
///
/// Which boxes appear is derived from the item's own EvidenceDefinition flags and its
/// measurement requirement - never from item ids - so a new item gets a correct map
/// from its data. The Current box is the first step in the expected order that is not
/// yet done, read from live state, so an out-of-order action (final photo before
/// measuring) shows the photo as done and the measurement still Current.
///
/// Exactly ONE step is Current at a time - the first incomplete step in the intended
/// procedure (Overall Scene Photography first), whether or not the game would also allow
/// a later action right now. It is guidance on what to do next, not a list of executable
/// actions, so every other incomplete step is Pending. Resolve enforces
/// currentCount &lt;= 1 itself; the UI never has to choose.
///
/// Never reveals deduction content: step names and guidance are procedure only.
/// </summary>
public static class EvidenceProcedureMapResolver
{
    public const string SceneStepId = "scene_photography";

    private enum StepKind
    {
        Status,
        Measurement,
        Sketch,
        BloodSample,
        ContainSample,
        DrySample,
        PackageSample,
        SealSample,
        StoreSample,
        StoreBag,
        FingerprintOnScene,
        FingerprintComparison,
        StoreFingerprint,
        LabProcess,
        TraceMarker,
        BodyReport,
        BodyRemoval
    }

    private class StepDef
    {
        public string id;
        public string name;
        public string guidance;
        public StepKind kind;
        public EvidenceStatus status; // for StepKind.Status
    }

    /// <summary>
    /// The map for evidenceId, global Scene Photography box first. Empty when the item is
    /// unknown. Callers decide which items may be shown (see EvidenceProcedureUI).
    /// </summary>
    public static List<ProcedureMapStep> Resolve(string evidenceId)
    {
        var result = new List<ProcedureMapStep>();
        var esm = EvidenceStateManager.Instance;
        var record = esm != null ? esm.GetRecord(evidenceId) : null;
        if (record == null || record.definition == null)
        {
            return result;
        }

        var scene = ScenePhotographyManager.Instance;
        bool hasRoute = scene != null && scene.Points.Count > 0;
        bool sceneDone = !hasRoute || scene.IsScenePhotographyComplete;
        if (hasRoute)
        {
            result.Add(new ProcedureMapStep
            {
                stepId = SceneStepId,
                displayName = "Overall Scene Photography",
                isGlobal = true,
                state = sceneDone ? ProcedureMapStepState.Completed : ProcedureMapStepState.Current,
                guidance = sceneDone ? null : "Complete the overall scene photographs first."
            });
        }

        bool currentAssigned = !sceneDone;
        if (ProcedureInterlock.GloveChangeRequired && ArrivalPPEStation.Instance.CurrentSampleStored)
        {
            result.Add(new ProcedureMapStep { stepId="replace_gloves", displayName="Replace Used Gloves", isGlobal=true,
                state=currentAssigned ? ProcedureMapStepState.Pending : ProcedureMapStepState.Current,
                guidance=ProcedureInterlock.Reason });
            currentAssigned=true;
        }
        else if (ProcedureInterlock.PendingFingerprintItemId != null
            && EvidenceInventory.Instance.GetSampleSource(ProcedureInterlock.PendingFingerprintItemId) != evidenceId)
        {
            result.Add(new ProcedureMapStep { stepId="finish_fingerprint", displayName="Finish Held Fingerprint Card", isGlobal=true,
                state=currentAssigned ? ProcedureMapStepState.Pending : ProcedureMapStepState.Current,
                guidance=ProcedureInterlock.Reason });
            currentAssigned=true;
        }
        foreach (var def in BuildSteps(record))
        {
            var step = new ProcedureMapStep { stepId = def.id, displayName = def.name };
            if (IsComplete(def, record))
            {
                step.state = ProcedureMapStepState.Completed;
            }
            else if (!currentAssigned)
            {
                // The first incomplete step in the intended order is the one priority, even
                // if a later action is also possible (e.g. final photo before measuring).
                step.state = ProcedureMapStepState.Current;
                step.guidance = def.guidance;
                step.subStep = SubStep(def, record);
                if (def.kind == StepKind.Measurement && MeasurementStateManager.Instance != null)
                {
                    // This item's progress (only discovered items' pairs count) and ONE next action.
                    var m = MeasurementStateManager.Instance.GetEvidenceMeasurementStatus(record.definition.evidenceId);
                    step.guidance = $"Fixed references: {m.fixedCompleted}/{m.fixedRequired}   Evidence distances: {m.pairsCompleted}/{m.pairsAvailable}";
                    step.subStep = string.IsNullOrEmpty(m.nextSuggestion) ? null : m.nextSuggestion;
                }
                currentAssigned = true;
            }
            else
            {
                step.state = ProcedureMapStepState.Pending;
            }
            result.Add(step);
        }

        EnforceSingleCurrent(result);
        return result;
    }

    /// <summary>Resolve, packaged with the single Current step, its instruction and whether the item is complete.</summary>
    public static ProcedureMapResult ResolveMap(string evidenceId)
    {
        var map = new ProcedureMapResult { steps = Resolve(evidenceId) };
        bool anyEvidenceStep = false;
        bool allDone = true;
        foreach (var step in map.steps)
        {
            if (step.state == ProcedureMapStepState.Current)
            {
                map.current = step;
            }
            if (!step.isGlobal)
            {
                anyEvidenceStep = true;
            }
            allDone &= step.state == ProcedureMapStepState.Completed;
        }
        map.isComplete = anyEvidenceStep && allDone;
        map.instruction = map.current?.guidance;
        map.subStep = map.current?.subStep;
        return map;
    }

    /// <summary>How many steps are Current (0 or 1 by construction).</summary>
    public static int CurrentCount(List<ProcedureMapStep> steps)
    {
        int count = 0;
        foreach (var step in steps)
        {
            if (step.state == ProcedureMapStepState.Current)
            {
                count++;
            }
        }
        return count;
    }

    // Invariant: at most one Current. Should never trigger; if it ever does, the earliest
    // (highest-priority) step keeps it and the rest become Pending.
    private static void EnforceSingleCurrent(List<ProcedureMapStep> steps)
    {
        bool seen = false;
        foreach (var step in steps)
        {
            if (step.state != ProcedureMapStepState.Current)
            {
                continue;
            }
            if (seen)
            {
                Debug.LogError($"[EvidenceProcedureMapResolver] More than one Current step ({step.stepId}) - demoted to Pending.");
                step.state = ProcedureMapStepState.Pending;
                step.guidance = null;
                step.subStep = null;
            }
            seen = true;
        }
    }

    /// <summary>The index of the Current step, or -1 when everything is complete.</summary>
    public static int FocusIndex(List<ProcedureMapStep> steps)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].state == ProcedureMapStepState.Current)
            {
                return i;
            }
        }
        return -1;
    }

    // --- Which steps apply: derived from the item's own data -------------------

    private static List<StepDef> BuildSteps(EvidenceRecord record)
    {
        var d = record.definition;
        var steps = new List<StepDef>
        {
            new StepDef { id = "initial_photo", name = "Initial Photo", kind = StepKind.Status, status = EvidenceStatus.PhotographedRaw,
                guidance = "Take the original photograph without a ruler. X raises the camera; trigger takes the photo." }
        };

        if (d.requiresChalk)
        {
            steps.Add(new StepDef { id = "chalk", name = "Chalk Position", kind = StepKind.Status, status = EvidenceStatus.ChalkMarked,
                guidance = "Chalk the outline of the evidence's position." });
        }

        bool inPlace = d.examinedInPlace;
        steps.Add(new StepDef { id = "marker", name = d.requiresBodyRemoval ? "Body Marker A" : inPlace ? "Body Marker" : "Evidence Marker", kind = StepKind.Status, status = EvidenceStatus.Marked,
            guidance = d.requiresBodyRemoval ? "Place Body Marker A after completing the chalk outline." : inPlace ? "Place the lettered body marker." : "Place the evidence marker." });

        if (d.requiresTraceMarker)
        {
            // One record, two tents: the trace on the item (blood on the knife blade) gets its own.
            steps.Add(new StepDef { id = "trace_marker", name = "Blood Trace Marker", kind = StepKind.TraceMarker,
                guidance = "Place a second evidence marker on the blood trace on the blade." });
        }

        if (d.requiresBodyRemoval || d.measurementAfterFinalPhoto)
            steps.Add(new StepDef { id = "final_photo", name = "Final Photo", kind = StepKind.Status, status = EvidenceStatus.PhotographedFinal,
                guidance = FinalPhotoGuidance(d) });

        if (FindMeasurementRequirement(d.evidenceId) != null)
        {
            steps.Add(new StepDef { id = "measurement", name = "Measurement", kind = StepKind.Measurement,
                guidance = "Measure the evidence from its two fixed references, then its distances to the other evidence." });
        }

        if (CrimeSceneSketchManager.Instance?.Definition?.FindElement(d.evidenceId) != null)
            steps.Add(new StepDef { id = "sketch", name = "Scene Sketch", kind = StepKind.Sketch,
                guidance = "Hold Y, select Scene Sketch, then place this measured item on the floor plan. Its recorded distances appear automatically." });

        if (d.requiresBodyRemoval)
            steps.Add(new StepDef { id = "record", name = "Record BODY-A Findings", kind = StepKind.BodyReport,
                guidance = "After measuring and documenting the body position, record BODY-A findings with the Recorder." });

        if (!d.requiresBodyRemoval && !d.measurementAfterFinalPhoto) steps.Add(new StepDef { id = "final_photo", name = "Final Photo", kind = StepKind.Status, status = EvidenceStatus.PhotographedFinal,
            guidance = FinalPhotoGuidance(d) });

        if (d.requiresSwabbing)
        {
            steps.Add(new StepDef { id = "blood_sample", name = "Blood Sample", kind = StepKind.BloodSample,
                guidance = d.swabBeforeFingerprinting && d.requiresFingerprinting
                    ? "Collect the blood sample before fingerprint processing."
                    : "Collect the blood sample with a swab." });
            steps.Add(new StepDef { id = "contain_sample", name = "Contain Blood Sample", kind = StepKind.ContainSample,
                guidance = "Touch the carrier with the recovered swab and press trigger to contain it." });
            steps.Add(new StepDef { id = "dry_sample", name = "Dry Blood Sample", kind = StepKind.DrySample,
                guidance = "At the outside cabinet, press A to start drying. Close menus and stay nearby until drying finishes." });
            steps.Add(new StepDef { id = "package_sample", name = "Paper Package", kind = StepKind.PackageSample,
                guidance = "At the drying cabinet, press A to retrieve the dried swab in its individual paper package." });
            steps.Add(new StepDef { id = "seal_sample", name = "Seal Blood Package", kind = StepKind.SealSample,
                guidance = "Seal the prepared paper package with the trigger." });
            steps.Add(new StepDef { id = "store_sample", name = "Store Blood Sample", kind = StepKind.StoreSample,
                guidance = "Press B to store the sealed blood sample before continuing." });
        }

        if (d.requiresFingerprinting && d.fingerprintBeforeCollection)
        {
            steps.Add(new StepDef { id = "fingerprint_process", name = "Fingerprint Process", kind = StepKind.FingerprintOnScene,
                guidance = "Process the fingerprint at the scene." });
            steps.Add(new StepDef { id = "fingerprint_compare", name = "Compare Fingerprint", kind = StepKind.FingerprintComparison,
                guidance = "Carry the held fingerprint card to the examination desk. Press A and confirm the matching reference." });
            steps.Add(new StepDef { id = "store_fingerprint", name = "Store Matched Fingerprint", kind = StepKind.StoreFingerprint,
                guidance = "Close comparison, then press B to store the matched fingerprint card before continuing." });
        }

        if (!d.requiresBodyRemoval)
            steps.Add(new StepDef { id = "record", name = "Record", kind = StepKind.Status, status = EvidenceStatus.Logged,
                guidance = "Complete the evidence record." });
        else
            steps.Add(new StepDef { id = "body_removal", name = "Prepare Body for Removal", kind = StepKind.BodyRemoval,
                guidance = "Approach BODY-A with tools put away. Press A to prepare the body for removal, then confirm." });

        if (!inPlace && !d.collectedBySwab)
        {
            steps.Add(new StepDef { id = "collect", name = "Collect in Bag", kind = StepKind.Status, status = EvidenceStatus.Collected,
                guidance = "Place the evidence in an evidence bag." });
            steps.Add(new StepDef { id = "seal", name = "Seal", kind = StepKind.Status, status = EvidenceStatus.Sealed,
                guidance = "Seal the evidence bag." });
            steps.Add(new StepDef { id = "store_bag", name = "Store Evidence Bag", kind = StepKind.StoreBag,
                guidance = "Press B to store the sealed evidence bag." });

            if (d.requiresFingerprinting && !d.fingerprintBeforeCollection)
            {
                steps.Add(new StepDef { id = "lab_process", name = "Lab Fingerprint Process", kind = StepKind.LabProcess,
                    guidance = "Process the fingerprint at the lab station." });
            }
        }
        return steps;
    }

    private static string FinalPhotoGuidance(EvidenceDefinition definition)
    {
        if (definition.requiresBodyRemoval) return "Photograph the body with Body Marker A in frame.";
        var camera = Object.FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
        return camera?.PhotoScale?.RequiresScale(definition.evidenceId) == true
            ? "Raise camera (X), place ruler (A), frame item, marker and ruler until green, then trigger."
            : "Photograph the evidence with its marker in frame.";
    }

    private static string SampleId(EvidenceRecord record)
    {
        var inventory = EvidenceInventory.Instance;
        if (inventory != null)
            foreach (var id in inventory.AllTrackedEntryIds)
                if (inventory.GetSampleSource(id) == record.definition.evidenceId && !inventory.IsFingerprintSample(id)) return id;
        return null;
    }

    private static bool SampleSealed(EvidenceRecord record) => record.swabSampleSealed
        || (SampleId(record) is string id && EvidenceInventory.Instance.IsSampleSealed(id));

    private static bool PreparationAtLeast(EvidenceRecord record, SwabTool.PreparationStage stage) => SampleSealed(record)
        || (SwabTool.Instance != null && SwabTool.Instance.PreparationSourceId == record.definition.evidenceId
            && SwabTool.Instance.Preparation >= stage);

    private static MeasurementRequirement FindMeasurementRequirement(string evidenceId)
    {
        var set = MeasurementStateManager.Instance != null ? MeasurementStateManager.Instance.RequirementSet : null;
        if (set == null || set.requirements == null)
        {
            return null;
        }
        foreach (var r in set.requirements)
        {
            if (r != null && r.required && r.targetEvidenceId == evidenceId)
            {
                return r;
            }
        }
        return null;
    }

    // --- Completion: authoritative state ----------------------------------------

    private static bool IsComplete(StepDef def, EvidenceRecord record)
    {
        var esm = EvidenceStateManager.Instance;
        switch (def.kind)
        {
            case StepKind.Sketch:
                return CrimeSceneSketchManager.Instance != null && CrimeSceneSketchManager.Instance.IsEvidencePlaced(record.definition.evidenceId);
            case StepKind.BodyReport:
                return record.bodyFindingsRecorded || record.status == EvidenceStatus.Logged;
            case StepKind.BodyRemoval:
                return record.bodyRemovalStatus == BodyRemovalStatus.Removed;
            case StepKind.Status:
                return esm.IsAtOrPastStatus(record.definition.evidenceId, def.status);
            case StepKind.Measurement:
                // Both fixed references plus every pair with an already-discovered item. A
                // later discovery adds a pair and reopens this step (guidance only - an
                // already-taken final photo is never invalidated).
                var req = FindMeasurementRequirement(record.definition.evidenceId);
                return req != null && MeasurementStateManager.Instance.GetEvidenceMeasurementStatus(record.definition.evidenceId).IsComplete;
            case StepKind.BloodSample:
                return record.swabbingDone;
            case StepKind.ContainSample:
                return SampleId(record) != null || PreparationAtLeast(record, SwabTool.PreparationStage.NeedsDrying);
            case StepKind.DrySample:
                return PreparationAtLeast(record, SwabTool.PreparationStage.Dried);
            case StepKind.PackageSample:
                return PreparationAtLeast(record, SwabTool.PreparationStage.Packaged);
            case StepKind.SealSample:
                return SampleSealed(record);
            case StepKind.StoreSample:
                return SampleId(record) is string sample && EvidenceInventory.Instance.IsExplicitlyStoredSample(sample);
            case StepKind.StoreBag:
                return EvidenceInventory.Instance != null && EvidenceInventory.Instance.IsExplicitlyStoredBag(record.definition.evidenceId);
            case StepKind.TraceMarker:
                return record.traceTentNumber.HasValue
                    || esm.IsAtOrPastStatus(record.definition.evidenceId, EvidenceStatus.PhotographedFinal);
            case StepKind.FingerprintOnScene:
            case StepKind.LabProcess:
                return record.fingerprintingDone;
            case StepKind.FingerprintComparison:
                return esm.IsFingerprintMatched(record.definition.evidenceId);
            case StepKind.StoreFingerprint:
                return EvidenceInventory.Instance != null && EvidenceInventory.Instance.IsExplicitlyStoredSample(EvidenceInventory.FingerprintSampleId(record.definition.evidenceId));
            default:
                return false;
        }
    }

    // Finer progress inside a multi-action step (on-scene fingerprinting today).
    private static string SubStep(StepDef def, EvidenceRecord record)
    {
        if (def.kind == StepKind.BloodSample && SwabTool.Instance != null && SwabTool.Instance.HasSample
            && SwabTool.Instance.PendingSampleSourceId == record.definition.evidenceId)
            return "Touch the carrier with the recovered swab and press trigger to contain it.";
        if (def.kind == StepKind.DrySample && SwabTool.Instance?.PreparationSourceId == record.definition.evidenceId
            && SwabTool.Instance.Preparation == SwabTool.PreparationStage.Drying)
            return $"Drying {Mathf.RoundToInt(SwabTool.Instance.DryingProgress * 100)}% — close menus and stay by the cabinet to resume.";
        if (def.kind != StepKind.FingerprintOnScene)
        {
            return null;
        }
        var d = record.definition;
        if (record.fingerprintLifted && !record.fingerprintingDone) return "Carry the held card to the examination desk and compare the fingerprint before storing it.";
        if (d.requiresFlipBeforeFingerprinting && !record.isFlipped)
        {
            return "Turn the item over";
        }
        var print = EvidenceFingerprintMarker.FindByEvidenceId(d.evidenceId);
        bool dusted = print != null && print.IsDusted;
        if (!dusted)
        {
            var kit = FingerprintKitTool.Instance;
            return kit != null && kit.DustLoaded ? "Brush the fingerprint" : "Apply fingerprint powder";
        }
        return "Lift the fingerprint";
    }
}
