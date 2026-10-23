using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlayerController = Oculus.Interaction.Locomotion.CharacterController;

/// <summary>Saved-layout checks and real Meta-controller traversal, without moving the live player.</summary>
public static class NewHouseInvestigationValidation
{
    [MenuItem("ViCSI/Scene Setup/Validate New House Investigation")]
    public static void Validate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Requires Edit mode.");
        Directory.CreateDirectory(NewHouseInvestigationSetup.Evidence);
        int failures=0;var report=new StringBuilder();
        Action<bool,string> check=(ok,label)=>{report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)failures++;};
        var scene=SceneManager.GetActiveScene();
        var old=scene.GetRootGameObjects().Single(g=>g.name=="Harong");
        check(!old.activeSelf && !old.GetComponentsInChildren<Renderer>(true).Any(r=>r.enabled&&r.gameObject.activeInHierarchy),"Old house retained and inactive");
        var evidence=UnityEngine.Object.FindObjectsByType<EvidenceProp>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(p=>!string.IsNullOrEmpty(p.evidenceId)).ToArray();
        foreach(string id in new[]{"BODY-A","EVD-014","EVD-015","EVD-018"})
        {
            var p=evidence.Single(x=>x.evidenceId==id);var pos=p.transform.position;
            check(pos.x>15.6f&&pos.x<19.4f&&pos.z>11.63f&&pos.z<14.64f,"Evidence in rear kitchen: "+id+" "+pos.ToString("F3"));
        }
        var measurements=AssetDatabase.LoadAssetAtPath<MeasurementRequirementSet>("Assets/_Project/Data/Measurement/Measurements_CSI_Environment.asset");
        foreach(var r in measurements.requirements)
        {
            var a=NewHouseInvestigationSetup.Ref(r.referenceAId);var b=NewHouseInvestigationSetup.Ref(r.referenceBId);
            MeasurementGeometry.TryGetEvidenceAnchor(r.targetEvidenceId,out var point);
            check(Mathf.Abs(MeasurementGeometry.DistanceToReference(a,point,true)-r.expectedDistanceA)<.001f&&Mathf.Abs(MeasurementGeometry.DistanceToReference(b,point,true)-r.expectedDistanceB)<.001f,"Measurement expectations match relocated anchors: "+r.requirementId);
            check(Mathf.Abs(Vector3.Dot(a.SurfaceNormal(true),b.SurfaceNormal(true)))<.001f&&a.Zone.Box.isTrigger&&b.Zone.Box.isTrigger,"Perpendicular fixed surfaces and selectable zones: "+r.requirementId);
        }
        var template=UnityEngine.Object.FindAnyObjectByType<CrimeSceneSketchTemplate>(FindObjectsInactive.Include);
        var problems=template.definition.Validate(template,measurements);
        check(problems.Count==0,"Sketch definition and all stable slot/anchor IDs agree: "+string.Join(";",problems));
        check(template.mappedArea!=null&&template.transform.Find("GroundFloorPlan")!=null&&template.transform.Find("UpperFloorPlan")!=null,"Sketch has kitchen detail, ground-floor plan and upstairs plan");
        foreach(var e in template.definition.evidenceElements)
        {
            MeasurementGeometry.TryGetEvidenceAnchor(e.evidenceId,out var p);
            var anchor=template.FindAnchor(e.correctAnchorId);
            check(Vector2.Distance(template.ToBoard(anchor.transform),template.WorldToBoard(p))<.05f&&anchor.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length==0,"Invisible sketch target tracks actual evidence: "+e.evidenceId);
        }
        foreach(var s in template.definition.measurementSlots)
        {
            MeasurementGeometry.TryGetEvidenceAnchor(s.evidenceId,out var p);
            var reference=NewHouseInvestigationSetup.Ref(s.referenceId);
            check(Vector2.Distance(template.ToBoard(template.FindSlot(s.slotId).referenceFoot),template.WorldToBoard(MeasurementGeometry.ReferenceFoot(reference,p)))<.05f,"Sketch distance line reaches its relocated reference: "+s.slotId);
        }
        foreach(var graphic in template.GetComponentsInChildren<SketchPlanGraphic>())
            check(graphic.GetComponent<CanvasRenderer>()!=null&&graphic.strokes.Count>0&&graphic.strokes.All(l=>float.IsFinite(l.from.x)&&float.IsFinite(l.from.y)&&float.IsFinite(l.to.x)&&float.IsFinite(l.to.y)),"Renderable finite architectural linework: "+graphic.name);
        var cordon=UnityEngine.Object.FindAnyObjectByType<SceneCordon>();
        check(cordon.Contains(NewHouseInvestigationSetup.BodyPosition+Vector3.up)&&!cordon.Contains(NewHouseInvestigationSetup.Arrival+Vector3.up*1.7f),"Cordon protects new house and leaves arrival outside");
        check(cordon.CordonVisible&&!cordon.EntranceIsOpen,"Cordon stays visibly secured");
        var source=UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        var probe=new GameObject("New house traversal probe"){hideFlags=HideFlags.HideAndDontSave,layer=2};
        try
        {
            var capsule=probe.AddComponent<CapsuleCollider>();capsule.radius=source.Radius;capsule.height=1.7f;
            var cc=probe.AddComponent<PlayerController>();cc.InjectCapsule(capsule);cc.LayerMask=source.LayerMask.value&~(1<<2);cc.MaxStep=source.MaxStep;cc.MaxSlopeAngle=source.MaxSlopeAngle;cc.SkinWidth=source.SkinWidth;cc.MaxReboundSteps=source.MaxReboundSteps;
            foreach(var photo in UnityEngine.Object.FindObjectsByType<ScenePhotoPoint>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).OrderBy(p=>p.sequenceIndex))
            {
                Place(cc,photo.transform.position);
                check(Mathf.Abs(Feet(cc).y-photo.transform.position.y)<.06f,"Photography station has floor support: "+photo.photoPointId);
                var feet=Feet(cc);var overlaps=Physics.OverlapCapsule(feet+Vector3.up*.26f,feet+Vector3.up*1.44f,.24f,cc.LayerMask,QueryTriggerInteraction.Ignore);
                check(overlaps.Length==0,"Standing room at photography station: "+photo.photoPointId+" "+string.Join(",",overlaps.Select(c=>c.name)));
                Vector3 eye=photo.transform.position+Vector3.up*1.6f;Vector3 direction=photo.cameraTarget.position-eye;
                bool blocked=Physics.Raycast(eye,direction.normalized,out var hit,Mathf.Max(0,direction.magnitude-.15f),cc.LayerMask,QueryTriggerInteraction.Ignore);
                check(!blocked||hit.collider.GetComponentInParent<EvidenceProp>()!=null,"Photography target visible: "+photo.photoPointId+(blocked?" hit "+hit.collider.name:""));
            }
            Place(cc,new Vector3(16.9f,0,.7f));
            var observe = GameObject.Find("IOC_Observe_Point").transform;
            foreach(var point in new[]{new Vector3(16.9f,0,2.7f),new Vector3(16.9f,1.2f,5.2f),new Vector3(16.7f,1.2f,7.3f),new Vector3(15.55f,1.2f,7.3f),new Vector3(15.55f,1.2f,10.8f),new Vector3(15.55f,1.2f,11.9f),observe.position})
            {
                Walk(cc,point);
                check(Vector3.Distance(Feet(cc),point)<.12f,"Walk from arrival into rear kitchen: "+point.ToString("F2")+" actual="+Feet(cc).ToString("F2"));
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(probe);Physics.SyncTransforms();}
        report.AppendLine("failures="+failures+" compilationFailed="+EditorUtility.scriptCompilationFailed+" playing="+EditorApplication.isPlaying+" dirty="+scene.isDirty);
        File.WriteAllText(NewHouseInvestigationSetup.Evidence+"/layout-validation.txt",report.ToString());
    }
    static Vector3 Feet(PlayerController c)=>c.Pose.position-Vector3.up*c.Height*.5f;
    static void Place(PlayerController c,Vector3 p){c.SetPosition(p+Vector3.up*(c.Height*.5f+.01f));Physics.SyncTransforms();c.TryGround(.08f);}
    static void Walk(PlayerController c,Vector3 p)
    {
        for(int i=0;i<1000;i++){var d=p-Feet(c);d.y=0;if(d.magnitude<.015f){for(int j=0;j<30;j++){c.Move(Vector3.down*.02f);Physics.SyncTransforms();}return;}c.Move(Vector3.ClampMagnitude(d,1.5f/90)+Vector3.down*.02f);Physics.SyncTransforms();}
    }
    public static void Capture()
    {
        CaptureScene("kitchen",new Vector3(18.9f,2.8f,12),new Vector3(16.9f,1.7f,13.3f));
        CaptureScene("kitchen-overview",new Vector3(15.9f,3.3f,12),new Vector3(17.3f,1.6f,13.1f));
        CaptureScene("arrival",new Vector3(16.9f,1.7f,-1),new Vector3(16.9f,2.4f,7));
        var template=UnityEngine.Object.FindAnyObjectByType<CrimeSceneSketchTemplate>(FindObjectsInactive.Include);
        var transforms=template.GetComponentsInChildren<Transform>(true);var layers=transforms.Select(t=>t.gameObject.layer).ToArray();
        try
        {
            foreach(var t in transforms)t.gameObject.layer=31;
            var board=template.Board;var position=board.position-board.forward*5;
            CaptureImage("sketch-map",position,board.position,board.up,1<<31,board.rect.height*board.lossyScale.y*.53f);
        }
        finally{for(int i=0;i<transforms.Length;i++)transforms[i].gameObject.layer=layers[i];}
    }
    static void CaptureScene(string name,Vector3 from,Vector3 target)=>CaptureImage(name,from,target,Vector3.up,~0,0);
    static void CaptureImage(string name,Vector3 from,Vector3 target,Vector3 up,int mask,float ortho)
    {
        var go=new GameObject("Migration preview camera"){hideFlags=HideFlags.HideAndDontSave};var c=go.AddComponent<Camera>();
        c.transform.position=from;c.transform.rotation=Quaternion.LookRotation(target-from,up);c.fieldOfView=75;c.nearClipPlane=.05f;c.farClipPlane=100;c.cullingMask=mask;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.97f,.96f,.92f,1);
        if(ortho>0){c.orthographic=true;c.orthographicSize=ortho;}
        var rt=RenderTexture.GetTemporary(1200,1200,24);var previous=RenderTexture.active;
        try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;var texture=new Texture2D(1200,1200,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1200,1200),0,0);texture.Apply();File.WriteAllBytes(NewHouseInvestigationSetup.Evidence+"/"+name+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);}
        finally{c.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(go);}
    }
}
