using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>Actual scene-photo completion, independent discovery, world sizing, depth rendering and clean saved photos.</summary>
public sealed class EvidenceDiscoveryDotTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    readonly StringBuilder report = new("=== EvidenceDiscoveryDotTest ===\n");
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    int failures;
    void Check(string name, bool ok) { if (!ok) failures++; report.AppendLine((ok ? "PASS: " : "FAIL: ") + name); }
    public void RunAllTests() { AutomatedTestSandbox.Activate(nameof(EvidenceDiscoveryDotTest)); StartCoroutine(Run()); }
    IEnumerator Run()
    {
        yield return null;
        var pre=FindAnyObjectByType<PreSessionFlowController>();
        if(pre!=null){pre.enabled=false;PlayerUIGate.Exit(typeof(PreSessionFlowController).GetField("uiGateToken",Private).GetValue(pre));}
        FindAnyObjectByType<StudentIdentificationPanel>(FindObjectsInactive.Include)?.Hide();
        if(ReasoningCheckpointManager.Instance!=null)ReasoningCheckpointManager.Instance.enabled=false;
        if(HypothesisCheckpointManager.Instance!=null)HypothesisCheckpointManager.Instance.enabled=false;
        if(ProceduralTrainingGuidance.Instance!=null)ProceduralTrainingGuidance.Instance.enabled=false;
        if(NarrativeEventManager.Instance!=null){NarrativeEventManager.Instance.enabled=false;NarrativeEventManager.Instance.StopAllCoroutines();}
        STCSNotificationUI.Instance?.HideImmediately();
        var esm=EvidenceStateManager.Instance;var photos=ScenePhotographyManager.Instance;
        string[] ids={"BODY-A","EVD-014","EVD-015","EVD-018"};
        var props=ids.Select(EvidenceProp.FindById).ToArray();var dots=props.Select(p=>p.GetComponent<EvidenceDiscoveryDot>()).ToArray();
        var positions=props.Select(p=>p.transform.position).ToArray();var scales=props.Select(p=>p.transform.localScale).ToArray();
        Check("All four physical items auto-attach exactly one dot",dots.All(d=>d!=null)&&props.All(p=>p.GetComponents<EvidenceDiscoveryDot>().Length==1));
        if(dots.Any(d=>d==null)){Finish();yield break;}
        Check("Hidden fingerprint targets stay excluded",EvidenceProp.All.Where(p=>p.RequiresExternalDiscovery).All(p=>p.GetComponent<EvidenceDiscoveryDot>()==null));
        Check("Held tool placeholders stay excluded",FindObjectsByType<EvidenceProp>().Where(p=>string.IsNullOrEmpty(p.evidenceId)).All(p=>p.GetComponent<EvidenceDiscoveryDot>()==null));
        foreach(var dot in dots)dot.Refresh();
        Check("No dots before completed scene photography",!photos.IsScenePhotographyComplete&&dots.All(d=>!d.IsVisible));
        Check("Initial discovery states are unchanged",ids.All(id=>esm.GetRecord(id).status==EvidenceStatus.NotFound));
        var original=Camera.main;string oldTag=original.tag;original.tag="Untagged";
        var cameraObject=new GameObject("Discovery dot camera",typeof(Camera));var cam=cameraObject.GetComponent<Camera>();cam.tag="MainCamera";cam.enabled=true;cam.fieldOfView=70;cam.aspect=1.4f;
        var rig=FindAnyObjectByType<OVRCameraRig>();bool rigEnabled=rig!=null&&rig.enabled;if(rig!=null)rig.enabled=false;
        int baselineEvents=SessionLogger.Instance.EventCounts.Values.Sum();bool allAtCompletion=false;
        Action completed=()=>allAtCompletion=dots.All(d=>d.IsVisible);
        photos.OnScenePhotographyCompleted+=completed;
        try
        {
            Check("Original photo route completes through real shutter gates",ScenePhotographyTestUtil.CompleteRoute());
            Check("All undiscovered dots appear together on the completion event",allAtCompletion&&dots.All(d=>d.IsVisible));
            int eventsAfter=SessionLogger.Instance.EventCounts.Values.Sum();
            for(int j=0;j<20;j++)foreach(var dot in dots)dot.Refresh();
            Check("Dot refresh never discovers evidence or writes session events",ids.All(id=>esm.GetRecord(id).status==EvidenceStatus.NotFound)&&SessionLogger.Instance.EventCounts.Values.Sum()==eventsAfter);
            for(int i=0;i<dots.Length;i++)
            {
                var dot=dots[i];var rect=(RectTransform)dot.DotCanvas.transform;
                Check(ids[i]+": small constant 5 cm world diameter",Mathf.Abs(rect.rect.width*rect.lossyScale.x-.05f)<.0001f&&Mathf.Abs(rect.lossyScale.y-1)<.0001f);
                Check(ids[i]+": dot floats above physical item",dot.WorldPosition.y>props[i].transform.position.y);
                Check(ids[i]+": no text, arrows, hit targets or colliders",dot.DotCanvas.GetComponentsInChildren<Text>().Length==0
                    &&dot.DotCanvas.GetComponent<GraphicRaycaster>()==null&&dot.DotCanvas.GetComponentsInChildren<Collider>().Length==0
                    &&dot.DotCanvas.GetComponent<EvidenceDiscoveryDotGraphic>()!=null&&!dot.DotCanvas.GetComponent<EvidenceDiscoveryDotGraphic>().raycastTarget);
                Canvas.ForceUpdateCanvases();
                var graphic=dot.DotCanvas.GetComponent<EvidenceDiscoveryDotGraphic>();
                Check(ids[i]+": native circle mesh and supported depth-tested shader",graphic.canvasRenderer.GetMesh().vertexCount>0&&graphic.material.shader.isSupported);
            }
            cam.transform.rotation=Quaternion.Euler(10,35,0);foreach(var dot in dots)dot.Refresh();
            Check("Dots billboard toward headset without moving evidence",dots.All(d=>Quaternion.Angle(d.DotCanvas.transform.rotation,cam.transform.rotation)<.01f));
            using(EvidenceDiscoveryDot.HideForCapture())
            {
                Check("Synchronous capture scope hides every dot",dots.All(d=>!d.IsVisible));
                using(EvidenceDiscoveryDot.HideForCapture())Check("Nested capture remains hidden",dots.All(d=>!d.IsVisible));
                Check("Inner disposal does not restore outer capture",dots.All(d=>!d.IsVisible));
            }
            Check("Capture scope restores still-undiscovered dots",dots.All(d=>d.IsVisible));
            bool sawCapture=false,dotInCapture=false;
            Action<ScriptableRenderContext,Camera> captureCheck=(context,camera)=>{if(camera.name=="PhotoCaptureCamera"){sawCapture=true;dotInCapture|=dots.Any(d=>d.IsVisible);}};
            RenderPipelineManager.beginCameraRendering+=captureCheck;
            var listenerObject=new GameObject("Discovery dot album fixture");
            try
            {
                var listener=listenerObject.AddComponent<PhotoCaptureListener>();
                listener.CaptureScenePhoto(photos.Points[0]);
                Check("Actual album render excludes dots and restores the live view",sawCapture&&!dotInCapture&&dots.All(d=>d.IsVisible));
            }
            finally{RenderPipelineManager.beginCameraRendering-=captureCheck;Destroy(listenerObject);}
            RenderOcclusion(cam,dots[0]);foreach(var dot in dots)dot.Refresh();
            var phase=typeof(SceneTurnover).GetProperty("Phase").GetSetMethod(true);var originalPhase=SceneTurnover.Phase;
            try
            {
                phase.Invoke(null,new object[]{ScenarioPhase.IOCAssessment});foreach(var dot in dots)dot.Refresh();
                Check("IOC phase cannot reveal SOCO evidence dots",dots.All(d=>!d.IsVisible));
            }
            finally{phase.Invoke(null,new object[]{originalPhase});foreach(var dot in dots)dot.Refresh();}
            Check("Returning to allowed investigation restores all remaining dots",dots.All(d=>d.IsVisible));
            props[1].enabled=false;dots[1].Refresh();Check("Disabled evidence hides its dot",!dots[1].IsVisible);props[1].enabled=true;dots[1].Refresh();
            dots[1].enabled=false;Check("Disabling dot immediately hides standalone visual",!dots[1].IsVisible);dots[1].enabled=true;dots[1].Refresh();
            Check("Re-enabling undiscovered evidence restores its dot",dots[1].IsVisible);
            int foundEvents=0;Action<string,EvidenceStatus> found=(id,status)=>{if(status==EvidenceStatus.Found)foundEvents++;};
            EvidenceStateManager.OnEvidenceStatusChanged+=found;
            try
            {
                Check("Existing discovery call still applies",esm.MarkFound("EVD-014",ToolType.None)==TransitionResult.Applied);
                Check("Discovery keeps every unrecorded evidence dot visible",dots.All(d=>d.IsVisible));
                Check("Repeated discovery does not create extra events",esm.MarkFound("EVD-014",ToolType.None)==TransitionResult.Duplicate&&foundEvents==1);
                using(EvidenceDiscoveryDot.HideForCapture())esm.MarkFound("EVD-018",ToolType.None);
                Check("Capture restores discovered but unrecorded evidence dots",dots.All(d=>d.IsVisible));
                var setter=typeof(ScenePhotographyManager).GetProperty("IsScenePhotographyComplete").GetSetMethod(true);
                setter.Invoke(photos,new object[]{false});foreach(var dot in dots)dot.Refresh();
                Check("Missing photo completion hides remaining dots",dots.All(d=>!d.IsVisible));
                setter.Invoke(photos,new object[]{true});foreach(var dot in dots)dot.Refresh();
                Check("Discovered unrecorded items relight when photo readiness returns",dots.All(d=>d.IsVisible));
                esm.MarkFound("BODY-A",ToolType.None);esm.MarkFound("EVD-015",ToolType.None);
                Check("Discovering all evidence leaves all unrecorded dots visible",dots.All(d=>d.IsVisible)&&foundEvents==4);
            }
            finally{EvidenceStateManager.OnEvidenceStatusChanged-=found;}
            foreach(var id in ids)
            {
                var record=esm.GetRecord(id);var dot=dots[Array.IndexOf(ids,id)];
                foreach(var status in new[]{EvidenceStatus.PhotographedRaw,EvidenceStatus.ChalkMarked,EvidenceStatus.Marked,EvidenceStatus.PhotographedFinal})
                {
                    record.status=status;dot.Refresh();
                    Check(id+": dot persists at "+status,dot.IsVisible);
                }
                record.swabbingDone=record.swabSampleSealed=record.definition.requiresSwabbing;
                record.fingerprintingDone=record.fingerprintLifted=record.definition.requiresFingerprinting;
                record.tentLetter=id=="BODY-A"?"A":null;
                MeasurementTestUtil.RecordExact(id,1);MeasurementTestUtil.RecordExact(id,2);MeasurementTestUtil.RecordAvailablePairsFor(id);
            }
            var ui=RecorderReportUI.Instance;
            foreach(var id in ids)
            {
                var dot=dots[Array.IndexOf(ids,id)];var choices=RecorderDocumentation.Subject(id).statements.Take(3).Select(s=>s.id).ToArray();
                STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
                Check(id+": real Recorder opens without hiding its dot",RecorderTool.Instance.TryOpenReport(EvidenceProp.FindById(id))&&dot.IsVisible);
                ui.SelectLineForTest(choices[0]);ui.SelectLineForTest(choices[1]);
                Check(id+": incomplete confirmation does not hide the dot",ui.Confirm()==TransitionResult.Violation&&dot.IsVisible);
                ui.Hide();dot.Refresh();Check(id+": cancelling Recorder keeps the dot",dot.IsVisible);
                RecorderTool.Instance.TryOpenReport(EvidenceProp.FindById(id));foreach(var choice in choices)ui.SelectLineForTest(choice);
                Check(id+": selecting three statements does not hide the dot",dot.IsVisible);
                Check(id+": successful Recorder confirmation hides the dot immediately",ui.Confirm()==TransitionResult.Applied&&!dot.IsVisible);
                using(EvidenceDiscoveryDot.HideForCapture()){}
                Check(id+": capture restoration cannot relight a confirmed entry",!dot.IsVisible);
                Check(id+": other unrecorded subjects keep their dots",dots.Where((d,i)=>esm.GetRecord(ids[i]).recorderDocumentation==null).All(d=>d.IsVisible));
            }
            Check("All dots disappear only after all four entries are confirmed",dots.All(d=>!d.IsVisible));
            var legacy=esm.GetRecord("EVD-018");var savedSnapshot=legacy.recorderDocumentation;legacy.recorderDocumentation=null;
            foreach(var status in new[]{EvidenceStatus.Logged,EvidenceStatus.ReadyForCollection,EvidenceStatus.Collected,EvidenceStatus.Sealed,EvidenceStatus.Processed})
            {
                legacy.status=status;dots[3].Refresh();Check("Legacy recorded item stays hidden at "+status,!dots[3].IsVisible);
            }
            legacy.recorderDocumentation=savedSnapshot;
            Check("Evidence positions and imported model scales are unchanged",props.Select((p,i)=>p.transform.position==positions[i]&&p.transform.localScale==scales[i]).All(ok=>ok));
        }
        finally
        {
            photos.OnScenePhotographyCompleted-=completed;original.tag=oldTag;if(rig!=null)rig.enabled=rigEnabled;Destroy(cameraObject);
        }
        Finish();
    }
    void RenderOcclusion(Camera cam,EvidenceDiscoveryDot dot)
    {
        Vector3 oldCamera=cam.transform.position;Quaternion oldRotation=cam.transform.rotation;
        var rect=dot.DotCanvas.transform;Vector3 oldDot=rect.position;Quaternion oldDotRotation=rect.rotation;
        Color oldBackground=cam.backgroundColor;var oldFlags=cam.clearFlags;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);var wallMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        wallMaterial.SetColor("_BaseColor",Color.red);wall.GetComponent<Renderer>().sharedMaterial=wallMaterial;
        cam.transform.SetPositionAndRotation(new Vector3(1000,100,1000),Quaternion.identity);cam.backgroundColor=Color.black;cam.clearFlags=CameraClearFlags.SolidColor;
        rect.SetPositionAndRotation(cam.transform.position+Vector3.forward*2,Quaternion.identity);
        wall.transform.position=cam.transform.position+Vector3.forward;wall.transform.localScale=new Vector3(.5f,.5f,.1f);wall.SetActive(false);
        try
        {
            Color visible=CenterPixel(cam,"dot-visible");wall.SetActive(true);Color hidden=CenterPixel(cam,"dot-occluded");
            Check("Rendered highlighted dot is visible in open view",visible.g>.25f&&visible.b>.25f);
            Check("Opaque geometry occludes the dot (no through-wall highlight)",hidden.r>.2f&&hidden.g<.15f&&hidden.b<.15f);
        }
        finally
        {
            cam.transform.SetPositionAndRotation(oldCamera,oldRotation);cam.backgroundColor=oldBackground;cam.clearFlags=oldFlags;
            rect.SetPositionAndRotation(oldDot,oldDotRotation);Destroy(wall);Destroy(wallMaterial);
        }
    }
    static Color CenterPixel(Camera cam,string name)
    {
        Canvas.ForceUpdateCanvases();var rt=RenderTexture.GetTemporary(640,480,24);var previous=RenderTexture.active;var previousTarget=cam.targetTexture;
        try
        {
            cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var image=new Texture2D(640,480,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,480),0,0);image.Apply();
            var color=image.GetPixel(320,240);System.IO.Directory.CreateDirectory("docs/evidence/evidence-discovery-dots");
            System.IO.File.WriteAllBytes("docs/evidence/evidence-discovery-dots/"+name+".png",image.EncodeToPNG());Destroy(image);return color;
        }
        finally{cam.targetTexture=previousTarget;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
    }
    void Finish(){report.AppendLine(failures==0?"=== PASS ===":"=== FAIL: "+failures+" ===");Report=report.ToString();IsDone=true;Debug.Log(Report);}
}
