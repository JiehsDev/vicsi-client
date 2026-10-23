using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>Current checklist, real sample preparation, album geometry and XR inspection controls.</summary>
public sealed class UIWorkflowRefreshTest : MonoBehaviour
{
    public const int Revision = 2;
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== UIWorkflowRefreshTest ===\n");
    private int failures;
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private InputDevice device;
    private InputDevice leftDevice;
    public void RunAllTests() { Application.logMessageReceived+=OnLog;StartCoroutine(Run()); }
    private void OnDestroy()=>Application.logMessageReceived-=OnLog;
    private void OnLog(string message,string stack,LogType type) { if(type==LogType.Exception&&!IsDone){Check(message,false);Finish();} }
    private void Check(string name,bool valid){report.AppendLine((valid?"PASS: ":"FAIL: ")+name);if(!valid)failures++;}
    private void Finish(){report.AppendLine(failures==0?"=== PASS ===":"=== FAIL ===");Report=report.ToString();IsDone=true;}
    private static void Clear(){STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();}
    private static string Current()=>EvidenceProcedureMapResolver.ResolveMap("EVD-014").current?.stepId;
    private IEnumerator Button(bool down){InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"],down?1f:0f);yield return null;yield return null;}
    private IEnumerator Stick(Vector2 value){InputSystem.QueueDeltaStateEvent((Vector2Control)device["thumbstick"],value);yield return null;yield return null;}
    private void Preview(Camera pov,Transform panel,string name)
    {
        var go=new GameObject("UI review render camera",typeof(Camera));var camera=go.GetComponent<Camera>();camera.CopyFrom(pov);camera.enabled=false;
        camera.transform.SetPositionAndRotation(pov.transform.position,pov.transform.rotation);camera.transform.LookAt(panel.position);camera.fieldOfView=60;camera.aspect=4f/3;
        var target=RenderTexture.GetTemporary(1280,960,24);var previous=RenderTexture.active;var texture=new Texture2D(1280,960,TextureFormat.RGB24,false);
        try{Canvas.ForceUpdateCanvases();camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,960),0,0);texture.Apply();
            Directory.CreateDirectory("docs/evidence/ui-refresh-2026-10-20");File.WriteAllBytes("docs/evidence/ui-refresh-2026-10-20/"+name+".png",texture.EncodeToPNG());}
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Destroy(texture);Destroy(go);}
    }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(UIWorkflowRefreshTest));yield return null;
        if(ReasoningCheckpointManager.Instance!=null)ReasoningCheckpointManager.Instance.enabled=false;
        if(HypothesisCheckpointManager.Instance!=null)HypothesisCheckpointManager.Instance.enabled=false;
        if(ProceduralTrainingGuidance.Instance!=null)ProceduralTrainingGuidance.Instance.enabled=false;
        if(NarrativeEventManager.Instance!=null){NarrativeEventManager.Instance.enabled=false;NarrativeEventManager.Instance.StopAllCoroutines();}
        Clear();
        var emptyAlbum=FindFirstObjectByType<PhotoAlbumUI>(FindObjectsInactive.Include);emptyAlbum.Show();yield return null;
        Check("Empty gallery explains how to take photographs",emptyAlbum.IsEmptyStateVisible);emptyAlbum.Hide();
        Check("Scene route can be completed before evidence UI checks",ScenePhotographyTestUtil.CompleteRoute());Clear();
        var state=EvidenceStateManager.Instance;var rig=FindFirstObjectByType<OVRCameraRig>();rig.enabled=false;
        var pov=Camera.main;var originalPosition=pov.transform.position;var originalRotation=pov.transform.rotation;
        state.MarkFound("EVD-014",ToolType.None);
        Check("Original-photo instruction excludes the ruler",EvidenceProcedureMapResolver.ResolveMap("EVD-014").instruction.Contains("without a ruler"));
        state.MarkPhotographedRaw("EVD-014",ToolType.Photographer);state.MarkChalkMarked("EVD-014",ToolType.ChalkMark);state.MarkTented("EVD-014",ToolType.EvidenceMarker,1);TraceMarkerTestUtil.MarkTraceIfRequired("EVD-014");
        MeasurementTestUtil.RecordExact("EVD-014",1);MeasurementTestUtil.RecordExact("EVD-014",2);MeasurementTestUtil.RecordAvailablePairsFor("EVD-014");Clear();
        Check("Scene Sketch is required immediately after measurement",EvidenceProcedureMapResolver.ResolveMap("EVD-014").current?.stepId=="sketch");
        Check("Measured item placed on Scene Sketch",MeasurementTestUtil.CompleteSketchFor("EVD-014"));Clear();
        var map=EvidenceProcedureMapResolver.ResolveMap("EVD-014");
        Check("Final-photo checklist explains the ruler and its controls",map.current?.stepId=="final_photo"&&map.instruction.Contains("ruler")&&map.instruction.Contains("(A)"));
        state.MarkPhotographedFinal("EVD-014",ToolType.Photographer);Clear();
        var coordinator=HeldItemCoordinator.Instance;var swab=SwabTool.Instance;
        coordinator.ToggleEquipTool(ToolType.Swab,rig.rightHandAnchor);Clear();
        Check("Actual swab can recover the trace",swab.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId("EVD-014")));Clear();
        Check("Actual containment leaves drying as the next action",swab.TryContain()==TransitionResult.Applied&&Current()=="dry_sample");Clear();
        var inventory=EvidenceInventory.Instance;string sample=swab.PendingContainerId;
        var station=SamplePreparationStation.Instance;var access=FindAnyObjectByType<SceneCordon>().AccessRegister;
        pov.transform.position=station.transform.position-station.transform.forward*.95f+Vector3.up*1.6f;access.RecordSimulationReposition(pov.transform.position);Clear();
        Check("Drying starts through the real station",station.Interact());
        swab.AdvanceDrying(station,1);
        Check("Checklist reports drying progress without completing packaging",EvidenceProcedureMapResolver.ResolveMap("EVD-014").subStep?.Contains("%") == true
            &&Current()=="dry_sample"&&!inventory.IsSampleSealed(sample));
        swab.AdvanceDrying(station,SwabTool.SimulatedDryingSeconds);
        Check("Dried swab requires explicit paper-package retrieval",Current()=="package_sample");
        Check("Retrieving the paper package advances only to sealing",station.Interact()&&Current()=="seal_sample");Clear();
        Check("Sealing still requires B storage",swab.TrySeal()==TransitionResult.Applied&&Current()=="store_sample"&&!inventory.IsExplicitlyStoredSample(sample));Clear();
        Check("Explicit storage requires fresh gloves before more evidence work",coordinator.TryStoreHeldEvidence()&&Current()=="replace_gloves");Clear();
        yield return GloveChangeTestUtil.VisitAndReplace();Clear();
        Check("Fresh gloves restore fingerprint work",Current()=="fingerprint_process");
        Check("Other evidence receives no preparation credit",state.GetRecord("EVD-015").status==EvidenceStatus.NotFound);
        var album=PhotoAlbumManager.Instance;var ui=FindFirstObjectByType<PhotoAlbumUI>(FindObjectsInactive.Include);
        // Route completion can now populate the gallery; inspect that context too.
        Check("Scene capture records keep their route identity",album.Records.All(r=>r.Kind==PhotoCaptureKind.Scene));
        pov.transform.SetPositionAndRotation(new Vector3(1000,1000,1000),Quaternion.identity);
        ui.Show();yield return null;Check("Album opens without a second modal owner",PlayerUIGate.LockOwnerNames.Contains("PhotoAlbumUI"));ui.Hide();
        var texture=new Texture2D(400,300);var pixels=Enumerable.Repeat(new Color(.25f,.38f,.5f),400*300).ToArray();texture.SetPixels(pixels);texture.Apply();
        album.AddPhoto(texture,"EVD-014","Kitchen knife",PhotoCaptureKind.Original);
        // Each capture has its own texture identity, as it does in the capture listener.
        var second=new Texture2D(400,300);second.SetPixels(pixels);second.Apply();album.AddPhoto(second,"EVD-014","Kitchen knife",PhotoCaptureKind.ScaledFinal);
        var record=album.Records.Last();Check("Photo metadata retains subject, stage and UTC capture time",record.SubjectId=="EVD-014"&&record.Kind==PhotoCaptureKind.ScaledFinal&&record.CapturedAtUtc.Kind==System.DateTimeKind.Utc);
        InputSystem.RegisterLayout("{\"name\":\"UIReviewController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"},{\"name\":\"thumbstick\",\"layout\":\"Vector2\",\"format\":\"VEC2\"}]}");
        device=InputSystem.AddDevice("UIReviewController");InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        ui.Show();typeof(PhotoAlbumUI).GetMethod("SetSelectedIndex",Flags).Invoke(ui,new object[]{album.Photos.Count-2});yield return null;yield return null;
        Check("Gallery identifies the original photograph",ui.SelectedCaption.Contains("Kitchen knife")&&ui.SelectedCaption.Contains("Original"));
        var gallery=(GameObject)typeof(PhotoAlbumUI).GetField("panelRoot",Flags).GetValue(ui);
        Preview(pov,gallery.transform,"album-gallery");
        yield return Button(true);yield return Button(false);
        var size=ui.DisplayedImage.rect.size;
        Check("Large view preserves 4:3 with letterboxing",Mathf.Abs(size.x/size.y-4f/3)<.001f&&ui.Zoom==1);
        yield return Stick(Vector2.up);yield return Stick(Vector2.zero);
        Check("Actual XR up zooms without changing photograph",ui.Zoom==2&&ui.SelectedCaption.Contains("Original"));
        leftDevice=InputSystem.AddDevice("UIReviewController");InputSystem.SetDeviceUsage(leftDevice,CommonUsages.LeftHand);
        ui.SetZoom(4);
        InputSystem.QueueDeltaStateEvent((Vector2Control)leftDevice["thumbstick"],Vector2.right);
        yield return new WaitForSecondsRealtime(2);
        InputSystem.QueueDeltaStateEvent((Vector2Control)leftDevice["thumbstick"],Vector2.zero);yield return null;
        var viewport=(RectTransform)ui.DisplayedImage.parent;
        float extent=(ui.DisplayedImage.rect.width-viewport.rect.width)*.5f;
        Check("Actual left XR stick pans and stays inside the photo bounds",ui.DisplayedImage.anchoredPosition.x>extent*.9f
            &&ui.DisplayedImage.anchoredPosition.x<=extent+.01f);
        yield return Stick(Vector2.right);yield return Stick(Vector2.zero);
        Check("Browsing resets zoom and identifies the next photo",ui.Zoom==1&&ui.SelectedCaption.Contains("with scale"));
        var panel=(GameObject)typeof(PhotoAlbumUI).GetField("panelRoot",Flags).GetValue(ui);
        foreach(var text in panel.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.name=="Selected photo caption"||t.name=="Album controls"))
        {
            text.ForceMeshUpdate();Check(text.name+" fits without ellipsis",!text.isTextOverflowing&&text.preferredHeight<=text.rectTransform.rect.height);
        }
        Preview(pov,panel.transform,"album-inspection");
        ui.Hide();Check("Closing album releases modal ownership",!PlayerUIGate.IsBlockedByOther(null));
        InputSystem.RemoveDevice(device);device=null;InputSystem.RemoveDevice(leftDevice);leftDevice=null;InputSystem.RemoveLayout("UIReviewController");
        pov.transform.SetPositionAndRotation(originalPosition,originalRotation);rig.enabled=true;
        Finish();
    }
}
