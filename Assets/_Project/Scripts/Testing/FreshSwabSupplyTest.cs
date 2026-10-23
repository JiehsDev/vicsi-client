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
public sealed class FreshSwabSupplyTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== FreshSwabSupplyTest ===\n");
    private int failures;
    private InputDevice device, left;
    private ToolWheelController wheel;
    private string wheelFeedback;
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
        var folder=i>=0?Path.GetDirectoryName(args[i+1]):"docs/evidence/fresh-swab";Directory.CreateDirectory(folder);
        var target=RenderTexture.GetTemporary(1280,960,24);var active=RenderTexture.active;
        var image=new Texture2D(1280,960,TextureFormat.RGB24,false);
        try { camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1280,960),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG()); }
        finally { camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(image); }
    }
    private object SwabView()
    {
        var views=(System.Collections.IList)typeof(ToolWheelController).GetField("segmentViews",Flags).GetValue(wheel);
        foreach(var view in views)if((ToolType)view.GetType().GetField("Role").GetValue(view)==ToolType.Swab)return view;
        return null;
    }
    private IEnumerator WheelSwab(Camera camera,string image,bool muted)
    {
        Clear();InputSystem.QueueDeltaStateEvent((ButtonControl)left["secondaryButton"],1f);yield return null;yield return null;
        Check("Actual Y opens the wheel: "+image,wheel.IsCanvasActive);
        var views=(System.Collections.IList)typeof(ToolWheelController).GetField("segmentViews",Flags).GetValue(wheel);
        int index=views.IndexOf(SwabView());float angle=(index+.5f)*Mathf.PI*2/views.Count;
        InputSystem.QueueDeltaStateEvent((Vector2Control)device["thumbstick"],new Vector2(Mathf.Sin(angle),Mathf.Cos(angle)));
        yield return null;yield return new WaitForSecondsRealtime(.2f);
        var view=SwabView();var type=view.GetType();var icon=(UnityEngine.UI.Image)type.GetField("Icon").GetValue(view);
        var monogram=(TMPro.TMP_Text)type.GetField("Monogram").GetValue(view);
        float alpha=icon!=null?icon.color.a:monogram.color.a;
        var subtitle=(TMPro.TMP_Text)typeof(ToolWheelController).GetField("centerSubtitle",Flags).GetValue(wheel);
        var wedge=(UnityEngine.UI.Image)type.GetField("Wedge").GetValue(view);
        Check("Hovered swab retains correct opacity and hover style: "+image,muted?alpha<.4f&&wedge.color.r<.1f&&(float)type.GetField("LiftT").GetValue(view)==0:alpha>.9f&&wedge.color.r>.9f);
        Check("Wheel reason fits the actual hub: "+image,!subtitle.isTextOverflowing&&(!muted||subtitle.text==SwabTool.Instance.AvailabilityReason));
        Canvas.ForceUpdateCanvases();Preview(camera,image);
        InputSystem.QueueDeltaStateEvent((ButtonControl)left["secondaryButton"],0f);yield return null;yield return null;
        Check("Y release closes the wheel: "+image,!wheel.IsCanvasActive);
        InputSystem.QueueDeltaStateEvent((Vector2Control)device["thumbstick"],Vector2.zero);yield return null;
        wheelFeedback=NotificationManager.Instance.UI.CurrentTooltip;Clear();
    }
    private IEnumerator Run()
    {
        yield return null;
        var pre=FindAnyObjectByType<PreSessionFlowController>();if(pre!=null){pre.enabled=false;PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken",Flags).GetValue(pre));}
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if(ReasoningCheckpointManager.Instance!=null)ReasoningCheckpointManager.Instance.enabled=false;
        if(HypothesisCheckpointManager.Instance!=null)HypothesisCheckpointManager.Instance.enabled=false;
        if(ProceduralTrainingGuidance.Instance!=null)ProceduralTrainingGuidance.Instance.enabled=false;
        if(NarrativeEventManager.Instance!=null){NarrativeEventManager.Instance.enabled=false;NarrativeEventManager.Instance.StopAllCoroutines();}
        Clear();ScenePhotographyTestUtil.CompleteRoute();Clear();
        var station=ArrivalPPEStation.Instance;var swab=SwabTool.Instance;var state=EvidenceStateManager.Instance;
        var inventory=EvidenceInventory.Instance;var coordinator=HeldItemCoordinator.Instance;var cordon=FindAnyObjectByType<SceneCordon>();
        var guide=station.GetComponent<GloveStationGuide>();wheel=FindAnyObjectByType<ToolWheelController>();
        var rig=FindAnyObjectByType<OVRCameraRig>();rig.enabled=false;var original=Camera.main;string tag=original.tag;original.tag="Untagged";
        var eye=new GameObject("Fresh swab test eye",typeof(Camera));var camera=eye.GetComponent<Camera>();camera.tag="MainCamera";camera.aspect=4f/3;camera.fieldOfView=60;
        foreach(var follow in FindObjectsByType<HudFollowCamera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            typeof(HudFollowCamera).GetField("targetCamera",Flags).SetValue(follow,camera);
        InputSystem.RegisterLayout("{\"name\":\"FreshSwabController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"secondaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"thumbstick\",\"layout\":\"Stick\",\"format\":\"VEC2\"}]}");
        device=InputSystem.AddDevice("FreshSwabController");InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        left=InputSystem.AddDevice("FreshSwabController");InputSystem.SetDeviceUsage(left,CommonUsages.LeftHand);
        try
        {
            camera.transform.position=station.transform.position-station.transform.forward*1.15f+Vector3.up*1.6f;
            camera.transform.LookAt(station.transform.position+Vector3.up*.9f);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,true);yield return new WaitForSecondsRealtime(.3f);
            cordon.AccessRegister.ObserveBoundary(camera.transform.position,camera.transform.position,true);
            Check("Normal session starts without a fresh swab",!swab.HasFreshSwab&&station.HasCleanGloves&&station.SwabSupplyNeeded&&!swab.CanEquipCurrent);
            Check("Initial swab supply does not distract from earlier scene photography",!station.GuidanceNeeded&&!swab.SupplyRequested);
            Check("One shared supply table includes a packaged swab tray",station.transform.Find("Fresh sealed swab pack")!=null&&station.transform.Find("Packaged swab supply tray")!=null);
            Equip(ToolType.Recorder);yield return WheelSwab(camera,"wheel-no-fresh-swab",true);
            Check("Unavailable wheel selection preserves the held recorder",PlayerToolRegistry.CurrentRole==ToolType.Recorder);
            Check("Trying the unavailable slot requests the shared supply destination",station.GuidanceNeeded&&swab.SupplyRequested);
            Check("Registry and coordinator reject exhaustion before changing tools",!coordinator.ToggleEquipTool(ToolType.Swab,rig.rightHandAnchor)
                &&!PlayerToolRegistry.ToggleEquip(ToolType.Swab,rig.rightHandAnchor)&&!PlayerToolRegistry.RequestEquip(ToolType.Swab)&&PlayerToolRegistry.CurrentRole==ToolType.Recorder);
            coordinator.HolsterCurrentTool();Clear();
            var token=new object();PlayerUIGate.Enter(token);yield return Tap();Check("Unrelated modal UI cannot grant a pack",!station.IsSwabPackPickedUp&&!swab.HasFreshSwab);PlayerUIGate.Exit(token);
            yield return Button("primaryButton",true);Check("A picks up an unopened pack without loading it",station.IsSwabPackPickedUp&&!swab.HasFreshSwab);
            yield return null;yield return null;Check("Held opening A cannot load the swab",station.IsSwabPackPickedUp&&!swab.HasFreshSwab);
            yield return Button("primaryButton",false);yield return Tap("secondaryButton");Check("B returns an unopened pack",!station.IsSwabPackPickedUp&&!swab.HasFreshSwab);
            yield return Tap();var near=camera.transform.position;camera.transform.position+=Vector3.back*5;yield return null;yield return null;
            Check("Walking away returns the pack without a fresh swab",!station.IsSwabPackPickedUp&&!swab.HasFreshSwab);camera.transform.position=near;
            yield return Tap();PlayerUIGate.Enter(token);yield return null;yield return null;Check("Modal interruption cancels a pending pack",!station.IsSwabPackPickedUp&&!swab.HasFreshSwab);PlayerUIGate.Exit(token);Clear();
            yield return Tap();yield return Tap();Clear();Check("Distinct A loads one fresh swab without evidence or glove credit",swab.HasFreshSwab&&station.HasCleanGloves&&!inventory.AllEntryIds.Any()&&Count(SessionEventType.FreshSwabLoaded)==1);
            yield return WheelSwab(camera,"wheel-fresh-swab",false);Check("Fresh wheel selection equips the real kit",PlayerToolRegistry.CurrentRole==ToolType.Swab&&swab.IsVirtuallyEquipped);
            foreach(string id in new[]{"EVD-014","EVD-015"})
            {
                state.MarkFound(id,ToolType.None);state.MarkPhotographedRaw(id,ToolType.Photographer);state.MarkChalkMarked(id,ToolType.ChalkMark);state.MarkTented(id,ToolType.EvidenceMarker,id=="EVD-014"?1:2);
                TraceMarkerTestUtil.MarkTraceIfRequired(id);state.MarkPhotographedFinal(id,ToolType.Photographer);
            }
            foreach (string id in new[]{"EVD-014","EVD-015"}) Check("Measurement/sketch fixture: "+id,MeasurementTestUtil.CompleteSketchFor(id));
            Clear();Check("Rejected sampling does not consume the unused swab",!swab.TrySampleRequiredSwab(null)&&swab.HasFreshSwab);
            Check("Successful sampling consumes exactly one fresh swab",swab.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-014"))&&!swab.HasFreshSwab&&swab.HasSample&&Count(SessionEventType.FreshSwabUsed)==1);
            var tip=(Renderer)typeof(SwabTool).GetField("tipRenderer",Flags).GetValue(swab);var sampledMesh=tip.GetComponent<MeshFilter>().sharedMesh;
            Equip(ToolType.Recorder);Clear();Check("Holstering retains the blood-loaded swab",swab.HasSample&&tip.GetComponent<MeshFilter>().sharedMesh==sampledMesh);
            yield return WheelSwab(camera,"wheel-loaded-recovery",true);Check("Grey loaded kit is recoverable to finish its sample",PlayerToolRegistry.CurrentRole==ToolType.Swab&&swab.HasSample&&swab.CanEquipCurrent);
            Check("Containment uses the original source despite consumed swab",swab.TryContain()==TransitionResult.Applied&&state.GetRecord("EVD-014").swabbingDone);
            string sample=swab.PendingContainerId;Check("A pending container blocks replacement packs",!station.Interact()&&!swab.NeedsFreshSupply);
            yield return WheelSwab(camera,"wheel-contained-sample",true);
            Check("Contained-sample wheel selection preserves the held sample and asks to finish it",inventory.CurrentHeldEvidenceId==sample
                &&PlayerToolRegistry.CurrentRole==ToolType.None&&wheelFeedback==swab.AvailabilityReason);
            Check("Current sample completes protected preparation before B",SamplePreparationTestUtil.PrepareCurrent());
            Clear();yield return Tap("secondaryButton");Clear();
            Check("Actual B seals and stores current sample with used gloves",inventory.IsSampleSealed(sample)&&inventory.IsExplicitlyStoredSample(sample)&&station.NeedsChange&&!swab.HasUnfinishedSample);
            Check("Sealing never restores the clean tip or swab availability",!swab.HasFreshSwab&&!swab.CanEquipCurrent&&!tip.enabled&&tip.GetComponent<MeshFilter>().sharedMesh==sampledMesh);
            Check("Another source cannot use the spent swab",!swab.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-015"))) ;Clear();
            Check("Stored sample keeps its blood-bearing swab model",inventory.GetSampleSource(sample)=="EVD-014"&&inventory.IsExplicitlyStoredSample(sample));
            yield return Tap();yield return Tap();yield return Tap();Clear();
            Check("Fresh gloves alone leave the swab exhausted",station.HasCleanGloves&&!swab.HasFreshSwab&&station.GuidanceNeeded&&station.DestinationText==GameDialogue.Text("swab-destination"));
            Check("Unavailable swab cannot unequip sealed held evidence",coordinator.EquipEvidence(sample,rig.rightHandAnchor)&&!coordinator.ToggleEquipTool(ToolType.Swab,rig.rightHandAnchor)&&inventory.CurrentHeldEvidenceId==sample);
            coordinator.TryStoreHeldEvidence();Clear();yield return WheelSwab(camera,"wheel-used-swab",true);
            Check("Selecting the spent slot creates no kit",PlayerToolRegistry.CurrentRole==ToolType.None&&!swab.IsVirtuallyEquipped);
            Preview(camera,"outside-swab-supply");yield return Tap();yield return Tap();Clear();guide.Refresh();
            Check("Fresh supply alone restores the slot and retires the shared destination",swab.HasFreshSwab&&!station.GuidanceNeeded&&!guide.IsPinVisible&&Count(SessionEventType.FreshSwabLoaded)==2);
            // Exercise the legacy generic sampling entry point with a reversible runtime-only fixture.
            var record=state.GetRecord("EVD-018");var definition=record.definition;var prior=record.status;
            var fixture=Instantiate(definition);fixture.requiresSwabbing=false;record.definition=fixture;record.status=EvidenceStatus.ReadyForCollection;
            try { Check("Generic sampling also consumes one swab",swab.TrySwab(EvidenceProp.FindById("EVD-018"))&&!swab.HasFreshSwab&&Count(SessionEventType.FreshSwabUsed)==2); }
            finally {record.definition=definition;record.status=prior;Destroy(fixture);}
        }
        finally {InputSystem.RemoveDevice(device);InputSystem.RemoveDevice(left);original.tag=tag;Destroy(eye);rig.enabled=true;Clear();}
        SceneManager.LoadScene("CSI_Environment");yield return null;yield return null;yield return new WaitForSeconds(.2f);
        Check("Reload has no fresh swab, pending pack or sample",!SwabTool.Instance.HasFreshSwab&&!SwabTool.Instance.HasUnfinishedSample&&!ArrivalPPEStation.Instance.IsSwabPackPickedUp&&!EvidenceInventory.Instance.AllEntryIds.Any());
        Finish();
    }
}
