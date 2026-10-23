// Assets/_Project/Scripts/Testing/MeasurementProcedureTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Play-mode check of measurement inside the evidence procedure:
/// BODY-A: raw photo -> chalk -> Marker A -> final photo -> measurement -> record.
/// Bottle: marker -> final photo -> measurements before fingerprint processing.
/// Knife/blood retain marker -> measurement -> final photo -> rest of procedure.
/// Photos go through the real PhotographTool (its own aim update + shutter), chalk through
/// the real ChalkMarkTool trace, measurement through the real MeasurementTool trigger
/// handler; the rest of each item's procedure is GreyboxFlowTest resumed from where the
/// item stands. Two modes, one per fresh Play session:
///   Normal     - all four items follow their own order: no ProcedureOutOfOrder.
///   WrongOrder - BODY-A final photo before measuring is valid; the knife with only Reference A still warns.
/// </summary>
public class MeasurementProcedureTest : MonoBehaviour
{
    public enum Mode { Normal, WrongOrder }

    private const string Warning = "Measure this item first so its position can be preserved alongside its photographs and evidence record.";

    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private Mode mode;
    private EvidenceStateManager esm;
    private PhotographTool camera;
    private MeasurementTool tape;
    private OVRCameraRig rig;
    private List<string> failures;
    private StringBuilder report;

    public void RunAllTests(Mode testMode)
    {
        mode = testMode;
        IsDone = false;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        failures = new List<string>();
        report = new StringBuilder($"=== MeasurementProcedureTest ({mode}) ===\n");
        esm = EvidenceStateManager.Instance;
        rig = FindFirstObjectByType<OVRCameraRig>();
        camera = FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
        tape = FindFirstObjectByType<MeasurementTool>(FindObjectsInactive.Include);
        var flow = FindFirstObjectByType<GreyboxFlowTest>(FindObjectsInactive.Include);
        if (esm == null || rig == null || camera == null || tape == null || flow == null || MeasurementStateManager.Instance == null)
        {
            Report = "FAIL: required objects missing.";
            IsDone = true;
            yield break;
        }
        yield return null;

        // Knife and blood retain their Warn rule before PhotographedFinal.
        foreach (var id in new[] { "EVD-014", "EVD-015" })
        {
            var steps = esm.GetRecord(id).definition.stepRequirements;
            var rule = steps.Find(s => s != null && s.stepId == "final-evidence-photograph");
            Check($"[data] {id}: Warn rule before PhotographedFinal requiring MeasurementCompleted(MEAS-{id})",
                rule != null && rule.beforeStatus == EvidenceStatus.PhotographedFinal && rule.enforcement == ProcedureEnforcement.Warn
                && rule.requirement.requirementType == RequirementType.MeasurementCompleted && rule.requirement.measurementRequirementId == "MEAS-" + id
                && rule.PlayerMessage == Warning);
        }

        var bottleDefinition=esm.GetRecord("EVD-018").definition;
        var bottleFixed=bottleDefinition.stepRequirements.Find(s=>s.stepId=="bottle-measure-before-fingerprint-fixed");
        var bottlePairs=bottleDefinition.stepRequirements.Find(s=>s.stepId=="bottle-measure-before-fingerprint-pairs");
        Check("[data] bottle measurements follow its final photo and block fingerprint processing",bottleDefinition.measurementAfterFinalPhoto
            &&bottleFixed!=null&&bottleFixed.action==ProcedureAction.FingerprintProcessing&&bottleFixed.enforcement==ProcedureEnforcement.Block
            &&bottleFixed.requirement.requirementType==RequirementType.MeasurementCompleted&&bottleFixed.requirement.measurementRequirementId=="MEAS-EVD-018"
            &&bottlePairs!=null&&bottlePairs.action==ProcedureAction.FingerprintProcessing&&bottlePairs.enforcement==ProcedureEnforcement.Block
            &&bottlePairs.requirement.requirementType==RequirementType.EvidencePairMeasurementsComplete);
        var bodyRule = esm.GetRecord("BODY-A").definition.stepRequirements.Find(s => s.stepId == "measurement-before-body-record");
        Check("[data] BODY-A measurements block recording after the final photo", bodyRule != null
            && bodyRule.beforeStatus == EvidenceStatus.Logged && bodyRule.enforcement == ProcedureEnforcement.Block);

        foreach (var id in new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" })
        {
            esm.MarkFound(id, ToolType.None);
        }
        yield return DismissCheckpoints();
        Check("[scene] overall scene photography completed before evidence work", ScenePhotographyTestUtil.CompleteRoute());
        yield return null;

        if (mode == Mode.Normal)
        {
            yield return NormalOrder("BODY-A", 0);
            // Evidence records wait for the discovered evidence's measurements (fixed references + pairs among discovered items).
            MeasurementTestUtil.CompleteDiscoveredMeasurements();
            MeasurementTestUtil.CompleteSketchFor("BODY-A");
            esm.RecordReportLinesSelected("BODY-A", new[] { "body-a-location", "body-a-position", "body-a-wound-sharp-object" });
            Check("[BODY-A] Recorder after the final photo and measurements", esm.MarkLogged("BODY-A", ToolType.Recorder) == TransitionResult.Applied);
            Check("[BODY-A] never collected (examined in place, scene custody)",
                esm.GetRecord("BODY-A").status == EvidenceStatus.Logged && esm.GetRecord("BODY-A").custodyLocation == EvidenceCustodyLocation.Scene);

            int tent = 1;
            foreach (var id in new[] { "EVD-014", "EVD-015", "EVD-018" })
            {
                yield return NormalOrder(id, tent++);
                flow.SetEvidenceIdsForTest(new[] { id }, tent, true, resume: true);
                string flowReport = flow.RunFullFlow();
                Check($"[{id}] rest of the procedure (GreyboxFlowTest resumed after the final photo)", flowReport.Contains("=== PASS"));
                if (!flowReport.Contains("=== PASS")) report.AppendLine(flowReport);
                yield return DismissCheckpoints();
                Check($"[{id}] reached Processed", esm.GetRecord(id).status == EvidenceStatus.Processed);
            }
            flow.SetEvidenceIdsForTest(new[] { "EVD-014", "EVD-015", "EVD-018" });

            var knife = esm.GetRecord("EVD-014");
            Check("[EVD-014] swab done and print matched to Paul (on-scene kit)",
                knife.swabbingDone && knife.fingerprintLabStatus == FingerprintLabStatus.Matched && knife.fingerprintLabMatchResult == "Paul");
            var bottle = esm.GetRecord("EVD-018");
            Check("[EVD-018] lab print matched to Paul (station)",
                bottle.fingerprintLabStatus == FingerprintLabStatus.Matched && bottle.fingerprintLabMatchResult == "Paul");
            Check("[log] no ProcedureOutOfOrder in the correctly ordered session", CountInLog("\"eventType\":\"ProcedureOutOfOrder\"") == 0);
        }
        else
        {
            // BODY-A: marker, then the final photo with no measurement at all.
            yield return RawPhotoChalkMarker("BODY-A", 0);
            int before = CountInLog("\"eventType\":\"ProcedureOutOfOrder\"");
            Photo("BODY-A");
            Check("[BODY-A revised order] final photo applied before measurement", esm.GetRecord("BODY-A").status == EvidenceStatus.PhotographedFinal);
            Check("[BODY-A revised order] no measurement warning", camera.LastProcedureWarning == null);
            yield return null;
            Check("[BODY-A revised order] no ProcedureOutOfOrder for final photo before measurement", CountInLog("\"eventType\":\"ProcedureOutOfOrder\"") == before);
            Check("[BODY-A revised order] no stale measurement-before-photo prerequisite in event log", !LogContains("\"value\":\"measurement:MEAS-BODY-A\""));
            Photo("BODY-A");
            yield return null;
            Check("[BODY-A revised order] a second shutter press adds no warning", camera.LastProcedureWarning == null && CountInLog("\"eventType\":\"ProcedureOutOfOrder\"") == before);

            // EVD-014: only Reference A measured before the final photo.
            yield return RawPhotoChalkMarker("EVD-014", 1);
            Equip(ToolType.Measurement);
            MeasureSide("EVD-014", MeasurementTestUtil.Ref("EVD-014", 1));
            Check("[partial] Reference A only: requirement incomplete", !MeasurementStateManager.Instance.IsMeasurementComplete("MEAS-EVD-014")
                && MeasurementStateManager.Instance.GetRecord("MEAS-EVD-014").hasA);
            // Only the fixed-reference rule is under test here: its distance to the body is measured.
            MeasurementTestUtil.RecordAvailablePairsFor("EVD-014");
            before = CountInLog("\"eventType\":\"ProcedureOutOfOrder\"");
            Equip(ToolType.Photographer);
            Photo("EVD-014");
            yield return null;
            Check("[partial] final photo applied with the warning, one event",
                esm.GetRecord("EVD-014").status == EvidenceStatus.PhotographedFinal && camera.LastProcedureWarning == Warning
                && CountInLog("\"eventType\":\"ProcedureOutOfOrder\"") - before == 1);
            Equip(ToolType.Measurement);
            MeasureSide("EVD-014", MeasurementTestUtil.Ref("EVD-014", 2));
            Check("[partial] Reference B afterwards completes the measurement", MeasurementStateManager.Instance.IsMeasurementComplete("MEAS-EVD-014"));
        }

        HeldItemCoordinator.Instance.HolsterCurrentTool();
        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    // raw photo (no warning) -> chalk -> marker -> measure A+B -> final photo (no warning)
    private IEnumerator NormalOrder(string id, int tentNumber)
    {
        int before = CountInLog("\"eventType\":\"ProcedureOutOfOrder\"");
        yield return RawPhotoChalkMarker(id, tentNumber);

        var definition=esm.GetRecord(id).definition;
        bool finalFirst=definition.requiresBodyRemoval||definition.measurementAfterFinalPhoto;
        if (finalFirst) { Photo(id); yield return null; }

        Equip(ToolType.Measurement);
        MeasureSide(id, MeasurementTestUtil.Ref(id, 1));
        MeasureSide(id, MeasurementTestUtil.Ref(id, 2));
        MeasurementTestUtil.RecordAvailablePairsFor(id); // evidence distances to the items already discovered
        Check($"[{id}] measurements follow the required photograph/marker order",
            MeasurementStateManager.Instance.IsMeasurementComplete("MEAS-" + id)
            && esm.GetRecord(id).status == (finalFirst ? EvidenceStatus.PhotographedFinal : EvidenceStatus.Marked));

        Equip(ToolType.Photographer);
        if (!finalFirst) Photo(id);
        yield return null;
        Check($"[{id}] final photo: applied, no warning, no ProcedureOutOfOrder",
            esm.GetRecord(id).status == EvidenceStatus.PhotographedFinal && camera.LastProcedureWarning == null
            && CountInLog("\"eventType\":\"ProcedureOutOfOrder\"") == before);
    }

    private IEnumerator RawPhotoChalkMarker(string id, int tentNumber)
    {
        int before = CountInLog("\"eventType\":\"ProcedureOutOfOrder\"");
        Equip(ToolType.Photographer);
        Photo(id);
        yield return null;
        Check($"[{id}] initial photo applied with no measurement warning",
            esm.GetRecord(id).status == EvidenceStatus.PhotographedRaw && camera.LastProcedureWarning == null
            && CountInLog("\"eventType\":\"ProcedureOutOfOrder\"") == before);

        if (esm.GetRecord(id).definition.requiresChalk)
        {
            var guide = EvidenceChalkGuide.FindActive(id);
            if (guide != null && ChalkMarkTool.Instance != null)
            {
                for (int p = 0; p < guide.PointCount && !guide.IsComplete; p++)
                {
                    ChalkMarkTool.Instance.TryTraceAt(guide.GetPointPosition(p));
                }
            }
            Check($"[{id}] chalk outline traced", esm.GetRecord(id).status == EvidenceStatus.ChalkMarked);
        }

        var marked = id == "BODY-A"
            ? esm.MarkTentedWithLetter(id, ToolType.LetterTentMarker, "A")
            : esm.MarkTented(id, ToolType.EvidenceMarker, tentNumber);
        Check($"[{id}] {(id == "BODY-A" ? "letter marker A" : "numbered tent")} placed", marked == TransitionResult.Applied);
    }

    // --- Real tools -------------------------------------------------------

    private void Equip(ToolType role)
    {
        if (PlayerToolRegistry.VirtuallyEquippedRole != role)
        {
            HeldItemCoordinator.Instance.ToggleEquipTool(role, rig.rightHandAnchor);
        }
    }

    // The camera aims through the headset camera: point it at the item, run the tool's own
    // aim update, then its own shutter.
    private void Photo(string id)
    {
        TraceMarkerTestUtil.MarkTraceIfRequired(id); // the knife's second tent (blood on the blade) before its final photo
        Equip(ToolType.Photographer);
        var head = Camera.main.transform;
        Vector3 target = EvidenceProp.FindById(id).transform.position;
        Vector3 eye = target + new Vector3(0.5f, 0.9f, 0.1f);
        head.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
        var bf = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(PhotographTool).GetMethod("UpdateAimTarget", bf).Invoke(camera, null);
        PhotoScaleTestUtil.PrepareForFinalPhoto(camera, id);
        typeof(PhotographTool).GetMethod("TakePhoto", bf).Invoke(camera, null);
        PhotoScaleTestUtil.FinishPhoto(camera);
    }

    // Select the item, hook the tape at the reference, hold the end over the item, trigger.
    private void MeasureSide(string id, string referenceId)
    {
        MeasurementGeometry.TryGetEvidenceAnchor(id, out var anchor);
        if (tape.CurrentPhase == MeasurementTool.Phase.SelectTarget || tape.SelectedRequirementId != "MEAS-" + id)
        {
            PlaceTip(anchor + Vector3.up + Vector3.forward * 0.05f, anchor);
            tape.HandleTrigger();
        }

        // Inside this item's own reference zone (per-evidence fixed references).
        Vector3 hook = MeasurementTestUtil.HookPoint(referenceId);
        PlaceTip(hook, hook + Vector3.back);
        tape.HandleTrigger();

        Vector3 end = new Vector3(anchor.x + 0.02f, 1.0f, anchor.z);
        PlaceTip(end, end + Vector3.down);
        tape.HandleTrigger();
    }

    private void PlaceTip(Vector3 tipWorld, Vector3 lookAt)
    {
        var t = tape.transform;
        t.rotation = Quaternion.LookRotation(lookAt - tipWorld);
        t.position += tipWorld - tape.TapeTip.position;
    }

    private IEnumerator DismissCheckpoints()
    {
        for (int i = 0; i < 5; i++)
        {
            var checkpoint = FindFirstObjectByType<HypothesisCheckpointUI>();
            if (checkpoint != null && checkpoint.IsShowing)
            {
                checkpoint.AnswerForTest();
            }
            yield return null;
        }
    }

    // --- Log / report --------------------------------------------------------

    private static string ReadSessionLog()
    {
        SessionLogger.Instance?.Flush();
        string path = SessionLogger.Instance != null ? SessionLogger.Instance.ActiveLogFilePath : null;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return string.Empty;
        using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
        using var reader = new System.IO.StreamReader(fs);
        return reader.ReadToEnd();
    }

    private static bool LogContains(string token) => ReadSessionLog().Contains(token);

    private static int CountInLog(string token)
    {
        string text = ReadSessionLog();
        int n = 0, i = 0;
        while ((i = text.IndexOf(token, i, System.StringComparison.Ordinal)) >= 0)
        {
            n++;
            i += token.Length;
        }
        return n;
    }

    private void Check(string label, bool passed)
    {
        report.AppendLine($"  {(passed ? "PASS" : "FAIL")}: {label}");
        if (!passed) failures.Add(label);
    }
}
