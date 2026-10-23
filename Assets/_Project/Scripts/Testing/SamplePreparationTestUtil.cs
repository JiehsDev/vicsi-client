using UnityEngine;
using System.Collections;

/// <summary>Legacy synchronous fixtures visit the real cabinet and exercise its gated actions.
/// The focused suite separately drives actual XR input and the full real-time fill.</summary>
public static class SamplePreparationTestUtil
{
    public static bool LastVisitSucceeded { get; private set; }
    public static IEnumerator VisitAndPrepare()
    {
        if(!AutomatedTestSandbox.IsActive)throw new System.InvalidOperationException("Preparation fixtures require the sandbox.");
        LastVisitSucceeded=false;
        var station=SamplePreparationStation.Instance;var camera=Camera.main;
        var position=camera.transform.position;var rotation=camera.transform.rotation;
        var access=Object.FindAnyObjectByType<SceneCordon>().AccessRegister;
        STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
        try
        {
            camera.transform.position=station.transform.position-station.transform.forward*.95f+Vector3.up*1.6f;
            access.ObserveBoundary(camera.transform.position,camera.transform.position,true);
            yield return new WaitForSecondsRealtime(.3f);
            access.ObserveBoundary(camera.transform.position,camera.transform.position,true);
            if(!station.Interact())yield break;
            yield return new WaitForSecondsRealtime(SwabTool.SimulatedDryingSeconds+.2f);
            LastVisitSucceeded=station.Interact();
        }
        finally {camera.transform.SetPositionAndRotation(position,rotation);}
        access.ObserveBoundary(position,position,true);yield return new WaitForSecondsRealtime(.3f);access.ObserveBoundary(position,position,true);
        STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
    }
    public static bool PrepareCurrent()
    {
        if(!AutomatedTestSandbox.IsActive)throw new System.InvalidOperationException("Preparation fixtures require the sandbox.");
        var swab=SwabTool.Instance;if(swab==null || !swab.NeedsPreparation)return true;
        var station=SamplePreparationStation.Instance;var camera=Camera.main;
        if(station==null || camera==null)return false;
        var position=camera.transform.position;var rotation=camera.transform.rotation;
        var access=Object.FindAnyObjectByType<SceneCordon>().AccessRegister;
        STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
        try
        {
            camera.transform.position=station.transform.position-station.transform.forward*.95f+Vector3.up*1.6f;
            access?.RecordSimulationReposition(camera.transform.position);
            if(swab.Preparation==SwabTool.PreparationStage.NeedsDrying && !station.Interact())
            {
                Debug.LogError("[PreparationFixture] Start refused: range="+station.IsInRange+", inside="+access.IsInside
                    +", uiLocks="+PlayerUIGate.LockCount+", toolLock="+ToolInputLock.HasUIOwners
                    +", dialogue="+STCSNotificationUI.Instance.IsShowing+", queued="+STCSNotificationUI.Instance.PendingCount
                    +", held="+EvidenceInventory.Instance.CurrentHeldEvidenceId+", expected="+swab.PendingContainerId);
                return false;
            }
            swab.AdvanceDrying(station,SwabTool.SimulatedDryingSeconds);
            return station.Interact() && swab.Preparation==SwabTool.PreparationStage.Packaged;
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position,rotation);access?.RecordSimulationReposition(position);
            STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
        }
    }
}
