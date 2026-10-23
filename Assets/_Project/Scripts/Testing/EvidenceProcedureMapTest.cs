// Assets/_Project/Scripts/Testing/EvidenceProcedureMapTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Play-mode checks for EvidenceProcedureMapResolver / EvidenceProcedureUI, driven through
/// the real lifecycle APIs the tools call. Must run in a fresh play session (it checks the
/// clean start state and the before-scene-photography state first).
///
/// Core rule under test: exactly ONE step is Current until an item's procedure is
/// complete, then none - always the first incomplete step in the intended order.
/// </summary>
public class EvidenceProcedureMapTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private readonly List<string> failures = new();
    private readonly StringBuilder report = new();

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private void Check(string label, bool ok)
    {
        report.AppendLine((ok ? "  PASS: " : "  FAIL: ") + label);
        if (!ok)
        {
            failures.Add(label);
        }
    }

    private static string Ids(List<ProcedureMapStep> steps)
    {
        var parts = new List<string>();
        foreach (var s in steps)
        {
            parts.Add(s.stepId);
        }
        return string.Join(",", parts);
    }

    private static ProcedureMapStep Step(string evidenceId, string stepId)
    {
        foreach (var s in EvidenceProcedureMapResolver.Resolve(evidenceId))
        {
            if (s.stepId == stepId)
            {
                return s;
            }
        }
        return null;
    }

    // Exactly one Current step, and it is the expected one (also checks the single-current invariant).
    private void ExpectCurrent(string evidenceId, string expected, string after)
    {
        var map = EvidenceProcedureMapResolver.ResolveMap(evidenceId);
        int count = EvidenceProcedureMapResolver.CurrentCount(map.steps);
        string got = map.current != null ? map.current.stepId : "none";
        Check($"[{evidenceId}] after {after}: exactly one Current = {expected} (count {count}, got {got})",
            count == 1 && got == expected && !map.isComplete && !string.IsNullOrEmpty(map.instruction));
    }

    private void ExpectComplete(string evidenceId, string after)
    {
        var map = EvidenceProcedureMapResolver.ResolveMap(evidenceId);
        bool allDone = true;
        foreach (var s in map.steps)
        {
            allDone &= s.state == ProcedureMapStepState.Completed;
        }
        Check($"[{evidenceId}] after {after}: procedure complete - 0 Current, every step Completed",
            map.isComplete && map.current == null && EvidenceProcedureMapResolver.CurrentCount(map.steps) == 0 && allDone);
    }

    private IEnumerator Run()
    {
        report.AppendLine("=== EvidenceProcedureMapTest ===");
        var esm = EvidenceStateManager.Instance;
        var scene = ScenePhotographyManager.Instance;
        var ui = EvidenceProcedureUI.Instance;
        if (esm == null || scene == null || ui == null || MeasurementStateManager.Instance == null)
        {
            Report = "FAIL: required managers missing.";
            IsDone = true;
            yield break;
        }

        // --- Clean session start ---
        bool clean = true;
        foreach (var id in new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" })
        {
            foreach (var s in EvidenceProcedureMapResolver.Resolve(id))
            {
                clean &= s.isGlobal || s.state != ProcedureMapStepState.Completed;
            }
        }
        Check("[reset] fresh session: no evidence step completed on any item", clean && !scene.IsScenePhotographyComplete);

        // --- Which boxes appear ---
        Check("[BODY-A] map = scene, initial photo, chalk, Marker A, final photo, measurement, findings record, removal",
            Ids(EvidenceProcedureMapResolver.Resolve("BODY-A")) == "scene_photography,initial_photo,chalk,marker,final_photo,measurement,sketch,record,body_removal"
            && Step("BODY-A", "marker").displayName == "Body Marker A");
        Check("[EVD-014 knife] map = scene, photo, chalk, marker, blood trace marker, measurement, final photo, blood sample, fingerprint, record, collect, seal",
            Ids(EvidenceProcedureMapResolver.Resolve("EVD-014")) == "scene_photography,initial_photo,chalk,marker,trace_marker,measurement,sketch,final_photo,blood_sample,contain_sample,dry_sample,package_sample,seal_sample,store_sample,fingerprint_process,fingerprint_compare,store_fingerprint,record,collect,seal,store_bag");
        Check("[EVD-015 spatter] map = scene, photo, chalk, marker, measurement, final photo, blood sample, seal sample, record",
            Ids(EvidenceProcedureMapResolver.Resolve("EVD-015")) == "scene_photography,initial_photo,chalk,marker,measurement,sketch,final_photo,blood_sample,contain_sample,dry_sample,package_sample,seal_sample,store_sample,record");
        Check("[EVD-018 bottle] map = final photo, measurement, on-scene fingerprint, record, collect, seal",
            Ids(EvidenceProcedureMapResolver.Resolve("EVD-018")) == "scene_photography,initial_photo,chalk,marker,final_photo,measurement,sketch,fingerprint_process,fingerprint_compare,store_fingerprint,record,collect,seal,store_bag");

        // --- Before scene photography: the global box is the ONLY Current; everything else Pending ---
        foreach (var id in new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" })
        {
            esm.MarkFound(id, ToolType.None);
        }
        yield return DismissCheckpoints();
        ExpectCurrent("EVD-014", EvidenceProcedureMapResolver.SceneStepId, "discovery, scene photography not done");
        // Only the knife is photographed now; each other item is photographed when its own
        // section starts. An evidence record waits for every discovered item's measurements,
        // so photographing everything up front would measure every item at the knife's record.
        esm.MarkPhotographedRaw("EVD-014", ToolType.Photographer); // allowed early (Warn)
        ExpectCurrent("EVD-014", EvidenceProcedureMapResolver.SceneStepId, "initial photo taken before scene photography");
        Check("[EVD-014] chalk (refused until scene photos) is just Pending - never a second highlighted box",
            Step("EVD-014", "chalk").state == ProcedureMapStepState.Pending && Step("EVD-014", "initial_photo").state == ProcedureMapStepState.Completed);

        // --- UI: only photographed items, live refresh, locks ---
        ui.ShowFor("EVD-014");
        Check("[panel] opens on the knife with UI gate, locomotion and tool lock held",
            ui.IsShowing && ui.FocusedEvidenceId == "EVD-014" && PlayerUIGate.IsBlocked && ToolInputLock.IsLocked);
        Check("[scene] overall scene photography completed", ScenePhotographyTestUtil.CompleteRoute());
        yield return null;
        yield return null;
        ExpectCurrent("EVD-014", "chalk", "scene photography");
        Check("[panel] the open panel updated itself to exactly one NOW box (chalk)", CurrentOf(ui) == "chalk" && CountCurrent(ui) == 1);
        ui.Hide();
        Check("[panel] closing releases the UI gate and tool lock", !ui.IsShowing && !PlayerUIGate.IsBlocked && !ToolInputLock.IsLocked);

        // --- Knife, one step at a time (exactly one Current at every stage) ---
        esm.MarkChalkMarked("EVD-014", ToolType.ChalkMark);
        ExpectCurrent("EVD-014", "marker", "chalk");
        esm.MarkTented("EVD-014", ToolType.EvidenceMarker, 1);
        ExpectCurrent("EVD-014", "trace_marker", "marker (the blood on the blade takes its own tent)");
        Check("[trace] knife's final photo refused until the blood trace has its own tent",
            !esm.CanAdvanceTo("EVD-014", EvidenceStatus.PhotographedFinal));
        TraceMarkerTestUtil.MarkTraceIfRequired("EVD-014");
        ExpectCurrent("EVD-014", "measurement", "blood trace marker");

        // Two actions technically available: measuring, and the final photo (only a Warn rule).
        Check("[multiple available] after the marker the game would also accept the final photo",
            esm.CanAdvanceTo("EVD-014", EvidenceStatus.PhotographedFinal));
        Check("[multiple available] ...but only Measurement (the earlier intended step) is Current; Final Photo stays Pending",
            Step("EVD-014", "measurement").state == ProcedureMapStepState.Current && Step("EVD-014", "final_photo").state == ProcedureMapStepState.Pending);

        MeasurementTestUtil.RecordExact("EVD-014", 1);
        ExpectCurrent("EVD-014", "measurement", "one reference measured");
        MeasurementTestUtil.RecordExact("EVD-014", 2);
        MeasurementTestUtil.RecordAvailablePairsFor("EVD-014"); // and its distances to the evidence already discovered
        ExpectCurrent("EVD-014", "sketch", "measurements complete before processing");
        Check("[EVD-014] measured item placed on sketch",MeasurementTestUtil.CompleteSketchFor("EVD-014"));
        ExpectCurrent("EVD-014", "final_photo", "measurement complete");
        esm.MarkPhotographedFinal("EVD-014", ToolType.Photographer);
        ExpectCurrent("EVD-014", "blood_sample", "final photo");
        yield return UIProcedureTestUtil.CollectAndStoreBlood("EVD-014");
        Check("[knife] blood is prepared, sealed and explicitly stored", UIProcedureTestUtil.LastSampleStored);
        ExpectCurrent("EVD-014", "replace_gloves", "blood sample secured");
        yield return GloveChangeTestUtil.VisitAndReplace();
        ExpectCurrent("EVD-014", "fingerprint_process", "fresh gloves");

        // Fingerprint: one main box, one sub-action at a time.
        var kit = FingerprintKitTool.Instance;
        var print = EvidenceFingerprintMarker.FindByEvidenceId("EVD-014");
        Check($"[fingerprint] first sub-action: 'Apply fingerprint powder' (got '{Step("EVD-014", "fingerprint_process").subStep}')",
            kit != null && !kit.DustLoaded && Step("EVD-014", "fingerprint_process").subStep == "Apply fingerprint powder");
        if (kit != null)
        {
            kit.TryLoadDust();
        }
        ExpectCurrent("EVD-014", "fingerprint_process", "dust loaded");
        Check($"[fingerprint] then: 'Brush the fingerprint' (got '{Step("EVD-014", "fingerprint_process").subStep}')",
            Step("EVD-014", "fingerprint_process").subStep == "Brush the fingerprint");
        // Dusting accumulates per frame while the brush stays on the print, like a real
        // brushing stroke - keep brushing until the print is revealed.
        float brushed = 0f;
        while (kit != null && print != null && !print.IsDusted && brushed < 15f)
        {
            if (Step("EVD-014", "fingerprint_process").subStep != "Brush the fingerprint")
            {
                break;
            }
            kit.TryDust(print);
            brushed += Time.deltaTime;
            yield return null;
        }
        bool dusted = print != null && print.IsDusted;
        ExpectCurrent("EVD-014", "fingerprint_process", "print brushed");
        Check($"[fingerprint] then: 'Lift the fingerprint' (dusted={dusted}, got '{Step("EVD-014", "fingerprint_process").subStep}')",
            dusted && Step("EVD-014", "fingerprint_process").subStep == "Lift the fingerprint");
        if (kit != null && print != null)
        {
            kit.TryLift(print);
        }
        ExpectCurrent("EVD-014", "fingerprint_process", "fingerprint lifted but not stored");
        Check("[fingerprint] lifted sub-action requires comparison before storage",
            Step("EVD-014", "fingerprint_process").subStep.Contains("compare the fingerprint before storing"));
        Check("[fingerprint] premature storage is blocked",!HeldItemCoordinator.Instance.TryStoreHeldEvidence());
        Check("[fingerprint] correct desk comparison identifies the held card",FingerprintComparisonTestUtil.CompareStoredSource("EVD-014"));
        ExpectCurrent("EVD-014", "store_fingerprint", "comparison");
        Check("[fingerprint] player stores the derived print", HeldItemCoordinator.Instance.TryStoreHeldEvidence());
        yield return DismissCheckpoints();
        ExpectCurrent("EVD-014", "record", "fingerprint stored");
        // Evidence records wait for the discovered evidence's measurements (fixed references + pairs among discovered items).
        MeasurementTestUtil.CompleteDiscoveredMeasurements();
        esm.RecordReportLinesSelected("EVD-014", new[] { "knife-wound-match" });
        esm.MarkLogged("EVD-014", ToolType.Recorder);
        yield return DismissCheckpoints();
        ExpectCurrent("EVD-014", "collect", "record");
        esm.MarkCollected("EVD-014", ToolType.EvidenceCollector);
        ExpectCurrent("EVD-014", "seal", "collect");
        esm.MarkSealed("EVD-014", ToolType.EvidenceCollector);
        yield return DismissCheckpoints();
        ExpectCurrent("EVD-014", "store_bag", "seal");
        Check("[knife] sealed bag stores through real custody APIs",UIProcedureTestUtil.StoreSealedBag("EVD-014"));
        ExpectComplete("EVD-014", "explicit bag storage");
        ui.ShowFor("EVD-014");
        yield return null;
        Check("[completion] the panel shows 'Procedure Complete' and no NOW box", ui.ShowingProcedureComplete && CountCurrent(ui) == 0);
        ui.Hide();

        // --- BODY-A ---
        esm.MarkPhotographedRaw("BODY-A", ToolType.Photographer);
        yield return DismissCheckpoints();
        ExpectCurrent("BODY-A", "chalk", "initial photo + scene photography (outline before the marker)");
        esm.MarkChalkMarked("BODY-A", ToolType.ChalkMark);
        ExpectCurrent("BODY-A", "marker", "chalk");
        esm.MarkTentedWithLetter("BODY-A", ToolType.LetterTentMarker, "A");
        ExpectCurrent("BODY-A", "final_photo", "body marker");
        esm.MarkPhotographedFinal("BODY-A", ToolType.Photographer);
        ExpectCurrent("BODY-A", "measurement", "final body photo");
        MeasurementTestUtil.RecordExact("BODY-A", 1);
        MeasurementTestUtil.RecordExact("BODY-A", 2);
        MeasurementTestUtil.RecordAvailablePairsFor("BODY-A"); // and its distances to the evidence already discovered
        ExpectCurrent("BODY-A", "sketch", "measurements complete before processing");
        Check("[BODY-A] measured item placed on sketch",MeasurementTestUtil.CompleteSketchFor("BODY-A"));
        ExpectCurrent("BODY-A", "record", "measurement");
        esm.RecordReportLinesSelected("BODY-A", new[] { "body-a-location", "body-a-position", "body-a-wound-sharp-object" });
        esm.MarkLogged("BODY-A", ToolType.Recorder);
        yield return DismissCheckpoints();
        ExpectCurrent("BODY-A", "body_removal", "recorded body findings");
        esm.MarkBodyPreparedForRemoval("BODY-A"); esm.MarkBodyRemoved("BODY-A");
        ExpectComplete("BODY-A", "removal");

        // --- Wrong order: final photo before measurement (bottle) ---
        esm.MarkPhotographedRaw("EVD-018", ToolType.Photographer);
        yield return DismissCheckpoints();
        ExpectCurrent("EVD-018", "chalk", "initial photo + scene photography");
        esm.MarkChalkMarked("EVD-018", ToolType.ChalkMark);
        ExpectCurrent("EVD-018", "marker", "chalk");
        esm.MarkTented("EVD-018", ToolType.EvidenceMarker, 2);
        ExpectCurrent("EVD-018", "final_photo", "marker");
        esm.MarkPhotographedFinal("EVD-018", ToolType.Photographer); // Warn: allowed, logged out of order
        Check("[wrong order] Final Photo Completed, Measurement still the one Current",
            Step("EVD-018", "final_photo").state == ProcedureMapStepState.Completed && Step("EVD-018", "measurement").state == ProcedureMapStepState.Current
            && EvidenceProcedureMapResolver.CurrentCount(EvidenceProcedureMapResolver.Resolve("EVD-018")) == 1);
        Check("[wrong order] later processing (record, collect, seal, lab) all Pending",
            Step("EVD-018", "record").state == ProcedureMapStepState.Pending && Step("EVD-018", "collect").state == ProcedureMapStepState.Pending
            && Step("EVD-018", "seal").state == ProcedureMapStepState.Pending && Step("EVD-018", "fingerprint_process").state == ProcedureMapStepState.Pending);
        MeasurementTestUtil.RecordExact("EVD-018", 1);
        MeasurementTestUtil.RecordExact("EVD-018", 2);
        MeasurementTestUtil.RecordAvailablePairsFor("EVD-018"); // and its distances to the evidence already discovered
        ExpectCurrent("EVD-018", "sketch", "measurements complete before processing");
        Check("[EVD-018] measured item placed on sketch",MeasurementTestUtil.CompleteSketchFor("EVD-018"));
        ExpectCurrent("EVD-018", "fingerprint_process", "measurement completed");
        var bottlePrint=EvidenceFingerprintMarker.FindByEvidenceId("EVD-018");
        double bottleDustDeadline=Time.realtimeSinceStartupAsDouble+8;
        while(!bottlePrint.IsDusted && Time.realtimeSinceStartupAsDouble<bottleDustDeadline){kit.TryDust(bottlePrint);yield return null;}
        kit.TryLift(bottlePrint);
        Check("[bottle] fingerprint map requires comparison before storage",Step("EVD-018","fingerprint_process").subStep.Contains("compare the fingerprint before storing"));
        Check("[bottle] real comparison identifies the held card",FingerprintComparisonTestUtil.CompareStoredSource("EVD-018"));
        HeldItemCoordinator.Instance.TryStoreHeldEvidence();yield return DismissCheckpoints();
        ExpectCurrent("EVD-018", "record", "bottle prints stored");
        var bottleLines = new List<string>();
        foreach (var option in esm.GetRecord("EVD-018").definition.reportLineOptions)
        {
            if (option != null && option.isAccurate)
            {
                bottleLines.Add(option.id);
            }
        }
        esm.RecordReportLinesSelected("EVD-018", bottleLines);
        var logged = esm.MarkLogged("EVD-018", ToolType.Recorder);
        yield return DismissCheckpoints();
        if (logged == TransitionResult.Applied)
        {
            ExpectCurrent("EVD-018", "collect", "record");
            esm.MarkCollected("EVD-018", ToolType.EvidenceCollector);
            ExpectCurrent("EVD-018", "seal", "collect");
            esm.MarkSealed("EVD-018", ToolType.EvidenceCollector);
            yield return DismissCheckpoints();
            ExpectCurrent("EVD-018", "store_bag", "seal after on-scene fingerprints");
            Check("[bottle] sealed bag stores through real custody APIs",UIProcedureTestUtil.StoreSealedBag("EVD-018"));
            ExpectComplete("EVD-018", "explicit bag storage");
        }
        else
        {
            Check($"[EVD-018] bottle record step applied (got {logged})", false);
        }

        // --- Spatter ---
        esm.MarkPhotographedRaw("EVD-015", ToolType.Photographer);
        yield return DismissCheckpoints();
        ExpectCurrent("EVD-015", "chalk", "initial photo + scene photography (outline before the marker)");
        esm.MarkChalkMarked("EVD-015", ToolType.ChalkMark);
        ExpectCurrent("EVD-015", "marker", "chalk");
        esm.MarkTented("EVD-015", ToolType.EvidenceMarker, 3);
        ExpectCurrent("EVD-015", "measurement", "marker");
        MeasurementTestUtil.RecordExact("EVD-015", 1);
        MeasurementTestUtil.RecordExact("EVD-015", 2);
        MeasurementTestUtil.RecordAvailablePairsFor("EVD-015"); // and its distances to the evidence already discovered
        ExpectCurrent("EVD-015", "sketch", "measurements complete before processing");
        Check("[EVD-015] measured item placed on sketch",MeasurementTestUtil.CompleteSketchFor("EVD-015"));
        ExpectCurrent("EVD-015", "final_photo", "measurement");
        esm.MarkPhotographedFinal("EVD-015", ToolType.Photographer);
        ExpectCurrent("EVD-015", "blood_sample", "final photo");
        yield return UIProcedureTestUtil.CollectAndStoreBlood("EVD-015");
        Check("[spatter] real sample prepared and stored",UIProcedureTestUtil.LastSampleStored);
        ExpectCurrent("EVD-015", "replace_gloves", "sample stored");
        yield return GloveChangeTestUtil.VisitAndReplace();
        ExpectCurrent("EVD-015", "record", "sample stored");
        esm.RecordReportLinesSelected("EVD-015", new[] { "blood-spatter-location", "blood-spatter-droplet-pattern", "blood-spatter-mechanism-only" });
        esm.MarkLogged("EVD-015", ToolType.Recorder);
        yield return DismissCheckpoints();
        ExpectComplete("EVD-015", "record");

        // --- Leak guard and switching ---
        Check("[leak] only photographed items can be shown", !EvidenceProcedureUI.IsEligible("EVD-999"));
        ui.Show();
        ui.SwitchItem(1);
        Check($"[panel] left/right switches to another photographed item ({ui.FocusedEvidenceId})", EvidenceProcedureUI.IsEligible(ui.FocusedEvidenceId));
        ui.Hide();

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    private static string CurrentOf(EvidenceProcedureUI ui)
    {
        foreach (var s in ui.CurrentSteps)
        {
            if (s.state == ProcedureMapStepState.Current)
            {
                return s.stepId;
            }
        }
        return "none";
    }

    private static int CountCurrent(EvidenceProcedureUI ui)
    {
        int n = 0;
        foreach (var s in ui.CurrentSteps)
        {
            if (s.state == ProcedureMapStepState.Current)
            {
                n++;
            }
        }
        return n;
    }

    private static void Measure(string requirementId, string referenceId)
    {
        var m = MeasurementStateManager.Instance;
        if (m.TryGetSceneDistance(requirementId, referenceId, out var d))
        {
            m.RecordMeasurement(requirementId, referenceId, d);
        }
    }

    // Hypothesis checkpoints can open mid-test; answer them so the run isn't held up.
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
}
