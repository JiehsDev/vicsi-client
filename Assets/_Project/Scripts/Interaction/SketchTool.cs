// Assets/_Project/Scripts/Interaction/SketchTool.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grabbable sketchpad prop for the Sketcher role. Follows the same aim-and-activate
/// contract as PhotographTool - grab it, point the Activate raycast at an evidence
/// item, press to act.
///
/// Unlike photography, which really is a separate artifact per item, a real crime-scene
/// sketch is one spatial document with every item's numbered marker plotted onto it.
/// So the interaction here does not produce a per-item drawing: aiming at an item that
/// has already been Marked (and, per the existing sequence, Photographed) and pressing
/// Activate auto-projects that item's position onto the single shared
/// MasterSketchManager (no freehand drawing - this project isn't assessing drawing
/// skill) and then reports EvidenceStatus.Sketched exactly like every other tool
/// reports its own status, through the same gated SetStatus. MasterSketchUI is where
/// the accumulated result is actually reviewed.
/// </summary>
public class SketchTool : PlayerTool
{
    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Raycast")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float maxDistance = 5f;

    public override ToolType ToolRole => ToolType.Sketcher;

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
            TrySketch();
        }
    }

    private void TrySketch()
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

        if (ProceduralGateValidator.Instance != null && !ProceduralGateValidator.Instance.CanTransition(evidence.evidenceId, EvidenceStatus.Sketched))
        {
            string reason = ProceduralGateValidator.Instance.GetBlockReason(evidence.evidenceId, EvidenceStatus.Sketched);
            Debug.Log($"[SketchTool] Can't mark {evidence.evidenceId} Sketched: {reason}");
            NotificationManager.Notify(reason);
            // A refusal by the procedural gate. Distinguishable from a confirmed action
            // on purpose - the game declined, which is not the same as the player
            // having picked the wrong item.
            InteractionFeedback.Blocked();
            return;
        }

        // Gate already confirmed above, same as every other tool here - stamp this
        // item's tent number onto the one shared sketch before reporting the status
        // change, so MasterSketchUI never observes Sketched without the annotation
        // that's supposed to have caused it.
        MasterSketchManager.Instance?.RecordAnnotation(evidence);

        ReportEvidence(evidence.evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkSketched(id, tool), "sketched");
    }
}
