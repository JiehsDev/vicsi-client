// Assets/_Project/Scripts/Testing/ProcedureFoundationTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using UnityEngine;

/// <summary>
/// Play-mode check of the procedure-foundation prep: no live test finding, the
/// ScenePhotographyCompleted requirement + obj-document-scene, the ProcedureStepRequirement
/// hook (one temporary Warn and one temporary Block rule, removed afterwards), the bottle's
/// real lab match and the knife's unchanged on-scene match.
/// Run once per fresh Play session: RunAllTests(), then poll IsDone / Report.
/// </summary>
public class ProcedureFoundationTest : MonoBehaviour
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
        var report = new StringBuilder("=== ProcedureFoundationTest ===\n");
        var esm = EvidenceStateManager.Instance;
        var objectives = ObjectiveStateManager.Instance;
        var photos = ScenePhotographyManager.Instance;
        var flow = FindFirstObjectByType<GreyboxFlowTest>(FindObjectsInactive.Include);
        if (esm == null || objectives == null || photos == null || flow == null || FindingStateManager.Instance == null)
        {
            Report = "FAIL: required managers missing.";
            IsDone = true;
            yield break;
        }

        // Let ScenePhotographyManager.Start collect its points.
        yield return null;
        yield return null;

        // --- A: no live test finding ---
        Check(failures, report, "[A] FIND-TEST-HARMLESS not registered", FindingStateManager.Instance.GetRecord("FIND-TEST-HARMLESS") == null);
        Check(failures, report, "[A] FIND-TEST-HARMLESS not confirmed", !FindingStateManager.Instance.IsConfirmed("FIND-TEST-HARMLESS"));

        // --- D/E before the route ---
        var scenePhotoReq = new RequirementNode { kind = RequirementNodeKind.Leaf, requirementType = RequirementType.ScenePhotographyCompleted };
        Check(failures, report, "[E] ScenePhotographyCompleted false before the route", !RequirementEvaluator.IsSatisfied(scenePhotoReq));
        Check(failures, report, "[E] NOT(ScenePhotographyCompleted) true before the route",
            RequirementEvaluator.IsSatisfied(Group(RequirementLogicalOperator.Not, scenePhotoReq)));
        Check(failures, report, "[D] obj-document-scene incomplete before the route", !objectives.IsCompleted("obj-document-scene"));

        esm.MarkFound("BODY-A", ToolType.None);
        yield return null;
        Check(failures, report, "[D] obj-document-scene still incomplete after BODY-A is Found", !objectives.IsCompleted("obj-document-scene"));

        // --- D/E: complete the real route through ScenePhotographyManager's own shutter handler ---
        var handleShutter = typeof(ScenePhotographyManager).GetMethod("HandleShutter", BindingFlags.NonPublic | BindingFlags.Instance);
        var tool = FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
        var cam = Camera.main;
        // The route activates its first point after an instruction delay; HandleShutter
        // starts it itself, so walk "current, else first incomplete" in order.
        foreach (var point in photos.Points)
        {
            if (point.IsCompleted)
            {
                continue;
            }
            bool beforeDone = RequirementEvaluator.IsSatisfied(scenePhotoReq);
            if (beforeDone)
            {
                Check(failures, report, "[E] requirement became true before the last point", false);
            }
            cam.transform.position = point.transform.position + Vector3.up * 1.6f;
            if (point.cameraTarget != null)
            {
                cam.transform.rotation = Quaternion.LookRotation(point.cameraTarget.position - cam.transform.position);
            }
            else
            {
                cam.transform.rotation = Quaternion.LookRotation(point.transform.forward);
            }
            handleShutter.Invoke(photos, new object[] { tool, false });
        }
        yield return null;
        Check(failures, report, $"[D] every scene photo point completed ({CountCompleted(photos)}/{photos.Points.Count})", photos.IsScenePhotographyComplete);
        Check(failures, report, "[E] ScenePhotographyCompleted true after the route", RequirementEvaluator.IsSatisfied(scenePhotoReq));
        Check(failures, report, "[D] obj-document-scene completed by the route", objectives.IsCompleted("obj-document-scene"));

        // --- Step-requirement hook: one Warn + one Block rule on BODY-A, removed afterwards ---
        // (after the route: the authored "overall scene photography first" rules on the same
        // transitions are satisfied by then, so only the test's own rules can fire.)
        var bodyDefinition = esm.GetRecord("BODY-A").definition;
        var warn = new ProcedureStepRequirement
        {
            stepId = "test-scene-photos-before-evidence-photo",
            beforeStatus = EvidenceStatus.PhotographedRaw,
            requirement = new RequirementNode { kind = RequirementNodeKind.Leaf, requirementType = RequirementType.ObjectiveCompleted, objectiveId = "obj-test-never-completes" },
            enforcement = ProcedureEnforcement.Warn,
            message = "Scene photography"
        };
        var block = new ProcedureStepRequirement
        {
            stepId = "test-block-before-marker",
            beforeStatus = EvidenceStatus.Marked,
            requirement = new RequirementNode { kind = RequirementNodeKind.Leaf, requirementType = RequirementType.ObjectiveCompleted, objectiveId = "obj-test-never-completes" },
            enforcement = ProcedureEnforcement.Block,
            message = "Test rule: marker blocked."
        };
        int authoredStepCount = bodyDefinition.stepRequirements.Count;
        bodyDefinition.stepRequirements.Add(warn);
        bodyDefinition.stepRequirements.Add(block);
        try
        {
            Check(failures, report, "[hook] Warn rule lets the raw photo through", esm.MarkPhotographedRaw("BODY-A", ToolType.None) == TransitionResult.Applied);
            Check(failures, report, "[hook] Block rule: CanTransition(Marked) false", !ProceduralGateValidator.Instance.CanTransition("BODY-A", EvidenceStatus.Marked));
            Check(failures, report, "[hook] Block rule: player-facing reason is the authored message",
                ProceduralGateValidator.Instance.GetBlockReason("BODY-A", EvidenceStatus.Marked) == "Test rule: marker blocked.");
            Check(failures, report, "[hook] Block rule refuses the marker", esm.MarkTentedWithLetter("BODY-A", ToolType.LetterTentMarker, "A") == TransitionResult.Violation);
            Check(failures, report, "[hook] status unchanged by the refused marker", esm.GetRecord("BODY-A").status == EvidenceStatus.PhotographedRaw);
        }
        finally
        {
            bodyDefinition.stepRequirements.Remove(warn);
            bodyDefinition.stepRequirements.Remove(block);
        }
        Check(failures, report, "[hook] temporary rules removed (definition back to its authored rules)", bodyDefinition.stepRequirements.Count == authoredStepCount);
        esm.MarkChalkMarked("BODY-A", ToolType.ChalkMark); // the body's outline is owed before its marker
        Check(failures, report, "[hook] marker allowed once the Block rule is gone", esm.MarkTentedWithLetter("BODY-A", ToolType.LetterTentMarker, "A") == TransitionResult.Applied);

        // --- B: bottle through the real lifecycle incl. lab station dust/lift/compare ---
        flow.SetEvidenceIdsForTest(new[] { "EVD-018" }, 0, true);
        string bottleReport = flow.RunFullFlow();
        Check(failures, report, "[B] bottle lifecycle (measurements, real brush/lift and B storage)", bottleReport.Contains("=== PASS"));
        if (!bottleReport.Contains("=== PASS")) report.AppendLine(bottleReport);
        yield return null;
        var bottle = esm.GetRecord("EVD-018");
        Check(failures, report, $"[B] bottle lab status Matched (got {bottle.fingerprintLabStatus})", bottle.fingerprintLabStatus == FingerprintLabStatus.Matched);
        Check(failures, report, "[B] bottle identifies John, Peter and Paul", bottle.fingerprintLabMatchIdentities.SequenceEqual(new[]{"John","Peter","Paul"}));
        Check(failures, report, "[B] bottle Processed", bottle.status == EvidenceStatus.Processed);
        var beat5 = FindNarrativeTrigger("beat-5-bottle-supports-brother");
        Check(failures, report, "[B] beat-5 trigger now requires the real lab match", beat5 != null && ContainsLabMatchLeaf(beat5, "EVD-018"));
        Check(failures, report, "[B] beat-5 trigger satisfied by authoritative state", beat5 != null && RequirementEvaluator.IsSatisfied(beat5));
        Check(failures, report, "[B] REL-bottle-supports-brother unlocked", RelationshipStateManager.Instance.GetRecord("REL-bottle-supports-brother")?.state >= RelationshipState.Available);

        // --- C: knife on-scene print unchanged ---
        flow.SetEvidenceIdsForTest(new[] { "EVD-014" }, 1, true);
        string knifeReport = flow.RunFullFlow();
        Check(failures, report, "[C] knife lifecycle (powder/brush/lift via FingerprintKitTool)", knifeReport.Contains("=== PASS"));
        if (!knifeReport.Contains("=== PASS")) report.AppendLine(knifeReport);
        yield return null;
        var knife = esm.GetRecord("EVD-014");
        Check(failures, report, $"[C] knife lab status Matched, identity '{knife.fingerprintLabMatchResult}'", knife.fingerprintLabStatus == FingerprintLabStatus.Matched && knife.fingerprintLabMatchResult == "Paul");
        Check(failures, report, "[C] FIND-KNIFE-PRINT-BROTHER confirmed", FindingStateManager.Instance.IsConfirmed("FIND-KNIFE-PRINT-BROTHER"));
        flow.SetEvidenceIdsForTest(new[] { "EVD-014", "EVD-015", "EVD-018" });

        // --- Session log ---
        SessionLogger.Instance?.Flush();
        string log = ReadSessionLog();
        // Count only this test's own temporary rule - the authored measure-before-final-photo
        // rules legitimately fire too when GreyboxFlowTest photographs without measuring.
        int outOfOrder = CountOccurrences(log, "\"value\":\"test-scene-photos-before-evidence-photo\"");
        Check(failures, report, $"[hook] exactly one ProcedureOutOfOrder logged by the test's Warn rule (got {outOfOrder})", outOfOrder == 1);
        Check(failures, report, "[hook] ProcedureOutOfOrder carries procedureId/evidenceId/expectedPrerequisite",
            log.Contains("test-scene-photos-before-evidence-photo") && log.Contains("\"expectedPrerequisite\""));
        Check(failures, report, "[B] beat-5 dialogue triggered", log.Contains("\"targetId\":\"beat-5-bottle-supports-brother\""));
        Check(failures, report, "[A] FIND-TEST-HARMLESS absent from the session log", !log.Contains("FIND-TEST-HARMLESS"));

        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    private static RequirementNode Group(RequirementLogicalOperator op, params RequirementNode[] children)
    {
        var node = new RequirementNode { kind = RequirementNodeKind.Group, operatorType = op };
        node.children.AddRange(children);
        return node;
    }

    private static int CountCompleted(ScenePhotographyManager photos)
    {
        int n = 0;
        foreach (var p in photos.Points)
        {
            if (p.IsCompleted) n++;
        }
        return n;
    }

    private static RequirementNode FindNarrativeTrigger(string eventId)
    {
        var field = typeof(NarrativeEventManager).GetField("eventSet", BindingFlags.NonPublic | BindingFlags.Instance);
        var set = field?.GetValue(NarrativeEventManager.Instance) as NarrativeEventSet;
        var ev = set?.events.Find(e => e.eventId == eventId);
        return ev?.trigger;
    }

    private static bool ContainsLabMatchLeaf(RequirementNode node, string evidenceId)
    {
        if (node == null) return false;
        if (node.kind == RequirementNodeKind.Leaf)
        {
            return node.requirementType == RequirementType.FingerprintLabStatusAtOrAbove && node.evidenceId == evidenceId && node.requiredLabStatus == FingerprintLabStatus.Matched;
        }
        foreach (var child in node.children)
        {
            if (ContainsLabMatchLeaf(child, evidenceId)) return true;
        }
        return false;
    }

    private static int CountOccurrences(string text, string token)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(token, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
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
