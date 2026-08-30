// Assets/_Project/Scripts/Interaction/FlashlightTool.cs
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Grabbable UV flashlight prop for the IOC role. Unlike the other tools there's
/// nothing to collect or log here - pressing Activate just clicks the beam on and off,
/// like a real flashlight, so IOC has something to actually equip from the tool wheel.
///
/// The beam is forced to a purple Spot light every Awake - a UV flashlight should only
/// light up a cone in front of the prop, not glow like a point light in every direction.
/// While the beam is on, a raycast runs every frame from beamOrigin so other systems
/// (a highlight, a reveal effect, ...) can react to whatever the beam is currently
/// pointed at via CurrentBeamTarget / OnBeamTargetChanged, the same event-driven pattern
/// PhotographTool uses for its aim state.
/// </summary>
public class FlashlightTool : PlayerTool
{
    [Header("Input (XRI Input Reader pattern)")]
    [SerializeField] private InputActionReference leftActivateAction;
    [SerializeField] private InputActionReference rightActivateAction;

    [Header("Beam")]
    [SerializeField] private Light beamLight;
    [SerializeField] private Color beamColor = new Color(0.55f, 0.05f, 1f);
    [SerializeField, Range(1f, 179f)] private float beamSpotAngle = 30f;

    [Header("Raycast")]
    [SerializeField] private Transform beamOrigin;
    [SerializeField] private float maxDistance = 5f;

    public override RoleId ToolRole => RoleId.IOC;

    /// <summary>The EvidenceProp currently in the beam's path, or null. Only ever non-null while the beam is on.</summary>
    public EvidenceProp CurrentBeamTarget { get; private set; }

    /// <summary>Fired whenever CurrentBeamTarget changes (including to/from null).</summary>
    public event Action<EvidenceProp> OnBeamTargetChanged;

    private bool isOn;

    protected override void Awake()
    {
        base.Awake();

        if (beamOrigin == null)
        {
            beamOrigin = transform;
        }

        if (beamLight != null)
        {
            beamLight.enabled = false;
            // A UV flashlight lights up a forward cone, not the surrounding room -
            // Spot (not Point) is what keeps the light in front of the prop only.
            beamLight.type = LightType.Spot;
            beamLight.spotAngle = beamSpotAngle;
            beamLight.color = beamColor;
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
            SetBeam(false);
            return;
        }

        bool trigger =
            (leftActivateAction != null && leftActivateAction.action.WasPressedThisFrame()) ||
            (rightActivateAction != null && rightActivateAction.action.WasPressedThisFrame());

        if (trigger)
        {
            SetBeam(!isOn);
        }

        if (isOn)
        {
            UpdateBeamTarget();
        }
    }

    private void SetBeam(bool on)
    {
        if (isOn == on)
        {
            return;
        }

        isOn = on;
        if (beamLight != null)
        {
            beamLight.enabled = on;
        }

        if (!on)
        {
            SetBeamTarget(null);
        }
    }

    private void UpdateBeamTarget()
    {
        EvidenceProp target = null;

        if (Physics.Raycast(beamOrigin.position, beamOrigin.forward, out var hit, maxDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            var evidence = hit.collider.GetComponentInParent<EvidenceProp>();
            if (evidence != null && !string.IsNullOrEmpty(evidence.evidenceId))
            {
                target = evidence;
            }
        }

        SetBeamTarget(target);
    }

    private void SetBeamTarget(EvidenceProp target)
    {
        if (target == CurrentBeamTarget)
        {
            return;
        }

        CurrentBeamTarget = target;
        OnBeamTargetChanged?.Invoke(target);
    }
}
