// Assets/_Project/Scripts/UI/HeldEvidenceStorePrompt.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ambient "press B to store in inventory" prompt shown whenever the player is
/// holding sealed evidence - EvidenceBagTool/SwabTool.TrySeal equips the freshly
/// sealed item straight into the player's hand (see those methods' own comments),
/// and this is the explicit "option to add it to inventory" that follows: press B
/// to send it back to Inventory (HeldItemCoordinator.UnequipEvidence) rather than
/// having to open the Evidence Inventory panel and re-select it there.
///
/// Right B is otherwise only ever consumed contextually elsewhere in this project
/// (closing a menu/panel, a nearby lever or tent pickup) - never while just walking
/// around holding something - so this ambient binding doesn't compete with those.
/// Respects PlayerUIGate like every other equip-adjacent action in this project.
/// </summary>
// Read B before panels process their close/skip input, so one press cannot close a modal and store underneath it.
[DefaultExecutionOrder(-100)]
public class HeldEvidenceStorePrompt : MonoBehaviour
{
    // Wording: ViCSIDialogueCatalog ("prompt-store-held-evidence", "held-evidence-stored").

    private InputAction storeAction;
    private bool promptShown;
    public bool IsPromptShown => promptShown;

    private void Awake()
    {
        storeAction = new InputAction("HeldEvidenceStorePrompt_Store", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
    }

    private void OnEnable()
    {
        storeAction.Enable();
    }

    private void OnDisable()
    {
        storeAction.Disable();
        HidePromptIfShown();
    }

    private void OnDestroy()
    {
        storeAction?.Dispose();
    }

    private void Update()
    {
        string evidenceId = CurrentHeldEvidenceId();
        if (evidenceId == null)
        {
            HidePromptIfShown();
            return;
        }

        var definition = EvidenceInventory.Instance.GetDefinition(evidenceId);
        string displayName = definition != null ? definition.PlayerDisplayName : evidenceId;
        if (EvidenceInventory.Instance.IsFingerprintSample(evidenceId)
            && !EvidenceStateManager.Instance.IsFingerprintMatched(EvidenceInventory.Instance.GetSampleSource(evidenceId)))
        {
            NotificationManager.ShowPrompt("Compare the held fingerprint card at the examination desk before storing it.");
            promptShown = true;
            return;
        }

        // A nearby EvidenceBoxDepositZone's own "Press A to deposit" prompt is more
        // specific/actionable than this ambient one - yield the shared prompt slot to
        // it rather than the two flickering over each other every frame. B still works
        // to store the item even while that other prompt is the one showing.
        if (!IsDepositPromptShowingFor(evidenceId))
        {
            NotificationManager.ShowPrompt(GameDialogue.Text(SwabTool.Instance?.NeedsPreparation == true && SwabTool.Instance.PendingContainerId == evidenceId
                ? "sample-prepare-first" : EvidenceInventory.Instance.IsFingerprintSample(evidenceId)
                ? "prompt-store-fingerprint-sample" : EvidenceInventory.Instance.IsBaggedEvidence(evidenceId)
                ? "prompt-store-bagged-evidence" : EvidenceInventory.Instance.GetSampleSource(evidenceId) != null
                    ? "prompt-store-blood-sample" : "prompt-store-held-evidence", displayName));
            promptShown = true;
        }
        else
        {
            HidePromptIfShown();
        }

        if (storeAction.WasPressedThisFrame())
        {
            TryStore();
        }
    }

    public bool TryStore()
    {
        string id = CurrentHeldEvidenceId();
        if (id == null) return false;
        string displayName = EvidenceInventory.Instance.GetDefinition(id)?.PlayerDisplayName ?? id;
        if (!HeldItemCoordinator.Instance.TryStoreHeldEvidence()) return false;
        HidePromptIfShown();
        GameDialogue.Say("held-evidence-stored", displayName);
        return true;
    }

    private static bool IsDepositPromptShowingFor(string evidenceId)
    {
        foreach (var zone in FindObjectsByType<EvidenceBoxDepositZone>(FindObjectsSortMode.None))
        {
            if (zone.IsShowingDepositPromptFor(evidenceId))
            {
                return true;
            }
        }
        return false;
    }

    private static string CurrentHeldEvidenceId()
    {
        if (PlayerUIGate.IsBlocked || ToolInputLock.HasUIOwners || SceneTurnover.IsInvestigationBlocked || (STCSNotificationUI.Instance != null && STCSNotificationUI.Instance.IsShowing)
            || HeldItemCoordinator.Instance == null || EvidenceInventory.Instance == null)
        {
            return null;
        }

        return HeldItemCoordinator.Instance.CurrentCategory == HeldItemCoordinator.HeldCategory.Evidence
            && ProcedureInterlock.AllowsHandling(HeldItemCoordinator.Instance.CurrentEvidenceId)
            ? HeldItemCoordinator.Instance.CurrentEvidenceId
            : null;
    }

    private void HidePromptIfShown()
    {
        if (!promptShown)
        {
            return;
        }

        NotificationManager.HidePrompt();
        promptShown = false;
    }
}
