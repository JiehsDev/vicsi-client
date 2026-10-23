using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A tent that applied no mark is recoverable immediately. A successful item
/// or trace marker stays until its evidence is recorded. Extra tents never inherit a link.</summary>
public class EvidenceTentPickup : MonoBehaviour
{
    private EvidenceTentTool owner;
    private LetterTentTool letterOwner;
    private string markerLetter;
    private float pickupRadius;
    private int tentIndex;
    private string markedEvidenceId;
    private InputAction pickupAction;
    private bool promptShown, marksTrace, reclaimed;
    private static EvidenceTentPickup promptOwner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPromptOwner() => promptOwner = null;
    public float PickupRadiusSqr => pickupRadius * pickupRadius;
    public bool MarksTrace => marksTrace;
    public string MarkedEvidenceId => markedEvidenceId;
    public int TentIndex => tentIndex;
    public string MarkerLetter => markerLetter;

    /// <param name="evidenceId">Only the evidence whose marking transition this placement applied; null when no transition occurred.</param>
    public void Initialize(EvidenceTentTool owningTool, float radius, int index, string evidenceId, bool isTraceMarker = false)
    {
        owner = owningTool; pickupRadius = radius; tentIndex = index;
        markedEvidenceId = evidenceId;
        marksTrace = isTraceMarker && !string.IsNullOrEmpty(evidenceId);
    }

    public void InitializeLetter(LetterTentTool owningTool, float radius, string letter, string evidenceId)
    {
        letterOwner = owningTool; pickupRadius = radius; tentIndex = -1;
        markerLetter = letter; markedEvidenceId = evidenceId;
    }

    private Dictionary<string,string> MarkerDetails() => new() {
        { "tentNumber", string.IsNullOrEmpty(markerLetter) ? (tentIndex + 1).ToString() : string.Empty },
        { "markerLetter", markerLetter ?? string.Empty }, { "wasTraceMarker", marksTrace.ToString() } };

    private void Awake() => pickupAction = new InputAction("EvidenceTentPickup_B", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
    private void OnEnable() { pickupAction.Enable(); EvidenceTentPickupCoordinator.Register(this); }
    private void OnDisable() { pickupAction.Disable(); EvidenceTentPickupCoordinator.Unregister(this); SetPrompt(false); }
    private void OnDestroy() { pickupAction?.Dispose(); SetPrompt(false); }

    private void Update()
    {
        var camera = Camera.main;
        bool candidate = camera != null && EvidenceTentPickupCoordinator.FindNearestInRange(camera.transform.position) == this;
        SetPrompt(candidate && !ProcedureInterlock.BlocksUnrelatedActions && !PlayerUIGate.IsBlocked && !ToolInputLock.HasUIOwners && !SceneTurnover.IsInvestigationBlocked && IsPickable());
        if (promptShown && pickupAction.WasPressedThisFrame()) TryReclaim();
    }

    private void SetPrompt(bool show)
    {
        if (show)
        {
            if (promptOwner == this && promptShown) return;
            if (promptOwner != null) promptOwner.promptShown = false;
            promptOwner = this; promptShown = true;
            NotificationManager.ShowPrompt(GameDialogue.Text("prompt-tent-pickup"));
        }
        else
        {
            promptShown = false;
            // An old candidate must not hide the newer candidate's hint.
            if (promptOwner != this) return;
            promptOwner = null; NotificationManager.HidePrompt();
        }
    }

    public bool IsPickable()
    {
        if (reclaimed) return false;
        if (string.IsNullOrEmpty(markedEvidenceId)) return true;
        var state = EvidenceStateManager.Instance;
        return state != null && state.GetRecord(markedEvidenceId) != null
            && state.IsAtOrPastStatus(markedEvidenceId, EvidenceStatus.Logged);
    }

    public void TryReclaim()
    {
        if (ProcedureInterlock.BlocksUnrelatedActions) return;
        if (reclaimed) return;
        if (!IsPickable())
        {
            GameDialogue.Say("tent-pickup-already-documented");
            var details = MarkerDetails(); details["reason"] = "evidence recording required";
            SessionLogger.Instance?.LogEvent(SessionEventType.MarkerReclaimBlocked, markedEvidenceId,
                details);
            return;
        }
        reclaimed = true;
        var reclaimDetails = MarkerDetails();
        reclaimDetails["wasEvidence"] = (!string.IsNullOrEmpty(markedEvidenceId)).ToString();
        reclaimDetails["statusReverted"] = "False";
        SessionLogger.Instance?.LogEvent(SessionEventType.MarkerReclaimed,
            string.IsNullOrEmpty(markedEvidenceId) ? "(non-evidence)" : markedEvidenceId,
            reclaimDetails);
        // Keep recorded status and marker/photo references intact: this is physical cleanup.
        InteractionFeedback.Confirm(); SetPrompt(false);
        owner?.ReclaimSlot(tentIndex);
        letterOwner?.ReclaimLetter(markerLetter);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
