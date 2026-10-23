using System.Reflection;
using UnityEngine;

/// <summary>Legacy camera fixtures explicitly perform the new ruler interaction before a final shot.</summary>
public static class PhotoScaleTestUtil
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Aim(PhotographTool camera,string id)
    {
        var prop=EvidenceProp.FindById(id); var head=Camera.main;
        Vector3 offset=id=="EVD-018"?new Vector3(.075f,.01f,0):id=="EVD-015"?new Vector3(0,0,-.16f):new Vector3(0,0,-.135f);
        Vector3 eye=prop.transform.position+(id=="EVD-018"?new Vector3(0,.1f,-.8f):new Vector3(0,.85f,-.45f));
        head.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(prop.transform.position+offset-eye));
        typeof(PhotographTool).GetField("povCamera",Private).SetValue(camera,head);
        typeof(PhotographTool).GetField("povCameraReady",Private).SetValue(camera,false);
        Physics.SyncTransforms();
        typeof(PhotographTool).GetMethod("UpdateAimTarget",Private).Invoke(camera,null);
    }
    public static void PrepareForFinalPhoto(PhotographTool camera,string id)
    {
        var record=EvidenceStateManager.Instance?.GetRecord(id);
        if(camera.PhotoScale==null || !camera.PhotoScale.RequiresScale(id) || record?.status!=EvidenceStatus.Marked) return;
        if(!camera.IsCameraHeld)
            HeldItemCoordinator.Instance.ToggleEquipTool(ToolType.Photographer,Object.FindFirstObjectByType<OVRCameraRig>().rightHandAnchor);
        if(!camera.IsAiming) typeof(PhotographTool).GetMethod("SetAiming",Private).Invoke(camera,new object[]{true});
        Camera.main.fieldOfView=45f; // This synchronous fixture represents the raised camera after its zoom settles.
        Aim(camera,id);
        if(camera.PhotoScale.PlacedEvidenceId!=id)
        {
            camera.PhotoScale.Remove("fixture-changing-subject");
            if(!camera.PhotoScale.TryPlace()) throw new System.InvalidOperationException("Fixture could not place the camera ruler for "+id);
        }
        typeof(PhotographTool).GetMethod("UpdateAimTarget",Private).Invoke(camera,null);
    }
    public static void FinishPhoto(PhotographTool camera)
    {
        if(!camera.LastCaptureUsedScale) return;
        camera.PhotoScale.Remove("fixture-photo-completed");
        typeof(PhotographTool).GetMethod("SetAiming",Private).Invoke(camera,new object[]{false});
    }
}
