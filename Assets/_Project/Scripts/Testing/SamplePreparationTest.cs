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
public sealed class SamplePreparationTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== SamplePreparationTest ===\n");
    private int failures;
    private InputDevice device, left;
    private int visibleFillPixels;
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
    private void Preview(Camera camera,string name)
    {
        var position=camera.transform.position;var rotation=camera.transform.rotation;var station=SamplePreparationStation.Instance;
        camera.transform.position=station.transform.position-station.transform.forward*1.65f+Vector3.up*1.5f;
        camera.transform.LookAt(station.transform.position+Vector3.up*1.1f);
        var args=System.Environment.GetCommandLineArgs();int i=System.Array.IndexOf(args,"-phase5Output");
        var folder=i>=0?Path.GetDirectoryName(args[i+1]):"docs/evidence/sample-preparation";Directory.CreateDirectory(folder);
        var target=RenderTexture.GetTemporary(1280,960,24);var active=RenderTexture.active;
        var image=new Texture2D(1280,960,TextureFormat.RGB24,false);
        try { camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1280,960),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());
            visibleFillPixels=0;var ring=station.GetComponentInChildren<DryingProgressGraphic>();
            if(ring!=null)
            {
                var center=camera.WorldToViewportPoint(ring.transform.position);
                for(int x=Mathf.Max(0,(int)(center.x*1280)-45);x<Mathf.Min(1280,(int)(center.x*1280)+45);x++)
                    for(int y=Mathf.Max(0,(int)(center.y*960)-45);y<Mathf.Min(960,(int)(center.y*960)+45);y++)
                    {var pixel=image.GetPixel(x,y);if(pixel.b>.75f&&pixel.g>.65f&&pixel.r<.6f)visibleFillPixels++;}
            }
        }
        finally { camera.targetTexture=null;camera.transform.SetPositionAndRotation(position,rotation);RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(image); }
    }
    private void ValidateTraversal(GloveStationGuide.RouteData route,SamplePreparationStation station)
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
                Check("Player capsule reaches preparation route point " + point.ToString("F2") + " actual=" + actual.ToString("F2"), Vector3.Distance(actual, point) < .18f);
            }
        }
        finally { DestroyImmediate(probe); liveCapsule.enabled = liveCapsuleEnabled; Physics.SyncTransforms(); }
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
        var station=SamplePreparationStation.Instance;var swab=SwabTool.Instance;var state=EvidenceStateManager.Instance;
        var inventory=EvidenceInventory.Instance;var coordinator=HeldItemCoordinator.Instance;var cordon=FindAnyObjectByType<SceneCordon>();
        var guide=ArrivalPPEStation.Instance.GetComponent<GloveStationGuide>();
        var rig=FindAnyObjectByType<OVRCameraRig>();rig.enabled=false;var original=Camera.main;string tag=original.tag;original.tag="Untagged";
        var eye=new GameObject("Preparation test eye",typeof(Camera));var camera=eye.GetComponent<Camera>();camera.tag="MainCamera";camera.aspect=4f/3;camera.fieldOfView=60;
        InputSystem.RegisterLayout("{\"name\":\"PreparationController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"triggerPressed\",\"layout\":\"Button\",\"format\":\"FLT\",\"usages\":[\"TriggerButton\"]}]}");
        device=InputSystem.AddDevice("PreparationController");InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        try
        {
            camera.transform.position=new Vector3(18,2.8f,12.1f);
            station.RefreshDisplay();
            Check("Drying action UI is hidden before blood collection",!station.IsActionDisplayVisible&&!station.CanInteract);
            Check("One separate protected cabinet exists",station!=null && FindObjectsByType<SamplePreparationStation>(FindObjectsSortMode.None).Length==1
                && Vector3.Distance(station.transform.position,ArrivalPPEStation.Instance.transform.position)>.9f
                && station.transform.Find("Protective cabinet roof")!=null);
            Check("Cabinet and its standing approach are outside the controlled footprint",!cordon.IsWithinSecurityFootprint(station.transform.position)
                &&cordon.IsSecurelyOutside(station.transform.position-station.transform.forward*.95f,.3f));
            Check("Empty cabinet grants no progress or evidence",!station.Interact()&&!station.GuidanceNeeded&&swab.Preparation==SwabTool.PreparationStage.None&&!inventory.AllEntryIds.Any());
            foreach(string id in new[]{"EVD-014","EVD-015"})
            {
                state.MarkFound(id,ToolType.None);state.MarkPhotographedRaw(id,ToolType.Photographer);state.MarkChalkMarked(id,ToolType.ChalkMark);
                state.MarkTented(id,ToolType.EvidenceMarker,id=="EVD-014"?1:2);TraceMarkerTestUtil.MarkTraceIfRequired(id);state.MarkPhotographedFinal(id,ToolType.Photographer);
            }
            foreach (string id in new[]{"EVD-014","EVD-015"}) Check("Measurement/sketch fixture: "+id,MeasurementTestUtil.CompleteSketchFor(id));
            Equip(ToolType.Swab);Clear();
            Check("Sampling retains the knife source",swab.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-014"))&&swab.HasSample);
            Clear();Check("A loose swab cannot start cabinet drying",!station.Interact()&&!station.GuidanceNeeded);
            Check("Contained carrier needs drying",swab.TryContain()==TransitionResult.Applied&&swab.Preparation==SwabTool.PreparationStage.NeedsDrying);
            string sample=swab.PendingContainerId;var carrier=inventory.HeldVisual;int entries=inventory.AllEntryIds.Count();Clear();
            Check("Trigger cannot seal a wet sample",swab.TrySeal()==TransitionResult.Violation&&!state.GetRecord("EVD-014").swabSampleSealed);Clear();
            Check("B cannot store a wet sample",!coordinator.TryStoreHeldEvidence()&&!inventory.IsExplicitlyStoredSample(sample));Clear();guide.Refresh();
            Check("Preparation waypoint takes priority before glove changes",guide.DestinationActive&&guide.IsPinVisible&&guide.IsVisible&&!ArrivalPPEStation.Instance.GuidanceNeeded);
            Check("Across-house preparation is refused",!station.Interact());
            ValidateTraversal(guide.Route,station);
            camera.transform.position=station.transform.position-station.transform.forward*.95f+Vector3.up*1.6f;
            camera.transform.LookAt(station.transform.position+Vector3.up*1.2f);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,true);yield return new WaitForSecondsRealtime(.3f);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,true);Clear();
            Check("Exterior settled cabinet is available",station.CanInteract);
            yield return Button("primaryButton",true);
            Check("Actual A parks the same carrier at the labelled rack",swab.Preparation==SwabTool.PreparationStage.Drying
                &&carrier.transform.parent.name=="Source-specific drying position"&&inventory.CurrentHeldEvidenceId==sample&&inventory.AllEntryIds.Count()==entries);
            Check("Drying does not seal or store evidence",!inventory.IsSampleSealed(sample)&&!inventory.IsExplicitlyStoredSample(sample));
            yield return new WaitForSecondsRealtime(1.3f);
            var ring=station.GetComponentInChildren<DryingProgressGraphic>();
            Check("Circular fill advances proportionally",ring!=null&&ring.Progress>.2f&&ring.Progress<.65f&&Mathf.Abs(ring.Progress-swab.DryingProgress)<.02f);
            station.RefreshDisplay();Preview(camera,"drying-in-progress");
            Check("Circular fill renders visible cyan pixels in the actual station view",visibleFillPixels>30&&ring.canvasRenderer!=null);
            Check("Drying shows the exposed swab inside the protective cabinet",station.transform.Find("Source-specific drying position/Exposed swab in protected drying holder")!=null
                &&!carrier.GetComponentsInChildren<Renderer>().Any(r=>r.enabled));
            var label=station.GetComponentInChildren<UnityEngine.UI.Text>();
            Check("Sign identifies blood evidence drying and its source using casework wording",label.text.Contains("Air-drying bloodstained swab")&&label.text.Contains(EvidenceDisplay.Label("EVD-014"))
                &&!label.text.Contains("EVD-")&&label.preferredHeight<=label.rectTransform.rect.height);
            Check("Drying instructions use casework wording throughout",new[]{"sample-station-idle","sample-start-drying","sample-confirm-package",
                "sample-drying","sample-prepare-first","sample-package-ready","sample-destination","sample-finish-preparation"}
                .All(id=>!new[]{"simulat","practice","tutorial","sample"}.Any(word=>GameDialogue.Text(id).ToLowerInvariant().Contains(word))));
            STCSNotificationUI.Instance.Show(new DialogueRequest {text="Preparation interruption fixture",duration=.25f,
                priority=DialogueKind.Guidance,skippable=false,sourceId="preparation-test",historyCategory="System"});
            yield return null;yield return null;float spokenPause=swab.DryingProgress;
            yield return new WaitForSecondsRealtime(.1f);
            Check("Spoken guidance pauses drying while its own timer can run",STCSNotificationUI.Instance.IsShowing
                &&Mathf.Approximately(spokenPause,swab.DryingProgress)&&!PlayerUIGate.IsBlocked);
            yield return new WaitForSecondsRealtime(1.4f);
            Check("Spoken guidance finishes and drying resumes without a manual reset",!STCSNotificationUI.Instance.IsShowing&&swab.DryingProgress>spokenPause);
            var modal=new object();PlayerUIGate.Enter(modal);float paused=swab.DryingProgress;yield return new WaitForSecondsRealtime(.35f);
            Check("Modal pauses progress and actions without losing source",Mathf.Approximately(paused,swab.DryingProgress)&&!station.Interact()&&swab.PreparationSourceId=="EVD-014");PlayerUIGate.Exit(modal);
            camera.transform.position+=Vector3.back*4;paused=swab.DryingProgress;yield return new WaitForSecondsRealtime(.35f);
            Check("Walking away pauses and preserves the parked carrier",Mathf.Approximately(paused,swab.DryingProgress)&&carrier.transform.parent.name=="Source-specific drying position");
            Check("No storage or fresh supplies during preparation",!coordinator.TryStoreHeldEvidence()&&!ArrivalPPEStation.Instance.Interact());Clear();
            camera.transform.position=station.transform.position-station.transform.forward*.95f+Vector3.up*1.6f;
            yield return new WaitForSecondsRealtime(5);
            Check("Fill reaches one and remains dry but unpackaged",swab.Preparation==SwabTool.PreparationStage.Dried&&Mathf.Approximately(ring.Progress,1)
                &&carrier.GetComponent<PaperSwabPackage>()==null&&Count(SessionEventType.SampleDryingCompleted)==1);
            Check("Holding the initial A does not auto-confirm packaging",inventory.CurrentHeldEvidenceId==sample&&!inventory.IsSampleSealed(sample));
            Preview(camera,"drying-complete");yield return Button("primaryButton",false);
            station.enabled=false;yield return null;
            Check("Disabling cabinet restores carrier and releases its UI ownership",carrier.transform.parent==rig.rightHandAnchor&&!PlayerUIGate.IsBlocked&&swab.Preparation==SwabTool.PreparationStage.Dried);
            station.enabled=true;yield return null;Clear();yield return Tap();Clear();
            Check("Distinct A confirms source and retrieves paper package",swab.Preparation==SwabTool.PreparationStage.Packaged&&carrier.transform.parent==rig.rightHandAnchor
                &&carrier.GetComponent<PaperSwabPackage>()!=null&&carrier.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains(EvidenceDisplay.Label("EVD-014"))
                &&!carrier.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("EVD-"));
            Check("Packaging grants no seal, storage, glove or fresh-swab credit",!inventory.IsSampleSealed(sample)&&!inventory.IsExplicitlyStoredSample(sample)
                &&ArrivalPPEStation.Instance.NeedsChange&&!swab.HasFreshSwab&&Count(SessionEventType.SamplePackaged)==1);
            Check("Repeated preparation cannot duplicate or relabel the sample",!station.Interact()&&inventory.AllEntryIds.Count()==entries&&inventory.GetSampleSource(sample)=="EVD-014");
            yield return Tap("triggerPressed");Clear();
            Check("Actual trigger seals the prepared held package despite used gloves",inventory.IsSampleSealed(sample)
                &&!inventory.IsExplicitlyStoredSample(sample)&&ArrivalPPEStation.Instance.NeedsChange);
            Check("Early B resumes normal sealing and storage after preparation",coordinator.TryStoreHeldEvidence()&&inventory.IsSampleSealed(sample)&&inventory.IsExplicitlyStoredSample(sample));Clear();
            station.RefreshDisplay();
            Check("Completed preparation hides drying action UI",!station.IsActionDisplayVisible&&!station.CanInteract);
            Check("Used gloves prevent re-equipping the stored package",!coordinator.EquipEvidence(sample,rig.rightHandAnchor));
            yield return GloveChangeTestUtil.VisitAndReplace();Clear();
            Check("Stored package can be re-equipped with its wrapper and source",coordinator.EquipEvidence(sample,rig.rightHandAnchor)
                &&inventory.HeldVisual==carrier&&carrier.GetComponent<PaperSwabPackage>()!=null&&inventory.GetSampleSource(sample)=="EVD-014");
            Preview(camera,"preparation-station");coordinator.TryStoreHeldEvidence();Clear();
            Check("Next sample remains unmodified",!state.GetRecord("EVD-015").swabbingDone&&!state.GetRecord("EVD-015").swabSampleSealed);
            Check("One source produced exactly one set of preparation events",Count(SessionEventType.SampleDryingStarted)==1&&Count(SessionEventType.SampleDryingCompleted)==1&&Count(SessionEventType.SamplePackaged)==1);
            yield return GloveChangeTestUtil.VisitAndReplace();Clear();Equip(ToolType.Swab);Clear();
            Check("Second source gets its own fresh sample",swab.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-015"))&&swab.TryContain()==TransitionResult.Applied);
            Check("Second sample starts with no inherited drying or packaging",swab.Preparation==SwabTool.PreparationStage.NeedsDrying&&swab.DryingProgress==0&&swab.PreparationSourceId=="EVD-015");
            Check("Second source uses the same protected preparation path",SamplePreparationTestUtil.PrepareCurrent()&&coordinator.TryStoreHeldEvidence());Clear();
            Check("Independent packages retain original identities and custody",inventory.IsExplicitlyStoredSample(sample)&&inventory.GetSampleSource(sample)=="EVD-014"
                &&inventory.IsExplicitlyStoredSample("EVD-015")&&inventory.GetSampleSource("EVD-015")=="EVD-015"&&Count(SessionEventType.SamplePackaged)==2);
        }
        finally {InputSystem.RemoveDevice(device);original.tag=tag;Destroy(eye);rig.enabled=true;Clear();}
        SceneManager.LoadScene("CSI_Environment");yield return null;yield return null;yield return new WaitForSeconds(.2f);
        Check("Scene reload clears progress, pending source, package and cabinet ownership",SwabTool.Instance.Preparation==SwabTool.PreparationStage.None
            &&SwabTool.Instance.PreparationSourceId==null&&!SamplePreparationStation.Instance.GuidanceNeeded&&!EvidenceInventory.Instance.AllEntryIds.Any());
        Finish();
    }
}
