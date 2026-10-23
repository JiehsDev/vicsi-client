// Assets/_Project/Scripts/Testing/MeasurementTestUtil.cs
using UnityEngine;

/// <summary>
/// Test helpers for per-evidence measurement references: every item names its own two
/// references on its MeasurementRequirement, so tests look them up instead of hardcoding
/// ids, hook the tape inside each reference's own zone, and record exact distances.
/// </summary>
public static class MeasurementTestUtil
{
    public static bool CompleteSceneSketch()
    {
        if (!AutomatedTestSandbox.IsActive) throw new System.InvalidOperationException("Sketch fixtures require the sandbox.");
        var sketch = CrimeSceneSketchManager.Instance;
        if (sketch == null || sketch.IsSubmitted) return true;
        if (!GloveChangeTestUtil.EnsureClean()) return false;
        if (!ScenePhotographyTestUtil.CompleteRoute() || !CompleteSceneMeasurements()) return false;
        foreach (var element in sketch.Definition.evidenceElements)
            if (!CompleteSketchFor(element.evidenceId)) return false;
        return sketch.Submit();
    }
    /// <summary>Explicit fixture step for the required measurement -> sketch -> processing order.</summary>
    public static bool CompleteSketchFor(string evidenceId)
    {
        if (!AutomatedTestSandbox.IsActive) throw new System.InvalidOperationException("Sketch fixtures require the sandbox.");
        var sketch = CrimeSceneSketchManager.Instance;
        var element = sketch?.Definition?.FindElement(evidenceId);
        if (element == null || sketch.IsEvidencePlaced(evidenceId)) return true;
        PrepareBodyForMeasurement();
        var requirement = RequirementFor(evidenceId);
        var record = MeasurementStateManager.Instance.GetRecord(requirement.requirementId);
        if (!record.hasA || !record.withinToleranceA) RecordExact(evidenceId, 1);
        if (!record.hasB || !record.withinToleranceB) RecordExact(evidenceId, 2);
        RecordAvailablePairsFor(evidenceId);
        return sketch.PlaceEvidence(evidenceId, element.correctAnchorId) == SketchPlacementResult.Placed;
    }
    public static MeasurementRequirement RequirementFor(string evidenceId)
    {
        var set = MeasurementStateManager.Instance != null ? MeasurementStateManager.Instance.RequirementSet : null;
        return set != null ? set.requirements.Find(r => r != null && r.targetEvidenceId == evidenceId) : null;
    }

    /// <summary>The item's reference id for slot 1 (A) or 2 (B).</summary>
    public static string Ref(string evidenceId, int slot)
    {
        var r = RequirementFor(evidenceId);
        return r == null ? null : (slot == 1 ? r.referenceAId : r.referenceBId);
    }

    /// <summary>A point where the tape end counts as held at this reference: its zone centre, or just off its anchor/surface.</summary>
    public static Vector3 HookPoint(string referenceId)
    {
        var reference = SceneReferencePoint.Find(referenceId);
        if (reference == null)
        {
            return Vector3.zero;
        }
        var zone = reference.Zone;
        if (zone != null)
        {
            return zone.Box.transform.TransformPoint(zone.Box.center);
        }
        Vector3 offset = reference.shape == SceneReferencePoint.ReferenceShape.Plane ? reference.SurfaceNormal(true) * 0.1f : new Vector3(0.1f, 0f, -0.1f);
        return new Vector3(reference.AnchorPosition.x, 1.1f, reference.AnchorPosition.z) + offset;
    }

    /// <summary>Records the exact scene distance for slot 1/2 of the item's requirement through the real manager.</summary>
    public static MeasurementRecordResult RecordExact(string evidenceId, int slot)
    {
        var r = RequirementFor(evidenceId);
        var m = MeasurementStateManager.Instance;
        string refId = Ref(evidenceId, slot);
        if (r == null || m == null || !m.TryGetSceneDistance(r.requirementId, refId, out var d))
        {
            return MeasurementRecordResult.UnknownRequirement;
        }
        return m.RecordMeasurement(r.requirementId, refId, d);
    }

    /// <summary>
    /// Brings every scene-roster item to at least PhotographedRaw (completing the scene
    /// photography route first if needed), making its measurements discoverable.
    /// BODY-A additionally needs its final photo before measurements can be recorded.
    /// Items already further along are left alone.
    /// </summary>
    public static bool DiscoverRoster()
    {
        var m = MeasurementStateManager.Instance;
        var esm = EvidenceStateManager.Instance;
        if (m == null || esm == null || m.RequirementSet == null)
        {
            return false;
        }
        bool ok = true;
        foreach (var id in m.RequirementSet.ScenePairRoster())
        {
            if (MeasurementStateManager.IsDiscoveredForMeasurement(id))
            {
                continue;
            }
            ScenePhotographyTestUtil.CompleteRoute();
            if (!esm.IsAtOrPastStatus(id, EvidenceStatus.Found))
            {
                esm.MarkFound(id, ToolType.None);
            }
            esm.MarkPhotographedRaw(id, ToolType.Photographer);
            ok &= MeasurementStateManager.IsDiscoveredForMeasurement(id);
        }
        return ok;
    }

    /// <summary>Records the exact distance for one pair through the real manager (authoritative measurement points).</summary>
    public static EvidencePairRecordResult RecordPairExact(string evidenceX, string evidenceY)
    {
        var m = MeasurementStateManager.Instance;
        if (m == null || !m.TryGetPairSceneDistance(evidenceX, evidenceY, out var d))
        {
            return EvidencePairRecordResult.NoMeasurementPoint;
        }
        return m.RecordPairMeasurement(evidenceX, evidenceY, d);
    }

    /// <summary>
    /// Records the exact distance for every available, unmeasured pair involving evidenceId
    /// (pairs with already-discovered, still-measurable items) - the "evidence distances"
    /// part of measuring an item for its evidence record.
    /// </summary>
    public static void RecordAvailablePairsFor(string evidenceId)
    {
        var m = MeasurementStateManager.Instance;
        if (m == null) return;
        foreach (var pair in m.GetAvailablePairsFor(evidenceId))
        {
            if (!pair.isComplete) RecordPairExact(pair.EvidenceA, pair.EvidenceB);
        }
    }

    /// <summary>
    /// Completes what the Recorder waits for, without discovering anything: both fixed
    /// references of every discovered roster item and every pair among discovered items.
    /// Returns whether discovered scene measurements are now complete.
    /// </summary>
    public static bool CompleteDiscoveredMeasurements()
    {
        var m = MeasurementStateManager.Instance;
        if (m == null || m.RequirementSet == null)
        {
            return false;
        }
        PrepareBodyForMeasurement();
        foreach (var r in m.RequirementSet.requirements)
        {
            if (r == null || !r.required || !MeasurementStateManager.IsDiscoveredForMeasurement(r.targetEvidenceId)) continue;
            var record = m.GetRecord(r.requirementId);
            if (record == null) continue;
            if (!(record.hasA && record.withinToleranceA)) RecordExact(r.targetEvidenceId, 1);
            if (!(record.hasB && record.withinToleranceB)) RecordExact(r.targetEvidenceId, 2);
        }
        foreach (var pair in m.GetAvailablePairs())
        {
            if (!pair.isComplete) RecordPairExact(pair.EvidenceA, pair.EvidenceB);
        }
        return m.AreDiscoveredSceneMeasurementsComplete;
    }

    /// <summary>
    /// Completes every scene measurement of the FULL roster: discovers the roster,
    /// records each item's missing fixed references exactly, then every unmeasured pair.
    /// Returns whether scene measurements are now complete.
    /// </summary>
    public static bool CompleteSceneMeasurements()
    {
        var m = MeasurementStateManager.Instance;
        if (m == null || !DiscoverRoster())
        {
            return false;
        }
        PrepareBodyForMeasurement();
        foreach (var r in m.RequirementSet.requirements)
        {
            if (r == null || !r.required) continue;
            var record = m.GetRecord(r.requirementId);
            if (record == null) continue;
            if (!(record.hasA && record.withinToleranceA)) RecordExact(r.targetEvidenceId, 1);
            if (!(record.hasB && record.withinToleranceB)) RecordExact(r.targetEvidenceId, 2);
        }
        foreach (var pair in m.AllPairs)
        {
            if (!pair.isComplete) RecordPairExact(pair.EvidenceA, pair.EvidenceB);
        }
        return m.AreSceneMeasurementsComplete;
    }

    /// <summary>Measurement fixtures honor the body-only final-photo prerequisite.</summary>
    public static void PrepareBodyForMeasurement()
    {
        var esm = EvidenceStateManager.Instance;
        if (esm == null) return;
        var bottle=esm.GetRecord("EVD-018");
        if (bottle?.definition?.measurementAfterFinalPhoto == true && MeasurementStateManager.IsDiscoveredForMeasurement("EVD-018"))
        {
            if (bottle.status==EvidenceStatus.Found) esm.MarkPhotographedRaw("EVD-018",ToolType.Photographer);
            if (bottle.status==EvidenceStatus.PhotographedRaw) esm.MarkChalkMarked("EVD-018",ToolType.ChalkMark);
            if (bottle.status==EvidenceStatus.ChalkMarked) esm.MarkTented("EVD-018",ToolType.EvidenceMarker,18);
            if (bottle.status==EvidenceStatus.Marked) esm.MarkPhotographedFinal("EVD-018",ToolType.Photographer);
        }
        if (!MeasurementStateManager.IsDiscoveredForMeasurement("BODY-A")) return;
        var body = esm.GetRecord("BODY-A");
        if (body?.definition?.requiresBodyRemoval != true) return;
        if (body.status == EvidenceStatus.PhotographedRaw) esm.MarkChalkMarked("BODY-A", ToolType.ChalkMark);
        if (body.status == EvidenceStatus.ChalkMarked) esm.MarkTentedWithLetter("BODY-A", ToolType.LetterTentMarker, "A");
        if (body.status == EvidenceStatus.Marked) esm.MarkPhotographedFinal("BODY-A", ToolType.Photographer);
    }
}
