// Assets/_Project/Scripts/RoleSystem/PlayerTool.cs
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;

/// <summary>
/// Base class for every capability the single player character can pick up and use -
/// camera, sketchpad, evidence bag, recorder, and whatever comes after. There is no
/// separate playable character per role: ToolType just tags which capability a given
/// tool represents, and every tool self-registers with PlayerToolRegistry on enable.
/// Adding a new tool means subclassing this and dropping it in the scene - no other
/// script needs to change.
///
/// Tools are toggleable, not physical pickups: every tool starts hidden and inert
/// (renderers off, colliders off) and only appears, attached to a hand, once equipped
/// through the tool wheel. There's nothing sitting out in the world to walk up to.
/// </summary>
[RequireComponent(typeof(Grabbable))]
public abstract class PlayerTool : MonoBehaviour
{
    [Header("Virtual Equip (tool wheel)")]
    [Tooltip("Local position/rotation applied when this tool is virtually equipped to a hand anchor via the tool wheel, rather than physically grabbed from the world.")]
    [SerializeField] private Vector3 equipLocalPosition;
    [SerializeField] private Vector3 equipLocalEulerAngles;

    /// <summary>Which role/capability this tool represents. Must not be ToolType.None.</summary>
    public abstract ToolType ToolRole { get; }

    protected Grabbable Grabbable { get; private set; }

    /// <summary>True while a hand/controller is actively selecting this tool's Grabbable, or while it's attached to a hand anchor via EquipToHand (the tool wheel).</summary>
    protected bool IsHeld => (Grabbable != null && Grabbable.SelectingPointsCount > 0) || IsVirtuallyEquipped;

    /// <summary>True while this tool is attached to a hand anchor via EquipToHand (the tool wheel), as opposed to a physical grab.</summary>
    public bool IsVirtuallyEquipped { get; private set; }

    private Rigidbody toolRigidbody;
    private Collider[] toolColliders;
    private Renderer[] toolRenderers;
    private HandGrabInteractable handGrabInteractable;
    private GrabInteractable grabInteractable;

    private Transform homeParent;
    private Vector3 homeLocalPosition;
    private Quaternion homeLocalRotation;

    protected virtual void Awake()
    {
        Grabbable = GetComponent<Grabbable>();
        toolRigidbody = GetComponent<Rigidbody>();
        toolColliders = GetComponentsInChildren<Collider>(true);
        toolRenderers = GetComponentsInChildren<Renderer>(true);
        handGrabInteractable = GetComponent<HandGrabInteractable>();
        grabInteractable = GetComponent<GrabInteractable>();

        homeParent = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;

        // Hidden and inert until equipped from the tool wheel - no physical prop
        // sitting out in the world to find.
        SetEquippedVisualState(false);

        if (ToolRole == ToolType.None)
        {
            Debug.LogWarning($"[{GetType().Name}] ToolRole is ToolType.None; this tool won't be reachable through PlayerToolRegistry.", this);
        }
    }

    protected virtual void OnEnable()
    {
        if (Grabbable != null)
        {
            Grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }
        PlayerToolRegistry.Register(this);
    }

    protected virtual void OnDisable()
    {
        if (Grabbable != null)
        {
            Grabbable.WhenPointerEventRaised -= HandlePointerEvent;
        }
        PlayerToolRegistry.Unregister(this);
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select)
        {
            PlayerToolRegistry.NotifyToolGrabbed(this);
        }
    }

    /// <summary>
    /// Marks this tool as the character's current tool without requiring a physical
    /// grab - for future non-grab flows (a tool-select menu, teleport-to-hand, etc).
    /// Override to add tool-specific equip behaviour, but always call base.RequestEquip().
    /// </summary>
    public virtual void RequestEquip()
    {
        PlayerToolRegistry.NotifyToolGrabbed(this);
    }

    /// <summary>
    /// Instantly attaches this tool to a hand anchor as though pulled from a virtual
    /// holster (the tool wheel), instead of being physically grabbed from the world.
    /// Disables this tool's physics/grab interactables while equipped so it doesn't
    /// fight hand physics or get re-grabbed mid-air. No-ops (safely) if a hand is
    /// already physically holding this tool, or if it's already virtually equipped.
    /// </summary>
    public virtual void EquipToHand(Transform handAnchor)
    {
        if (handAnchor == null || IsHeld)
        {
            return;
        }

        transform.SetParent(handAnchor, false);
        transform.localPosition = equipLocalPosition;
        transform.localRotation = Quaternion.Euler(equipLocalEulerAngles);
        IsVirtuallyEquipped = true;
        SetEquippedVisualState(true);
    }

    /// <summary>
    /// Returns this tool to hidden/inert, undoing EquipToHand(). Safe to call even when
    /// not equipped.
    /// </summary>
    public virtual void Holster()
    {
        if (!IsVirtuallyEquipped)
        {
            return;
        }

        IsVirtuallyEquipped = false;
        transform.SetParent(homeParent, false);
        transform.localPosition = homeLocalPosition;
        transform.localRotation = homeLocalRotation;
        SetEquippedVisualState(false);
    }

    // Shows/hides this tool: visible while equipped, invisible and inert otherwise.
    // The Rigidbody always stays kinematic - there's no physical grab-and-throw path
    // anymore, and a dynamic Rigidbody parented under a hand anchor would just sag
    // out of the hand under gravity instead of following it rigidly.
    //
    // Collider/HandGrabInteractable/GrabInteractable stay disabled in BOTH states,
    // not just while hidden. Equipping is exclusively the tool wheel's job (see
    // EquipToHand) - if these stayed enabled while equipped, a hand passing near the
    // tool (or ToggleGrab's sticky-select on it) could grab it mid-air, and Oculus's
    // own grab-follow transform would then fight the wheel's fixed hand-anchor
    // pinning, leaving the tool floating wherever that grab let go instead of
    // resetting to its equip pose.
    private void SetEquippedVisualState(bool equipped)
    {
        if (toolRigidbody != null)
        {
            toolRigidbody.isKinematic = true;
        }

        foreach (var collider in toolColliders)
        {
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        foreach (var renderer in toolRenderers)
        {
            if (renderer != null)
            {
                renderer.enabled = equipped;
            }
        }

        if (handGrabInteractable != null)
        {
            handGrabInteractable.enabled = false;
        }

        if (grabInteractable != null)
        {
            grabInteractable.enabled = false;
        }
    }

    /// <summary>
    /// Reports an evidence status change caused by this tool: invokes the matching
    /// EvidenceStateManager method (e.g. MarkPhotographed) and optionally fires the
    /// matching STCS trigger. Safe to call even if those managers aren't in the scene
    /// yet - logs a warning instead of throwing, so a new tool can never NRE here.
    /// </summary>
    protected void ReportEvidence(string evidenceId, System.Action<string, ToolType> markMethod, string stcsSuffix = null)
    {
        if (string.IsNullOrEmpty(evidenceId) || markMethod == null)
        {
            return;
        }

        if (EvidenceStateManager.Instance == null)
        {
            Debug.LogWarning($"[{GetType().Name}] No EvidenceStateManager.Instance in scene.");
            return;
        }

        markMethod(evidenceId, ToolRole);

        if (!string.IsNullOrEmpty(stcsSuffix))
        {
            FireEvidenceSTCSTrigger(evidenceId, stcsSuffix);
        }
    }

    /// <summary>
    /// Fires "evidence_&lt;digits&gt;_&lt;suffix&gt;" on STCSManager, e.g. "EVD-014" +
    /// "photographed" -> "evidence_014_photographed". No-op if STCSManager isn't present.
    /// </summary>
    protected static void FireEvidenceSTCSTrigger(string evidenceId, string suffix)
    {
        if (STCSManager.Instance == null || string.IsNullOrEmpty(evidenceId) || string.IsNullOrEmpty(suffix))
        {
            return;
        }

        var digits = System.Array.FindAll(evidenceId.ToCharArray(), char.IsDigit);
        if (digits.Length == 0)
        {
            return;
        }

        STCSManager.Instance.Fire($"evidence_{new string(digits)}_{suffix}");
    }
}
