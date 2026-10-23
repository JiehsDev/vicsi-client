using System.Collections;
using UnityEngine;

/// <summary>Real preparation/storage for UI fixtures that previously stopped at recovery or sealing.</summary>
public static class UIProcedureTestUtil
{
    public static bool LastSampleStored { get; private set; }
    public static IEnumerator CollectAndStoreBlood(string id)
    {
        if(!AutomatedTestSandbox.IsActive)throw new System.InvalidOperationException("UI fixtures require the sandbox.");
        LastSampleStored=false;EvidenceProcedureUI.Instance?.Hide();
        STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
        yield return GloveChangeTestUtil.VisitAndReplace();
        var coordinator=HeldItemCoordinator.Instance;
        if(PlayerToolRegistry.CurrentRole!=ToolType.Swab)coordinator.ToggleEquipTool(ToolType.Swab,Object.FindFirstObjectByType<OVRCameraRig>().rightHandAnchor);
        if (!MeasurementTestUtil.CompleteSketchFor(id)) yield break;
        var swab=SwabTool.Instance;
        STCSNotificationUI.Instance?.HideImmediately();
        if(!swab.TrySampleRequiredSwab(EvidenceSwabRequirement.FindByEvidenceId(id)))yield break;
        STCSNotificationUI.Instance?.HideImmediately();
        if(swab.TryContain()!=TransitionResult.Applied)yield break;
        STCSNotificationUI.Instance?.HideImmediately();
        if(!SamplePreparationTestUtil.PrepareCurrent()||swab.TrySeal()!=TransitionResult.Applied)yield break;
        STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
        LastSampleStored=coordinator.TryStoreHeldEvidence();
        yield return null;
    }
    public static bool StoreSealedBag(string id)
    {
        if(!AutomatedTestSandbox.IsActive)throw new System.InvalidOperationException("UI fixtures require the sandbox.");
        EvidenceProcedureUI.Instance?.Hide();STCSNotificationUI.Instance?.HideImmediately();NotificationManager.Instance?.UI.ClearTooltip();
        var prop=EvidenceProp.FindById(id);
        bool result=EvidenceInventory.Instance.RegisterBaggedEvidence(id)
            &&HeldItemCoordinator.Instance.EquipEvidence(id,Object.FindFirstObjectByType<OVRCameraRig>().rightHandAnchor)
            &&HeldItemCoordinator.Instance.TryStoreHeldEvidence();
        // This checklist fixture continues testing measurements of other items afterwards.
        // Retain its original scene anchors, as the development measurement preset does.
        if(prop!=null)prop.gameObject.SetActive(true);
        return result;
    }
}
