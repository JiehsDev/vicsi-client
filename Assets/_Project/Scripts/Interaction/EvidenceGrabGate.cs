// Assets/_Project/Scripts/Interaction/EvidenceGrabGate.cs
using UnityEngine;
using UnityEngine.InputSystem;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

/// <summary>
/// Keeps an evidence item's physical grab (HandGrabInteractable/GrabInteractable)
/// disabled until it is genuinely ReadyForCollection - consistent with every other
/// action in this project waiting until the moment it's procedurally valid, rather
/// than being freely available and only checked after the fact. Before that point the
/// right hand should not be able to pick this item up at all: collection is meant to
/// happen only once photographing, sketching and logging are done, not whenever a
/// player notices they can grab it.
///
/// For an item with a required biological trace (EvidenceDefinition.requiresSwabbing,
/// e.g. the blood on the kitchen knife's blade), reaching ReadyForCollection is not
/// enough on its own - grab stays disabled until the trace has also been swabbed
/// (EvidenceRecord.swabbingDone). Otherwise a player could simply pick the knife up
/// and walk off with it, destroying the very trace evidence EvidenceStateManager's
/// own Collected gate (IsBlockedBySwabbing) already refuses to let past without a
/// swab - this component is that same rule enforced one step earlier, at the physical
/// interaction, not just at the state-machine transition.
///
/// Sits alongside EvidenceProp on the same GameObject, which must also carry
/// Grabbable/HandGrabInteractable/GrabInteractable (added once per evidence prop for
/// the two-handed bagging redesign - see EvidenceBagTool, which disables these again
/// once an item is actually bagged, since a bagged item shouldn't be independently
/// re-grabbable either).
/// </summary>
[RequireComponent(typeof(EvidenceProp))]
public class EvidenceGrabGate : MonoBehaviour
{
    private EvidenceProp prop;
    private HandGrabInteractable handGrabInteractable;
    private GrabInteractable grabInteractable;

    [Tooltip("A grip pressed within this distance (m) of the item's surface while it isn't ready to collect shows why. Matches EvidenceBagTool's grab reach.")]
    [SerializeField] private float refusalReach = 0.15f;

    [Tooltip("Minimum seconds between two \"not ready to collect\" messages for this item.")]
    [SerializeField] private float refusalCooldownSeconds = 2f;

    private bool grabEnabled;
    private float lastRefusalTime = -999f;
    private InputAction rightGripAction;
    private InputAction leftGripAction;
    private OVRCameraRig cameraRig;
    private bool lastUIBlocked, suppressedHandGrab, suppressedControllerGrab;

    private void Awake()
    {
        prop = GetComponent<EvidenceProp>();
        handGrabInteractable = GetComponent<HandGrabInteractable>();
        grabInteractable = GetComponent<GrabInteractable>();
        rightGripAction = new InputAction(name + "_GrabGateRightGrip", InputActionType.Button, "<XRController>{RightHand}/gripPressed");
        leftGripAction = new InputAction(name + "_GrabGateLeftGrip", InputActionType.Button, "<XRController>{LeftHand}/gripPressed");
        SetGrabEnabled(false);
        Debug.Log($"[EvidenceGrabGate] {prop.evidenceId}: grab disabled at startup " +
            $"(handGrabInteractable found={handGrabInteractable != null}, grabInteractable found={grabInteractable != null}); " +
            "waiting for ReadyForCollection.");
    }

    private void OnEnable()
    {
        ToolInputLock.OnChanged += RefreshUIInput;
        EvidenceStateManager.OnEvidenceStatusChanged += HandleStatusChanged;
        EvidenceStateManager.OnEvidenceSwabbed += HandleSwabbed;
        EvidenceStateManager.OnEvidenceFingerprinted += HandleFingerprinted;
        rightGripAction.Enable();
        leftGripAction.Enable();
    }

    private void OnDisable()
    {
        ToolInputLock.OnChanged -= RefreshUIInput;
        EvidenceStateManager.OnEvidenceStatusChanged -= HandleStatusChanged;
        EvidenceStateManager.OnEvidenceSwabbed -= HandleSwabbed;
        EvidenceStateManager.OnEvidenceFingerprinted -= HandleFingerprinted;
        rightGripAction.Disable();
        leftGripAction.Disable();
    }

    private void OnDestroy()
    {
        rightGripAction?.Dispose();
        leftGripAction?.Dispose();
    }

    /// <summary>
    /// The grab is simply switched off until the item is ready, so a grip on it would
    /// otherwise do nothing at all and leave the player guessing. When a hand grips at
    /// the item while it's gated, name the one step still missing
    /// (EvidenceStateManager.GetCollectionBlockReason). Feedback only: nothing is logged
    /// as a procedural violation and no state changes.
    /// </summary>
    private void Update()
    {
        if (lastUIBlocked != ToolInputLock.HasUIOwners) RefreshUIInput();
        if (grabEnabled || ToolInputLock.IsLocked || PlayerUIGate.IsBlocked)
        {
            return;
        }
        bool right = rightGripAction.WasPressedThisFrame();
        bool left = leftGripAction.WasPressedThisFrame();
        if (!right && !left)
        {
            return;
        }
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
            if (cameraRig == null) return;
        }
        bool near = (right && cameraRig.rightHandAnchor != null && DistanceToItemSurface(cameraRig.rightHandAnchor.position) <= refusalReach)
            || (left && cameraRig.leftHandAnchor != null && DistanceToItemSurface(cameraRig.leftHandAnchor.position) <= refusalReach);
        if (near)
        {
            ShowNotReadyReason();
        }
    }

    /// <summary>Shows why this item can't be picked up yet (rate-limited). Returns the line shown, or null when nothing was shown.</summary>
    public string ShowNotReadyReason()
    {
        if (grabEnabled || Time.unscaledTime - lastRefusalTime < refusalCooldownSeconds || EvidenceStateManager.Instance == null)
        {
            return null;
        }
        string reason = EvidenceStateManager.Instance.GetCollectionBlockReason(prop.evidenceId);
        if (string.IsNullOrEmpty(reason))
        {
            return null;
        }
        lastRefusalTime = Time.unscaledTime;
        string itemName = EvidenceStateManager.Instance.GetDisplayName(prop.evidenceId);
        GameDialogue.Say("collect-not-ready", itemName, reason);
        return GameDialogue.Text("collect-not-ready", itemName, reason);
    }

    /// <summary>Whether the physical grab is currently switched on.</summary>
    public bool GrabEnabled => grabEnabled;

    // Same measure as EvidenceBagTool: to the item's solid geometry, not its pivot.
    private float DistanceToItemSurface(Vector3 point)
    {
        float best = Vector3.Distance(point, transform.position);
        foreach (var collider in GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || (collider is MeshCollider mesh && !mesh.convex))
            {
                continue;
            }
            best = Mathf.Min(best, Vector3.Distance(point, collider.ClosestPoint(point)));
        }
        return best;
    }

    private void HandleStatusChanged(string evidenceId, EvidenceStatus status)
    {
        if (evidenceId != prop.evidenceId)
        {
            return;
        }

        RefreshGrabState();
    }

    /// <summary>
    /// Swabbing is a per-item flag, not a status in RequiredSequence (see
    /// EvidenceStateManager.OnEvidenceSwabbed), so it never reaches HandleStatusChanged
    /// above - this is the other half of what can make grab valid, for the common case
    /// where an item is already sitting at ReadyForCollection and the swab is the very
    /// last thing the player does before grab should unlock.
    /// </summary>
    private void HandleSwabbed(string evidenceId)
    {
        if (evidenceId != prop.evidenceId)
        {
            return;
        }

        RefreshGrabState();
    }

    /// <summary>
    /// Same reasoning as HandleSwabbed, for fingerprintingDone instead of swabbingDone
    /// - see RefreshGrabState's fingerprintSatisfied check and
    /// EvidenceStateManager.OnEvidenceFingerprinted's own comment.
    /// </summary>
    private void HandleFingerprinted(string evidenceId)
    {
        if (evidenceId != prop.evidenceId)
        {
            return;
        }

        RefreshGrabState();
    }

    /// <summary>
    /// Re-evaluates from the record's actual current state rather than trusting
    /// whichever event triggered the call. Swabbing/on-scene-fingerprinting now gate
    /// Logged (see EvidenceStateManager.IsBlockedBySwabbing/IsBlockedByOnSceneFingerprinting),
    /// so by the time ReadyForCollection fires they are always already satisfied -
    /// HandleSwabbed/HandleFingerprinted firing while status is still PhotographedFinal
    /// is therefore always a safe no-op (readyForCollection reads false at that
    /// moment). Both event handlers still funnel into this same check rather than each
    /// hardcoding one half of the condition, since re-deriving from the record is
    /// cheap and keeps this correct regardless of exactly which event fires last.
    /// </summary>
    private void RefreshGrabState()
    {
        var record = EvidenceStateManager.Instance?.GetRecord(prop.evidenceId);
        bool readyForCollection = record != null && record.status == EvidenceStatus.ReadyForCollection;
        bool swabSatisfied = record?.definition == null || !record.definition.requiresSwabbing || record.swabbingDone;

        // Mirrors swabSatisfied's own shape for the OTHER precondition an item can
        // carry ahead of Collected (EvidenceDefinition.fingerprintBeforeCollection):
        // reaching ReadyForCollection and being swabbed isn't enough on its own if
        // on-scene fingerprinting hasn't happened yet - without this, a player could
        // pick such an item up and walk off with it the instant it was swabbed,
        // before its print was ever lifted, the same "grab unlocks before the state
        // machine actually allows Collected" gap this component exists to close for
        // swabbing. Confirmed live as a real, reproducible gap on the knife (EVD-014)
        // back when it still used this shape: grab read enabled with
        // fingerprintingDone still false. Vacuously satisfied for any item that
        // doesn't use this shape at all - which, after EVD-014's own separate print
        // was removed and consolidated onto EVD-016, is every item that currently has
        // this component (EVD-019, the gloves, uses fingerprintBeforeCollection too
        // but has no EvidenceGrabGate at all - it isn't physically hand-grabbable).
        // The check stays: it's generic, not EVD-014-specific, and protects any
        // future item that combines both traits.
        bool fingerprintSatisfied = record?.definition == null || !record.definition.fingerprintBeforeCollection || record.fingerprintingDone;

        bool shouldEnable = readyForCollection && swabSatisfied && fingerprintSatisfied;

        Debug.Log($"[EvidenceGrabGate] {prop.evidenceId}: refresh - status={record?.status}, " +
            $"requiresSwabbing={(record?.definition != null && record.definition.requiresSwabbing)}, " +
            $"swabbingDone={record?.swabbingDone}, " +
            $"fingerprintBeforeCollection={(record?.definition != null && record.definition.fingerprintBeforeCollection)}, " +
            $"fingerprintingDone={record?.fingerprintingDone}, shouldEnable={shouldEnable}.");
        SetGrabEnabled(shouldEnable);
    }

    private void SetGrabEnabled(bool value)
    {
        grabEnabled = value;
        if (handGrabInteractable != null)
        {
            handGrabInteractable.enabled = value;
        }

        if (grabInteractable != null)
        {
            grabInteractable.enabled = value;
        }
        RefreshUIInput();

        Debug.Log($"[EvidenceGrabGate] {prop.evidenceId}: grab enabled set to {value} " +
            $"(handGrabInteractable.enabled={(handGrabInteractable != null ? handGrabInteractable.enabled.ToString() : "N/A - missing component")}, " +
            $"grabInteractable.enabled={(grabInteractable != null ? grabInteractable.enabled.ToString() : "N/A - missing component")}).");
    }

    private void RefreshUIInput()
    {
        lastUIBlocked = ToolInputLock.HasUIOwners;
        // Refuse new physical grabs while a modal owns input; keep a currently held
        // object selected so opening a question never drops evidence from the hand.
        if (lastUIBlocked)
        {
            if (handGrabInteractable != null && handGrabInteractable.enabled && handGrabInteractable.State != InteractableState.Select)
            { suppressedHandGrab = true; handGrabInteractable.enabled = false; }
            if (grabInteractable != null && grabInteractable.enabled && grabInteractable.State != InteractableState.Select)
            { suppressedControllerGrab = true; grabInteractable.enabled = false; }
        }
        else
        {
            if (suppressedHandGrab && handGrabInteractable != null) handGrabInteractable.enabled = grabEnabled;
            if (suppressedControllerGrab && grabInteractable != null) grabInteractable.enabled = grabEnabled;
            suppressedHandGrab = suppressedControllerGrab = false;
        }
    }
}
