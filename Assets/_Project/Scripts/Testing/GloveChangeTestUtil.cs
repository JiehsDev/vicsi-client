using UnityEngine;
using System.Collections;

/// <summary>Legacy flow fixtures visit the real station; never grant replacement state directly.</summary>
public static class GloveChangeTestUtil
{
    public static string LastFailure { get; private set; }
    public static IEnumerator VisitAndReplace()
    {
        var station=ArrivalPPEStation.Instance;
        if(station==null || (station.HasCleanGloves && !station.SwabSupplyNeeded)) yield break;
        if(!AutomatedTestSandbox.IsActive) throw new System.InvalidOperationException("Glove fixtures require the sandbox.");
        var original=Camera.main;string tag=original.tag;var role=PlayerToolRegistry.CurrentRole;
        var model=Object.FindAnyObjectByType<SceneCordon>().AccessRegister;
        HeldItemCoordinator.Instance.HolsterCurrentTool();original.tag="Untagged";
        var eye=new GameObject("Glove change boundary fixture eye",typeof(Camera));var camera=eye.GetComponent<Camera>();camera.tag="MainCamera";
        camera.transform.position=station.transform.position-station.transform.forward*.9f+Vector3.up*1.6f;
        try
        {
            // Match the observed head/capsule samples and retain the normal settling interval.
            // This older coroutine fixture does not drive the SDK capsule; the focused suite separately walks the full route.
            model.ObserveBoundary(camera.transform.position,camera.transform.position,true);
            yield return new WaitForSecondsRealtime(.3f);
            model.ObserveBoundary(camera.transform.position,camera.transform.position,true);
            EnsureClean();
            model.ObserveBoundary(original.transform.position,original.transform.position,true);
            yield return new WaitForSecondsRealtime(.3f);
            model.ObserveBoundary(original.transform.position,original.transform.position,true);
        }
        finally
        {
            original.tag=tag;Object.Destroy(eye);
            STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
            if(role!=ToolType.None)HeldItemCoordinator.Instance.ToggleEquipTool(role,Object.FindAnyObjectByType<OVRCameraRig>().rightHandAnchor);
        }
    }
    public static bool EnsureClean()
    {
        LastFailure=null;
        var station=ArrivalPPEStation.Instance;
        if(station==null || (station.HasCleanGloves && !station.SwabSupplyNeeded)) return true;
        if(!AutomatedTestSandbox.IsActive || !station.CurrentSampleStored) { LastFailure="sample not stored; unfinished="+SwabTool.Instance.HasUnfinishedSample;return false; }
        var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
        var role=PlayerToolRegistry.CurrentRole;
        HeldItemCoordinator.Instance.HolsterCurrentTool();
        STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
        try
        {
            camera.transform.position=station.transform.position-station.transform.forward*.9f+Vector3.up*1.6f;
            Object.FindAnyObjectByType<SceneCordon>().AccessRegister.RecordSimulationReposition(camera.transform.position);
            if(station.NeedsChange && !station.Interact()) { LastFailure="disposal refused; registeredInside="+Object.FindAnyObjectByType<SceneCordon>().AccessRegister.IsInside;return false; }
            if(!station.HasCleanGloves && !station.PairPickedUp && !station.Interact()) { LastFailure="pickup refused";return false; }
            if(!station.HasCleanGloves && !station.Interact())return false;
            if(station.SwabSupplyNeeded)
            {
                if(!station.IsSwabPackPickedUp && !station.Interact())return false;
                if(!station.Interact())return false;
            }
            return station.HasCleanGloves && !station.SwabSupplyNeeded;
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position,rotation);
            Object.FindAnyObjectByType<SceneCordon>().AccessRegister.RecordSimulationReposition(position);
            STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
            if(role!=ToolType.None) HeldItemCoordinator.Instance.ToggleEquipTool(role,Object.FindAnyObjectByType<OVRCameraRig>().rightHandAnchor);
        }
    }
}
