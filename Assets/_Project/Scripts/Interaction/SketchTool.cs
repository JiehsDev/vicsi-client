// Assets/_Project/Scripts/Interaction/SketchTool.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grabbable sketchpad prop for the Sketcher role. Follows the same aim-and-activate
/// contract as PhotographTool - grab it, point the Activate raycast at an evidence
/// item, press to act - but the sketching interaction/UI itself hasn't been designed
/// yet, so activating just marks the evidence Sketched. Fill in the real drawing
/// interaction later; EvidenceStateManager/ProceduralGateValidator already work
/// against ToolType.Sketcher as-is.
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
            return;
        }

        ReportEvidence(evidence.evidenceId, (id, tool) => EvidenceStateManager.Instance.MarkSketched(id, tool), "sketched");
    }
}
