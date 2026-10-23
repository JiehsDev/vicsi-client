using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Real photo-route lifecycle, camera projection and capture exclusion.</summary>
public sealed class PhotographyGuideTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report = new("=== PhotographyGuideTest ===\n");
    private int failures;
    private void Check(string label,bool ok) { report.AppendLine((ok?"PASS: ":"FAIL: ")+label);if(!ok)failures++; }
    public void RunAllTests() => StartCoroutine(Run());
    private IEnumerator Run()
    {
        AutomatedTestSandbox.Activate(nameof(PhotographyGuideTest));
        yield return null;
        if(ReasoningCheckpointManager.Instance!=null)ReasoningCheckpointManager.Instance.enabled=false;
        STCSNotificationUI.Instance?.HideImmediately();
        var manager = ScenePhotographyManager.Instance;
        var guide = manager.GetComponent<ScenePhotographyGuide>();
        Check("Guide attaches automatically, once, without scene wiring",guide!=null&&manager.GetComponents<ScenePhotographyGuide>().Length==1);
        var original = Camera.main;
        string originalTag = original.tag; original.tag="Untagged";
        var fixture = new GameObject("Photography guide test camera",typeof(Camera));
        var cam=fixture.GetComponent<Camera>();cam.tag="MainCamera";cam.enabled=true;cam.fieldOfView=70;cam.aspect=1.4f;
        var rig=FindFirstObjectByType<OVRCameraRig>();bool rigEnabled=rig!=null&&rig.enabled;if(rig!=null)rig.enabled=false;
        var token=new object();
        try
        {
            typeof(ScenePhotographyManager).GetMethod("ActivateFirstPoint",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
            var point=manager.CurrentPoint;
            Check("Route is active and guide follows its current point",point!=null);
            fixture.transform.position=point.transform.position+Vector3.up*1.6f+Vector3.back*4;
            Vector3 positionTarget=point.transform.position+Vector3.up*.12f;
            fixture.transform.rotation=Quaternion.LookRotation(positionTarget-fixture.transform.position);
            guide.Refresh();
            Check("Outside zone: pin points to the standing position, not the photo subject",guide.IsVisible&&guide.IsPin&&!guide.IsFramingTarget&&Vector3.Distance(guide.TargetPosition,positionTarget)<.001f);
            Preview(cam,"standing-pin");
            fixture.transform.rotation=Quaternion.LookRotation(-(positionTarget-fixture.transform.position));guide.Refresh();
            Check("Look away: arrow stays in the inset viewport, no mirrored pin",guide.IsVisible&&!guide.IsPin&&guide.MarkerViewport.x>=.15f&&guide.MarkerViewport.x<=.85f&&guide.MarkerViewport.y>=.21f&&guide.MarkerViewport.y<=.79f);
            Preview(cam,"offscreen-arrow");
            fixture.transform.rotation=Quaternion.LookRotation(positionTarget-fixture.transform.position);guide.Refresh();
            Check("Turn back: the arrow becomes a location pin over the same target",guide.IsPin&&Vector2.Distance(guide.MarkerViewport,new Vector2(.5f,.5f))<.001f);
            fixture.transform.position=point.transform.position+Vector3.up*1.6f;
            Vector3 subject=point.cameraTarget!=null?point.cameraTarget.position:fixture.transform.position+point.transform.forward*5;
            fixture.transform.rotation=Quaternion.LookRotation(subject-fixture.transform.position);guide.Refresh();
            Check("Inside zone: pin switches to the authored camera target",guide.IsVisible&&guide.IsPin&&guide.IsFramingTarget&&Vector3.Distance(guide.TargetPosition,subject)<.001f);
            Preview(cam,"framing-pin");
            Check("Guide does not complete a photo just by aligning",!point.IsCompleted&&!manager.IsScenePhotographyComplete);
            PlayerUIGate.Enter(token);guide.Refresh();Check("Other modal UI hides the guide",!guide.IsVisible);
            PlayerUIGate.Exit(token);
            var tool=FindFirstObjectByType<PhotographTool>(FindObjectsInactive.Include);
            PlayerUIGate.Enter(tool);guide.Refresh();Check("Camera viewfinder ownership permits the guide",guide.IsVisible);
            PlayerUIGate.Enter(token);guide.Refresh();Check("Another modal still wins while camera owns the gate",!guide.IsVisible);
            PlayerUIGate.Exit(token);PlayerUIGate.Exit(tool);guide.Refresh();
            using(guide.HideForCapture())
            {
                Check("Capture scope hides the guide synchronously",!guide.IsVisible);
                using(guide.HideForCapture())Check("Nested capture scope stays hidden",!guide.IsVisible);
                Check("Nested disposal does not restore outer capture",!guide.IsVisible);
            }
            Check("Capture scope restores the previous guide state",guide.IsVisible);
            bool sawCapture=false,guideInCapture=false;
            Action<ScriptableRenderContext,Camera> captureCheck=(context,camera)=>
            {
                if(camera.name!="PhotoCaptureCamera")return;
                sawCapture=true;guideInCapture|=guide.IsVisible;
            };
            RenderPipelineManager.beginCameraRendering+=captureCheck;
            var listenerObject=new GameObject("Guide capture fixture");
            try
            {
                var listener=listenerObject.AddComponent<PhotoCaptureListener>();
                typeof(PhotoCaptureListener).GetMethod("HandlePhotoCaptured",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(listener,null);
                Check("Actual album render excludes guidance, then restores it",sawCapture&&!guideInCapture&&guide.IsVisible);
            }
            finally { RenderPipelineManager.beginCameraRendering-=captureCheck;Destroy(listenerObject); }
            Check("Guide has no input raycaster, hit targets or collision objects",
                guide.GuideCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>()==null
                &&guide.GuideCanvas.GetComponentsInChildren<UnityEngine.UI.Graphic>().All(g=>!g.raycastTarget)
                &&guide.GuideCanvas.GetComponentsInChildren<Collider>().Length==0);
            Canvas.ForceUpdateCanvases();
            var graphic=guide.GuideCanvas.GetComponentInChildren<PhotographyGuideGraphic>();
            var mesh=graphic.canvasRenderer.GetMesh();
            Check("Native location-pin mesh renders with a supported shader",mesh!=null&&mesh.vertexCount>0&&graphic.material.shader.isSupported);
            ProjectionChecks();
            var shutter=typeof(ScenePhotographyManager).GetMethod("HandleShutter",BindingFlags.Instance|BindingFlags.NonPublic);
            shutter.Invoke(manager,new object[]{tool,false});guide.Refresh();
            Check("Successful shutter advances guidance to the next point",point.IsCompleted&&manager.CurrentPoint!=point&&guide.TargetPointId==manager.CurrentPoint.photoPointId);
            Check("Existing route still completes through its original shutter gates",ScenePhotographyTestUtil.CompleteRoute());
            guide.Refresh();Check("All required photos complete: guide disappears",manager.IsScenePhotographyComplete&&!guide.IsVisible);
            // A reload creates a new manager/guide with no retained completion or captured overlay state.
            guide.enabled=false;Check("Disable leaves no floating guidance canvas",!guide.IsVisible);
        }
        finally
        {
            PlayerUIGate.Exit(token);original.tag=originalTag;if(rig!=null)rig.enabled=rigEnabled;Destroy(fixture);
        }
        report.AppendLine(failures==0?"=== PASS, 0 failures ===":"=== FAIL: "+failures+" failure(s) ===");
        Report=report.ToString();IsDone=true;
    }
    private void ProjectionChecks()
    {
        foreach(var local in new[]{new Vector3(1,0,-1),new Vector3(-1,0,-1),new Vector3(0,0,-1),new Vector3(0,1,-1)})
        {
            var pos=ScenePhotographyGuide.Project(new Vector3(.5f,.5f,-1),local,false,out bool pin,out var direction);
            Check("Behind-eye projection is finite and points toward the correct turn: "+local,
                !pin&&float.IsFinite(pos.x)&&float.IsFinite(pos.y)&&pos.x>=.15f&&pos.x<=.85f&&pos.y>=.21f&&pos.y<=.79f
                &&(Mathf.Abs(local.x)<.01f||Mathf.Sign(direction.x)==Mathf.Sign(local.x)));
        }
        ScenePhotographyGuide.Project(new Vector3(.84f,.5f,1),Vector3.forward,false,out bool enter,out _);
        ScenePhotographyGuide.Project(new Vector3(.84f,.5f,1),Vector3.forward,true,out bool stay,out _);
        Check("Small boundary head jitter retains the previous arrow/pin state",!enter&&stay);
    }
    private static void Preview(Camera camera,string name)
    {
        Canvas.ForceUpdateCanvases();
        string dir="docs/evidence/photography-guide";System.IO.Directory.CreateDirectory(dir);
        var rt=RenderTexture.GetTemporary(1120,800,24);var old=RenderTexture.active;var oldTarget=camera.targetTexture;
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1120,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1120,800),0,0);image.Apply();
            System.IO.File.WriteAllBytes(dir+"/"+name+".png",image.EncodeToPNG());Destroy(image);
        }
        finally {camera.targetTexture=oldTarget;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);}
    }
}
