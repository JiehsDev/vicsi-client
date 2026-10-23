#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PlayerController = Oculus.Interaction.Locomotion.CharacterController;

/// <summary>Inspection and verification only; never saves or adjusts the scene.</summary>
public static class ExteriorPhotoStationAudit
{
    const string Output = "docs/evidence/exterior-photo-clearance";
    [Serializable] class Sample
    {
        public Vector3 station, groundedFeet;
        public bool supported, footprintSupported, visible, inZone;
        public string[] overlaps;
        public bool valid;
    }
    [Serializable] class Result
    {
        public string hierarchy, targetHierarchy;
        public Vector3 station, localPosition, target, forward;
        public float standingRadius, maxHeadHeight, angleTolerance, playerRadius, playerHeight, maxStep, skinWidth;
        public string[] nearbyColliders;
        public Sample original;
        public List<Sample> candidates = new();
        public bool navigationReachesStation, sceneLeftClean, compilationFailed;
    }
    public static void Inspect() => Run(false);
    public static void Verify() => Run(true);
    public static void VerifyAndEnvironment()
    {
        if (!Run(true, false)) { EditorApplication.Exit(1); return; }
        FinalConsolidationValidation.Run();
    }
    static bool Run(bool verify, bool exit = true)
    {
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Scenarios/CSI_Environment.unity");
        var photo = UnityEngine.Object.FindObjectsByType<ScenePhotoPoint>(FindObjectsSortMode.None).Single(p=>p.photoPointId=="scene-exterior-establishing");
        var source = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        var result = new Result { hierarchy=PathOf(photo.transform),targetHierarchy=PathOf(photo.cameraTarget),
            station=photo.transform.position,localPosition=photo.transform.localPosition,target=photo.cameraTarget.position,forward=photo.transform.forward,
            standingRadius=photo.standingRadius,maxHeadHeight=photo.maxHeadHeight,angleTolerance=photo.angleTolerance,
            playerRadius=source.Radius,playerHeight=1.7f,maxStep=source.MaxStep,skinWidth=source.SkinWidth,compilationFailed=EditorUtility.scriptCompilationFailed };
        var probe = new GameObject("Exterior station clearance probe") {hideFlags=HideFlags.HideAndDontSave,layer=2};
        try
        {
            var capsule=probe.AddComponent<CapsuleCollider>();capsule.radius=source.Radius;capsule.height=result.playerHeight;
            var cc=probe.AddComponent<PlayerController>();cc.InjectCapsule(capsule);cc.LayerMask=source.LayerMask.value&~(1<<2);
            cc.MaxStep=source.MaxStep;cc.MaxSlopeAngle=source.MaxSlopeAngle;cc.SkinWidth=source.SkinWidth;cc.MaxReboundSteps=source.MaxReboundSteps;
            result.nearbyColliders=Physics.OverlapBox(photo.transform.position+Vector3.up*.8f,new Vector3(2,1,2),Quaternion.identity,cc.LayerMask,QueryTriggerInteraction.Ignore)
                .Select(c=>PathOf(c.transform)+" bounds="+c.bounds.ToString("F4")).OrderBy(s=>s).ToArray();
            result.original=Evaluate(photo,cc,photo.transform.position);
            if(!verify)
            {
                // Try the actual support surface first; horizontal candidates are only diagnostic fallbacks.
                for(int x=-6;x<=6;x++)for(int z=-6;z<=6;z++)
                {
                    var candidate=photo.transform.position+new Vector3(x*.05f,0,z*.05f);
                    if(Physics.Raycast(candidate+Vector3.up*.5f,Vector3.down,out var hit,1,cc.LayerMask,QueryTriggerInteraction.Ignore)&&hit.normal.y>.9f)
                    {candidate.y=hit.point.y;result.candidates.Add(Evaluate(photo,cc,candidate));}
                }
                result.candidates=result.candidates.Where(s=>s.valid).OrderBy(s=>Vector3.Distance(s.station,photo.transform.position)).Take(8).ToList();
            }
            Place(cc,NewHouseInvestigationSetup.Arrival);
            var goal=photo.transform.position;
            for(int i=0;i<600;i++)
            {
                Vector3 direction=goal-Feet(cc);direction.y=0;
                cc.Move(Vector3.ClampMagnitude(direction,1.5f/90)+Vector3.down*.02f);Physics.SyncTransforms();
            }
            result.navigationReachesStation=Vector3.Distance(Feet(cc),goal)<.06f;
        }
        finally {UnityEngine.Object.DestroyImmediate(probe);Physics.SyncTransforms();}
        result.sceneLeftClean=!scene.isDirty;
        File.WriteAllText(Output+(verify?"/after.json":"/inspection.json"),JsonUtility.ToJson(result,true));
        bool pass=!result.compilationFailed&&result.sceneLeftClean&&(!verify||(result.original.valid&&result.navigationReachesStation));
        if (exit) EditorApplication.Exit(pass?0:1);
        return pass;
    }
    static Sample Evaluate(ScenePhotoPoint photo,PlayerController cc,Vector3 position)
    {
        Place(cc,position);var feet=Feet(cc);
        float radius=Mathf.Max(.24f,cc.Radius);
        var overlaps=Physics.OverlapCapsule(feet+Vector3.up*(radius+.02f),feet+Vector3.up*(1.7f-radius-.02f),radius,cc.LayerMask,QueryTriggerInteraction.Ignore);
        bool footprint=true;
        for(int i=0;i<16;i++)
        {
            float angle=2*Mathf.PI*i/16;var at=feet+new Vector3(Mathf.Cos(angle)*radius,.15f,Mathf.Sin(angle)*radius);
            footprint &= Physics.Raycast(at,Vector3.down,out var ground,.3f,cc.LayerMask,QueryTriggerInteraction.Ignore)&&ground.normal.y>.9f&&Mathf.Abs(ground.point.y-feet.y)<.06f;
        }
        Vector3 eye=position+Vector3.up*1.6f,direction=photo.cameraTarget.position-eye;
        bool blocked=Physics.Raycast(eye,direction.normalized,out var hit,Mathf.Max(0,direction.magnitude-.15f),cc.LayerMask,QueryTriggerInteraction.Ignore);
        bool visible=!blocked||hit.collider.GetComponentInParent<EvidenceProp>()!=null;
        var sample=new Sample {station=position,groundedFeet=feet,supported=Mathf.Abs(feet.y-position.y)<.06f,footprintSupported=footprint,
            visible=visible,inZone=photo.ContainsPlayer(eye),overlaps=overlaps.Select(c=>PathOf(c.transform)+" bounds="+c.bounds.ToString("F4")).ToArray()};
        sample.valid=sample.supported&&sample.footprintSupported&&sample.visible&&sample.inZone&&overlaps.Length==0;
        return sample;
    }
    static void Place(PlayerController cc,Vector3 p){cc.SetPosition(p+Vector3.up*(cc.Height*.5f+.01f));Physics.SyncTransforms();cc.TryGround(.08f);}
    static Vector3 Feet(PlayerController cc)=>cc.Pose.position-Vector3.up*cc.Height*.5f;
    static string PathOf(Transform t)=>t==null?"":t.parent==null?t.name:PathOf(t.parent)+"/"+t.name;
}
#endif
