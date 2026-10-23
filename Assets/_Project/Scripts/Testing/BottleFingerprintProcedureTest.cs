using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

/// <summary>Real bottle gates, brush/lift handlers, XR B storage, results, report, bagging and reset.</summary>
public sealed class BottleFingerprintProcedureTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== BottleFingerprintProcedureTest ===\n");
    private int failures;
    private void Check(string label,bool ok){report.AppendLine((ok?"PASS: ":"FAIL: ")+label);if(!ok)failures++;}
    public void RunAllTests(){AutomatedTestSandbox.Activate(nameof(BottleFingerprintProcedureTest));DontDestroyOnLoad(gameObject);StartCoroutine(Run());}
    private static void Clear(){STCSNotificationUI.Instance.HideImmediately();NotificationManager.Instance.UI.ClearTooltip();}
    private IEnumerator Run()
    {
        yield return null;
        ReasoningCheckpointManager.Instance.enabled=false;HypothesisCheckpointManager.Instance.enabled=false;ProceduralTrainingGuidance.Instance.enabled=false;
        Clear();
        var state=EvidenceStateManager.Instance;var inventory=EvidenceInventory.Instance;var held=HeldItemCoordinator.Instance;
        var record=state.GetRecord("EVD-018");var prop=EvidenceProp.FindById("EVD-018");
        var marker=EvidenceFingerprintMarker.FindByEvidenceId("EVD-018");var kit=FingerprintKitTool.Instance;
        var rig=FindAnyObjectByType<OVRCameraRig>();var measurements=MeasurementStateManager.Instance;
        string sample=EvidenceInventory.FingerprintSampleId("EVD-018");
        Check("bottle uses on-scene processing after final photography",record.definition.fingerprintBeforeCollection && record.definition.measurementAfterFinalPhoto);
        Check("physical print cluster belongs to original bottle",marker!=null && marker.transform.IsChildOf(prop.transform)
            && marker.evidenceId=="EVD-018" && marker.GetComponent<EvidenceProp>().evidenceId!="EVD-018");
        if(marker==null){Report=report.AppendLine("=== FAIL: missing bottle print marker ===").ToString();IsDone=true;yield break;}
        var cluster=marker.transform.Find("BottlePrintCluster");
        Check("three separate powdered print visuals start hidden",cluster!=null && cluster.childCount==3 && !cluster.gameObject.activeSelf);
        Check("old station is retired and rejects on-scene work",!FindAnyObjectByType<FingerprintStationTool>(FindObjectsInactive.Include).enabled
            && !FingerprintStationTool.Instance.TryDust("EVD-018"));
        Check("scene photography prerequisite completed",ScenePhotographyTestUtil.CompleteRoute());
        held.ToggleEquipTool(ToolType.FingerprintKit,rig.rightHandAnchor);kit.TryLoadDust();Clear();
        Check("dusting before final photograph is refused",!kit.TryDust(marker) && marker.DustProgress==0 && record.fingerprintLabMatchIdentities.Count==0);
        state.MarkFound("EVD-018",ToolType.None);state.MarkPhotographedRaw("EVD-018",ToolType.Photographer);
        state.MarkChalkMarked("EVD-018",ToolType.ChalkMark);state.MarkTented("EVD-018",ToolType.EvidenceMarker,18);
        var requirement=measurements.RequirementSet.requirements.Single(r=>r.targetEvidenceId=="EVD-018");
        Check("measurement before final photography is refused",measurements.RecordMeasurement(requirement.requirementId,requirement.referenceAId,requirement.expectedDistanceA)==MeasurementRecordResult.PrerequisiteBlocked
            && !measurements.GetRecord(requirement.requirementId).hasA);
        Check("final photograph no longer warns about measurements",state.GetUnmetProcedureWarnings("EVD-018",EvidenceStatus.PhotographedFinal).Count==0
            && state.MarkPhotographedFinal("EVD-018",ToolType.Photographer)==TransitionResult.Applied);
        state.MarkFound("EVD-014",ToolType.None);state.MarkPhotographedRaw("EVD-014",ToolType.Photographer); // creates a real required bottle-to-knife pair
        Clear();Check("dusting without measurements is refused",!kit.TryDust(marker) && marker.DustProgress==0);
        Check("first fixed reference alone does not unlock dust",MeasurementTestUtil.RecordExact("EVD-018",1)==MeasurementRecordResult.Recorded
            && !kit.TryDust(marker) && marker.DustProgress==0);
        Check("both fixed references still require discovered evidence distances",MeasurementTestUtil.RecordExact("EVD-018",2)==MeasurementRecordResult.Completed
            && !kit.TryDust(marker) && marker.DustProgress==0);
        Check("required bottle-to-knife distance records",MeasurementTestUtil.RecordPairExact("EVD-018","EVD-014")==EvidencePairRecordResult.Recorded);
        Check("measurements alone do not unlock powdering before sketch placement",state.GetFingerprintBlockReason("EVD-018")!=null && !kit.TryDust(marker) && marker.DustProgress==0);
        Check("measured bottle placed on sketch",MeasurementTestUtil.CompleteSketchFor("EVD-018"));
        Clear();
        Check("measurement and sketch unlock powdering",measurements.GetEvidenceMeasurementStatus("EVD-018").IsComplete && state.GetFingerprintBlockReason("EVD-018")==null);
        Check("Recorder and collection remain blocked until print storage",!RecorderTool.Instance.TryOpenReport(prop)
            && EvidenceBagTool.Instance.TryInsert(prop)==TransitionResult.Violation && !prop.GetComponent<EvidenceGrabGate>().GrabEnabled);
        Check("undusted prints cannot be lifted",kit.TryLift(marker)!=TransitionResult.Applied && !record.fingerprintLifted);
        Clear();
        kit.HandleBrushTriggerEnter(marker.transform.Find("TouchZone").GetComponent<Collider>());
        double deadline=Time.realtimeSinceStartupAsDouble+8;
        while(!marker.IsDusted && Time.realtimeSinceStartupAsDouble<deadline){kit.TryDust(marker);yield return null;}
        Check("sustained brush contact reveals all three recovered prints",marker.IsDusted && cluster.gameObject.activeInHierarchy
            && cluster.GetComponentsInChildren<Renderer>().Length==3 && cluster.GetComponentsInChildren<Renderer>().All(r=>r.enabled && r.sharedMaterial.color.maxColorComponent<.25f));
        Check("dusting does not identify people or finish processing",!record.fingerprintingDone && record.fingerprintLabMatchIdentities.Count==0
            && record.fingerprintLabStatus==FingerprintLabStatus.NotSent && !EvidenceNoteComposer.Compose(record).Contains("Fingerprint identification"));
        Check("matched findings and Recorder choices stay hidden before B storage",!EvidenceNoteComposer.Compose(record).Contains("Three separate recovered prints")
            && !RequirementEvaluator.IsSatisfied(record.definition.reportLineOptions.Single(o=>o.id=="bottle-first-encountered").extraRequirement)
            && !RequirementEvaluator.IsSatisfied(record.definition.reportLineOptions.Single(o=>o.id=="bottle-fingerprint-pending").extraRequirement));
        Clear();held.ToggleEquipTool(ToolType.FingerprintTape,rig.rightHandAnchor);
        FingerprintTapeTool.Instance.HandleTipTriggerEnter(marker.transform.Find("TouchZone").GetComponent<Collider>());
        Check("lift creates source-linked held evidence",kit.TryLift(marker)==TransitionResult.Applied && record.fingerprintLifted
            && inventory.CurrentHeldEvidenceId==sample && inventory.GetSampleSource(sample)=="EVD-018" && !inventory.IsInInventory(sample));
        Check("lift alone leaves identification and Recorder blocked",!record.fingerprintingDone && record.fingerprintLabMatchIdentities.Count==0
            && !state.CanAdvanceTo("EVD-018",EvidenceStatus.Logged) && !marker.gameObject.activeSelf);
        Clear();yield return null;yield return null;
        InputSystem.RegisterLayout("{\"name\":\"ViCSIBottleFingerprintController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"}]}");
        var device=InputSystem.AddDevice("ViCSIBottleFingerprintController");InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["secondaryButton"],1f);yield return null;yield return null;
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["secondaryButton"],0f);yield return null;
        InputSystem.RemoveDevice(device);InputSystem.RemoveLayout("ViCSIBottleFingerprintController");
        Check("actual B cannot store the unmatched recovered sample",!inventory.IsExplicitlyStoredSample(sample) && !inventory.IsInInventory(sample)
            && inventory.CurrentHeldEvidenceId==sample && !record.fingerprintingDone);
        Check("Lift leaves bottle identification pending without announcing a result",record.fingerprintLabStatus==FingerprintLabStatus.NotSent
            &&record.fingerprintLabMatchIdentities.Count==0
            &&!DialogueHistoryManager.Instance.Entries.Any(e=>e.text==GameDialogue.Text("bottle-fingerprint-identification")));
        Check("Correct reference at the actual comparison desk identifies the bottle lift",FingerprintComparisonTestUtil.CompareStoredSource("EVD-018"));
        Clear();
        Check("Only a matched fingerprint can be explicitly stored",held.TryStoreHeldEvidence() && inventory.IsExplicitlyStoredSample(sample)
            &&inventory.IsInInventory(sample)&&inventory.CurrentHeldEvidenceId==null);
        Check("identification contains three distinct people",record.fingerprintLabStatus==FingerprintLabStatus.Matched
            && record.fingerprintLabMatchIdentities.SequenceEqual(new[]{"John","Peter","Paul"}));
        Check("knife remains independently unidentified",!state.GetRecord("EVD-014").fingerprintingDone && state.GetRecord("EVD-014").fingerprintLabMatchIdentities.Count==0);
        Check("notes expose the earned multi-person result",new[]{"John","Peter","Paul"}.All(name=>EvidenceNoteComposer.Compose(record).Contains(name))
            && EvidenceNoteComposer.Compose(record).Contains("Fingerprint identification"));
        Check("automatic identification announcement is delivered",DialogueHistoryManager.Instance.Entries.Count(e=>e.text==GameDialogue.Text("bottle-fingerprint-identification"))==1);
        state.MarkMatched("EVD-018");held.TryStoreHeldEvidence();
        Check("identification cannot announce twice",DialogueHistoryManager.Instance.Entries.Count(e=>e.text==GameDialogue.Text("bottle-fingerprint-identification"))==1);
        SessionLogger.Instance.Flush();
        var events=SessionLogReader.ReadEvents(SessionLogger.Instance.ActiveLogFilePath);
        var identified=events.Where(e=>e.eventType=="FingerprintIdentified" && e.targetId=="EVD-018").ToArray();
        Check("one unscored identification event carries every name",identified.Length==1 && SessionLogReader.PayloadValue(identified[0],"scored")=="false"
            && new[]{"John","Peter","Paul"}.All(name=>SessionLogReader.PayloadValue(identified[0],"matches").Contains(name)));
        var config=ScriptableObject.CreateInstance<AssessmentUploadConfiguration>();config.uploadRawEventLogEnabled=false;
        var scenario=ScriptableObject.CreateInstance<ScenarioDefinition>();scenario.scenarioId="CSI-ENVIRONMENT-001";scenario.scoringRulesVersion="1.7.0";
        var dto=AssessmentSessionBuilder.Build(SessionLogger.Instance.ActiveLogFilePath,config,scenario,"","");
        var uploadedMatches=dto.orderedEvents.Where(e=>e.eventType=="FingerprintIdentified" && e.targetId=="EVD-018").ToArray();
        Check("minimal upload retains the bottle ID and every matched identity",dto.evidenceResults.Count==0 && uploadedMatches.Length==1
            && new[]{"John","Peter","Paul"}.All(name=>uploadedMatches[0].payload.Any(p=>p.key=="matches" && p.value!=null && p.value.Contains(name))));
        Check("essential event survives disabled raw logs",!dto.includesRawEventLog && dto.deductionAssessmentEvents.Count==0 && uploadedMatches.Length==1);
        System.IO.Directory.CreateDirectory("docs/evidence/bottle-on-scene");System.IO.File.WriteAllText("docs/evidence/bottle-on-scene/upload-wire.json",EducationalSessionRecord.Serialize(dto,true));
        Destroy(config);Destroy(scenario);
        Check("bottle measurement fixture can complete remaining discovered records",MeasurementTestUtil.CompleteDiscoveredMeasurements());
        Clear();Check("Recorder opens after fingerprint storage",RecorderTool.Instance.TryOpenReport(prop) && RecorderReportUI.Instance.IsShowing);
        foreach(var line in new[]{"rec-bottle-location-v1","rec-bottle-condition-v1","rec-bottle-prints-v1"}) Check("Recorder accepts observable report line "+line,RecorderReportUI.Instance.SelectLineForTest(line));
        Check("a fourth bottle report line is refused",!RecorderReportUI.Instance.SelectLineForTest("rec-bottle-time-v1"));
        Check("exactly three lines log the bottle",RecorderReportUI.Instance.Confirm()==TransitionResult.Applied && record.status==EvidenceStatus.ReadyForCollection && prop.GetComponent<EvidenceGrabGate>().GrabEnabled);
        Clear();held.ToggleEquipTool(ToolType.EvidenceCollector,rig.leftHandAnchor);
        Check("real bag insertion and sealing finish processing",EvidenceBagTool.Instance.TryInsert(prop)==TransitionResult.Applied && EvidenceBagTool.Instance.TrySeal()==TransitionResult.Applied
            && record.status==EvidenceStatus.Processed && !inventory.IsInInventory("EVD-018"));
        Check("bag still requires separate B storage",held.TryStoreHeldEvidence() && inventory.IsInInventory("EVD-018") && SOCOCloseout.Instance.EvidenceComplete(record.definition));
        Check("collection retains the documented measurement records",measurements.GetRecord(requirement.requirementId).IsComplete);
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path);yield return null;
        var reset=EvidenceStateManager.Instance.GetRecord("EVD-018");var resetMarker=EvidenceFingerprintMarker.FindByEvidenceId("EVD-018");
        Check("restart clears dust, lift, matches and sample custody",reset.status==EvidenceStatus.NotFound && !reset.fingerprintLifted && !reset.fingerprintingDone
            && reset.fingerprintLabMatchIdentities.Count==0 && resetMarker!=null && !resetMarker.IsDusted && !EvidenceInventory.Instance.AllEntryIds.Any());
        report.AppendLine(failures==0?"=== PASS, 0 failures ===":"=== FAIL: "+failures+" failure(s) ===");Report=report.ToString();IsDone=true;
    }
}
