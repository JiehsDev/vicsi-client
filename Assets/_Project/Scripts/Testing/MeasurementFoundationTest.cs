// Assets/_Project/Scripts/Testing/MeasurementFoundationTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Play-mode check of the measurement-data foundation: reference resolution, partial /
/// complete / out-of-tolerance / late measurements, RequirementType.MeasurementCompleted
/// and the logged events. Measured values come from MeasurementStateManager.
/// TryGetSceneDistance (the true scene distance) - no measuring tool exists yet.
/// Run once per fresh Play session: RunAllTests(), then poll IsDone / Report.
/// </summary>
public class MeasurementFoundationTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var failures = new List<string>();
        var report = new StringBuilder("=== MeasurementFoundationTest ===\n");
        var m = MeasurementStateManager.Instance;
        var flow = FindFirstObjectByType<GreyboxFlowTest>(FindObjectsInactive.Include);
        if (m == null || m.RequirementSet == null || flow == null || EvidenceStateManager.Instance == null)
        {
            Report = "FAIL: MeasurementStateManager / its set / GreyboxFlowTest missing.";
            IsDone = true;
            yield break;
        }
        yield return null;

        // --- Reset: a fresh Play session starts with nothing measured ---
        bool allClean = true;
        int count = 0;
        foreach (var r in m.AllRecords)
        {
            count++;
            allClean &= r.progress == MeasurementProgress.NotStarted && !r.hasA && !r.hasB && !r.takenAfterDisturbance;
        }
        Check(failures, report, $"[reset] {count} requirements tracked, all NotStarted at session start", count == 4 && allClean);

        // --- Reference resolution ---
        bool allResolve = true;
        var seenRefs = new System.Collections.Generic.HashSet<string>();
        foreach (var r in m.AllRecords)
        {
            allResolve &= SceneReferencePoint.Find(r.requirement.referenceAId) != null && SceneReferencePoint.Find(r.requirement.referenceBId) != null;
            seenRefs.Add(r.requirement.referenceAId);
            seenRefs.Add(r.requirement.referenceBId);
        }
        Check(failures, report, "[refs] every item's two references resolve in the scene", allResolve);
        Check(failures, report, $"[refs] every item has its own pair - {seenRefs.Count} distinct references for 4 items", seenRefs.Count == 8);
        Check(failures, report, "[refs] unknown id resolves to null", SceneReferencePoint.Find("REF-NOPE") == null && SceneReferencePoint.Find(null) == null);
        Check(failures, report, "[refs] unknown reference rejected safely",
            m.RecordMeasurement("MEAS-EVD-015", "REF-NOPE", 1f) == MeasurementRecordResult.UnknownReference && m.GetRecord("MEAS-EVD-015").progress == MeasurementProgress.NotStarted);
        Check(failures, report, "[refs] unknown requirement rejected safely", m.RecordMeasurement("MEAS-NOPE", A("MEAS-EVD-014"), 1f) == MeasurementRecordResult.UnknownRequirement);

        var stray = new GameObject("REF-TEST-STRAY").AddComponent<SceneReferencePoint>();
        stray.referenceId = "REF-TEST-STRAY";
        Check(failures, report, "[refs] a real reference that isn't this requirement's is rejected",
            m.RecordMeasurement("MEAS-EVD-015", "REF-TEST-STRAY", 1f) == MeasurementRecordResult.WrongReference);
        Destroy(stray.gameObject);
        Check(failures, report, "[refs] negative distance rejected", m.RecordMeasurement("MEAS-EVD-015", A("MEAS-EVD-015"), -1f) == MeasurementRecordResult.InvalidDistance);

        // Baked expected values match the live scene (same geometry code).
        bool bakedMatches = true;
        foreach (var r in m.AllRecords)
        {
            m.TryGetSceneDistance(r.requirement.requirementId, r.requirement.referenceAId, out var da);
            m.TryGetSceneDistance(r.requirement.requirementId, r.requirement.referenceBId, out var db);
            bakedMatches &= Mathf.Abs(da - r.requirement.expectedDistanceA) < 0.002f && Mathf.Abs(db - r.requirement.expectedDistanceB) < 0.002f;
        }
        Check(failures, report, "[bake] every expected distance matches the live scene within 2 mm", bakedMatches);

        var req = new RequirementNode { kind = RequirementNodeKind.Leaf, requirementType = RequirementType.MeasurementCompleted, measurementRequirementId = "MEAS-EVD-015" };

        // --- Partial ---
        Check(failures, report, "[eval] MeasurementCompleted false before any measurement", !RequirementEvaluator.IsSatisfied(req));
        Check(failures, report, "[partial] spatter: REF-A recorded", m.RecordMeasurement("MEAS-EVD-015", A("MEAS-EVD-015"), True("MEAS-EVD-015", A("MEAS-EVD-015"))) == MeasurementRecordResult.Recorded);
        var spatter = m.GetRecord("MEAS-EVD-015");
        Check(failures, report, "[partial] state Partial, A within tolerance, B missing", spatter.progress == MeasurementProgress.Partial && spatter.hasA && spatter.withinToleranceA && !spatter.hasB);
        Check(failures, report, "[partial] requirement still incomplete", !m.IsMeasurementComplete("MEAS-EVD-015") && !RequirementEvaluator.IsSatisfied(req));

        // --- Complete ---
        Check(failures, report, "[complete] spatter: REF-B completes it", m.RecordMeasurement("MEAS-EVD-015", B("MEAS-EVD-015"), True("MEAS-EVD-015", B("MEAS-EVD-015")) + 0.04f) == MeasurementRecordResult.Completed);
        Check(failures, report, $"[complete] errors recorded (A {spatter.errorA:0.000}, B {spatter.errorB:0.000}), before disturbance, timestamped",
            spatter.IsComplete && Mathf.Abs(spatter.errorB - 0.04f) < 0.002f && spatter.TakenBeforeDisturbance && spatter.completedAtSessionTime >= 0f);
        Check(failures, report, "[eval] MeasurementCompleted true after the valid pair", RequirementEvaluator.IsSatisfied(req));
        Check(failures, report, "[eval] NOT(MeasurementCompleted) now false", !RequirementEvaluator.IsSatisfied(Group(RequirementLogicalOperator.Not, req)));
        Check(failures, report, "[eval] missing id fails safely",
            !RequirementEvaluator.IsSatisfied(new RequirementNode { kind = RequirementNodeKind.Leaf, requirementType = RequirementType.MeasurementCompleted }));
        Check(failures, report, "[complete] re-measuring a complete requirement changes nothing", m.RecordMeasurement("MEAS-EVD-015", A("MEAS-EVD-015"), 99f) == MeasurementRecordResult.AlreadyComplete && spatter.measuredA < 50f);

        // --- Out of tolerance ---
        var body = m.GetRecord("MEAS-BODY-A");
        m.RecordMeasurement("MEAS-BODY-A", A("MEAS-BODY-A"), True("MEAS-BODY-A", A("MEAS-BODY-A")) + 0.5f);
        var bodyResult = m.RecordMeasurement("MEAS-BODY-A", B("MEAS-BODY-A"), True("MEAS-BODY-A", B("MEAS-BODY-A")));
        Check(failures, report, "[tolerance] wrong A (+0.5 m) + correct B does not complete", bodyResult == MeasurementRecordResult.Recorded && body.progress == MeasurementProgress.Partial && !body.withinToleranceA && body.withinToleranceB);
        Check(failures, report, "[tolerance] re-measuring A correctly completes it", m.RecordMeasurement("MEAS-BODY-A", A("MEAS-BODY-A"), True("MEAS-BODY-A", A("MEAS-BODY-A"))) == MeasurementRecordResult.Completed);

        // --- Late (after disturbance): knife and bottle through the real lifecycle first ---
        // Evidence records now wait for the discovered evidence's measurements, so the
        // lifecycle measures both items before logging/collecting them (GreyboxFlowTest's
        // default pre-log step). Those measurements are then cleared, so the rules below are
        // exercised on items that really were collected before being measured; the events
        // the lifecycle logged are counted separately and added to the expectations.
        SessionLogger.Instance?.Flush();
        string preFlowLog = ReadSessionLog();
        flow.SetEvidenceIdsForTest(new[] { "EVD-014", "EVD-018" }, 0, true);
        string flowReport = flow.RunFullFlow();
        Check(failures, report, "[late] knife + bottle collected through the real lifecycle", flowReport.Contains("=== PASS"));
        if (!flowReport.Contains("=== PASS")) report.AppendLine(flowReport);
        flow.SetEvidenceIdsForTest(new[] { "EVD-014", "EVD-015", "EVD-018" });
        yield return null;
        SessionLogger.Instance?.Flush();
        string postFlowLog = ReadSessionLog();
        int flowStarted = Count(postFlowLog, "\"eventType\":\"MeasurementStarted\"") - Count(preFlowLog, "\"eventType\":\"MeasurementStarted\"");
        int flowTaken = Count(postFlowLog, "\"eventType\":\"MeasurementTaken\"") - Count(preFlowLog, "\"eventType\":\"MeasurementTaken\"");
        int flowCompleted = Count(postFlowLog, "\"eventType\":\"MeasurementCompleted\"") - Count(preFlowLog, "\"eventType\":\"MeasurementCompleted\"");
        int flowOutOfTolerance = Count(postFlowLog, "\"eventType\":\"MeasurementOutOfTolerance\"") - Count(preFlowLog, "\"eventType\":\"MeasurementOutOfTolerance\"");
        int flowAfterDisturbance = Count(postFlowLog, "\"eventType\":\"MeasurementAfterDisturbance\"") - Count(preFlowLog, "\"eventType\":\"MeasurementAfterDisturbance\"");
        m.ResetSession();

        var knifeRequirement = m.RequirementSet.Find("MEAS-EVD-014");
        Check(failures, report, "[late] knife counts as disturbed", m.IsDisturbed(knifeRequirement));
        m.RecordMeasurement("MEAS-EVD-014", A("MEAS-EVD-014"), knifeRequirement.expectedDistanceA);
        var knifeResult = m.RecordMeasurement("MEAS-EVD-014", B("MEAS-EVD-014"), knifeRequirement.expectedDistanceB);
        var knife = m.GetRecord("MEAS-EVD-014");
        Check(failures, report, "[late] Warn: completes but flagged taken after disturbance",
            knifeResult == MeasurementRecordResult.Completed && knife.takenAfterDisturbance && !knife.TakenBeforeDisturbance);

        var bottleRequirement = m.RequirementSet.Find("MEAS-EVD-018");
        var originalEnforcement = bottleRequirement.enforcement;
        bottleRequirement.enforcement = ProcedureEnforcement.Block;
        try
        {
            m.RecordMeasurement("MEAS-EVD-018", A("MEAS-EVD-018"), bottleRequirement.expectedDistanceA);
            var bottleResult = m.RecordMeasurement("MEAS-EVD-018", B("MEAS-EVD-018"), bottleRequirement.expectedDistanceB);
            Check(failures, report, "[late] Block: recorded but never completes", bottleResult == MeasurementRecordResult.Recorded && !m.IsMeasurementComplete("MEAS-EVD-018"));
        }
        finally
        {
            bottleRequirement.enforcement = originalEnforcement;
        }
        Check(failures, report, "[late] temporary Block enforcement restored", bottleRequirement.enforcement == ProcedureEnforcement.Warn);

        // --- Events ---
        SessionLogger.Instance?.Flush();
        string log = ReadSessionLog();
        int started = Count(log, "\"eventType\":\"MeasurementStarted\"");
        int taken = Count(log, "\"eventType\":\"MeasurementTaken\"");
        int completed = Count(log, "\"eventType\":\"MeasurementCompleted\"");
        int outOfTolerance = Count(log, "\"eventType\":\"MeasurementOutOfTolerance\"");
        int afterDisturbance = Count(log, "\"eventType\":\"MeasurementAfterDisturbance\"");
        Check(failures, report, $"[events] Started x4 + {flowStarted} from the lifecycle (got {started})", started == 4 + flowStarted);
        Check(failures, report, $"[events] Taken x9 + {flowTaken} from the lifecycle (got {taken})", taken == 9 + flowTaken);
        Check(failures, report, $"[events] Completed x3 + {flowCompleted} from the lifecycle (got {completed})", completed == 3 + flowCompleted);
        Check(failures, report, $"[events] OutOfTolerance x1 + {flowOutOfTolerance} from the lifecycle (got {outOfTolerance})", outOfTolerance == 1 + flowOutOfTolerance);
        Check(failures, report, $"[events] AfterDisturbance x4 + {flowAfterDisturbance} from the lifecycle (got {afterDisturbance})", afterDisturbance == 4 + flowAfterDisturbance);
        Check(failures, report, "[events] payload carries requirementId/evidenceId/referenceId/measured/expected/error/tolerance",
            log.Contains("\"requirementId\"") && log.Contains("\"referenceId\"") && log.Contains("\"measuredDistance\"") && log.Contains("\"expectedDistance\"") && log.Contains("\"tolerance\""));
        Check(failures, report, "[events] Block-mode late measurement logged as blocked", log.Contains("\"value\":\"blocked\""));
        Check(failures, report, "[events] no Transform/scene data leaked into the log", !log.Contains("REF-TEST-STRAY\",\"payload") && !log.Contains("m_LocalPosition"));

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    // Each item's own references (per-evidence fixed references), by requirement id.
    private static string A(string requirementId) => MeasurementStateManager.Instance.RequirementSet.Find(requirementId).referenceAId;
    private static string B(string requirementId) => MeasurementStateManager.Instance.RequirementSet.Find(requirementId).referenceBId;

    private static float True(string requirementId, string referenceId)
    {
        MeasurementStateManager.Instance.TryGetSceneDistance(requirementId, referenceId, out var d);
        return d;
    }

    private static RequirementNode Group(RequirementLogicalOperator op, params RequirementNode[] children)
    {
        var node = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = op };
        node.children.AddRange(children);
        return node;
    }

    private static int Count(string text, string token)
    {
        int n = 0, i = 0;
        while ((i = text.IndexOf(token, i, System.StringComparison.Ordinal)) >= 0)
        {
            n++;
            i += token.Length;
        }
        return n;
    }

    private static string ReadSessionLog()
    {
        string path = SessionLogger.Instance != null ? SessionLogger.Instance.ActiveLogFilePath : null;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return string.Empty;
        using var fs = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
        using var reader = new System.IO.StreamReader(fs);
        return reader.ReadToEnd();
    }

    private static void Check(List<string> failures, StringBuilder report, string label, bool passed)
    {
        report.AppendLine($"  {(passed ? "PASS" : "FAIL")}: {label}");
        if (!passed) failures.Add(label);
    }
}
