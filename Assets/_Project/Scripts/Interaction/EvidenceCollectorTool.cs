// Assets/_Project/Scripts/Interaction/EvidenceCollectorTool.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grabbable evidence bag/kit prop for the Evidence Collector role. Aim-and-activate
/// like PhotographTool, but only succeeds once ProceduralGateValidator says the item
/// is ReadyForCollection (already photographed, sketched, and logged) - refuses with
/// a log line, not an exception, when the gate isn't open yet.
/// </summary>
public class EvidenceCollectorTool : PlayerTool
{
    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Raycast")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float maxDistance = 5f;

    public override RoleId ToolRole => RoleId.EvidenceCollector;

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
            TryCollect();
        }
    }

    private void TryCollect()
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

        if (ProceduralGateValidator.Instance != null && !ProceduralGateValidator.Instance.CanCollect(evidence.evidenceId))
        {
            Debug.Log($"[EvidenceCollectorTool] Can't collect {evidence.evidenceId}: {ProceduralGateValidator.Instance.GetBlockReason(evidence.evidenceId)}");
            return;
        }

        ReportEvidence(evidence.evidenceId, (id, role) => EvidenceStateManager.Instance.MarkCollected(id, role), "collected");
    }
}
