// Assets/_Project/Scripts/Testing/ScenePhotographyProcedureTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Play-mode check that the overall scene photography comes before evidence work:
/// discovery free; initial evidence photo allowed but Warned (one ProcedureOutOfOrder);
/// chalk, marker, swab, fingerprint, collection and the tape measure refused with one
/// blocked action each and no ProcedureOutOfOrder; then the real route completes
/// obj-document-scene and the evidence workflow opens. Real tools throughout (camera,
/// chalk, tent tools, swab, dust brush, bag, tape measure).
/// Run once per fresh Play session: RunAllTests(), then poll IsDone / Report.
/// </summary>
public class ScenePhotographyProcedureTest : MonoBehaviour
{
    private const string PhotoWarning = "Document the overall scene before detailed evidence photographs so each item can be reviewed in its original scene context.";
    private const string MarkerBlock = "Complete overall scene photography before placing markers so the original appearance of the scene is preserved.";
    private const string ChalkBlock = "Complete overall scene photography before marking positions so the scene is documented before anything is added or altered.";
    private const string SwabBlock = "Document the overall scene before biological sampling so the material and its surroundings are recorded before collection.";
    private const string FingerprintBlock = "Complete overall scene photography before fingerprint processing so the original condition is documented before treatment.";
    private const string CollectBlock = "Complete overall scene photography before collection so the original positions and condition of items are preserved in the record.";

    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private EvidenceStateManager esm;
    private OVRCameraRig rig;
    private PhotographTool camera;
    private List<string> failures;
    private StringBuilder report;

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(ScenePhotographyProcedureTest));
        failures = new List<string>();
        report = new StringBuilder("=== ScenePhotographyProcedureTest ===\n");
        esm = EvidenceStateManager.Instance;
        rig = FindFirstObjectByType<OVRCameraRig>();
        camera = FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
        var tents = FindFirstObjectByType<EvidenceTentTool>(FindObjectsInactive.Include);
        var letterTent = FindFirstObjectByType<LetterTentTool>(FindObjectsInactive.Include);
        var tape = FindFirstObjectByType<MeasurementTool>(FindObjectsInactive.Include);
        var photos = ScenePhotographyManager.Instance;
        if (esm == null || rig == null || camera == null || tents == null || letterTent == null || tape == null || photos == null
            || SwabTool.Instance == null || FingerprintKitTool.Instance == null || EvidenceBagTool.Instance == null || ChalkMarkTool.Instance == null)
        {
            Report = "FAIL: required objects missing.";
            IsDone = true;
            yield break;
        }
        yield return null;
        yield return null;
        Check("[start] scene photography not complete, obj-document-scene open",
            !photos.IsScenePhotographyComplete && !ObjectiveStateManager.Instance.IsCompleted("obj-document-scene"));

        // --- Discovery: allowed, no block ---
        int blocked0 = Count("\"eventType\":\"EvidenceTransitionBlocked\"");
        bool found = true;
        foreach (var id in new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" })
        {
            found &= esm.MarkFound(id, ToolType.None) == TransitionResult.Applied;
        }
        yield return DismissCheckpoints();
        Check("[discovery] all four discovered before scene photography, nothing blocked, no warning",
            found && Count("\"eventType\":\"EvidenceTransitionBlocked\"") == blocked0 && Count("\"eventType\":\"ProcedureOutOfOrder\"") == 0);

        // --- Initial evidence photo: allowed, Warned once ---
        Photo("EVD-014");
        Check("[raw photo] knife photographed (allowed)", esm.GetRecord("EVD-014").status == EvidenceStatus.PhotographedRaw);
        Check("[raw photo] warning shown", camera.LastProcedureWarning == PhotoWarning);
        Check("[raw photo] exactly one ProcedureOutOfOrder for the knife's photo",
            CountLines("ProcedureOutOfOrder", "EVD-014") == 1 && LogContains("\"value\":\"scene-photos-before-evidence-photo\""));
        Photo("EVD-014");
        Check("[raw photo] a second shutter press adds no warning", CountLines("ProcedureOutOfOrder", "EVD-014") == 1 && camera.LastProcedureWarning == null);
        Photo("EVD-015");
        Photo("BODY-A");
        Photo("EVD-018");
        yield return null;
        Check("[raw photo] one warning per item, nothing blocked", Count("\"eventType\":\"ProcedureOutOfOrder\"") == 4 && Count("\"eventType\":\"EvidenceTransitionBlocked\"") == blocked0);

        // From here every refusal must add exactly one blocked action and no ProcedureOutOfOrder.
        int outOfOrder = Count("\"eventType\":\"ProcedureOutOfOrder\"");

        // --- Chalk (knife) ---
        var guide = EvidenceChalkGuide.FindActive("EVD-014");
        int blocked = Count("\"eventType\":\"EvidenceTransitionBlocked\"");
        bool traced = false;
        for (int p = 0; guide != null && p < guide.PointCount; p++)
        {
            traced |= ChalkMarkTool.Instance.TryTraceAt(guide.GetPointPosition(p));
        }
        Check("[chalk] knife outline refused: nothing traced, still PhotographedRaw",
            guide != null && !traced && guide.CoveredCount == 0 && esm.GetRecord("EVD-014").status == EvidenceStatus.PhotographedRaw);
        Check("[chalk] reason", esm.GetItemBlockReason("EVD-014", EvidenceStatus.ChalkMarked) == ChalkBlock);
        Check("[chalk] one blocked action for the whole press", Count("\"eventType\":\"EvidenceTransitionBlocked\"") - blocked == 1);

        // --- Markers ---
        foreach (var id in new[] { "EVD-014", "EVD-015", "EVD-018" })
        {
            // Reserved evidence numbers change the preview as its target changes.
            // Denial must instead preserve actual allocation and evidence state.
            int placedBefore = tents.PlacedCount;
            blocked = Count("\"eventType\":\"EvidenceTransitionBlocked\"");
            Equip(ToolType.EvidenceMarker);
            AimOrigin(tents, id);
            typeof(EvidenceTentTool).GetMethod("PlaceTent", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tents, null);
            Check($"[marker] {id}: no tent placed, state unchanged, one blocked action",
                tents.PlacedCount == placedBefore && !esm.GetRecord(id).tentNumber.HasValue
                && esm.GetRecord(id).status == EvidenceStatus.PhotographedRaw
                && Count("\"eventType\":\"EvidenceTransitionBlocked\"") - blocked == 1);
            Check($"[marker] {id}: reason", ProceduralGateValidator.Instance.GetBlockReason(id, EvidenceStatus.Marked) == MarkerBlock);
        }
        blocked = Count("\"eventType\":\"EvidenceTransitionBlocked\"");
        Equip(ToolType.LetterTentMarker);
        AimOrigin(letterTent, "BODY-A");
        Check("[marker] BODY-A letter marker refused, state unchanged, one blocked action",
            !letterTent.TryPlaceLetterTent() && esm.GetRecord("BODY-A").status == EvidenceStatus.PhotographedRaw
            && Count("\"eventType\":\"EvidenceTransitionBlocked\"") - blocked == 1);

        // --- Tape measure can't bypass the marker stage ---
        Equip(ToolType.Measurement);
        MeasurementGeometry.TryGetEvidenceAnchor("EVD-014", out var knifeAnchor);
        PlaceTip(tape, knifeAnchor + Vector3.up + Vector3.forward * 0.05f, knifeAnchor);
        tape.HandleTrigger();
        Check("[measure] tape refuses an unmarked item", tape.CurrentPhase == MeasurementTool.Phase.SelectTarget && tape.LastMessage == "Place an evidence marker before measuring.");

        // --- Swab (knife trace, spatter) ---
        foreach (var id in new[] { "EVD-014", "EVD-015" })
        {
            blocked = Count("\"eventType\":\"EvidenceTransitionBlocked\"");
            bool sampled = SwabTool.Instance.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId(id));
            Check($"[swab] {id}: refused, nothing sampled, one blocked action",
                !sampled && !SwabTool.Instance.HasSample && !esm.GetRecord(id).swabbingDone && Count("\"eventType\":\"EvidenceTransitionBlocked\"") - blocked == 1);
            Check($"[swab] {id}: reason, and the authoritative step refuses too",
                esm.GetActionBlockReason(id, ProcedureAction.BiologicalSampling) == SwabBlock && esm.MarkSwabbed(id, ToolType.Swab) == TransitionResult.Violation);
        }

        // --- Fingerprint (knife print; bottle rule present) ---
        var printMarker = EvidenceFingerprintMarker.FindByEvidenceId("EVD-014");
        FingerprintKitTool.Instance.TryLoadDust();
        blocked = Count("\"eventType\":\"EvidenceTransitionBlocked\"");
        bool dusted = FingerprintKitTool.Instance.TryDust(printMarker);
        FingerprintKitTool.Instance.TryDust(printMarker);
        Check("[fingerprint] knife: no powder applied, nothing revealed or fingerprinted, one blocked action",
            !dusted && !printMarker.IsDusted && !esm.GetRecord("EVD-014").fingerprintingDone
            && Count("\"eventType\":\"EvidenceTransitionBlocked\"") - blocked == 1);
        Check("[fingerprint] reason (scene first, ahead of the swab-first rule)",
            esm.GetFingerprintBlockReason("EVD-014") == FingerprintBlock && esm.GetFingerprintBlockReason("EVD-018") == FingerprintBlock);

        // --- Collection ---
        blocked = Count("\"eventType\":\"EvidenceTransitionBlocked\"");
        var insert = EvidenceBagTool.Instance.TryInsert(EvidenceProp.FindById("EVD-018"));
        Check("[collect] bottle into the bag refused, still on scene",
            insert == TransitionResult.Violation && esm.GetRecord("EVD-018").custodyLocation == EvidenceCustodyLocation.Scene
            && esm.GetRecord("EVD-018").status == EvidenceStatus.PhotographedRaw);
        Check("[collect] reason from the central gate",
            ProceduralGateValidator.Instance.GetBlockReason("EVD-018", EvidenceStatus.Collected) == CollectBlock
            && !ProceduralGateValidator.Instance.CanCollect("EVD-014"));
        Check("[collect] body stays non-collectible", ProceduralGateValidator.Instance.GetBlockReason("BODY-A", EvidenceStatus.Collected) != null);

        Check("[log] blocked actions never also logged as ProcedureOutOfOrder", Count("\"eventType\":\"ProcedureOutOfOrder\"") == outOfOrder);

        // --- Complete the overall scene photography ---
        Check("[route] all 7 points completed", ScenePhotographyTestUtil.CompleteRoute() && CountCompleted(photos) == photos.Points.Count && photos.Points.Count == 7);
        yield return null;
        Check("[route] obj-document-scene completed", ObjectiveStateManager.Instance.IsCompleted("obj-document-scene"));
        var completionMessage = GameDialogue.Text("photo-scene-complete");
        Check($"[route] transition message logged on completion ('{completionMessage}')",
            !string.IsNullOrEmpty(completionMessage) && LogContains("\"eventType\":\"ScenePhotographyCompleted\""));

        // --- Workflow now open ---
        for (int p = 0; p < guide.PointCount && !guide.IsComplete; p++)
        {
            ChalkMarkTool.Instance.TryTraceAt(guide.GetPointPosition(p));
        }
        Check("[after] knife chalk outline now completes", esm.GetRecord("EVD-014").status == EvidenceStatus.ChalkMarked);
        // The bottle's position is outlined before its tent too (EvidenceDefinition.requiresChalk).
        var bottleGuide = EvidenceChalkGuide.FindActive("EVD-018");
        for (int p = 0; bottleGuide != null && p < bottleGuide.PointCount && !bottleGuide.IsComplete; p++)
        {
            ChalkMarkTool.Instance.TryTraceAt(bottleGuide.GetPointPosition(p));
        }
        Check("[after] bottle chalk outline completes", esm.GetRecord("EVD-018").status == EvidenceStatus.ChalkMarked);
        foreach (var id in new[] { "EVD-015", "BODY-A" })
        {
            var g = EvidenceChalkGuide.FindActive(id);
            for (int p = 0; g != null && p < g.PointCount && !g.IsComplete; p++)
            {
                ChalkMarkTool.Instance.TryTraceAt(g.GetPointPosition(p));
            }
            Check($"[after] {id} chalk outline completes", esm.GetRecord(id).status == EvidenceStatus.ChalkMarked);
        }
        foreach (var id in new[] { "EVD-014", "EVD-015", "EVD-018" })
        {
            Equip(ToolType.EvidenceMarker);
            AimOrigin(tents, id);
            typeof(EvidenceTentTool).GetMethod("PlaceTent", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tents, null);
            Check($"[after] {id}: marker placed", esm.GetRecord(id).status == EvidenceStatus.Marked);
        }
        Equip(ToolType.LetterTentMarker);
        AimOrigin(letterTent, "BODY-A");
        Check("[after] BODY-A letter marker placed", letterTent.TryPlaceLetterTent() && esm.GetRecord("BODY-A").status == EvidenceStatus.Marked);
        Check("[after] scene route gate clears while measurement and sketch remain required",
            esm.GetActionBlockReason("EVD-015", ProcedureAction.BiologicalSampling) != SwabBlock
            && esm.GetActionBlockReason("EVD-014", ProcedureAction.FingerprintProcessing) != FingerprintBlock
            && CrimeSceneSketchManager.GetProcessingBlockReason("EVD-015") != null
            && CrimeSceneSketchManager.GetProcessingBlockReason("EVD-014") != null);

        HeldItemCoordinator.Instance.HolsterCurrentTool();
        // Phase 1 keeps the existing upload shape, including warnings and blocks.
        SessionLogger.Instance.Flush();
        var config = ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();
        config.uploadRawEventLogEnabled = true;
        config.maxEventsPerRequest = 5000;
        var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
        scenario.scoringRulesVersion = "1.6.0";
        var dto = AssessmentSessionBuilder.Build(SessionLogger.Instance.ActiveLogFilePath, config, scenario, null, null);
        Check("[phase1 payload] unchanged schema with explicit scoring 1.6.0", dto.payloadVersion == 1 && dto.scoringRulesVersion == "1.6.0");
        Check("[phase1 payload] all ProcedureOutOfOrder events preserved", dto.orderedEvents.FindAll(e => e.eventType == "ProcedureOutOfOrder").Count == outOfOrder);
        Check("[phase1 payload] blocked attempts and count retained", dto.proceduralViolations.Count > 0 && dto.clientReportedScores.proceduralViolationCount == dto.proceduralViolations.Count);
        Check("[phase1 payload] completion, photography and measurement analytics retained", dto.evidenceResults.Count > 0 && dto.hasScenePhotography && dto.hasMeasurement && dto.scenePhotography.completedPointCount == 7);
        var screen = FindAnyObjectByType<CompletionScreenUI>(FindObjectsInactive.Include);
        string photoReview = (string)typeof(CompletionScreenUI).GetMethod("BuildScenePhotographyPage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
        string measurementReview = (string)typeof(CompletionScreenUI).GetMethod("BuildMeasurementPage", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, null);
        Check("[phase1 UI] procedure pages are training with no score award", photoReview.Contains("Training") && measurementReview.Contains("Training") && !photoReview.Contains("Score:") && !measurementReview.Contains("Score:"));
        var scorer = FindAnyObjectByType<DeductionScorer>(FindObjectsInactive.Include);
        var board = FindAnyObjectByType<EvidenceBoardController>(FindObjectsInactive.Include);
        var before = scorer.Score(board);
        yield return new WaitForSeconds(.05f);
        var after = scorer.Score(board);
        Check("[phase1 deduction] elapsed training time cannot lower the board score", before.total == after.total && before.elapsedSeconds == after.elapsedSeconds && after.elapsedSeconds == 0f);
        scorer.BeginReasoning();
        scorer.BeginReasoning(); // Reopening an already-open board must not restart time.
        yield return new WaitForSeconds(.05f);
        scorer.EndReasoning();
        var reasoning = scorer.Score(board);
        yield return new WaitForSeconds(.05f);
        var paused = scorer.Score(board);
        Check("[phase1 deduction] reasoning time runs only during board review", reasoning.elapsedSeconds > 0f && paused.elapsedSeconds == reasoning.elapsedSeconds);
        Destroy(config);
        Destroy(scenario);
        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    // --- Real tools -------------------------------------------------------

    private void Equip(ToolType role)
    {
        if (PlayerToolRegistry.VirtuallyEquippedRole != role)
        {
            HeldItemCoordinator.Instance.ToggleEquipTool(role, rig.rightHandAnchor);
        }
    }

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

    private static void AimOrigin(Component tool, string id)
    {
        var origin = tool.GetType().GetField("placementOrigin", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tool) as Transform;
        Vector3 target = EvidenceProp.FindById(id).transform.position;
        Vector3 from = target + Vector3.up * 0.6f;
        var t = tool.transform;
        t.rotation = Quaternion.FromToRotation(origin.forward, Vector3.down) * t.rotation;
        t.position += from - origin.position;
    }

    private static void PlaceTip(MeasurementTool tape, Vector3 tipWorld, Vector3 lookAt)
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

    private static int CountCompleted(ScenePhotographyManager photos)
    {
        int n = 0;
        foreach (var p in photos.Points)
        {
            if (p.IsCompleted) n++;
        }
        return n;
    }

    // --- Log -------------------------------------------------------------------

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

    private static int Count(string token)
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

    private static int CountLines(string eventType, string targetId)
    {
        int n = 0;
        foreach (var line in ReadSessionLog().Split('\n'))
        {
            if (line.Contains("\"eventType\":\"" + eventType + "\"") && line.Contains("\"targetId\":\"" + targetId + "\""))
            {
                n++;
            }
        }
        return n;
    }

    private void Check(string label, bool passed)
    {
        report.AppendLine($"  {(passed ? "PASS" : "FAIL")}: {label}");
        if (!passed) failures.Add(label);
    }
}
