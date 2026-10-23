// Assets/_Project/Scripts/Testing/ChalkProcedureTest.cs
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>
/// Play-mode check of per-item chalk (EvidenceDefinition.requiresChalk). Drives the real
/// tools: PhotographTool (own aim update + shutter), EvidenceTentTool / LetterTentTool
/// (own placement raycast from their placementOrigin), ChalkMarkTool (guide trace and
/// the unneeded-chalk answer), MeasurementTool (trigger handler); then each item's
/// remaining procedure via GreyboxFlowTest resumed from where it stands.
/// Run once per fresh Play session: RunAllTests(), then poll IsDone / Report.
/// </summary>
public class ChalkProcedureTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }

    private EvidenceStateManager esm;
    private OVRCameraRig rig;
    private PhotographTool camera;
    private MeasurementTool tape;
    private EvidenceTentTool tents;
    private LetterTentTool letterTent;
    private List<string> failures;
    private StringBuilder report;

    public void RunAllTests()
    {
        IsDone = false;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        failures = new List<string>();
        report = new StringBuilder("=== ChalkProcedureTest ===\n");
        esm = EvidenceStateManager.Instance;
        rig = FindFirstObjectByType<OVRCameraRig>();
        camera = FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
        tape = FindFirstObjectByType<MeasurementTool>(FindObjectsInactive.Include);
        tents = FindFirstObjectByType<EvidenceTentTool>(FindObjectsInactive.Include);
        letterTent = FindFirstObjectByType<LetterTentTool>(FindObjectsInactive.Include);
        var flow = FindFirstObjectByType<GreyboxFlowTest>(FindObjectsInactive.Include);
        if (esm == null || rig == null || camera == null || tape == null || tents == null || letterTent == null || flow == null || ChalkMarkTool.Instance == null)
        {
            Report = "FAIL: required objects missing.";
            IsDone = true;
            yield break;
        }
        yield return null;

        // --- Configuration (data, not ids in code) ---
        Check("[data] BODY-A requiresChalk = true (outline preserves its original position)", Def("BODY-A").requiresChalk);
        Check("[data] EVD-014 requiresChalk = true", Def("EVD-014").requiresChalk);
        Check("[data] EVD-015 requiresChalk = true (stain boundary outlined before sampling)", Def("EVD-015").requiresChalk);
        Check("[data] EVD-018 requiresChalk = true (movable, later bagged: its position is outlined first)", Def("EVD-018").requiresChalk);

        foreach (var id in new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" })
        {
            esm.MarkFound(id, ToolType.None);
        }
        yield return DismissCheckpoints();
        Check("[scene] overall scene photography completed before evidence work", ScenePhotographyTestUtil.CompleteRoute());
        yield return null;

        foreach (var id in new[] { "BODY-A", "EVD-014", "EVD-015", "EVD-018" })
        {
            Photo(id);
            Check($"[{id}] initial photo", esm.GetRecord(id).status == EvidenceStatus.PhotographedRaw);
        }
        yield return null;
        Check("[guides] a chalk guide for every active item",
            EvidenceChalkGuide.FindActive("EVD-014") != null && EvidenceChalkGuide.FindActive("EVD-018") != null
            && EvidenceChalkGuide.FindActive("EVD-015") != null && EvidenceChalkGuide.FindActive("BODY-A") != null);
        var bodyGuide = EvidenceChalkGuide.FindActive("BODY-A");
        var spatterGuide = EvidenceChalkGuide.FindActive("EVD-015");
        Check($"[guides] body outline: {bodyGuide.PointCount} points on the floor around the body's footprint",
            bodyGuide.PointCount >= 24 && GuideOnPlane(bodyGuide, SupportingSurfaceHeight("BODY-A")) && GuideEncloses(bodyGuide, EvidenceProp.FindById("BODY-A").transform.position));
        Check($"[guides] spatter outline: {spatterGuide.PointCount} points flat on the floor around the stain",
            spatterGuide.PointCount >= 12 && GuideOnPlane(spatterGuide, SupportingSurfaceHeight("EVD-015")) && GuideEncloses(spatterGuide, EvidenceProp.FindById("EVD-015").transform.position));
        var bottleGuide = EvidenceChalkGuide.FindActive("EVD-018");
        Vector3 bottlePos = EvidenceProp.FindById("EVD-018").transform.position;
        Check($"[guides] bottle's outline lies on the counter top around its base (centre {bottleGuide.transform.position:F3})",
            GuideOnPlane(bottleGuide, SupportingSurfaceHeight("EVD-018"))
            && new Vector2(bottleGuide.transform.position.x - bottlePos.x, bottleGuide.transform.position.z - bottlePos.z).magnitude < 0.01f);

        // --- Wrong target: tracing one outline never advances its neighbour ---
        // The spatter's outline lies inside the body's (they come within a few cm on the
        // west side) and the knife's circle sits near the body's north-west edge: each stroke
        // must credit only the outline it is actually on (EvidenceChalkGuide.FindByNearestPoint).
        int bodyCoveredBefore = bodyGuide.CoveredCount;
        int knifeCoveredBefore = EvidenceChalkGuide.FindActive("EVD-014").CoveredCount;
        for (int p = 0; p < spatterGuide.PointCount && !spatterGuide.IsComplete; p++)
        {
            ChalkMarkTool.Instance.TryTraceAt(spatterGuide.GetPointPosition(p));
        }
        Check($"[no fake progress] tracing the spatter outline completes the spatter only (body covered {bodyGuide.CoveredCount}, was {bodyCoveredBefore})",
            esm.GetRecord("EVD-015").status == EvidenceStatus.ChalkMarked && esm.GetRecord("BODY-A").status == EvidenceStatus.PhotographedRaw
            && !bodyGuide.IsComplete && bodyGuide.CoveredCount == bodyCoveredBefore
            && EvidenceChalkGuide.FindActive("EVD-014").CoveredCount == knifeCoveredBefore);

        // --- Body: letter marker before chalk is refused; chalk, then the letter marker ---
        Equip(ToolType.LetterTentMarker);
        AimOrigin(letterTent, "BODY-A");
        Check("[body] letter marker before chalk refused: still PhotographedRaw, reason given",
            !letterTent.TryPlaceLetterTent() && esm.GetRecord("BODY-A").status == EvidenceStatus.PhotographedRaw
            && ProceduralGateValidator.Instance.GetBlockReason("BODY-A", EvidenceStatus.Marked) == "Mark the evidence position with chalk first.");
        for (int p = 0; p < bodyGuide.PointCount && !bodyGuide.IsComplete; p++)
        {
            ChalkMarkTool.Instance.TryTraceAt(bodyGuide.GetPointPosition(p));
        }
        Check("[body] chalk outline -> ChalkMarked (only the chalk step)", esm.GetRecord("BODY-A").status == EvidenceStatus.ChalkMarked);
        Equip(ToolType.LetterTentMarker);
        AimOrigin(letterTent, "BODY-A");
        Check("[body] letter marker A after chalk", letterTent.TryPlaceLetterTent() && esm.GetRecord("BODY-A").status == EvidenceStatus.Marked);
        // --- Knife: tent before chalk is refused ---
        int placedBefore = tents.PlacedCount;
        Equip(ToolType.EvidenceMarker);
        PlaceNumberedTentAt("EVD-014");
        Check("[knife] tent before chalk refused: no tent placed, still PhotographedRaw",
            tents.PlacedCount == placedBefore && !esm.GetRecord("EVD-014").tentNumber.HasValue && esm.GetRecord("EVD-014").status == EvidenceStatus.PhotographedRaw);
        Check("[knife] reason is 'Mark the evidence position with chalk first.'",
            ProceduralGateValidator.Instance.GetBlockReason("EVD-014", EvidenceStatus.Marked) == "Mark the evidence position with chalk first.");

        // chalk -> tent -> measurement -> final photo
        var guide = EvidenceChalkGuide.FindActive("EVD-014");
        for (int p = 0; p < guide.PointCount && !guide.IsComplete; p++)
        {
            ChalkMarkTool.Instance.TryTraceAt(guide.GetPointPosition(p));
        }
        Check("[knife] chalk outline -> ChalkMarked", esm.GetRecord("EVD-014").status == EvidenceStatus.ChalkMarked);
        Equip(ToolType.EvidenceMarker);
        PlaceNumberedTentAt("EVD-014");
        Check("[knife] tent after chalk -> Marked", esm.GetRecord("EVD-014").status == EvidenceStatus.Marked);

        // --- Bottle: same chalk procedure as the knife ---
        placedBefore = tents.PlacedCount;
        Equip(ToolType.EvidenceMarker);
        PlaceNumberedTentAt("EVD-018");
        Check("[bottle] tent before chalk refused: no tent placed, still PhotographedRaw",
            tents.PlacedCount == placedBefore && !esm.GetRecord("EVD-018").tentNumber.HasValue && esm.GetRecord("EVD-018").status == EvidenceStatus.PhotographedRaw
            && ProceduralGateValidator.Instance.GetBlockReason("EVD-018", EvidenceStatus.Marked) == "Mark the evidence position with chalk first.");
        for (int p = 0; p < bottleGuide.PointCount && !bottleGuide.IsComplete; p++)
        {
            ChalkMarkTool.Instance.TryTraceAt(bottleGuide.GetPointPosition(p));
        }
        Check("[bottle] chalk outline -> ChalkMarked (only the chalk step)", esm.GetRecord("EVD-018").status == EvidenceStatus.ChalkMarked);
        Equip(ToolType.EvidenceMarker);
        PlaceNumberedTentAt("EVD-018");
        Check("[bottle] tent after chalk -> Marked", esm.GetRecord("EVD-018").status == EvidenceStatus.Marked);
        // The body and bottle need their final photographs before measurements.
        // Prepare the bottle before the knife's final photo needs their pair distance.
        yield return MeasureThenFinalPhoto("BODY-A");
        yield return MeasureThenFinalPhoto("EVD-018");
        yield return MeasureThenFinalPhoto("EVD-014");

        // --- Spatter: chalked above (stain boundary), then its tent ---
        Equip(ToolType.EvidenceMarker);
        PlaceNumberedTentAt("EVD-015");
        Check("[spatter] tent after chalk -> Marked, ChalkMarked held first",
            esm.GetRecord("EVD-015").status == EvidenceStatus.Marked && LoggedStatus("EVD-015", "ChalkMarked") && LoggedStatus("EVD-015", "Marked"));
        yield return MeasureThenFinalPhoto("EVD-015");

        // Evidence records wait for the discovered evidence's measurements (fixed references + pairs among discovered items).
        MeasurementTestUtil.CompleteDiscoveredMeasurements();
        MeasurementTestUtil.CompleteSketchFor("BODY-A");
        esm.RecordReportLinesSelected("BODY-A", new[] { "body-a-location", "body-a-position", "body-a-wound-sharp-object" });
        Check("[body] Recorder after the final photo; never collected",
            esm.MarkLogged("BODY-A", ToolType.Recorder) == TransitionResult.Applied && esm.GetRecord("BODY-A").custodyLocation == EvidenceCustodyLocation.Scene);

        // --- Rest of each procedure through the real tools ---
        int tent = 10;
        foreach (var id in new[] { "EVD-014", "EVD-015", "EVD-018" })
        {
            yield return GloveChangeTestUtil.VisitAndReplace();
            flow.SetEvidenceIdsForTest(new[] { id }, tent++, true, resume: true);
            string flowReport = flow.RunFullFlow();
            Check($"[{id}] Recorder / swab / fingerprint / collect / bag / seal -> Processed", flowReport.Contains("=== PASS") && esm.GetRecord(id).status == EvidenceStatus.Processed);
            if (!flowReport.Contains("=== PASS")) report.AppendLine(flowReport);
            yield return DismissCheckpoints();
        }
        flow.SetEvidenceIdsForTest(new[] { "EVD-014", "EVD-015", "EVD-018" });

        var knife = esm.GetRecord("EVD-014");
        var bottle = esm.GetRecord("EVD-018");
        Check("[knife] swab + print matched (Paul)", knife.swabbingDone && knife.fingerprintLabMatchResult == "Paul");
        Check("[bottle] lab print matched (Paul)", bottle.fingerprintLabStatus == FingerprintLabStatus.Matched && bottle.fingerprintLabMatchResult == "Paul");
        Check("[findings] knife print, wound and spatter findings confirmed",
            FindingStateManager.Instance.IsConfirmed("FIND-KNIFE-PRINT-BROTHER") && FindingStateManager.Instance.IsConfirmed("FIND-BODY-SHARP-WOUND"));
        Check("[relationships] bottle-brother relationship unlocked", RelationshipStateManager.Instance.GetRecord("REL-bottle-supports-brother")?.state >= RelationshipState.Available);
        Check("[log] no ProcedureOutOfOrder (each item follows its own photograph/measurement order)", CountInLog("\"eventType\":\"ProcedureOutOfOrder\"") == 0);
        Check("[notes] every chalked item's note says 'Position outlined'",
            EvidenceNoteComposer.Compose(esm.GetRecord("EVD-015")).Contains("Position outlined") && EvidenceNoteComposer.Compose(esm.GetRecord("BODY-A")).Contains("Position outlined")
            && EvidenceNoteComposer.Compose(knife).Contains("Position outlined") && EvidenceNoteComposer.Compose(bottle).Contains("Position outlined"));

        HeldItemCoordinator.Instance.HolsterCurrentTool();
        report.AppendLine(failures.Count == 0 ? "=== PASS, 0 failures ===" : $"=== FAIL: {failures.Count} failure(s) ===");
        Report = report.ToString();
        IsDone = true;
    }

    // Every guide point lies at floor height (within 2 cm).
    private static float SupportingSurfaceHeight(string id)
    {
        var prop=EvidenceProp.FindById(id);
        var hits=Physics.RaycastAll(prop.transform.position+Vector3.up*.1f,Vector3.down,5f,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
            if(hit.normal.y>.9f && hit.collider.GetComponentInParent<EvidenceProp>()==null
                && hit.collider.GetComponentInParent<OVRCameraRig>()==null) return hit.point.y;
        return float.PositiveInfinity;
    }

    private static bool GuideOnPlane(EvidenceChalkGuide guide, float y)
    {
        for (int i = 0; i < guide.PointCount; i++)
        {
            if (Mathf.Abs(guide.GetPointPosition(i).y - y) > 0.02f) return false;
        }
        return true;
    }

    // The item's position lies inside the outline's floor-plan bounding box.
    private static bool GuideEncloses(EvidenceChalkGuide guide, Vector3 item)
    {
        Vector3 p0 = guide.GetPointPosition(0);
        float minX = p0.x, maxX = p0.x, minZ = p0.z, maxZ = p0.z;
        for (int i = 1; i < guide.PointCount; i++)
        {
            var q = guide.GetPointPosition(i);
            minX = Mathf.Min(minX, q.x); maxX = Mathf.Max(maxX, q.x); minZ = Mathf.Min(minZ, q.z); maxZ = Mathf.Max(maxZ, q.z);
        }
        return item.x > minX && item.x < maxX && item.z > minZ && item.z < maxZ;
    }

    private IEnumerator MeasureThenFinalPhoto(string id)
    {
        var definition=esm.GetRecord(id).definition;
        bool finalFirst = definition.requiresBodyRemoval || definition.measurementAfterFinalPhoto;
        if (finalFirst) { Photo(id); yield return null; }
        Equip(ToolType.Measurement);
        MeasureSide(id, MeasurementTestUtil.Ref(id, 1));
        MeasureSide(id, MeasurementTestUtil.Ref(id, 2));
        MeasurementTestUtil.RecordAvailablePairsFor(id); // evidence distances to the items already discovered
        if (!finalFirst) Photo(id);
        yield return null;
        Check($"[{id}] documentation follows its photograph/measurement order without warning",
            MeasurementStateManager.Instance.IsMeasurementComplete("MEAS-" + id) && esm.GetRecord(id).status == EvidenceStatus.PhotographedFinal && camera.LastProcedureWarning == null);
    }

    // --- Real tools -------------------------------------------------------

    private EvidenceDefinition Def(string id) => esm.GetRecord(id).definition;

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

    // Point a tent tool's own placementOrigin straight down at the item, from 0.6 m above.
    private static void AimOrigin(Component tool, string id)
    {
        var origin = tool.GetType().GetField("placementOrigin", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tool) as Transform;
        Vector3 target = EvidenceProp.FindById(id).transform.position;
        Vector3 from = target + Vector3.up * 0.6f;
        var t = tool.transform;
        t.rotation = Quaternion.FromToRotation(origin.forward, Vector3.down) * t.rotation;
        t.position += from - origin.position;
    }

    private void PlaceNumberedTentAt(string id)
    {
        AimOrigin(tents, id);
        typeof(EvidenceTentTool).GetMethod("PlaceTent", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tents, null);
    }

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

    // Whether an EvidenceStatusChanged event for evidenceId with this status was logged.
    private static bool LoggedStatus(string evidenceId, string status)
    {
        foreach (var line in ReadSessionLog().Split('\n'))
        {
            if (line.Contains("\"eventType\":\"EvidenceStatusChanged\"") && line.Contains("\"targetId\":\"" + evidenceId + "\"")
                && line.Contains("\"value\":\"" + status + "\""))
            {
                return true;
            }
        }
        return false;
    }

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
