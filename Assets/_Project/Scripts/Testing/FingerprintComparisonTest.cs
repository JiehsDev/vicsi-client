using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

/// <summary>Real sampling, storage, station A/B input, navigation and session reset.</summary>
public sealed class FingerprintComparisonTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== FingerprintComparisonTest ===\n");
    private int failures;
    private InputDevice device, left;
    private Color previewPaperColor;
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private void Check(string text,bool value) { report.AppendLine((value?"PASS: ":"FAIL: ")+text);if(!value)failures++; }
    private static void Clear() { STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip(); }
    private static int Count(SessionEventType type)=>SessionLogger.Instance.EventCounts.TryGetValue(type,out int n)?n:0;
    public void RunAllTests() { DontDestroyOnLoad(gameObject);Application.logMessageReceived+=OnLog;StartCoroutine(Run()); }
    private void OnDestroy()=>Application.logMessageReceived-=OnLog;
    private void OnLog(string message,string stack,LogType type) { if(type==LogType.Exception&&!IsDone) { Check(message,false);Finish(); } }
    private void Finish() { report.AppendLine(failures==0?"=== PASS ===":"=== FAIL ===");Report=report.ToString();IsDone=true; }
    private IEnumerator Button(string name,bool down)
    { InputSystem.QueueDeltaStateEvent((ButtonControl)device[name],down?1f:0f);yield return null;yield return null; }
    private IEnumerator Tap(string name="primaryButton") { yield return Button(name,true);yield return Button(name,false); }
    private static void Equip(ToolType role)=>HeldItemCoordinator.Instance.ToggleEquipTool(role,Object.FindAnyObjectByType<OVRCameraRig>().rightHandAnchor);
    private void ValidateTraversal(GloveStationGuide.RouteData route,FingerprintComparisonStation station)
    {
        var source = FindFirstObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        var probe = new GameObject("Preparation route capsule probe") { layer = 2 };
        // Meta locomotion uses standalone Physics.CapsuleCast queries, which do not
        // consult Physics.IgnoreCollision pairs. The probe replaces this player's capsule.
        var liveCapsule = (CapsuleCollider)typeof(Oculus.Interaction.Locomotion.CharacterController).GetField("_capsule", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
        bool liveCapsuleEnabled = liveCapsule.enabled;
        liveCapsule.enabled = false;
        try
        {
            var capsule = probe.AddComponent<CapsuleCollider>(); capsule.radius = source.Radius; capsule.height = 1.7f;
            var controller = probe.AddComponent<Oculus.Interaction.Locomotion.CharacterController>(); controller.InjectCapsule(capsule);
            controller.LayerMask = source.LayerMask.value & ~(1 << 2); controller.MaxStep = source.MaxStep;
            controller.MaxSlopeAngle = source.MaxSlopeAngle; controller.SkinWidth = source.SkinWidth; controller.MaxReboundSteps = source.MaxReboundSteps;
            controller.SetPosition(route.waypoints[0] + Vector3.up * (controller.Height * .5f + .01f)); Physics.SyncTransforms(); controller.TryGround(.08f);
            foreach (var point in route.waypoints.Skip(1).Concat(new[]{station.transform.position-station.transform.forward*.85f}))
            {
                for (int i = 0; i < 1000; i++)
                {
                    Vector3 feet = controller.Pose.position - Vector3.up * controller.Height * .5f;
                    Vector3 direction = point - feet; direction.y = 0;
                    controller.Move(Vector3.ClampMagnitude(direction, 1.5f / 90) + Vector3.down * .02f); Physics.SyncTransforms();
                }
                var actual = controller.Pose.position - Vector3.up * controller.Height * .5f;
                Check("Player capsule reaches comparison route point " + point.ToString("F2") + " actual=" + actual.ToString("F2"), Vector3.Distance(actual, point) < .18f);
            }
        }
        finally { DestroyImmediate(probe); liveCapsule.enabled = liveCapsuleEnabled; Physics.SyncTransforms(); }
    }
    private void Preview(Camera camera,string name)
    {
        var args=System.Environment.GetCommandLineArgs();int i=System.Array.IndexOf(args,"-phase5Output");
        var folder=i>=0?Path.GetDirectoryName(args[i+1]):"docs/evidence/fingerprint-comparison";Directory.CreateDirectory(folder);
        var target=RenderTexture.GetTemporary(1280,960,24);var active=RenderTexture.active;
        var texture=new Texture2D(1280,960,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1280,960),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());
            if(name=="comparison-hint")
            {
                var position=camera.WorldToViewportPoint(FingerprintComparisonStation.Instance.RecoveredGraphic.transform.TransformPoint(new Vector3(127,100,0)));
                previewPaperColor=texture.GetPixel(Mathf.Clamp((int)(position.x*1280),0,1279),Mathf.Clamp((int)(position.y*960),0,959));
            }
        }
        finally{camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(texture);}
    }
    private IEnumerator Click(FingerprintComparisonStation station,string id,Camera camera)
    {
        var target=station.Pointer.Targets.Single(item=>item.id==id);
        var point=target.rect.TransformPoint(target.rect.rect.center);
        station.Pointer.RayOverride=()=>new Ray(camera.transform.position,(point-camera.transform.position).normalized);
        yield return Tap("triggerPressed");station.Pointer.RayOverride=null;
    }
    private IEnumerator LiftCard(string id)
    {
        var record=EvidenceStateManager.Instance.GetRecord(id);record.status=EvidenceStatus.PhotographedFinal;
        record.swabbingDone=record.swabSampleSealed=record.definition.requiresSwabbing;
        if(id=="EVD-018")
        {
            MeasurementTestUtil.RecordExact(id,1);MeasurementTestUtil.RecordExact(id,2);MeasurementTestUtil.RecordAvailablePairsFor(id);
        }
        Check("Measured item sketched before lifting: "+id,MeasurementTestUtil.CompleteSketchFor(id));
        var marker=EvidenceFingerprintMarker.FindByEvidenceId(id);marker.GetComponent<UVRevealable>().TriggerReveal();
        var kit=FingerprintKitTool.Instance;kit.TryLoadDust();double deadline=Time.realtimeSinceStartupAsDouble+10;
        while(!marker.IsDusted&&Time.realtimeSinceStartupAsDouble<deadline){kit.TryDust(marker);yield return null;}
        Clear();Equip(ToolType.FingerprintTape);
        Check(id+" uses the real dust/lift path",marker.IsDusted&&kit.TryLift(marker)==TransitionResult.Applied);Clear();
        Check("Held lift is eligible for comparison before storage",FingerprintComparisonStation.Instance.AvailableSamples.Contains(EvidenceInventory.FingerprintSampleId(id)));
        yield return Tap("secondaryButton");Clear();
        Check(id+" actual B cannot store the unmatched lift",!EvidenceInventory.Instance.IsExplicitlyStoredSample(EvidenceInventory.FingerprintSampleId(id)));
        Check(id+" storage leaves identity pending with no match result",record.fingerprintLabStatus==FingerprintLabStatus.NotSent
            &&string.IsNullOrEmpty(record.fingerprintLabMatchResult)&&record.fingerprintLabMatchIdentities.Count==0);
    }
    private IEnumerator Run()
    {
        yield return null;
        var pre=FindAnyObjectByType<PreSessionFlowController>();
        if(pre!=null){pre.enabled=false;PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken",Flags).GetValue(pre));}
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if(ReasoningCheckpointManager.Instance!=null)ReasoningCheckpointManager.Instance.enabled=false;
        if(HypothesisCheckpointManager.Instance!=null)HypothesisCheckpointManager.Instance.enabled=false;
        if(ProceduralTrainingGuidance.Instance!=null)ProceduralTrainingGuidance.Instance.enabled=false;
        if(NarrativeEventManager.Instance!=null){NarrativeEventManager.Instance.enabled=false;NarrativeEventManager.Instance.StopAllCoroutines();}
        Clear();ScenePhotographyTestUtil.CompleteRoute();Clear();
        var station=FingerprintComparisonStation.Instance;var state=EvidenceStateManager.Instance;var inventory=EvidenceInventory.Instance;
        var cordon=FindAnyObjectByType<SceneCordon>();var supplies=ArrivalPPEStation.Instance;var guide=supplies.GetComponent<GloveStationGuide>();
        var rig=FindAnyObjectByType<OVRCameraRig>();rig.enabled=false;var original=Camera.main;string tag=original.tag;original.tag="Untagged";
        var eye=new GameObject("Fingerprint comparison test eye",typeof(Camera));var camera=eye.GetComponent<Camera>();camera.tag="MainCamera";camera.aspect=4f/3;camera.fieldOfView=60;
        InputSystem.RegisterLayout("{\"name\":\"FingerprintPracticeController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"triggerPressed\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"thumbstick\",\"layout\":\"Stick\",\"format\":\"VEC2\"}]}");
        device=InputSystem.AddDevice("FingerprintPracticeController");InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        try
        {
            camera.transform.position=new Vector3(18,2.8f,12.1f);
            Check("One separate examination desk has monitor and labelled tray",station!=null&&FindObjectsByType<FingerprintComparisonStation>().Length==1
                &&station.transform.Find("Comparison monitor")!=null&&station.transform.Find("Labelled packaged lift tray")!=null);
            var comparisonTop=station.transform.Find("Examination desk").GetComponent<Collider>().bounds;
            var supplyTop=supplies.transform.Find("Glove supply tabletop").GetComponent<Collider>().bounds;
            var dryingTop=SamplePreparationStation.Instance.transform.Find("Separate preparation tabletop").GetComponent<Collider>().bounds;
            Check("Desk uses authored yard staging with separate work surfaces",cordon.FingerprintStationAnchor!=null
                &&Vector3.Distance(station.transform.position,cordon.FingerprintStationAnchor.position)<.001f
                &&!comparisonTop.Intersects(supplyTop)&&!comparisonTop.Intersects(dryingTop)
                &&Vector3.Distance(comparisonTop.ClosestPoint(dryingTop.center),dryingTop.ClosestPoint(comparisonTop.center))>.5f);
            Check("Desk and standing approach are outside the controlled footprint",!cordon.IsWithinSecurityFootprint(station.transform.position)
                &&cordon.IsSecurelyOutside(station.transform.position-station.transform.forward*1.25f,.3f));
            Check("Monitor does not intersect existing solid scene geometry",Physics.OverlapBox(station.transform.position+new Vector3(0,1.51f,.12f),new Vector3(.59f,.47f,.0225f),Quaternion.identity,~0,QueryTriggerInteraction.Ignore)
                .All(collider=>collider.transform.IsChildOf(station.transform)));
            Check("No examination is offered before an explicitly stored lift",!station.GuidanceNeeded&&station.AvailableSamples.Count==0&&!station.Open());
            yield return LiftCard("EVD-014");
            Check("Held lift activates comparison with its original source",station.GuidanceNeeded&&station.AvailableSamples.SequenceEqual(new[]{EvidenceInventory.FingerprintSampleId("EVD-014")}));
            Clear();guide.Refresh();Check("Shared waypoint guides from inside to comparison",guide.DestinationActive&&guide.IsPinVisible&&guide.IsVisible
                &&guide.WaypointCanvas.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("Fingerprint examination"));
            Check("Storing the knife lift cannot identify Paul or confirm the finding",!FindingStateManager.Instance.IsConfirmed("FIND-KNIFE-PRINT-BROTHER")
                &&Count(SessionEventType.FingerprintIdentified)==0);
            Check("Storage cannot unlock the Paul-handled-knife conclusion",!ConclusionStateManager.Instance.IsAvailable("CONCLUSION-BROTHER-HANDLED-KNIFE"));
            using(GloveStationGuide.HideForCapture())Check("Comparison waypoint is hidden for evidence photography",!guide.IsPinVisible&&!guide.IsVisible);
            ValidateTraversal(guide.Route,station);
            Check("A distant station cannot open or mutate comparison",!station.Open()&&station.AttemptsFor("EVD-014")==0);
            camera.transform.position=station.transform.position-station.transform.forward*1.25f+Vector3.up*1.6f;
            camera.transform.LookAt(station.transform.position+Vector3.up*1.5f);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,true);yield return new WaitForSecondsRealtime(.3f);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,true);Clear();
            Check("Unrelated tool pickup cannot bypass held-card comparison",!HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Recorder,rig.rightHandAnchor)&&inventory.CurrentHeldEvidenceId==EvidenceInventory.FingerprintSampleId("EVD-014"));
            var token=new object();PlayerUIGate.Enter(token);Check("Other modal owners block opening",!station.Open());PlayerUIGate.Exit(token);
            Preview(camera,"outside-comparison-desk");Clear();yield return Button("primaryButton",true);
            Check("Actual A opens the source-labelled examination",station.IsShowing&&station.CurrentSourceId=="EVD-014"&&station.CurrentSampleId==EvidenceInventory.FingerprintSampleId("EVD-014"));
            yield return new WaitForSecondsRealtime(.4f);
            Check("Held opening A cannot submit a guess",station.AttemptsFor("EVD-014")==0);
            yield return Button("primaryButton",false);
            Check("Tutorial owns tool input and panel controls",ToolInputLock.IsLocked&&PlayerUIGate.ConfirmOwnerToken==station);
            var before=FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(),false,FinalDeductionAssessment.Rules);
            var custody=inventory.GetCustody(station.CurrentSampleId);var status=state.GetRecord("EVD-014").status;var identities=state.GetRecord("EVD-014").fingerprintLabMatchIdentities.ToArray();
            var originalEntries=inventory.AllTrackedEntryIds.ToArray();
            yield return Click(station,"hint",camera);
            Check("Actual controller ray click reveals two labelled feature hints",station.RecoveredGraphic.ShowFeatures&&station.ReferenceGraphic.ShowFeatures&&station.Feedback.Contains("ridge ending")&&station.Feedback.Contains("ridge split"));
            yield return Click(station,"left-zoom-in",camera);yield return Click(station,"left-rotate-left",camera);
            yield return Click(station,"right-zoom-in",camera);yield return Click(station,"right-rotate-right",camera);
            Check("Both prints independently zoom and rotate",station.RecoveredGraphic.Zoom==1.25f&&station.RecoveredGraphic.Rotation==10
                &&station.ReferenceGraphic.Zoom==1.25f&&station.ReferenceGraphic.Rotation==10);
            Preview(camera,"comparison-hint");
            Check("Monitor paper remains unobscured by other scene overlays",previewPaperColor.r>.8f&&previewPaperColor.g>.8f&&previewPaperColor.b>.7f);
            yield return Tap();Check("Incorrect A guess explains a visible difference and remains open",station.IsShowing&&!station.IsComplete("EVD-014")&&station.AttemptsFor("EVD-014")==1&&station.Feedback.Contains("different position"));
            Check("Wrong comparison cannot identify a person or unlock the knife finding",state.GetRecord("EVD-014").fingerprintLabStatus==FingerprintLabStatus.SentToLab
                &&!FindingStateManager.Instance.IsConfirmed("FIND-KNIFE-PRINT-BROTHER")&&Count(SessionEventType.FingerprintIdentified)==0);
            Check("Wrong comparison cannot unlock the handling conclusion",!ConclusionStateManager.Instance.IsAvailable("CONCLUSION-BROTHER-HANDLED-KNIFE"));
            Check("Wrong comparison cannot unlock or require the vendor interview",!VendorFollowupInterview.Instance.FollowupUnlocked
                &&VendorFollowupInterview.CompletionBlockReason==null);
            for(int i=0;i<30;i++)station.Submit();
            Check("Thirty further wrong guesses have no lockout or timer",station.AttemptsFor("EVD-014")==31&&station.IsShowing&&!station.IsComplete("EVD-014"));
            InputSystem.QueueDeltaStateEvent((Vector2Control)device["thumbstick"],new Vector2(-1,0));yield return null;yield return null;
            InputSystem.QueueDeltaStateEvent((Vector2Control)device["thumbstick"],Vector2.zero);yield return null;
            Check("Actual stick selects a different reference",station.SelectedReference==2);
            yield return Tap();Check("Correct A confirms the case match and identity finding",station.IsComplete("EVD-014")&&station.Feedback.Contains("Match confirmed")
                &&state.GetRecord("EVD-014").fingerprintLabStatus==FingerprintLabStatus.Matched&&state.GetRecord("EVD-014").fingerprintLabMatchResult=="Paul"
                &&FindingStateManager.Instance.IsConfirmed("FIND-KNIFE-PRINT-BROTHER")&&Count(SessionEventType.FingerprintIdentified)==1);
            Check("Confirmed comparison unlocks the handling conclusion",ConclusionStateManager.Instance.IsAvailable("CONCLUSION-BROTHER-HANDLED-KNIFE"));
            Check("Correct comparison immediately requires vendor follow-up without a reasoning response",VendorFollowupInterview.Instance.RequiresFollowup
                &&!ReasoningCheckpointManager.Instance.IsAnswered("RC-03-FINGERPRINT"));
            Clear();
            Check("Casework UI contains no practice, tutorial or simulated-lab wording",station.Labels.All(label=>
                !new[]{"practice","tutorial","training station","illustrat","simulated","score penalty"}.Any(word=>label.text.ToLowerInvariant().Contains(word))));
            Check("Comparison instructions describe matching the held card before storage",!GameDialogue.Text("fp-practice-open").Contains("empty hands")
                &&GameDialogue.Text("fp-practice-idle").Contains("before storing"));
            Preview(camera,"comparison-complete");Canvas.ForceUpdateCanvases();
            foreach(var label in station.Labels.Where(label=>label.isTextOverflowing))report.AppendLine("INFO: overflow in "+label.name+": "+label.text);
            Check("All comparison text fits the native monitor",station.Labels.All(label=>!label.isTextOverflowing));
            var after=FinalDeductionAssessment.Score(FinalDeductionAssessment.CaptureLive(),false,FinalDeductionAssessment.Rules);
            Check("Wrong and correct guesses preserve assessment score",before.totalScore==after.totalScore&&before.reasoningComponent==after.reasoningComponent&&before.relationshipComponent==after.relationshipComponent);
            Check("Comparison preserves sample custody and source evidence procedure status",inventory.AllTrackedEntryIds.SequenceEqual(originalEntries)&&inventory.GetCustody(station.CurrentSampleId)==custody
                &&state.GetRecord("EVD-014").status==status);
            Check("Identification and completion are recorded once",Count(SessionEventType.FingerprintPracticeCompleted)==1&&station.Submit()
                &&Count(SessionEventType.FingerprintPracticeCompleted)==1&&Count(SessionEventType.FingerprintIdentified)==1);
            yield return Tap("secondaryButton");Check("Actual B closes modal ownership while matched storage remains required",!station.IsShowing&&!PlayerUIGate.IsBlocked&&!ToolInputLock.HasUIOwners&&ProcedureInterlock.BlocksUnrelatedActions);
            Clear();yield return Tap("secondaryButton");
            Check("A separate B stores the matched card and releases the procedure gate",inventory.IsExplicitlyStoredSample(EvidenceInventory.FingerprintSampleId("EVD-014"))&&!ToolInputLock.IsLocked&&!ProcedureInterlock.BlocksUnrelatedActions);
            var vendorGuide=FindAnyObjectByType<ProcedureDestinationGuide>();Clear();vendorGuide.Refresh();
            Check("After card storage the next destination is the required vendor",vendorGuide.TargetId=="vendor-followup"&&vendorGuide.IsVisible);
            Clear();guide.Refresh();Check("Completed source retires the destination",!station.GuidanceNeeded&&!guide.DestinationActive);
            yield return Tap();Check("Completed sample no longer advertises comparison",!station.IsShowing&&station.IsComplete("EVD-014")&&!station.IsActionPromptVisible);
            camera.transform.position+=Vector3.back*4;yield return null;yield return null;
            Check("Walking away closes without losing completion or custody",!station.IsShowing&&!PlayerUIGate.IsBlocked&&station.IsComplete("EVD-014")&&inventory.IsExplicitlyStoredSample(EvidenceInventory.FingerprintSampleId("EVD-014")));
            yield return LiftCard("EVD-018");
            camera.transform.position=station.transform.position-station.transform.forward*1.25f+Vector3.up*1.6f;
            cordon.AccessRegister.RecordSimulationReposition(camera.transform.position);Clear();yield return Tap();
            Check("New stored source starts an independent comparison",station.IsShowing&&station.CurrentSourceId=="EVD-018"&&!station.IsComplete("EVD-018")&&station.AttemptsFor("EVD-018")==0);
            yield return new WaitForSecondsRealtime(.35f); // Respect the existing pointer's opening-gesture guard.
            yield return Click(station,"next-sample",camera);Check("Next-sample cannot switch from the required held card to a completed card",station.CurrentSourceId=="EVD-018"&&station.IsComplete("EVD-014"));
            station.SelectReference(1);station.Submit();
            Check("Second source has a distinct correct reference and independent completion",station.CurrentSourceId=="EVD-018"&&station.IsComplete("EVD-018")&&station.IsComplete("EVD-014")&&Count(SessionEventType.FingerprintPracticeCompleted)==2);
            PlayerUIGate.Enter(token);yield return null;Check("Modal interruption closes and preserves the other owner",!station.IsShowing&&PlayerUIGate.IsBlocked);PlayerUIGate.Exit(token);Clear();
            station.enabled=false;Check("Disable releases modal ownership while retaining the matched held card",!station.IsShowing&&!PlayerUIGate.IsBlocked&&!ToolInputLock.HasUIOwners&&inventory.CurrentHeldEvidenceId==EvidenceInventory.FingerprintSampleId("EVD-018"));station.enabled=true;
            Clear();Check("Matched card can still be stored after interruption",HeldItemCoordinator.Instance.TryStoreHeldEvidence()&&!ProcedureInterlock.BlocksUnrelatedActions);
        }
        finally{InputSystem.RemoveDevice(device);original.tag=tag;Destroy(eye);rig.enabled=true;station.Close();Clear();}
        SceneManager.LoadScene("CSI_Environment");yield return null;yield return null;yield return new WaitForSeconds(.2f);
        Check("Scene reload resets comparison completion, attempts and sample selection",!FingerprintComparisonStation.Instance.IsComplete("EVD-014")
            &&FingerprintComparisonStation.Instance.AttemptsFor("EVD-014")==0&&FingerprintComparisonStation.Instance.CurrentSampleId==null&&!FingerprintComparisonStation.Instance.IsShowing);
        Finish();
    }
}
