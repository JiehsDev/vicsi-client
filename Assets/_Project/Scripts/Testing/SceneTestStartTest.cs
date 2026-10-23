#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Fresh-scene integration checks for every manual preset and the real restart path.</summary>
public sealed class SceneTestStartTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== SceneTestStartTest ===\n");
    private int failures;
    private void Check(string label, bool value)
    { report.AppendLine((value ? "PASS: " : "FAIL: ") + label); if (!value) failures++; }
    public void RunAllTests() { DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
    private IEnumerator Load(SceneTestStartingPoint point)
    {
        SessionState.SetInt(SceneTestStart.EditorSelectionKey, (int)point);
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/_Project/Scenes/Scenarios/CSI_Environment.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 5; i++) yield return null;
        double deadline = Time.realtimeSinceStartupAsDouble + 15;
        while (SceneTestStart.IsPreparing && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
    }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(SceneTestStartTest));
        BatchLocomotionFramePump.Activate();
        int original = SessionState.GetInt(SceneTestStart.EditorSelectionKey, -1);
        int queue = AutomatedTestSandbox.RealQueueEntryCount();
        int owners = AutomatedTestSandbox.RealOwnerRecordCount();
        SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic", true);
        try
        {
            Check("Menu contains every preset once without changing serialized IDs", SceneTestStart.MenuOrder.Distinct().Count()==System.Enum.GetValues(typeof(SceneTestStartingPoint)).Length
                && SceneTestStart.MenuOrder.Length==System.Enum.GetValues(typeof(SceneTestStartingPoint)).Length
                && (int)SceneTestStartingPoint.CustodyPass==11 && (int)SceneTestStartingPoint.KnifeBagging==7);
            foreach (SceneTestStartingPoint point in System.Enum.GetValues(typeof(SceneTestStartingPoint)))
            {
                yield return Load(point);
                string label = SceneTestStart.Label(point);
                var start = SceneTestStart.Instance;
                var esm = EvidenceStateManager.Instance;
                var photos = ScenePhotographyManager.Instance;
                var sketch = CrimeSceneSketchManager.Instance;
                var measures = MeasurementStateManager.Instance;
                var milestone = point == SceneTestStartingPoint.KnifeBagging || point == SceneTestStartingPoint.BottleFingerprinting
                    || point == SceneTestStartingPoint.BodyRecording || point == SceneTestStartingPoint.AllEvidenceMeasured
                    || point == SceneTestStartingPoint.KnifeScalePhotography || point == SceneTestStartingPoint.BloodstainScalePhotography
                    || point == SceneTestStartingPoint.BottleScalePhotography
                    ? SceneTestStartingPoint.PhotographyDone : point == SceneTestStartingPoint.CustodyPass || point == SceneTestStartingPoint.FingerprintComparison
                        ? SceneTestStartingPoint.AllEvidenceProceduresDone : point;
                Check(label + ": correct preset, ready, no setup failure", start != null && start.ActivePoint == point
                    && (point == SceneTestStartingPoint.NormalPlay || start.IsReady) && start.Failure == null);
                if (start == null || start.Failure != null) { report.AppendLine("INFO: " + start?.Failure); continue; }
                if (point == SceneTestStartingPoint.NormalPlay)
                {
                    Check(label + ": IOC gate and evidence untouched", !SceneTestStart.IsActive
                        && SceneTurnover.Phase == ScenarioPhase.IOCTurnover
                        && esm.AllRecords.All(r => r.status == EvidenceStatus.NotFound)
                        && !photos.IsScenePhotographyComplete && sketch.State == SketchState.NotStarted);
                    continue;
                }
                Check(label + ": sandbox before uploader/logging and no student owner", AutomatedTestSandbox.IsActive
                    && SessionLogger.Instance.ActiveLogFilePath.StartsWith(AutomatedTestSandbox.Root)
                    && PreSessionUnlocked());
                Check(label + ": correct scene phase", point == SceneTestStartingPoint.InvestigationSubmitted || point == SceneTestStartingPoint.CustodyPass
                    ? SceneTurnover.Phase == ScenarioPhase.SOCOCloseout || SceneTurnover.Phase == ScenarioPhase.EvidenceInventoryReview
                    : SceneTurnover.Phase == ScenarioPhase.SOCOInvestigation);
                Check(label + ": photography matches milestone", photos.IsScenePhotographyComplete
                    == (milestone >= SceneTestStartingPoint.PhotographyDone));
                Vector3 expectedFeet = point == SceneTestStartingPoint.IOCDone ? photos.Points.First().transform.position
                    : new Vector3(15.55f, 1.2f, 10.8f);
                var controller = FindFirstObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
                Vector3 actualFeet = controller.Pose.position - Vector3.up * controller.Height * .5f;
                Vector3 eye = Camera.main.transform.position;
                if (point == SceneTestStartingPoint.CustodyPass)
                {
                    expectedFeet = EvidenceBoxDepositZone.Instance.transform.position + EvidenceBoxDepositZone.Instance.transform.forward * .4f;
                    expectedFeet.y = start.PreparedFeet.y; // Ground ray ran before teleport; do not raycast into the arriving capsule.
                }
                if (point == SceneTestStartingPoint.FingerprintComparison)
                {
                    expectedFeet = FingerprintComparisonStation.Instance.transform.position + Vector3.back * .9f;
                    expectedFeet.y = start.PreparedFeet.y;
                }
                report.AppendLine("INFO: " + label + " eye=" + eye.ToString("F4") + " feet=" + actualFeet.ToString("F4")
                    + " expected=" + expectedFeet.ToString("F4") + " gloves=" + FindAnyObjectByType<SceneCordon>().PPEReady
                    + " entryReturns=" + FindAnyObjectByType<SceneCordon>().EntryReturns);
                Check(label + ": capsule AND headset reach the selected activity", Vector2.Distance(
                    new Vector2(eye.x, eye.z), new Vector2(expectedFeet.x, expectedFeet.z)) < .25f
                    && Vector3.Distance(actualFeet, expectedFeet) < .15f);
                if (point == SceneTestStartingPoint.KnifeScalePhotography || point == SceneTestStartingPoint.BloodstainScalePhotography
                    || point == SceneTestStartingPoint.BottleScalePhotography)
                {
                    string id = point == SceneTestStartingPoint.KnifeScalePhotography ? "EVD-014"
                        : point == SceneTestStartingPoint.BloodstainScalePhotography ? "EVD-015" : "EVD-018";
                    var subject = esm.GetRecord(id);
                    var camera = FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
                    Check(label + ": original photo and markers prepare a real final-photo transition", subject.status == EvidenceStatus.Marked
                        && subject.tentNumber.HasValue && (!subject.definition.requiresTraceMarker || subject.traceTentNumber.HasValue)
                        && esm.CanAdvanceTo(id, EvidenceStatus.PhotographedFinal));
                    Check(label + ": physical markers and chalk exist", FindObjectsByType<EvidenceTentPickup>(FindObjectsSortMode.None)
                        .Count(t=>t.MarkedEvidenceId==id)==(subject.definition.requiresTraceMarker?2:1)
                        && (!subject.definition.requiresChalk || FindObjectsByType<EvidenceChalkGuide>(FindObjectsSortMode.None).Any(g=>g.EvidenceId==id&&g.IsComplete)));
                    Check(label + ": held camera leaves raising and ruler placement to the player", camera.IsCameraHeld
                        && !camera.IsAiming && camera.PhotoScale.RequiresScale(id) && !camera.PhotoScale.IsPlaced);
                    Check(label + ": samples, recording, other items and scored answers remain untouched", !subject.swabbingDone
                        && !subject.fingerprintingDone && subject.selectedReportLineIds.Count==0 && !EvidenceInventory.Instance.AllEntryIds.Any()
                        && esm.AllRecords.Where(r=>r!=subject).All(r=>r.status==EvidenceStatus.NotFound)
                        && ReasoningCheckpointManager.Instance.Records.Count==0);
                    try
                    {
                        int before=PhotoAlbumManager.Instance.Photos.Count;
                        PhotoScaleTestUtil.PrepareForFinalPhoto(camera,id);
                        typeof(PhotographTool).GetMethod("TakePhoto",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(camera,null);
                        Check(label + ": actual ruler and shutter save the final photo with physical markers present",
                            subject.status==EvidenceStatus.PhotographedFinal && camera.LastCaptureUsedScale
                            && PhotoAlbumManager.Instance.Photos.Count==before+1);
                        PhotoScaleTestUtil.FinishPhoto(camera);
                    }
                    catch(System.Exception ex)
                    {
                        Check(label + ": actual photograph could be completed",false);
                        report.AppendLine("INFO: "+ex);
                    }
                    string previous=SessionLogger.Instance.SessionId;
                    start.RestartSelectedTest();for(int i=0;i<8;i++)yield return null;
                    Check(label + ": restart restores the pending final photo and camera without a ruler", SceneTestStart.Instance.IsReady
                        && SessionLogger.Instance.SessionId!=previous && EvidenceStateManager.Instance.GetRecord(id).status==EvidenceStatus.Marked
                        && FindFirstObjectByType<PhotographTool>().IsCameraHeld && !FindFirstObjectByType<PhotographTool>().PhotoScale.IsPlaced);
                }
                else if (point == SceneTestStartingPoint.KnifeBagging)
                {
                    var knife = esm.GetRecord("EVD-014");
                    var prop = EvidenceProp.FindById("EVD-014");
                    Check(label + ": knife ready, grabbable and still on scene", knife.status == EvidenceStatus.ReadyForCollection
                        && knife.swabbingDone && knife.fingerprintingDone && prop.GetComponent<EvidenceGrabGate>().GrabEnabled
                        && knife.custodyLocation == EvidenceCustodyLocation.Scene && prop.gameObject.activeInHierarchy);
                    Check(label + ": other evidence untouched and inventory empty", esm.AllRecords.Where(r => r != knife)
                        .All(r => r.status == EvidenceStatus.NotFound) && !EvidenceInventory.Instance.AllEntryIds.Any());
                    Check(label + ": bag equipped in left hand with right hand free", EvidenceBagTool.Instance.IsVirtuallyEquipped
                        && EvidenceBagTool.Instance.transform.parent == FindFirstObjectByType<OVRCameraRig>().leftHandAnchor
                        && EvidenceInventory.Instance.CurrentHeldEvidenceId == null);
                    Check(label + ": no scored reasoning responses fabricated", ReasoningCheckpointManager.Instance.Records.Count == 0);
                    Check(label + ": real bag insertion and sealing remain available", EvidenceBagTool.Instance.TryInsert(prop) == TransitionResult.Applied
                        && EvidenceBagTool.Instance.TrySeal() == TransitionResult.Applied);
                    Check(label + ": explicit storage completes the knife bag", HeldItemCoordinator.Instance.TryStoreHeldEvidence()
                        && EvidenceInventory.Instance.IsInInventory("EVD-014"));
                    string previous = SessionLogger.Instance.SessionId;
                    start.RestartSelectedTest();
                    for (int i = 0; i < 8; i++) yield return null;
                    Check(label + ": restart restores loose ready knife and equipped bag", SceneTestStart.Instance.IsReady
                        && SessionLogger.Instance.SessionId != previous
                        && EvidenceStateManager.Instance.GetRecord("EVD-014").status == EvidenceStatus.ReadyForCollection
                        && !EvidenceInventory.Instance.AllEntryIds.Any() && EvidenceBagTool.Instance.IsVirtuallyEquipped);
                }
                else if (point == SceneTestStartingPoint.BottleFingerprinting)
                {
                    var bottle=esm.GetRecord("EVD-018");
                    Check(label+": final photo and bottle measurements prepared",bottle.status==EvidenceStatus.PhotographedFinal
                        && measures.GetEvidenceMeasurementStatus("EVD-018").IsComplete && esm.GetFingerprintBlockReason("EVD-018")==null);
                    Check(label+": prints remain undusted and unidentified",!bottle.fingerprintLifted && !bottle.fingerprintingDone
                        && bottle.fingerprintLabMatchIdentities.Count==0 && !EvidenceFingerprintMarker.FindByEvidenceId("EVD-018").IsDusted);
                    Check(label+": kit equipped and other evidence untouched (kit="+FingerprintKitTool.Instance.IsVirtuallyEquipped
                        +", others="+string.Join(",",esm.AllRecords.Where(r=>r!=bottle).Select(r=>r.definition.evidenceId+":"+r.status))+")",FingerprintKitTool.Instance.IsVirtuallyEquipped
                        && esm.AllRecords.Where(r=>r!=bottle).All(r=>r.status==EvidenceStatus.NotFound)
                        && !EvidenceInventory.Instance.AllEntryIds.Any() && ReasoningCheckpointManager.Instance.Records.Count==0);
                    string previous=SessionLogger.Instance.SessionId;
                    start.RestartSelectedTest();for(int i=0;i<8;i++)yield return null;
                    Check(label+": restart prepares a fresh fingerprint exercise",SceneTestStart.Instance.IsReady
                        && SessionLogger.Instance.SessionId!=previous && !EvidenceStateManager.Instance.GetRecord("EVD-018").fingerprintingDone
                        && FingerprintKitTool.Instance.IsVirtuallyEquipped);
                }
                else if (point == SceneTestStartingPoint.BodyRecording)
                {
                    var body = esm.GetRecord("BODY-A");
                    Check(label + ": final photo, Marker A and all measurements prepared", body.status == EvidenceStatus.PhotographedFinal
                        && body.tentLetter == "A" && measures.AreSceneMeasurementsComplete && esm.CanAdvanceTo("BODY-A", EvidenceStatus.Logged));
                    Check(label + ": report and removal remain for the player", !body.bodyFindingsRecorded
                        && body.selectedReportLineIds.Count == 0 && body.bodyRemovalStatus == BodyRemovalStatus.NotPrepared
                        && !EvidenceProp.FindById("BODY-A").GetComponent<BodyRemovalInteraction>().VisualRemoved);
                    Check(label + ": Recorder equipped and no scored answers or samples fabricated", RecorderTool.Instance.IsVirtuallyEquipped
                        && !EvidenceInventory.Instance.AllEntryIds.Any() && ReasoningCheckpointManager.Instance.Records.Count == 0
                        && esm.AllRecords.All(r => !r.swabbingDone && !r.fingerprintingDone && r.selectedReportLineIds.Count == 0));
                    string previous = SessionLogger.Instance.SessionId;
                    start.RestartSelectedTest(); for (int i = 0; i < 8; i++) yield return null;
                    Check(label + ": restart restores unrecorded body and equipped Recorder", SceneTestStart.Instance.IsReady
                        && SessionLogger.Instance.SessionId != previous && RecorderTool.Instance.IsVirtuallyEquipped
                        && EvidenceStateManager.Instance.GetRecord("BODY-A").status == EvidenceStatus.PhotographedFinal
                        && !EvidenceStateManager.Instance.GetRecord("BODY-A").bodyFindingsRecorded);
                }
                else if (point == SceneTestStartingPoint.AllEvidenceMeasured)
                {
                    var markers = FindObjectsByType<EvidenceTentPickup>(FindObjectsSortMode.None);
                    Check(label + ": all four items measured and unrecorded", measures.AreSceneMeasurementsComplete
                        && esm.AllRecords.Count()==4 && esm.AllRecords.All(r=>r.status==EvidenceStatus.PhotographedFinal
                            && r.selectedReportLineIds.Count==0 && !r.bodyFindingsRecorded));
                    Check(label + ": no sampling, fingerprints, custody or answers skipped", esm.AllRecords.All(r=>!r.swabbingDone
                        && !r.fingerprintingDone && r.custodyLocation==EvidenceCustodyLocation.Scene)
                        && !EvidenceInventory.Instance.AllEntryIds.Any() && ReasoningCheckpointManager.Instance.Records.Count==0);
                    Check(label + ": real item, trace and body markers wait for recording", markers.Count(t=>!string.IsNullOrEmpty(t.MarkedEvidenceId))==5
                        && markers.Where(t=>!string.IsNullOrEmpty(t.MarkedEvidenceId)).All(t=>!t.IsPickable())
                        && markers.Any(t=>t.MarksTrace) && markers.Any(t=>t.MarkerLetter=="A"));
                    var unused=markers.SingleOrDefault(t=>string.IsNullOrEmpty(t.MarkedEvidenceId));
                    Check(label + ": spare unused tent is immediately pickable", unused!=null && unused.IsPickable());
                    Check(label + ": Recorder equipped but body sketch is still required", RecorderTool.Instance.IsVirtuallyEquipped
                        && !esm.CanAdvanceTo("BODY-A",EvidenceStatus.Logged) && !esm.GetRecord("BODY-A").bodyFindingsRecorded);
                    Check(label + ": measured body sketch unlocks recording",MeasurementTestUtil.CompleteSketchFor("BODY-A") && esm.CanAdvanceTo("BODY-A",EvidenceStatus.Logged));
                    unused?.TryReclaim();
                    var bodyMarker=markers.FirstOrDefault(t=>t.MarkedEvidenceId=="BODY-A");
                    esm.RecordReportLinesSelected("BODY-A",new[]{"body-a-location","body-a-position","body-a-wound-sharp-object"});
                    esm.MarkLogged("BODY-A",ToolType.Recorder);
                    Check(label + ": recording body unlocks only its own marker", bodyMarker!=null && bodyMarker.IsPickable()
                        && markers.Where(t=>t.MarkedEvidenceId!="BODY-A" && !string.IsNullOrEmpty(t.MarkedEvidenceId)).All(t=>!t.IsPickable()));
                    bodyMarker?.TryReclaim();
                    string previous=SessionLogger.Instance.SessionId;
                    start.RestartSelectedTest();for(int i=0;i<8;i++)yield return null;
                    Check(label + ": restart restores measurements, unrecorded items and all six tents", SceneTestStart.Instance.IsReady
                        && SessionLogger.Instance.SessionId!=previous && MeasurementStateManager.Instance.AreSceneMeasurementsComplete
                        && EvidenceStateManager.Instance.AllRecords.All(r=>r.status==EvidenceStatus.PhotographedFinal && r.selectedReportLineIds.Count==0)
                        && FindObjectsByType<EvidenceTentPickup>(FindObjectsSortMode.None).Length==6);
                }
                else if (point < SceneTestStartingPoint.AllEvidenceProceduresDone)
                {
                    Check(label + ": evidence remains undiscovered", esm.AllRecords.All(r => r.status == EvidenceStatus.NotFound));
                    Check(label + ": previous reasoning suppressed without answer records", ReasoningCheckpointManager.Instance.Records.Count == 0
                        && ReasoningCheckpointManager.Instance.IsAnswered("RC-01-PETER"));
                }
                else
                {
                    Check(label + ": closeout waits for comparison only in the comparison preset",
                        SOCOCloseout.Instance.AllAccountedFor==(point!=SceneTestStartingPoint.FingerprintComparison));
                    Check(label + ": measurement anchors retained for all four items", measures.RequirementSet.requirements
                        .All(r => MeasurementGeometry.TryGetEvidenceAnchor(r.targetEvidenceId, out _)));
                    Check(label + ": no skipped reasoning answer was fabricated", ReasoningCheckpointManager.Instance.Records.Count == 0);
                    Check(label + ": permanent body outline remains after removal", FindObjectsByType<EvidenceChalkGuide>(
                        FindObjectsInactive.Exclude, FindObjectsSortMode.None).Any(g => g.EvidenceId == "BODY-A" && g.IsComplete));
                    Check(label + ": all six stored items include both secured fingerprint cards", EvidenceInventory.Instance.AllTrackedEntryIds.Count()==6
                        && new[]{"EVD-014","EVD-018"}.All(id=>(EvidenceInventory.Instance.IsExplicitlyStoredSample(EvidenceInventory.FingerprintSampleId(id))
                            || (point==SceneTestStartingPoint.FingerprintComparison&&id=="EVD-014"&&EvidenceInventory.Instance.CurrentHeldEvidenceId==EvidenceInventory.FingerprintSampleId(id)))
                            && EvidenceInventory.Instance.IsSampleSealed(EvidenceInventory.FingerprintSampleId(id))));
                    if (point == SceneTestStartingPoint.FingerprintComparison)
                    {
                        var station=FingerprintComparisonStation.Instance;
                        Check(label + ": held unmatched card is available at the actual desk", station!=null && station.AvailableSamples.Count==1
                            && station.CanOpen && HeldItemCoordinator.Instance.CurrentCategory==HeldItemCoordinator.HeldCategory.Evidence);
                        Check(label + ": practice attempts and completions remain for the player", new[]{"EVD-014","EVD-018"}.All(id=>!station.IsComplete(id)&&station.AttemptsFor(id)==0));
                        Check(label + ": held knife lift awaits comparison without a seeded identity",esm.GetRecord("EVD-014").fingerprintLabStatus==FingerprintLabStatus.SentToLab
                            &&esm.GetRecord("EVD-014").fingerprintLabMatchIdentities.Count==0&&!EvidenceInventory.Instance.IsExplicitlyStoredSample(EvidenceInventory.FingerprintSampleId("EVD-014")));
                        Check(label + ": vendor follow-up remains undiscovered for the player to unlock after comparison",
                            !VendorFollowupInterview.Instance.IsDiscovered && !VendorFollowupInterview.Instance.FollowupUnlocked);
                        string previous=SessionLogger.Instance.SessionId;
                        start.RestartSelectedTest();for(int i=0;i<8;i++)yield return null;
                        Check(label + ": restart restores the held unmatched card at the outside desk", SceneTestStart.Instance.IsReady
                            && SessionLogger.Instance.SessionId!=previous && FingerprintComparisonStation.Instance.AvailableSamples.Count==1
                            && FingerprintComparisonStation.Instance.CanOpen);
                    }
                }
                // Restarted presets own new scene instances; inspect those rather than destroyed prior managers.
                sketch = CrimeSceneSketchManager.Instance; measures = MeasurementStateManager.Instance;
                Check(label + ": measurement state matches milestone", measures.AreSceneMeasurementsComplete
                    == (milestone >= SceneTestStartingPoint.AllEvidenceProceduresDone || point == SceneTestStartingPoint.BodyRecording
                        || point == SceneTestStartingPoint.AllEvidenceMeasured));
                Check(label + ": sketch state matches milestone", milestone >= SceneTestStartingPoint.SketchDone || point == SceneTestStartingPoint.CustodyPass
                    ? sketch.IsSubmitted : milestone >= SceneTestStartingPoint.AllEvidenceProceduresDone ? sketch.State == SketchState.ReadyForSubmission
                    : point == SceneTestStartingPoint.BottleFingerprinting || point == SceneTestStartingPoint.KnifeBagging || point == SceneTestStartingPoint.BodyRecording ? sketch.PlacedEvidenceInOrder.Count == 1
                    : sketch.State == SketchState.NotStarted);
                if (point == SceneTestStartingPoint.MeasurementsDone)
                    Check(label + ": eight real measurements already attached to prior required placements", sketch.GetRecordedCards().Count == 8 && sketch.GetAvailableMeasurementCards().Count == 0);
                if (point == SceneTestStartingPoint.PhotographyDone)
                {
                    var bottle = EvidenceProp.FindById("EVD-018");
                    Check(label + ": spawn stays outside bottle discovery radius", Vector3.Distance(
                        Camera.main.transform.position, bottle.transform.position) > bottle.InteractionRadius + .1f);
                    var probe = new GameObject("Preset bottle approach probe") { tag = "Player" };
                    probe.transform.position = bottle.transform.position + Vector3.back * 3;
                    probe.AddComponent<SphereCollider>().radius = .08f;
                    var rb = probe.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                    yield return new WaitForFixedUpdate();
                    probe.transform.position = bottle.transform.position + new Vector3(0, -.2f, -1);
                    Physics.SyncTransforms();
                    for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
                    Check(label + ": actual approach still triggers bottle Found", esm.GetRecord("EVD-018").status == EvidenceStatus.Found);
                    Destroy(probe);
                    string previous = SessionLogger.Instance.SessionId;
                    start.RestartSelectedTest();
                    for (int i = 0; i < 8; i++) yield return null;
                    Check(label + ": restart uses fresh managers/session and removes previous Found", SceneTestStart.Instance.IsReady
                        && SessionLogger.Instance.SessionId != previous
                        && EvidenceStateManager.Instance.GetRecord("EVD-018").status == EvidenceStatus.NotFound);
                }
                if (point == SceneTestStartingPoint.AllEvidenceProceduresDone)
                {
                    yield return new WaitForSecondsRealtime(3.25f);
                    Check(label + ": no queued or delayed teaching from skipped procedures",
                        ProceduralTrainingGuidance.Instance.PendingCount == 0
                        && !STCSNotificationUI.Instance.IsShowing && STCSNotificationUI.Instance.PendingCount == 0);
                    var r = measures.RequirementSet.requirements.First();
                    bool ok = measures.TryGetSceneDistance(r.requirementId, r.referenceAId, out float distance);
                    var result = measures.RecordMeasurement(r.requirementId, r.referenceAId, distance);
                    Check(label + ": prior required measurement remains intact and repeat recording is harmless", ok
                        && result == MeasurementRecordResult.AlreadyComplete && measures.GetRecord(r.requirementId).WithinTolerance);
                }
                yield return null;
                if (point != SceneTestStartingPoint.InvestigationSubmitted)
                    Check(label + ": no stale checkpoint owns the UI", !FindFirstObjectByType<HypothesisCheckpointUI>().IsShowing
                        && ReasoningCheckpointManager.Instance.ActiveId == null);
            }
            yield return Load(SceneTestStartingPoint.NormalPlay);
            Check("Normal play after presets returns to fresh IOC with no fixture", !SceneTestStart.IsActive
                && SceneTurnover.Phase == ScenarioPhase.IOCTurnover && !ScenePhotographyManager.Instance.IsScenePhotographyComplete);
            Check("Real assessment queue and owner records unchanged", queue == AutomatedTestSandbox.RealQueueEntryCount()
                && owners == AutomatedTestSandbox.RealOwnerRecordCount());
        }
        finally
        {
            SessionState.SetInt(SceneTestStart.EditorSelectionKey, original);
            SessionState.SetBool("ViCSI.SceneTestStart.Diagnostic", false);
        }
        report.AppendLine(failures == 0 ? "=== PASS, 0 failures ===" : "=== FAIL: " + failures + " failures ===");
        Report = report.ToString(); IsDone = true;
    }
    private static bool PreSessionUnlocked() => FindFirstObjectByType<PreSessionFlowController>().SessionUnlocked;
}
#endif
