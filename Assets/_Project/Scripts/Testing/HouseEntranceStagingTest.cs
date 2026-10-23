using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

/// <summary>Actual runtime station footprints and Meta capsule routes in the front yard.</summary>
public sealed class HouseEntranceStagingTest : MonoBehaviour
{
    public bool IsDone { get; private set; }
    public string Report { get; private set; }
    private readonly StringBuilder report=new("=== HouseEntranceStagingTest ===\n");
    private int failures;
    private void Check(string text,bool ok) { report.AppendLine((ok?"PASS: ":"FAIL: ")+text);if(!ok)failures++; }
    public void RunAllTests()=>StartCoroutine(Run());
    private IEnumerator Run()
    {
        yield return null;
        var cordon=FindAnyObjectByType<SceneCordon>();
        var supply=cordon.PPEStation;
        var preparation=SamplePreparationStation.Instance;
        var comparison=FingerprintComparisonStation.Instance;
        var box=FindAnyObjectByType<EvidenceBoxDepositZone>();
        Check("All stations spawn in the saved scene",supply!=null&&preparation!=null&&comparison!=null&&box!=null);
        var left=cordon.transform.Find("EntrancePost_L");var right=cordon.transform.Find("EntrancePost_R");
        var frame=GameObject.Find("Vicsi_House").transform.Find("DoorFrame").GetComponent<Renderer>().bounds;
        Check("Inner cordon controls the house doorway",Mathf.Abs(left.position.z-frame.min.z+.02f)<.01f&&Mathf.Abs(right.position.z-left.position.z)<.01f);
        Check("Existing secured tape remains visible",cordon.CordonVisible&&!cordon.EntranceIsOpen);
        Check("Front-yard aisle is outside the controlled scene",!cordon.IsWithinSecurityFootprint(new Vector3(16.9f,1.6f,.75f))
            &&!cordon.IsWithinSecurityFootprint(new Vector3(18.2f,1.6f,1.6f)));
        Check("Yard stays outside hot zone; all house rooms require authorization",!cordon.RequiresBriefingAt(new Vector3(16.9f,1.6f,2.8f))&&cordon.Contains(new Vector3(17.3f,2.8f,13))&&cordon.Contains(new Vector3(17.7f,2.8f,9.6f)));
        Check("Comparison uses the authored yard anchor",Vector3.Distance(comparison.transform.position,cordon.FingerprintStationAnchor.position)<.001f);
        var stations=new Transform[]{supply.transform,preparation.transform,comparison.transform,box.transform};
        foreach(var station in stations)
        {
            var colliders=station.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();
            // The custody box is intentionally a trigger; measure its visible shell
            // when it has no solid collider rather than treating it as absent.
            var footprints=colliders.Length>0?colliders.Select(c=>c.bounds).ToArray():station.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).ToArray();
            bool inside=true;
            foreach(var b in footprints)
            {
                inside &= b.min.x>8.1f&&b.max.x<24.9f&&b.min.z>.25f&&b.max.z<2.65f;
            }
            Check(station.name+": physical footprint inside fence and clear of entrance",footprints.Length>0&&inside);
            var at=station.position-station.forward*.85f;
            Check(station.name+": standing approach stays outside inner cordon",!cordon.IsWithinSecurityFootprint(at));
        }
        var route=supply.GetComponent<GloveStationGuide>().Route;
        Check("Exit trail remains inside property fence",route.waypoints.All(p=>p.x>8.1f&&p.x<24.9f&&p.z>.25f));
        Check("Exit trail ends in the yard service aisle",Mathf.Abs(route.waypoints.Last().z-.75f)<.01f);
        WalkRoutes(route,stations);
        Capture(new Vector3(17.5f,6.8f,-4.8f),new Vector3(17.5f,.7f,2.4f),"front-yard-staging");
        Capture(new Vector3(16.9f,1.7f,.65f),new Vector3(16.9f,1.2f,4.5f),"house-entrance");
        report.AppendLine(failures==0?"=== PASS ===":"=== FAIL ===");Report=report.ToString();IsDone=true;
    }
    private void WalkRoutes(GloveStationGuide.RouteData route,Transform[] stations)
    {
        var source=FindAnyObjectByType<Oculus.Interaction.Locomotion.CharacterController>();
        var live=(CapsuleCollider)typeof(Oculus.Interaction.Locomotion.CharacterController).GetField("_capsule",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(source);
        bool enabled=live.enabled;live.enabled=false;
        var probe=new GameObject("Yard route capsule probe"){layer=2};
        try
        {
            var capsule=probe.AddComponent<CapsuleCollider>();capsule.radius=source.Radius;capsule.height=1.7f;
            var controller=probe.AddComponent<Oculus.Interaction.Locomotion.CharacterController>();controller.InjectCapsule(capsule);
            controller.LayerMask=source.LayerMask.value&~(1<<2);controller.MaxStep=source.MaxStep;controller.MaxSlopeAngle=source.MaxSlopeAngle;
            controller.SkinWidth=source.SkinWidth;controller.MaxReboundSteps=source.MaxReboundSteps;
            void StartAt(Vector3 at) { controller.SetPosition(at+Vector3.up*(controller.Height*.5f+.01f));Physics.SyncTransforms();controller.TryGround(.12f); }
            void Walk(Vector3 point,string label)
            {
                for(int i=0;i<1000;i++)
                {
                    var feet=controller.Pose.position-Vector3.up*(controller.Height*.5f);var delta=point-feet;delta.y=0;
                    controller.Move(Vector3.ClampMagnitude(delta,1.5f/90)+Vector3.down*.02f);Physics.SyncTransforms();
                }
                var actual=controller.Pose.position-Vector3.up*(controller.Height*.5f);var distance=actual-point;distance.y=0;
                Check(label+" target="+point.ToString("F2")+" reached="+actual.ToString("F2"),distance.magnitude<.18f);
            }
            StartAt(route.waypoints[0]);
            for(int i=1;i<route.waypoints.Length;i++) Walk(route.waypoints[i],"Capsule exits house waypoint "+i);
            foreach(var station in stations)
            {
                StartAt(route.waypoints.Last());
                Walk(station.position-station.forward*.85f,"Capsule reaches "+station.name);
                Walk(route.waypoints.Last(),"Capsule returns from "+station.name);
            }
        }
        finally {DestroyImmediate(probe);live.enabled=enabled;Physics.SyncTransforms();}
    }
    private void Capture(Vector3 at,Vector3 look,string name)
    {
        var go=new GameObject("Yard staging preview camera",typeof(Camera));var camera=go.GetComponent<Camera>();
        camera.transform.position=at;camera.transform.LookAt(look);camera.fieldOfView=65;
        var target=RenderTexture.GetTemporary(1440,1080,24);var active=RenderTexture.active;var texture=new Texture2D(1440,1080,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,1440,1080),0,0);texture.Apply();
            var args=System.Environment.GetCommandLineArgs();int index=System.Array.IndexOf(args,"-phase5Output");
            string folder=index>=0?Path.GetDirectoryName(args[index+1]):"docs/evidence/entrance-staging";Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());
        }
        finally {camera.targetTexture=null;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(texture);Destroy(go);}
    }
}
