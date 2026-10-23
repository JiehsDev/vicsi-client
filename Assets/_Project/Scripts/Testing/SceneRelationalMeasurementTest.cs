// Assets/_Project/Scripts/Testing/SceneRelationalMeasurementTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Evidence-to-evidence ("relational") scene measurement: roster-generated unique pairs,
/// discovery-safe availability, normalised pair identity (measured once, either
/// direction), the Measurement Tool's Evidence Distance mode, scene progress, the
/// Procedure Map suggestion, late pairs, the Recorder gate (SceneMeasurementsComplete),
/// the Recorder's spatial record, the upload section and reset. Fresh play session.
/// </summary>
public class SceneRelationalMeasurementTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();
    private MeasurementTool tool;
    private int nextTent = 1;

    private const string Body = "BODY-A";
    private const string Knife = "EVD-014";
    private const string Spatter = "EVD-015";
    private const string Bottle = "EVD-018";
    private static string Name(string id) => EvidenceStateManager.Instance.GetDisplayName(id);
    private const string GateMessage = "Complete the required scene measurements before the evidence record so it includes the original layout and spatial relationships.";

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "  PASS: " : "  FAIL: ") + label);
        if (!ok) failures.Add(label);
    }

    private static int Count(SessionEventType type)
    {
        var logger = SessionLogger.Instance;
        return logger != null && logger.EventCounts.TryGetValue(type, out int n) ? n : 0;
    }

    private IEnumerator Run()
    {
        report.AppendLine("=== SceneRelationalMeasurementTest ===");
        var m = MeasurementStateManager.Instance;
        var esm = EvidenceStateManager.Instance;
        var gate = ProceduralGateValidator.Instance;
        var rig = FindFirstObjectByType<OVRCameraRig>();
        if (m == null || esm == null || gate == null || rig == null || HeldItemCoordinator.Instance == null)
        {
            Report = "FAIL: managers/rig missing.";
            IsDone = true;
            yield break;
        }
        yield return null;

        // --- Pair generation (from the roster, never hard-coded) ---
        var roster = m.RequirementSet.ScenePairRoster();
        int n = roster.Count;
        Check($"[config] roster is data-driven: {string.Join(", ", roster)}", n == 4 && roster.Contains(Body) && roster.Contains(Knife) && roster.Contains(Spatter) && roster.Contains(Bottle));
        var keys = new HashSet<string>();
        bool normalised = true, noSelf = true;
        foreach (var p in m.AllPairs)
        {
            keys.Add(p.PairKey);
            normalised &= EvidencePairKey.Of(p.EvidenceA, p.EvidenceB).Key == EvidencePairKey.Of(p.EvidenceB, p.EvidenceA).Key && p.PairKey == EvidencePairKey.Of(p.EvidenceB, p.EvidenceA).Key;
            noSelf &= p.EvidenceA != p.EvidenceB;
        }
        Check($"[pairs] {n} items -> n(n-1)/2 = {n * (n - 1) / 2} unique pairs ({m.AllPairs.Count})", m.AllPairs.Count == n * (n - 1) / 2 && keys.Count == m.AllPairs.Count);
        Check("[pairs] keys are normalised (A|B == B|A) and no item is paired with itself", normalised && noSelf);
        Check("[pairs] BODY-A + EVD-014 and EVD-014 + BODY-A resolve to the same pair", EvidencePairKey.Of(Body, Knife).Equals(EvidencePairKey.Of(Knife, Body)) && m.GetPair(Knife, Body) == m.GetPair(Body, Knife) && m.GetPair(Body, Knife) != null);
        var full = m.GetSceneProgress(false);
        Check($"[progress] internal requirement = fixed {full.fixedRequired} + pairs {full.pairRequired} = {full.Required} (2n + n(n-1)/2)", full.fixedRequired == 2 * n && full.pairRequired == n * (n - 1) / 2 && full.Completed == 0);
        Check("[progress] current scenario totals 14 (8 fixed + 6 pairs)", full.Required == 14);

        // --- Discovery visibility ---
        Check("[leak] nothing discovered -> no pair available, player-facing progress empty", m.GetAvailablePairs().Count == 0 && m.GetSceneProgress(true).Required == 0);
        Check("[setup] scene photography route completed", ScenePhotographyTestUtil.CompleteRoute());
        foreach (var id in new[] { Knife, Body, Spatter, Bottle }) esm.MarkFound(id, ToolType.None);
        yield return null;
        Check("[leak] Found by proximity alone makes no pair available", m.GetAvailablePairs().Count == 0);
        esm.MarkPhotographedRaw(Knife, ToolType.Photographer);
        esm.MarkPhotographedRaw(Body, ToolType.Photographer);
        var avail = m.GetAvailablePairs();
        Check($"[visibility] knife + body discovered -> only Knife-Body available ({avail.Count})", avail.Count == 1 && avail[0].pair.Contains(Knife) && avail[0].pair.Contains(Body));
        var visible = m.GetSceneProgress(true);
        Check($"[visibility] player-facing progress counts only discovered items: fixed {visible.fixedCompleted}/{visible.fixedRequired}, pairs {visible.pairCompleted}/{visible.pairRequired}",
            visible.fixedRequired == 4 && visible.pairRequired == 1 && visible.Completed == 0);
        var bodyStatus = m.GetEvidenceMeasurementStatus(Body);
        Check($"[suggest] body's first suggestion is a fixed reference: '{bodyStatus.nextSuggestion}'", bodyStatus.nextSuggestion.StartsWith($"Measure {Name(Body)}") && bodyStatus.nextSuggestion.Contains(SceneReferencePoint.Find(MeasurementTestUtil.Ref(Body,1)).displayName));

        // --- Tool: Evidence Distance mode ---
        AdvanceToMarked(Knife);
        MeasurementTestUtil.RecordExact(Knife, 1);
        MeasurementTestUtil.RecordExact(Knife, 2);
        yield return DismissCheckpoints();
        HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Measurement, rig.rightHandAnchor);
        tool = MeasurementTool.Instance;
        yield return null;
        Check("[tool] measurement tool equipped", tool != null && tool.IsVirtuallyEquipped);
        AimAt(Knife);
        tool.HandleTrigger();
        Check($"[tool] knife's fixed references done -> Evidence Distance mode ('{tool.LastMessage}')",
            tool.CurrentMode == MeasurementTool.Mode.EvidenceDistance && tool.CurrentPhase == MeasurementTool.Phase.PairAtSource
            && tool.PairSourceEvidenceId == Knife && tool.LastMessage == $"Evidence distance: hold the tape at the {Name(Knife)}.");
        PlaceAt(Knife);
        tool.HandleTrigger();
        Check("[tool] tape held at the knife -> extending", tool.CurrentPhase == MeasurementTool.Phase.PairExtending);

        int measuredEvents = Count(SessionEventType.EvidencePairMeasured);
        int rejectedEvents = Count(SessionEventType.EvidencePairMeasurementRejected);
        PlaceAt(Bottle);
        tool.HandleTrigger();
        Check($"[tool wrong] undiscovered bottle at the tape end reads as empty floor ('{tool.LastMessage}'), nothing recorded or logged",
            tool.LastMessage == "Select another discovered evidence item." && !m.GetPair(Knife, Bottle).isComplete
            && Count(SessionEventType.EvidencePairMeasurementRejected) == rejectedEvents && tool.CurrentPhase == MeasurementTool.Phase.PairExtending);
        PlaceAt(Knife);
        tool.HandleTrigger();
        Check("[tool wrong] knife to itself refused (logged as SameEvidence)",
            tool.LastMessage == "Select another discovered evidence item." && Count(SessionEventType.EvidencePairMeasurementRejected) == rejectedEvents + 1);
        PlaceAt(Body);
        tool.HandleTrigger();
        Check($"[tool wrong] body before final photograph -> '{tool.LastMessage}'", tool.LastMessage == "Complete the final body photograph before recording body measurements." && !m.GetPair(Knife, Body).isComplete);
        AdvanceToMarked(Body);
        esm.MarkPhotographedFinal(Body, ToolType.Photographer);
        tool.HandleTrigger();
        var kb = m.GetPair(Knife, Body);
        m.TryGetPairSceneDistance(Knife, Body, out float kbTrue);
        Check($"[tool correct] Knife -> Body recorded once between the measurement points ('{tool.LastMessage}')",
            kb.isComplete && Mathf.Abs(kb.measuredDistance - kbTrue) < 1e-4f && Count(SessionEventType.EvidencePairMeasured) == measuredEvents + 1
            && tool.LastMessage == $"{Name(Knife)} – {Name(Body)}: {kbTrue:0.00} m. Evidence distances complete.");
        Check("[tool] no pair left for the knife -> back to selecting", tool.CurrentPhase == MeasurementTool.Phase.SelectTarget);
        AimAt(Knife);
        tool.HandleTrigger();
        Check($"[tool] knife fully measured -> '{tool.LastMessage}'", tool.LastMessage == "Measurement complete.");

        // --- Reverse direction is the same pair ---
        var reverse = m.RecordPairMeasurement(Body, Knife, kbTrue);
        Check($"[dedupe] Body -> Knife afterwards -> {reverse}; still one record, no second measurement event",
            reverse == EvidencePairRecordResult.AlreadyComplete && m.AllPairs.Count == 6 && Count(SessionEventType.EvidencePairMeasured) == measuredEvents + 1);
        Check("[wrong] manager refuses a pair with an undiscovered item without logging it",
            m.RecordPairMeasurement(Knife, Bottle, 1f) == EvidencePairRecordResult.NotDiscovered && Count(SessionEventType.EvidencePairMeasurementRejected) == rejectedEvents + 2);

        // --- Scene progress ---
        visible = m.GetSceneProgress(true);
        full = m.GetSceneProgress(false);
        Check($"[progress] visible: fixed {visible.fixedCompleted}/{visible.fixedRequired}, pairs {visible.pairCompleted}/{visible.pairRequired}, total {visible.Completed}/{visible.Required}",
            visible.fixedCompleted == 2 && visible.fixedRequired == 4 && visible.pairCompleted == 1 && visible.pairRequired == 1 && visible.Completed == 3 && visible.Required == 5);
        Check($"[progress] internal: {full.Completed}/{full.Required} (fixed {full.fixedCompleted}/{full.fixedRequired}, pairs {full.pairCompleted}/{full.pairRequired})",
            full.fixedCompleted == 2 && full.pairCompleted == 1 && full.Required == 14 && !m.AreSceneMeasurementsComplete);

        // --- Recorder gate: discovery-safe (only discovered evidence counts) ---
        AdvanceToMarked(Body);
        Check($"[recorder] body's own fixed references missing -> refused: '{gate.GetBlockReason(Body, EvidenceStatus.Logged)}'",
            !gate.CanTransition(Body, EvidenceStatus.Logged) && gate.GetBlockReason(Body, EvidenceStatus.Logged) == "Measure/document the body position and required evidence distances before recording BODY-A findings.");
        var recorder = FindFirstObjectByType<RecorderTool>(FindObjectsInactive.Include);
        if (recorder != null)
        {
            Check("[recorder] the Recorder refuses to open the report", !recorder.TryOpenReport(EvidenceProp.FindById(Body)) && !esm.IsAtOrPastStatus(Body, EvidenceStatus.Logged));
        }
        MeasurementTestUtil.RecordExact(Body, 1);
        MeasurementTestUtil.RecordExact(Body, 2);
        int outOfOrder = Count(SessionEventType.ProcedureOutOfOrder);
        Check("[final photo] body's photograph remains complete after measurements: no warning", esm.IsAtOrPastStatus(Body, EvidenceStatus.PhotographedFinal) && Count(SessionEventType.ProcedureOutOfOrder) == outOfOrder);
        visible = m.GetSceneProgress(true);
        Check("[recorder] measured body placed on sketch before recording",MeasurementTestUtil.CompleteSketchFor(Body));
        Check("[recorder CRITICAL] spatter + bottle undiscovered: Recorder allowed, no hidden-evidence refusal (internal set still incomplete)",
            gate.CanTransition(Body, EvidenceStatus.Logged) && gate.GetBlockReason(Body, EvidenceStatus.Logged) == null
            && m.AreDiscoveredSceneMeasurementsComplete && !m.AreSceneMeasurementsComplete);
        Check($"[leak] player-facing progress {visible.Completed}/{visible.Required} - no hidden total", visible.Completed == 5 && visible.Required == 5);
        esm.RecordReportLinesSelected(Body, new[] { "body-a-location", "body-a-position", "body-a-wound-sharp-object" });
        Check("[recorder] body record finalized", esm.MarkLogged(Body, ToolType.Recorder) == TransitionResult.Applied && esm.IsAtOrPastStatus(Body, EvidenceStatus.Logged));
        var bodyRecord = esm.GetRecord(Body);
        var bodyStatusAtLog = bodyRecord.status;
        float bodyChangedAt = bodyRecord.statusChangedAtTime;
        string noteBefore = EvidenceNoteComposer.Compose(bodyRecord);
        Check("[record] body's note shows its spatial measurements: knife distance only",
            noteBefore.Contains("Spatial measurements") && noteBefore.Contains($"{Name(Knife)} \u2014 {kb.DisplayDistance}") && !noteBefore.Contains($"{Name(Spatter)} \u2014"));

        // --- Later discovery: spatter adds pairs; the finalized record stays finalized ---
        esm.MarkPhotographedRaw(Spatter, ToolType.Photographer);
        avail = m.GetAvailablePairs();
        Check($"[later] spatter discovered -> Body-Spatter and Knife-Spatter appear ({avail.Count} available)",
            avail.Count == 3 && avail.Contains(m.GetPair(Body, Spatter)) && avail.Contains(m.GetPair(Knife, Spatter)));
        Check("[later] the body's record is not revoked or reopened", bodyRecord.status == bodyStatusAtLog && esm.IsAtOrPastStatus(Body, EvidenceStatus.Logged));
        visible = m.GetSceneProgress(true);
        Check($"[leak] player-facing progress {visible.Completed}/{visible.Required} (three discovered items) - never the 14-total", visible.Required == 9);
        bodyStatus = m.GetEvidenceMeasurementStatus(Body);
        Check($"[suggest] body: fixed {bodyStatus.fixedCompleted}/2, distances {bodyStatus.pairsCompleted}/{bodyStatus.pairsAvailable}; next '{bodyStatus.nextSuggestion}'",
            bodyStatus.fixedCompleted == 2 && bodyStatus.pairsCompleted == 1 && bodyStatus.pairsAvailable == 2
            && bodyStatus.nextSuggestion == $"Measure {Name(Body)} to {Name(Spatter)}.");
        Check("[leak] suggestions never name the undiscovered bottle", !bodyStatus.nextSuggestion.Contains("bottle") && !m.GetEvidenceMeasurementStatus(Spatter).nextSuggestion.Contains("bottle"));

        // --- Final photo: only currently discovered pairs; a later discovery never invalidates one ---
        AdvanceToMarked(Spatter);
        MeasurementTestUtil.RecordExact(Spatter, 1);
        MeasurementTestUtil.RecordExact(Spatter, 2);
        outOfOrder = Count(SessionEventType.ProcedureOutOfOrder);
        esm.MarkPhotographedFinal(Spatter, ToolType.Photographer);
        Check("[final photo] spatter's final photo with its two pairs missing: allowed, warned once (unscored step)",
            esm.IsAtOrPastStatus(Spatter, EvidenceStatus.PhotographedFinal) && Count(SessionEventType.ProcedureOutOfOrder) == outOfOrder + 1);
        Check("[final photo] the body's earlier final photo is untouched by the new pairs", esm.IsAtOrPastStatus(Body, EvidenceStatus.PhotographedFinal));

        // --- Late pairs: spatter sampling disturbs the stain; the knife's blade swab doesn't move the knife ---
        // Fixture for an already sampled surface; live sampling requires measurement and sketch first.
        esm.GetRecord(Spatter).swabbingDone = true;
        Check("[late] body-spatter measured after the spatter was sampled -> recorded, flagged late",
            MeasurementTestUtil.RecordPairExact(Body, Spatter) == EvidencePairRecordResult.Recorded && m.GetPair(Body, Spatter).takenAfterDisturbance && m.GetPair(Body, Spatter).isComplete);
        Check("[late] knife-spatter likewise late", MeasurementTestUtil.RecordPairExact(Knife, Spatter) == EvidencePairRecordResult.Recorded && m.GetPair(Knife, Spatter).takenAfterDisturbance);

        // --- The old record gains the new relationship without being re-recorded ---
        string noteAfter = EvidenceNoteComposer.Compose(bodyRecord);
        Check($"[record] body's note now also lists the spatter distance ({m.GetPair(Body, Spatter).DisplayDistance}), from live data",
            noteAfter.Contains($"{Name(Spatter)} \u2014 {m.GetPair(Body, Spatter).DisplayDistance}") && noteAfter.Contains($"{Name(Knife)} \u2014 {kb.DisplayDistance}"));
        Check("[record] no new Record action: the body's status and its last transition time are unchanged",
            bodyRecord.status == bodyStatusAtLog && Mathf.Approximately(bodyRecord.statusChangedAtTime, bodyChangedAt));

        AdvanceToMarked(Bottle);
        Check("[setup] bottle final photograph precedes its measurements", esm.MarkPhotographedFinal(Bottle, ToolType.Photographer)==TransitionResult.Applied);
        TraceMarkerTestUtil.MarkTraceIfRequired(Knife);
        esm.MarkPhotographedFinal(Knife, ToolType.Photographer);
        // Fixture for an already sampled surface; live sampling requires measurement and sketch first.
        esm.GetRecord(Knife).swabbingDone = true;
        Check("[late] knife-bottle after the knife's blade swab -> not late",
            MeasurementTestUtil.RecordPairExact(Knife, Bottle) == EvidencePairRecordResult.Recorded && !m.GetPair(Knife, Bottle).takenAfterDisturbance);
        Check("[late] knife's own fixed record unaffected by its swab", !m.IsDisturbed(MeasurementTestUtil.RequirementFor(Knife)));

        // --- Full current scene: 8 fixed + 6 pairs ---
        Check("[full] every fixed reference and pair completed", MeasurementTestUtil.CompleteSceneMeasurements());
        full = m.GetSceneProgress(false);
        Check($"[full] Scene Measurements: {full.Completed} / {full.Required} (fixed {full.fixedCompleted}/{full.fixedRequired}, distances {full.pairCompleted}/{full.pairRequired})",
            full.Completed == 14 && full.Required == 14 && full.fixedCompleted == 8 && full.pairCompleted == 6 && m.AreSceneMeasurementsComplete && m.AreDiscoveredSceneMeasurementsComplete);
        Check("[full] with all four discovered, player-facing progress equals the full set", m.GetSceneProgress(true).Completed == 14 && m.GetSceneProgress(true).Required == 14);
        Check("[full] the measurement gate no longer stands in front of any remaining record",
            esm.GetItemBlockReason(Knife, EvidenceStatus.Logged) != GateMessage && esm.GetItemBlockReason(Spatter, EvidenceStatus.Logged) != GateMessage);

        // --- Recorder spatial record (recorded values, tape precision, no reverse duplicates) ---
        var lines = m.GetSpatialSummary(Knife);
        var knifeRecord = m.GetRecord(MeasurementTestUtil.RequirementFor(Knife).requirementId);
        int pairLines = 0;
        bool valuesMatch = true;
        var seenOthers = new HashSet<string>();
        foreach (var l in lines)
        {
            if (!l.isPair) continue;
            pairLines++;
            var pr = m.GetPair(Knife, IdFor(l.label));
            valuesMatch &= pr != null && l.DisplayDistance == pr.DisplayDistance && seenOthers.Add(l.label);
        }
        string text = m.FormatSpatialSummary(Knife);
        Check($"[record] knife: 2 fixed references + {pairLines} evidence distances, each once, values identical to the records",
            lines.Count == 5 && pairLines == 3 && valuesMatch);
        Check("[record] layout: Fixed References / Evidence Distances, name \u2014 0.00 m",
            text.Contains("<b>Fixed References</b>") && text.Contains("<b>Evidence Distances</b>")
            && text.Contains($"{SceneReferencePoint.Find(MeasurementTestUtil.Ref(Knife,2)).displayName} \u2014 {knifeRecord.measuredB:0.00} m") && text.Contains($"{SceneReferencePoint.Find(MeasurementTestUtil.Ref(Knife,1)).displayName} \u2014 {knifeRecord.measuredA:0.00} m"));
        Check("[record] evidence distances listed by display name, never ids",
            text.Contains($"{Name(Body)} \u2014") && text.Contains($"{Name(Spatter)} \u2014") && text.Contains($"{Name(Bottle)} \u2014") && !text.Contains("EVD-") && !text.Contains("BODY-A"));
        bool everyItem = true;
        foreach (var id in new[] { Body, Knife, Spatter, Bottle })
        {
            int fx = 0, px = 0;
            foreach (var l in m.GetSpatialSummary(id)) { if (l.isPair) px++; else fx++; }
            everyItem &= fx == 2 && px == 3;
        }
        Check("[record] every item's spatial block: its 2 fixed references + 3 evidence distances", everyItem);

        // --- Upload section: additive, fixed scoring unchanged ---
        var assessment = m.BuildAssessment();
        Check($"[payload] pairs exposed ({assessment.pairs.Count}, complete {assessment.pairCompletedCount}, late {assessment.pairLateCount}) with keys, both ids, distances",
            assessment.pairs.Count == 6 && assessment.pairCompletedCount == 6 && assessment.pairRequiredCount == 6 && assessment.sceneMeasurementsComplete
            && assessment.pairs.TrueForAll(p => !string.IsNullOrEmpty(p.pairKey) && p.measuredDistance >= 0f) && assessment.pairLateCount >= 1);
        Check("[payload] fixed-reference aggregate unchanged (4 items, score from fixed references only)", assessment.requirementCount == 4 && assessment.items.Count == 4);

        // --- Sketch-ready data ---
        var sketchPair = m.GetPair(Spatter, Bottle);
        Check("[sketch] pair exposes ids, key, recorded + display distance, completion",
            sketchPair != null && sketchPair.isComplete && sketchPair.PairKey == "EVD-015|EVD-018" && sketchPair.DisplayDistance == $"{sketchPair.measuredDistance:0.00} m");

        // --- Reset ---
        m.ResetSession();
        full = m.GetSceneProgress(false);
        bool anyPair = false;
        foreach (var p in m.AllPairs) anyPair |= p.isComplete || p.order != 0;
        Check($"[reset] fixed and pair measurements cleared ({full.Completed}/{full.Required})", full.Completed == 0 && full.Required == 14 && !anyPair && m.AllPairs.Count == 6);

        HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Measurement, rig.rightHandAnchor);
        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    // Hypothesis checkpoints are forced screens that can open as evidence progresses and
    // hold the UI gate (no tool can be equipped); answer them like the other tests do.
    private static IEnumerator DismissCheckpoints()
    {
        yield return null;
        for (int i = 0; i < 5; i++)
        {
            var checkpoint = Object.FindFirstObjectByType<HypothesisCheckpointUI>();
            if (checkpoint == null || !checkpoint.IsShowing)
            {
                yield break;
            }
            checkpoint.AnswerForTest(0, 0);
            yield return null;
        }
    }

    private static string IdFor(string displayName)
    {
        foreach (var id in new[] { Body, Knife, Spatter, Bottle })
        {
            if (MeasurementStateManager.DisplayName(id) == displayName) return id;
        }
        return null;
    }

    private void AdvanceToMarked(string id)
    {
        var esm = EvidenceStateManager.Instance;
        if (!esm.IsAtOrPastStatus(id, EvidenceStatus.Found)) esm.MarkFound(id, ToolType.None);
        if (!esm.IsAtOrPastStatus(id, EvidenceStatus.PhotographedRaw)) esm.MarkPhotographedRaw(id, ToolType.Photographer);
        var def = esm.GetRecord(id).definition;
        if (def.requiresChalk && !esm.IsAtOrPastStatus(id, EvidenceStatus.ChalkMarked)) esm.MarkChalkMarked(id, ToolType.ChalkMark);
        if (!esm.IsAtOrPastStatus(id, EvidenceStatus.Marked))
        {
            if (def.examinedInPlace) esm.MarkTentedWithLetter(id, ToolType.LetterTentMarker, "A");
            else esm.MarkTented(id, ToolType.EvidenceMarker, nextTent++);
        }
    }

    private void PlaceTip(Vector3 tipWorld, Vector3 lookAt)
    {
        var t = tool.transform;
        t.rotation = Quaternion.LookRotation(lookAt - tipWorld);
        t.position += tipWorld - tool.TapeTip.position;
    }

    private void AimAt(string evidenceId)
    {
        MeasurementGeometry.TryGetEvidenceAnchor(evidenceId, out var anchor);
        PlaceTip(anchor + Vector3.up * 1.0f + Vector3.forward * 0.05f, anchor);
    }

    // Tape end held right over the item's measurement point.
    private void PlaceAt(string evidenceId)
    {
        MeasurementGeometry.TryGetEvidenceAnchor(evidenceId, out var anchor);
        Vector3 tip = new Vector3(anchor.x, anchor.y + 0.05f, anchor.z);
        PlaceTip(tip, tip + Vector3.down);
    }
}
