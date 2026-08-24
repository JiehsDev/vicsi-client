// Assets/_Project/Scripts/Interaction/RecorderTool.cs
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grabbable dictaphone/logging prop for the Recorder role. Aim-and-activate like
/// PhotographTool; activating logs the evidence into the case file
/// (EvidenceStatus.Logged), which is what opens the Evidence Collector's gate via
/// EvidenceStateManager.CheckReadyForCollection.
/// </summary>
public class RecorderTool : PlayerTool
{
    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Raycast")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private float maxDistance = 5f;

    public override RoleId ToolRole => RoleId.Recorder;

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
            TryLog();
        }
    }

    private void TryLog()
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

        ReportEvidence(evidence.evidenceId, (id, role) => EvidenceStateManager.Instance.MarkLogged(id, role), "logged");
    }
}
