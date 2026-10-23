using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

/// <summary>Actual A input, camera framing/capture, negative gates and lifetime for the photo ruler.</summary>
public sealed class PhotographicScaleTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== PhotographicScaleTest ===\n");
    private int failures;
    private InputDevice device;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private void Check(string name,bool ok) { report.AppendLine((ok?"PASS: ":"FAIL: ")+name);if(!ok)failures++; }
    public void RunAllTests() { DontDestroyOnLoad(gameObject);Application.logMessageReceived+=OnException;StartCoroutine(Run()); }
    private void OnDestroy() => Application.logMessageReceived-=OnException;
    private void OnException(string message,string stack,LogType type)
    {
        if(type!=LogType.Exception || IsDone) return;
        failures++;report.AppendLine("FAIL: Unhandled diagnostic exception: "+message+"\n"+stack);
        report.AppendLine("=== FAIL: exception ===");Report=report.ToString();IsDone=true;
    }
    private void Clear()
    {
        STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();NotificationManager.Instance?.UI.HidePrompt();
    }
    private IEnumerator Button(bool down)
    {
        InputSystem.QueueDeltaStateEvent((ButtonControl)device["primaryButton"],down?1f:0f);
        yield return null;yield return null;
    }
    private static void AimMode(PhotographTool camera,bool on) => typeof(PhotographTool).GetMethod("SetAiming",Private).Invoke(camera,new object[]{on});
    private static void Shutter(PhotographTool camera) => typeof(PhotographTool).GetMethod("TakePhoto",Private).Invoke(camera,null);
    private static int Count(SessionEventType type) => SessionLogger.Instance.EventCounts.TryGetValue(type,out int count)?count:0;
    private void Save(Texture2D texture,string name)
    {
        var args=System.Environment.GetCommandLineArgs();int index=System.Array.IndexOf(args,"-phase5Output");
        string path=index>=0?Path.GetDirectoryName(args[index+1]):"docs/evidence/photo-scale";
        Directory.CreateDirectory(path);File.WriteAllBytes(Path.Combine(path,name+".png"),texture.EncodeToPNG());
    }
    private void SaveViewfinder(Camera pov,string name)
    {
        var go=new GameObject("Scale HUD preview camera",typeof(Camera));var camera=go.GetComponent<Camera>();
        camera.CopyFrom(pov);camera.enabled=false;camera.aspect=4f/3f;
        camera.transform.SetPositionAndRotation(pov.transform.position,pov.transform.rotation);
        var target=RenderTexture.GetTemporary(960,720,24);var oldActive=RenderTexture.active;
        var image=new Texture2D(960,720,TextureFormat.RGB24,false);
        try { camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();Save(image,name); }
        finally {camera.targetTexture=null;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(target);Destroy(image);Destroy(go);}
    }
    private void CheckHint(PhotographTool camera,string label)
    {
        var hint=camera.ViewfinderOverlay.transform.Find("Photo scale camera accessory guidance").GetComponentInChildren<TMPro.TMP_Text>();
        Canvas.ForceUpdateCanvases();hint.ForceMeshUpdate();
        Check(label,hint.gameObject.activeInHierarchy&&!hint.isTextOverflowing&&hint.preferredHeight<=hint.rectTransform.rect.height);
    }
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(PhotographicScaleTest));yield return null;
        var pre=FindAnyObjectByType<PreSessionFlowController>();
        if(pre!=null) { pre.enabled=false;PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken",Private).GetValue(pre)); }
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if(ReasoningCheckpointManager.Instance!=null) ReasoningCheckpointManager.Instance.enabled=false;
        if(HypothesisCheckpointManager.Instance!=null) HypothesisCheckpointManager.Instance.enabled=false;
        var hyp=FindAnyObjectByType<HypothesisCheckpointUI>();if(hyp!=null)hyp.enabled=false;
        if(ProceduralTrainingGuidance.Instance!=null) ProceduralTrainingGuidance.Instance.enabled=false;
        if(NarrativeEventManager.Instance!=null) { NarrativeEventManager.Instance.enabled=false;NarrativeEventManager.Instance.StopAllCoroutines(); }
        Clear();
        var camera=FindAnyObjectByType<PhotographTool>(FindObjectsInactive.Include);var scale=camera.PhotoScale;
        var state=EvidenceStateManager.Instance;var album=PhotoAlbumManager.Instance;
        Check("Accessory is installed once without a tool-wheel role",scale!=null&&camera.GetComponents<PhotographicScaleAccessory>().Length==1
            && scale.GetComponents<PlayerTool>().Length==1 && !scale.IsPlaced);
        Check("Selected items use scales; body and derived prints retain their existing workflows",scale.RequiresScale("EVD-014")
            &&scale.RequiresScale("EVD-015")&&scale.RequiresScale("EVD-018")&&!scale.RequiresScale("BODY-A")&&!scale.RequiresScale("EVD-016"));
        Check("All ruler framing prompts resolve real catalog text",new[]{"photo-scale-fit","photo-scale-front","photo-scale-obstructed",
            "photo-scale-closer","photo-scale-aim-item","photo-scale-ready","photo-scale-remove"}.All(id=>ViCSIDialogueCatalog.Current.Find(id)!=null));
        Check("Scene photography route completes independently",ScenePhotographyTestUtil.CompleteRoute());Clear();
        var rig=FindFirstObjectByType<OVRCameraRig>();bool rigEnabled=rig.enabled;rig.enabled=false;
        var original=Camera.main;var originalTag=original.tag;original.tag="Untagged";
        var eye=new GameObject("Photo scale fixture eye",typeof(Camera));var pov=eye.GetComponent<Camera>();pov.tag="MainCamera";pov.fieldOfView=45;pov.aspect=1.9f;
        typeof(PhotographTool).GetField("povCamera",Private).SetValue(camera,pov);
        typeof(PhotographTool).GetField("povCameraReady",Private).SetValue(camera,false);
        var follow=camera.ViewfinderOverlay.GetComponent<HudFollowCamera>();
        var hudCamera=typeof(HudFollowCamera).GetField("targetCamera",Private);
        var originalHudCamera=hudCamera.GetValue(follow);hudCamera.SetValue(follow,pov);
        InputSystem.RegisterLayout("{\"name\":\"ViCSIPhotoScaleController\",\"extend\":\"XRController\",\"controls\":[{\"name\":\"primaryButton\",\"layout\":\"Button\",\"format\":\"FLT\"}]}");
        device=InputSystem.AddDevice("ViCSIPhotoScaleController");InputSystem.SetDeviceUsage(device,CommonUsages.RightHand);
        var token=new object();
        var positions=EvidenceProp.All.Where(p=>!string.IsNullOrEmpty(p.evidenceId)).ToDictionary(p=>p,p=>p.transform.position);
        try
        {
            HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Photographer,rig.rightHandAnchor);AimMode(camera,true);
            yield return new WaitForSecondsRealtime(.35f);
            foreach(string id in new[]{"EVD-014","EVD-015","EVD-018"})
            {
                state.MarkFound(id,ToolType.None);PhotoScaleTestUtil.Aim(camera,id);yield return null;Clear();
                Check(id+" cannot place a scale before its original photograph",camera.CurrentAimTarget?.evidenceId==id&&!scale.TryPlace());
                int before=album.Photos.Count;Shutter(camera);
                Check(id+" original photo saves without a scale or higher resolution",state.GetRecord(id).status==EvidenceStatus.PhotographedRaw
                    &&!scale.IsPlaced&&!camera.LastCaptureUsedScale&&album.Photos.Count==before+1&&album.Photos.Last().height==360);
                Save(album.Photos.Last(),id+"-original");Clear();
                Check(id+" original album record carries its subject and stage",album.Records.Last().SubjectId==id
                    &&album.Records.Last().Kind==PhotoCaptureKind.Original&&!string.IsNullOrEmpty(album.Records.Last().SubjectName));
                Check(id+" ruler placement waits for the existing item preparation steps",!scale.TryPlace());
                state.MarkChalkMarked(id,ToolType.None);state.MarkTented(id,ToolType.None,id=="EVD-014"?1:id=="EVD-015"?2:3);
                TraceMarkerTestUtil.MarkTraceIfRequired(id);PhotoScaleTestUtil.Aim(camera,id);Clear();
                // Confirm guards on the first item; all items then use actual A placement/removal.
                if(id=="EVD-014")
                {
                    PlayerUIGate.Enter(token);yield return Button(true);
                    Check("Other modal UI cannot place the ruler",!scale.IsPlaced&&!scale.TryPlace());
                    yield return Button(false);PlayerUIGate.Exit(token);
                    AimMode(camera,false);yield return Button(true);
                    Check("Lowered camera cannot place or consume A",!scale.IsPlaced&&!scale.TryPlace());yield return Button(false);AimMode(camera,true);
                    yield return new WaitForSecondsRealtime(.3f);
                    PhotoScaleTestUtil.Aim(camera,id);
                    var near=pov.transform.position;pov.transform.position+=Vector3.up*4;
                    Check("Distant assisted placement is refused",!scale.TryPlace());pov.transform.position=near;
                }
                before=album.Photos.Count;Shutter(camera);
                Check(id+" final photograph refuses a missing scale",state.GetRecord(id).status==EvidenceStatus.Marked&&album.Photos.Count==before&&!camera.LastCaptureUsedScale);
                Clear();yield return null;
                if(id=="EVD-014")
                {
                    yield return new WaitForSecondsRealtime(.2f);CheckHint(camera,"Assisted placement prompt fits the real viewfinder");
                    SaveViewfinder(pov,"camera-place-scale");
                }
                int placed=Count(SessionEventType.PhotographicScalePlaced);
                yield return Button(true);
                Check(id+" deliberate A places one assisted ruler for the current item",scale.IsPlaced&&scale.PlacedEvidenceId==id
                    &&Count(SessionEventType.PhotographicScalePlaced)==placed+1&&PlayerUIGate.ConfirmOwnerToken==camera);
                yield return null;yield return null;
                Check(id+" held A cannot also remove the ruler",scale.IsPlaced&&Count(SessionEventType.PhotographicScalePlaced)==placed+1);
                yield return Button(false);
                if(!scale.IsPlaced) { Check("Cannot continue without actual placement",false);break; }
                Check(id+" ruler is metric and render-only",Mathf.Abs(Vector3.Distance(scale.ScaleTransform.TransformPoint(Vector3.left*.15f),
                    scale.ScaleTransform.TransformPoint(Vector3.right*.15f))-.3f)<.00001f
                    &&scale.ScaleTransform.GetComponentsInChildren<Collider>().Length==0
                    &&scale.ScaleTransform.GetComponentsInChildren<Renderer>().Length==1
                    &&scale.ScaleTransform.GetComponent<Renderer>().sharedMaterial.shader.isSupported);
                if(id=="EVD-014")
                {
                    Check("A scale belonging to another item cannot satisfy its final photo",!scale.AllowsPhoto("EVD-018",EvidenceStatus.PhotographedFinal,out _));
                    Check("A visible ruler cannot appear in an original photograph",!scale.AllowsPhoto("EVD-015",EvidenceStatus.PhotographedRaw,out _));
                    HeldItemCoordinator.Instance.HolsterCurrentTool();yield return null;yield return null;
                    Check("Holstering returns the accessory without changing evidence progress",!scale.IsPlaced&&state.GetRecord(id).status==EvidenceStatus.Marked);
                    HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Photographer,rig.rightHandAnchor);AimMode(camera,true);
                    yield return new WaitForSecondsRealtime(.3f);PhotoScaleTestUtil.Aim(camera,id);yield return Button(true);yield return Button(false);
                }
                Vector3 position=pov.transform.position;Quaternion rotation=pov.transform.rotation;
                if(id=="EVD-015")
                {
                    pov.transform.SetPositionAndRotation(EvidenceProp.FindById(id).transform.position+new Vector3(.2f,.85f,-.16f),
                        Quaternion.LookRotation(Vector3.down,Vector3.forward));
                    Check("Visible bloodstain wings must fit even when the smaller placeholder and ruler fit",
                        !scale.AllowsPhoto(id,EvidenceStatus.PhotographedFinal,out _));
                    pov.transform.SetPositionAndRotation(position,rotation);
                }
                pov.transform.rotation=Quaternion.LookRotation(pov.transform.right);before=album.Photos.Count;Shutter(camera);
                Check(id+" cropped/out-of-frame ruler cannot advance the photo",state.GetRecord(id).status==EvidenceStatus.Marked&&album.Photos.Count==before);
                pov.transform.SetPositionAndRotation(position,rotation);Clear();PhotoScaleTestUtil.Aim(camera,id);
                var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Ruler occlusion fixture";
                blocker.transform.position=Vector3.Lerp(pov.transform.position,scale.ScaleTransform.position,.6f);blocker.transform.localScale=Vector3.one*.08f;Physics.SyncTransforms();
                Check(id+" an obstructed ruler is refused",!scale.AllowsPhoto(id,EvidenceStatus.PhotographedFinal,out string blockedReason)
                    &&blockedReason==GameDialogue.Text("photo-scale-obstructed"));
                blocker.GetComponent<Collider>().enabled=false;Destroy(blocker);Physics.SyncTransforms();
                PhotoScaleTestUtil.Aim(camera,id);Clear();yield return null;
                bool ready=scale.AllowsPhoto(id,EvidenceStatus.PhotographedFinal,out string why);
                report.AppendLine("INFO: "+id+" readiness="+ready+" target="+camera.CurrentAimTarget?.evidenceId+" reason="+why);
                Check(id+" real saved-photo aspect accepts the whole item and ruler",ready&&camera.IsConfirmedForCapture);
                if(id!="EVD-018")
                {
                    var nearPosition=pov.transform.position;var nearRotation=pov.transform.rotation;
                    pov.transform.position=EvidenceProp.FindById(id).transform.position+Vector3.up*1.6f+Vector3.back*.2f;
                    pov.transform.LookAt(Vector3.Lerp(EvidenceProp.FindById(id).transform.position,scale.ScaleTransform.position,.5f));
                    Check(id+" standing-height photography is accepted without a forced crouch",scale.AllowsPhoto(id,EvidenceStatus.PhotographedFinal,out _));
                    pov.transform.position=EvidenceProp.FindById(id).transform.position+Vector3.up*1.8f+Vector3.back*1.1f;
                    pov.transform.LookAt(Vector3.Lerp(EvidenceProp.FindById(id).transform.position,scale.ScaleTransform.position,.5f));
                    typeof(PhotographTool).GetMethod("UpdateAimTarget",Private).Invoke(camera,null);
                    Check(id+" standing a short step back gives a green frame",scale.AllowsPhoto(id,EvidenceStatus.PhotographedFinal,out _) && camera.IsConfirmedForCapture);
                    scale.Refresh();
                    Check(id+" standing view shows ready guidance",scale.GuidanceText==GameDialogue.Text("photo-scale-ready"));
                    pov.transform.position=EvidenceProp.FindById(id).transform.position+Vector3.up*2.8f+Vector3.back*1.1f;
                    pov.transform.LookAt(Vector3.Lerp(EvidenceProp.FindById(id).transform.position,scale.ScaleTransform.position,.5f));
                    Check(id+" unreadably distant ruler still asks the player to move closer",!scale.AllowsPhoto(id,EvidenceStatus.PhotographedFinal,out string distantReason)
                        &&distantReason==GameDialogue.Text("photo-scale-closer"));
                    pov.transform.SetPositionAndRotation(nearPosition,nearRotation);
                }
                var frontPosition=pov.transform.position;var frontRotation=pov.transform.rotation;
                pov.transform.position=scale.ScaleTransform.position+scale.ScaleTransform.forward*.7f;pov.transform.LookAt(scale.ScaleTransform.position);
                Check(id+" blank reverse face of ruler cannot satisfy scaled photography",!scale.AllowsPhoto(id,EvidenceStatus.PhotographedFinal,out _));
                pov.transform.SetPositionAndRotation(frontPosition,frontRotation);
                if(id=="EVD-014")
                {
                    pov.transform.position=EvidenceProp.FindById(id).transform.position+Vector3.up*1.8f+Vector3.back*1.1f;
                    pov.transform.LookAt(Vector3.Lerp(EvidenceProp.FindById(id).transform.position,scale.ScaleTransform.position,.5f));
                    typeof(PhotographTool).GetMethod("UpdateAimTarget",Private).Invoke(camera,null);
                    yield return new WaitForSecondsRealtime(.2f);CheckHint(camera,"Removal/framing prompt fits the real viewfinder");
                    SaveViewfinder(pov,"camera-scale-placed");
                }
                before=album.Photos.Count;int captured=Count(SessionEventType.PhotographicScaleCaptured);Shutter(camera);
                bool saved=state.GetRecord(id).status==EvidenceStatus.PhotographedFinal&&album.Photos.Count==before+1;
                Check(id+" scaled capture advances the existing final state exactly once",saved&&camera.LastCaptureUsedScale&&Count(SessionEventType.PhotographicScaleCaptured)==captured+1);
                if(saved)
                {
                    var image=album.Photos.Last();Save(image,id+"-scaled");
                    Check(id+" real saved image has readable-scale resolution and no overlay",image.width==1440&&image.height==1080&&camera.ViewfinderOverlay.activeSelf);
                    Check(id+" final album record distinguishes the scaled capture",album.Records.Last().SubjectId==id&&album.Records.Last().Kind==PhotoCaptureKind.ScaledFinal);
                }
                Shutter(camera);Check(id+" repeated shutter creates no extra photo or scale completion",album.Photos.Count==before+1&&Count(SessionEventType.PhotographicScaleCaptured)==captured+1);
                Clear();yield return Button(true);yield return Button(false);
                Check(id+" explicit A removes the ruler while retaining the completed photo",!scale.IsPlaced&&state.GetRecord(id).status==EvidenceStatus.PhotographedFinal);
            }
            Check("No source item moved and no inventory/sample processing was granted",positions.All(p=>p.Key!=null&&p.Key.transform.position==p.Value)
                &&new[]{"EVD-014","EVD-015","EVD-018"}.All(id=>!state.GetRecord(id).swabbingDone&&!state.GetRecord(id).fingerprintingDone
                    &&state.GetRecord(id).custodyLocation==EvidenceCustodyLocation.Scene));
        }
        finally
        {
            PlayerUIGate.Exit(token);scale.Remove("fixture-ended");AimMode(camera,false);
            original.tag=originalTag;rig.enabled=rigEnabled;
            typeof(PhotographTool).GetField("povCamera",Private).SetValue(camera,original);
            typeof(PhotographTool).GetField("povCameraReady",Private).SetValue(camera,false);
            hudCamera.SetValue(follow,originalHudCamera);
            Destroy(eye);InputSystem.RemoveDevice(device);InputSystem.RemoveLayout("ViCSIPhotoScaleController");
        }
        yield return SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);yield return null;
        var fresh=FindAnyObjectByType<PhotographTool>(FindObjectsInactive.Include);
        Check("Reload starts without a ruler or carried-over scaled capture",fresh!=null&&!fresh.PhotoScale.IsPlaced&&!fresh.LastCaptureUsedScale);
        report.AppendLine(failures==0?"=== PASS ===":"=== FAIL: "+failures+" ===");Report=report.ToString();IsDone=true;Debug.Log(Report);
    }
}
