// Assets/_Project/Scripts/Interaction/EvidenceFlipTool.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Aim-and-activate tool for physically flipping an item over - currently only
/// meaningful for the gloves (EVD-019, EvidenceDefinition.requiresFlipBeforeFingerprinting),
/// whose print is on the INSIDE surface, only reachable once turned over. Same
/// minimal "raycast for an EvidenceProp, press trigger, call the gated
/// EvidenceStateManager method" shape as RecorderTool - the simplest reasonable
/// interaction for what is otherwise just a single toggle action, not a new
/// interaction paradigm. Genuinely gated, not cosmetic: EvidenceStateManager.MarkFlipped
/// refuses outside PhotographedFinal, same as every other on-scene precondition.
/// </summary>
public class EvidenceFlipTool : PlayerTool
{
    public static EvidenceFlipTool Instance { get; private set; }

    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Raycast")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float maxDistance = 5f;

    public override ToolType ToolRole => ToolType.EvidenceFlip;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
        if (aimOrigin == null)
        {
            aimOrigin = transform;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
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
        // Dialogue History (a pure review panel) is open - no tool/world input behind it.
        if (ToolInputLock.IsLocked)
        {
            return;
        }

        if (!IsHeld)
        {
            return;
        }

        bool trigger =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());

        if (trigger)
        {
            TryFlipAimedTarget();
        }
    }

    private void TryFlipAimedTarget()
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

        TryFlip(evidence.evidenceId);
    }

    /// <summary>
    /// The real, gated flip action. Public so GreyboxFlowTest (and this task's own
    /// live verification) can exercise this exact logic directly, the same
    /// discipline every other tool's TryX method in this project follows.
    /// </summary>
    public TransitionResult TryFlip(string evidenceId)
    {
        if (string.IsNullOrEmpty(evidenceId) || EvidenceStateManager.Instance == null)
        {
            return TransitionResult.Violation;
        }

        var result = EvidenceStateManager.Instance.MarkFlipped(evidenceId, ToolRole);

        if (result == TransitionResult.Violation)
        {
            string reason = ProceduralGateValidator.Instance != null
                ? ProceduralGateValidator.Instance.GetBlockReason(evidenceId, EvidenceStatus.Logged)
                : GameDialogue.Text("flip-not-ready");
            NotificationManager.Notify(reason, DialogueKind.Error);
            InteractionFeedback.Blocked();
        }
        else if (result == TransitionResult.Applied)
        {
            // Real gap, not a hypothetical: unlike every other successful action in
            // this project (tented, photographed, logged, swabbed, dusted, lifted,
            // ...), a successful flip previously produced NO toast and no visual
            // change to the item - InteractionFeedback.Confirm() alone is a haptic
            // pulse plus a quiet generated tone, easy to miss entirely, especially
            // without a real headset. From the player's perspective that reads as
            // "nothing happened," i.e. broken, even though isFlipped genuinely did
            // flip. See MarkFlipped's own comment: this stays a pure state flag with
            // no visual rotation of the item by design - this toast is the fix for
            // the missing feedback, not a substitute for a visual flip.
            string flipName = EvidenceStateManager.Instance != null ? EvidenceStateManager.Instance.GetDisplayName(evidenceId) : evidenceId;
            GameDialogue.Say("flip-done", flipName);
            InteractionFeedback.Confirm();
        }

        return result;
    }
}
