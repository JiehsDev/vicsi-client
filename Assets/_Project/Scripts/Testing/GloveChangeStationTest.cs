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
public sealed class GloveChangeStationTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== GloveChangeStationTest ===\n");
    private int failures;
    private InputDevice device;
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
        var args=System.Environment.GetCommandLineArgs();int i=System.Array.IndexOf(args,"-phase5Output");
        var folder=i>=0?Path.GetDirectoryName(args[i+1]):"docs/evidence/glove-change";Directory.CreateDirectory(folder);
        var target=RenderTexture.GetTemporary(1280,960,24);var active=RenderTexture.active;
        var image=new Texture2D(1280,960,TextureFormat.RGB24,false);
        try { camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1280,960),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG()); }
        finally { camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(image); }
    }
    private void ValidateTraversal(GloveStationGuide.RouteData route,ArrivalPPEStation station)
    {
        var source = FindFirstObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        var probe = new GameObject("Glove table route capsule probe") { layer = 2 };
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
                Check("Real player capsule can follow the exit route to " + point.ToString("F2") + " actual=" + actual.ToString("F2"), Vector3.Distance(actual, point) < .18f);
            }
        }
        finally { DestroyImmediate(probe); liveCapsule.enabled = liveCapsuleEnabled; Physics.SyncTransforms(); }
    }
    private IEnumerator Run()
    {
        yield return null;
        var pre=FindAnyObjectByType<PreSessionFlowController>();
        if(pre!=null) {pre.enabled=false;PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken",Flags).GetValue(pre));}
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if(ReasoningCheckpointManager.Instance!=null)ReasoningCheckpointManager.Instance.enabled=false;
        if(HypothesisCheckpointManager.Instance!=null)HypothesisCheckpointManager.Instance.enabled=false;
        if(ProceduralTrainingGuidance.Instance!=null)ProceduralTrainingGuidance.Instance.enabled=false;
        if(NarrativeEventManager.Instance!=null){NarrativeEventManager.Instance.enabled=false;NarrativeEventManager.Instance.StopAllCoroutines();}
        Clear();ScenePhotographyTestUtil.CompleteRoute();Clear();
        var cordon=FindAnyObjectByType<SceneCordon>();var station=cordon.PPEStation;var guide=station.GetComponent<GloveStationGuide>();
        var state=EvidenceStateManager.Instance;var inventory=EvidenceInventory.Instance;var swab=SwabTool.Instance;
        Check("Exactly one shared glove state/station; no inventory item",station==ArrivalPPEStation.Instance&&station.HasCleanGloves
            &&FindObjectsByType<ArrivalPPEStation>(FindObjectsSortMode.None).Length==1&&!inventory.AllEntryIds.Any());
        Check("Recognisable dispenser/table/disposal are physical station props",station.transform.Find("Clean glove dispenser box")!=null
            &&station.transform.Find("Glove supply tabletop")!=null&&station.transform.Find("Used glove disposal bin")!=null);
        var rig=FindAnyObjectByType<OVRCameraRig>();rig.enabled=false;
        var original=Camera.main;string tag=original.tag;original.tag="Untagged";
        var eye=new GameObject("Glove station test eye",typeof(Camera));var camera=eye.GetComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=60;camera.aspect=4f/3;
        InputSystem.RegisterLayout("{\"name\":\"GloveStationController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"}]}");
        device=InputSystem.AddDevice("GloveStationController");InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        try
        {
            camera.transform.position=new Vector3(18,2.8f,12.1f);Clear();guide.Refresh();
            Check("Clean gloves suppress waypoint",!guide.IsPinVisible&&!guide.IsVisible);
            int visits=cordon.AccessRegister.Visits.Count, exits=Count(SessionEventType.SceneAccessExited);
            var outsideBaseline=station.transform.position-station.transform.forward*1.15f+Vector3.up*1.6f;
            cordon.AccessRegister.ObserveBoundary(outsideBaseline,outsideBaseline,cordon.CanEnter);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,cordon.CanEnter);
            yield return new WaitForSecondsRealtime(.25f);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,cordon.CanEnter);
            Check("Matched head/capsule boundary samples open an authorized visit",cordon.AccessRegister.IsInside&&cordon.AccessRegister.Visits.Count==visits+1);
            ValidateTraversal(guide.Route,station);
            int used=Count(SessionEventType.GlovesUsedForSampling),arrival=Count(SessionEventType.PPEEquipped);
            foreach(string id in new[]{"EVD-014","EVD-015"})
            {
                state.MarkFound(id,ToolType.None);state.MarkPhotographedRaw(id,ToolType.Photographer);state.MarkChalkMarked(id,ToolType.ChalkMark);
                state.MarkTented(id,ToolType.EvidenceMarker,id=="EVD-014"?1:2);TraceMarkerTestUtil.MarkTraceIfRequired(id);
                state.MarkPhotographedFinal(id,ToolType.Photographer);
            }
            Clear();Check("Rejected sample does not consume clean gloves",!swab.TrySampleRequiredSwab(null)&&station.HasCleanGloves);
            foreach (string id in new[]{"EVD-014","EVD-015"}) Check("Measurement/sketch fixture: "+id,MeasurementTestUtil.CompleteSketchFor(id));
            Equip(ToolType.Swab);Clear();
            Check("Successful blade sample marks gloves used once",swab.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-014"))
                &&station.NeedsChange&&station.GlovesEquipped&&Count(SessionEventType.GlovesUsedForSampling)==used+1);
            Check("Used gloves enforce follow-up without a modal movement lock",ProcedureInterlock.GloveChangeRequired
                && !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners && SceneTurnover.IsDeductionBlocked);
            Check("Unrelated tools are unavailable but the current swab can finish",!PlayerToolRegistry.CanEquip(ToolType.Photographer)
                && !PlayerToolRegistry.CanEquip(ToolType.Recorder) && PlayerToolRegistry.CanEquip(ToolType.Swab));
            state.MarkFound("EVD-018",ToolType.None);
            Check("Direct evidence actions cannot bypass glove enforcement",state.MarkPhotographedRaw("EVD-018",ToolType.Photographer)==TransitionResult.Violation
                && state.GetRecord("EVD-018").status==EvidenceStatus.Found);
            Check("Measurements wait for fresh gloves",MeasurementStateManager.Instance.RecordMeasurement("blocked-fixture","ref",1)==MeasurementRecordResult.PrerequisiteBlocked);
            Check("Sampling guidance survives the success notification",NotificationManager.Instance.UI.CurrentTooltip==GameDialogue.Text("gloves-finish-sample"));
            Clear();guide.Refresh();
            Check("Uncontained sample suppresses travel and disposal",!station.CurrentSampleStored&&!guide.IsPinVisible&&!station.Interact());
            Check("Current sample can still be contained with used gloves",swab.TryContain()==TransitionResult.Applied&&state.GetRecord("EVD-014").swabbingDone);
            string sample=swab.PendingContainerId;Clear();
            Check("Contained sample still requires explicit storage",sample!=null&&!station.CurrentSampleStored&&!station.Interact());
            var store=FindAnyObjectByType<HeldEvidenceStorePrompt>();
            yield return SamplePreparationTestUtil.VisitAndPrepare();
            Check("Current sample is dried and individually packaged before storage",SamplePreparationTestUtil.LastVisitSucceeded);
            // Preparation is now its own observed exterior trip before the supply trip.
            exits=Count(SessionEventType.SceneAccessExited);visits=cordon.AccessRegister.Visits.Count-1;
            Check("B storage completes current sample while gloves remain used",store.TryStore()&&inventory.IsSampleSealed(sample)
                &&inventory.IsExplicitlyStoredSample(sample)&&station.NeedsChange&&station.CurrentSampleStored);
            Check("Securing the sample does not bypass required glove replacement",ProcedureInterlock.GloveChangeRequired
                && !PlayerToolRegistry.CanEquip(ToolType.Swab) && !ProcedureInterlock.AllowsHandling("EVD-018"));
            Clear();guide.Refresh();
            Check("Stored sample activates waypoint and a route from the kitchen",guide.IsPinVisible&&guide.IsVisible&&guide.ArrowPositions.Count>0);
            camera.transform.position=new Vector3(17.7f,2.8f,12.05f);camera.transform.LookAt(new Vector3(15.55f,1.2f,11.9f));guide.Refresh();Preview(camera,"kitchen-waypoint");
            var waypointText=guide.WaypointCanvas.GetComponentInChildren<UnityEngine.UI.Text>();
            Check("Destination instruction fits its actual panel",waypointText.preferredHeight<=waypointText.rectTransform.rect.height);
            var modal=new object();PlayerUIGate.Enter(modal);guide.Refresh();Check("Modal UI suppresses destination and route",!guide.IsPinVisible&&!guide.IsVisible);PlayerUIGate.Exit(modal);guide.Refresh();
            using(var outer=GloveStationGuide.HideForCapture())
            {
                using(var inner=GloveStationGuide.HideForCapture())Check("Capture hides pin and arrows",!guide.IsPinVisible&&!guide.IsVisible);
                Check("Nested capture remains hidden",!guide.IsPinVisible&&!guide.IsVisible);
            }
            Check("Capture restores needed navigation",guide.IsPinVisible);
            Equip(ToolType.Swab);Clear();int sampleCount=inventory.AllEntryIds.Count();
            Check("Next source refuses used gloves without progress",!swab.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-015"))
                &&!swab.HasSample&&!state.GetRecord("EVD-015").swabbingDone&&inventory.AllEntryIds.Count()==sampleCount);
            HeldItemCoordinator.Instance.HolsterCurrentTool();Clear();
            Check("Station cannot replace gloves from across the house",!station.Interact());
            camera.transform.position=(cordon.transform.Find("EntrancePost_L").position+cordon.transform.Find("EntrancePost_R").position)*.5f+Vector3.forward*.7f+Vector3.up*1.1f;
            Check("Inside the secured house cannot remove gloves",cordon.RequiresBriefingAt(camera.transform.position)&&!station.Interact());
            camera.transform.position=station.transform.position-station.transform.forward*1.15f+Vector3.up*1.6f;
            Check("Disposal waits for the observed exit to settle",!station.Interact());
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,cordon.CanEnter);
            yield return new WaitForSecondsRealtime(.25f);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,cordon.CanEnter);
            Check("Table trip records a real exit without repeating authorization",!cordon.AccessRegister.IsInside&&Count(SessionEventType.SceneAccessExited)==exits+1&&cordon.AccessRegister.IsAuthorized);
            camera.transform.LookAt(station.transform.position+Vector3.up*.88f);Clear();yield return null;guide.Refresh();Preview(camera,"supply-table");
            Check("Local table prompt takes over without duplicate destination label",!guide.IsPinVisible&&!guide.IsVisible);
            Check("Used gloves refuse unrelated tool pickup before disposal",!HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Recorder,rig.rightHandAnchor)
                &&station.NeedsChange&&HeldItemCoordinator.Instance.CurrentCategory==HeldItemCoordinator.HeldCategory.None);Clear();
            Check("Station owns A ahead of nearby closed register",PlayerUIGate.ConfirmOwnerToken==station);
            report.AppendLine("INFO: table position="+station.transform.position.ToString("F4"));
            Check("Supply table is outside controlled footprint",!cordon.RequiresBriefingAt(camera.transform.position));
            var token=new object();PlayerUIGate.Enter(token);yield return Tap();
            Check("Modal UI blocks disposal",station.NeedsChange);PlayerUIGate.Exit(token);Clear();
            yield return Button("primaryButton",true);
            Check("One A disposes used gloves without wearing replacements",station.State==ArrivalPPEStation.GloveState.NotWorn&&!station.PairPickedUp&&!cordon.PPEReady);
            Check("Disposal alone keeps unrelated actions blocked until fresh gloves are worn",ProcedureInterlock.GloveChangeRequired&&!PlayerToolRegistry.CanEquip(ToolType.Recorder));
            Check("Bare hands cannot take the next sample",!swab.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-015"))&&!swab.HasSample);
            yield return null;yield return null;Check("Held disposal A cannot pick up clean pair",!station.PairPickedUp);
            yield return Button("primaryButton",false);Clear();yield return Tap();
            Check("Next deliberate A picks up a pair",station.PairPickedUp&&!station.GlovesEquipped);
            Vector3 tableEye=camera.transform.position;camera.transform.position+=Vector3.right*4;yield return null;yield return null;
            Check("Walking away returns pending pair without completing change",!station.PairPickedUp&&!station.HasCleanGloves);
            camera.transform.position=tableEye;Clear();yield return Tap();
            yield return Tap("secondaryButton");Check("B returns the pair without fresh credit",!station.PairPickedUp&&!station.HasCleanGloves);
            Clear();yield return Tap();yield return Button("primaryButton",true);
            Check("Separate A wears fresh gloves and clears requirement",station.HasCleanGloves&&station.ReplacementCount==1&&station.LastSampleSource==null);
            yield return null;yield return Button("primaryButton",false);Clear();guide.Refresh();
            Check("Glove change alone preserves the fresh-swab destination",station.GuidanceNeeded&&cordon.AccessRegister.IsAuthorized&&cordon.PPEReady&&!swab.HasFreshSwab);
            yield return Tap();yield return Tap();Clear();guide.Refresh();
            Check("Completing both supplies hides the waypoint",swab.HasFreshSwab&&!guide.IsPinVisible&&!guide.IsVisible);
            Check("One disposal/change event and no repeated arrival PPE event",Count(SessionEventType.UsedGlovesDisposed)==1
                &&Count(SessionEventType.FreshGlovesEquipped)==1&&Count(SessionEventType.PPEEquipped)==arrival);
            Check("Glove change preserves source and sealed stored sample",inventory.IsSampleSealed(sample)&&inventory.IsExplicitlyStoredSample(sample)
                &&state.GetRecord("EVD-014").swabbingDone&&!state.GetRecord("EVD-015").swabbingDone);
            camera.transform.position=new Vector3(18,2.8f,12.1f);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,cordon.CanEnter);
            yield return new WaitForSecondsRealtime(.25f);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,cordon.CanEnter);
            Check("Returning after replacement opens the next physical visit",cordon.AccessRegister.IsInside&&cordon.AccessRegister.Visits.Count==visits+2&&cordon.AccessRegister.IsAuthorized);
            Equip(ToolType.Swab);Clear();Check("Fresh gloves allow next source and become used again",swab.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-015"))
                &&station.NeedsChange&&Count(SessionEventType.GlovesUsedForSampling)==used+2);
        }
        finally {InputSystem.RemoveDevice(device);original.tag=tag;Destroy(eye);rig.enabled=true;Clear();}
        SceneManager.LoadScene("CSI_Environment");yield return null;yield return null;yield return new WaitForSeconds(.2f);
        Check("Reload clears glove changes, pending source and samples",ArrivalPPEStation.Instance.State==ArrivalPPEStation.GloveState.NotWorn
            &&ArrivalPPEStation.Instance.ReplacementCount==0&&ArrivalPPEStation.Instance.LastSampleSource==null&&!EvidenceInventory.Instance.AllEntryIds.Any());
        var freshGuide=ArrivalPPEStation.Instance.GetComponent<GloveStationGuide>();Clear();freshGuide.Refresh();
        Check("Waypoint waits for the First Responder briefing",!freshGuide.IsPinVisible&&!freshGuide.IsVisible);
        SceneTurnover.Controller.CompleteWithoutDialogue();Clear();
        Camera.main.transform.position=ArrivalPPEStation.Instance.transform.position+Vector3.back*3+Vector3.up*1.6f;
        freshGuide.Refresh();Check("Initial glove waypoint works during IOC assessment",freshGuide.IsPinVisible&&SceneTurnover.Phase==ScenarioPhase.IOCAssessment);
        Finish();
    }
}
