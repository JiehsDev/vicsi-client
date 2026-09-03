// Assets/_Project/Scripts/Interaction/EvidenceCollectorTool.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grabbable evidence bag/kit prop for the Evidence Collector role. Aim-and-activate
/// like PhotographTool, but only succeeds once ProceduralGateValidator says the item
/// is ReadyForCollection (already photographed, sketched, and logged) - refuses with
/// a log line, not an exception, when the gate isn't open yet.
///
/// The same tool also applies the tamper-evident SEAL, on the same button. What the
/// trigger does depends on where the aimed item is in the procedure: an item that is
/// ReadyForCollection gets collected, an item that is already Collected gets sealed.
/// One binding, disambiguated by state, because adding a second button would have
/// collided with the right controller's B (already shared by EvidenceTentPickup and
/// PhotoAlbumUI) and because it mirrors how the real object works - you are holding
/// the bag, and what you do to the item next is whatever that item needs next.
///
/// Sealing is a DELIBERATE player action, not an automatic bump when Collected is
/// reached. Same reasoning that made tenting deliberate rather than passive like
/// Found: a step the game performs on the player's behalf records nothing about
/// whether the player knew to perform it. A student who bags evidence and walks away
/// without sealing it has made a real chain-of-custody error, and the log has to be
/// able to show that.
/// </summary>
public class EvidenceCollectorTool : PlayerTool
{
    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Raycast")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float maxDistance = 5f;

    public override ToolType ToolRole => ToolType.EvidenceCollector;

    protected override void Awake()
    {
        base.Awake();
        if (aimOrigin == null)
        {
            aimOrigin = transform;
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        leftActivateAction?.action.Enable();
        rightActivateAction?.action.Enable();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        leftActivateAction?.action.Disable();
        rightActivateAction?.action.Disable();
    }

    private void Update()
    {
        if (!IsHeld)
        {
            return;
        }

        bool trigger =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());

        if (trigger)
        {
            TryAct();
        }
    }

    private void TryAct()
    {
        if (!Physics.Raycast(aimOrigin.position, aimOrigin.forward, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        var evidence = hit.collider.GetComponentInParent<EvidenceProp>();
        if (evidence == null || string.IsNullOrEmpty(evidence.evidenceId))
        {
            return;
        }

        // Already in the bag - the next thing this item needs is a seal, not another
        // collection. Checked before the collect path so the two can never compete.
        var record = EvidenceStateManager.Instance != null
            ? EvidenceStateManager.Instance.GetRecord(evidence.evidenceId)
            : null;

        if (record != null && record.status == EvidenceStatus.Collected)
        {
            TrySeal(evidence.evidenceId);
            return;
        }

        if (ProceduralGateValidator.Instance != null && !ProceduralGateValidator.Instance.CanCollect(evidence.evidenceId))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(evidence.evidenceId);
            Debug.Log($"[EvidenceCollectorTool] Can't collect {evidence.evidenceId}: {reason}");
            NotificationManager.Notify(reason);
            // A refusal by the procedural gate. Distinguishable from a confirmed action
            // on purpose - the game declined, which is not the same as the player
            // having picked the wrong item.
            InteractionFeedback.Blocked();
            return;
        }

        ReportEvidence(evidence.evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkCollected(id, tool), "collected");
    }

    private void TrySeal(string evidenceId)
    {
        if (ProceduralGateValidator.Instance != null
            && !ProceduralGateValidator.Instance.CanTransition(evidenceId, EvidenceStatus.Sealed))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(evidenceId, EvidenceStatus.Sealed);
            Debug.Log($"[EvidenceCollectorTool] Can't seal {evidenceId}: {reason}");
            NotificationManager.Notify(reason);
            InteractionFeedback.Blocked();
            return;
        }

        ReportEvidence(evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkSealed(id, tool), "sealed");
    }
}
