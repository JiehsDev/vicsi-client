// Assets/_Project/Scripts/RoleSystem/HeldItemCoordinator.cs
using UnityEngine;

/// <summary>
/// THE single public authority for "what is in the player's hand right now" - tools
/// AND evidence. Every controller that wants to equip/unequip something
/// (ToolWheelController for tools, EvidenceInventoryUI for evidence) goes through this,
/// never through PlayerToolRegistry or EvidenceInventory directly - see this feature's
/// own explicit mutual-exclusion requirement ("no controller independently decides what
/// is held").
///
/// Deliberately owns NO state of its own beyond a scene-manager Instance reference:
/// CurrentCategory/CurrentToolRole/CurrentEvidenceId are all computed live by reading
/// straight through to PlayerToolRegistry (which remains the sole owner of "actual tool
/// activation and holstering") and EvidenceInventory (which remains the sole owner of
/// "evidence ownership, custody, and the held-in-hand visual"). This is what makes it
/// possible for there to be exactly one source of truth for each of the four things this
/// feature's own review checklist calls out (current held category, current tool,
/// current evidence, hand visual) - a coordinator that cached its own copy would just be
/// a second, potentially-divergent answer to a question those two already answer
/// correctly.
///
/// A scene-manager MonoBehaviour singleton (same Instance pattern as
/// EvidenceStateManager/EvidenceInventory/SessionLogger), not a static class -
/// PlayerToolRegistry and PlayerUIGate are static because they predate this feature and
/// changing their shape wasn't in scope, but nothing here needs domain-reload-surviving
/// static state, so the scene-manager shape is preferred for new code per this feature's
/// own direction.
/// </summary>
public class HeldItemCoordinator : MonoBehaviour
{
    public static HeldItemCoordinator Instance { get; private set; }

    public enum HeldCategory { None, Tool, Evidence }

    /// <summary>What kind of thing is currently in the player's hand, computed live - never cached.</summary>
    public HeldCategory CurrentCategory
    {
        get
        {
            if (PlayerToolRegistry.CurrentRole != ToolType.None)
            {
                return HeldCategory.Tool;
            }
            if (EvidenceInventory.Instance != null && EvidenceInventory.Instance.CurrentHeldEvidenceId != null)
            {
                return HeldCategory.Evidence;
            }
            return HeldCategory.None;
        }
    }

    /// <summary>The currently equipped/held tool role, or ToolType.None - reads straight through to PlayerToolRegistry, never cached here.</summary>
    public ToolType CurrentToolRole => PlayerToolRegistry.CurrentRole;

    /// <summary>The currently held evidence id, or null - reads straight through to EvidenceInventory, never cached here.</summary>
    public string CurrentEvidenceId => EvidenceInventory.Instance != null ? EvidenceInventory.Instance.CurrentHeldEvidenceId : null;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        // Backstop for a PHYSICAL grab of a tool (bypassing the wheel entirely -
        // PlayerTool.HandlePointerEvent -> PlayerToolRegistry.NotifyToolGrabbed). The
        // wheel path already unequips held evidence deterministically before ever
        // calling PlayerToolRegistry (see ToggleEquipTool below); this only has work
        // left to do for a path that bypasses the coordinator entirely.
        PlayerToolRegistry.OnToolGrabbed += HandleToolGrabbedElsewhere;
    }

    private void OnDisable()
    {
        PlayerToolRegistry.OnToolGrabbed -= HandleToolGrabbedElsewhere;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandleToolGrabbedElsewhere(ToolType role, PlayerTool tool)
    {
        if (EvidenceInventory.Instance != null && EvidenceInventory.Instance.CurrentHeldEvidenceId != null)
        {
            EvidenceInventory.Instance.UnequipHeld();
        }
    }

    /// <summary>
    /// Requests that role become the equipped tool, mirroring PlayerToolRegistry.
    /// ToggleEquip's own toggle semantics (selecting the tool already in-hand holsters
    /// it instead) - what ToolWheelController.CloseWheel calls instead of reaching into
    /// PlayerToolRegistry directly. Unequips held evidence FIRST, deterministically,
    /// before PlayerToolRegistry ever runs - not left to the OnToolGrabbed backstop
    /// above, so the ordering is always the same regardless of event-subscription
    /// timing. Respects PlayerUIGate: refuses while blocked, the same contract every
    /// other equip-adjacent action in this project already has.
    /// </summary>
    public bool ToggleEquipTool(ToolType role, Transform handAnchor)
    {
        if(role!=PlayerToolRegistry.VirtuallyEquippedRole && !PlayerToolRegistry.CanEquip(role))return false;
        if (PlayerUIGate.IsBlocked || EvidenceInventory.Instance?.RequiresExplicitStorage == true)
        {
            return false;
        }

        if (EvidenceInventory.Instance != null && EvidenceInventory.Instance.CurrentHeldEvidenceId != null)
        {
            EvidenceInventory.Instance.UnequipHeld();
        }

        bool equipped=PlayerToolRegistry.ToggleEquip(role, handAnchor);
        if(equipped) InteractionSfx.Select();
        return equipped;
    }

    /// <summary>Holsters whatever tool is currently equipped (the tool wheel's "Empty Hands" wedge) - what ToolWheelController.CloseWheel calls instead of PlayerToolRegistry.HolsterCurrent directly. Never touches held evidence - selecting Empty Hands on the tool wheel says nothing about evidence.</summary>
    public void HolsterCurrentTool()
    {
        PlayerToolRegistry.HolsterCurrent();
    }

    /// <summary>
    /// Requests that evidenceId become the held item - what EvidenceInventoryUI calls to
    /// equip a bagged item from the panel. Holsters the current tool FIRST
    /// (PlayerToolRegistry.HolsterCurrent - a genuine holster, not a toggle: there is no
    /// "select the same tool again to unequip it" concept from the inventory panel),
    /// deterministically, before EvidenceInventory ever runs. Respects PlayerUIGate.
    ///
    /// requester is the caller's own PlayerUIGate token, if it holds one (e.g.
    /// EvidenceInventoryUI passes itself). Unlike ToolWheelController - which exits its
    /// own gate lock before ever calling the equivalent tool-equip path, since it closes
    /// on every selection - a persistent, browsable panel like the inventory UI is still
    /// holding its own lock at the moment the player equips something from it. Without
    /// requester, the plain PlayerUIGate.IsBlocked check below would always read true
    /// the instant such a caller has entered the gate, making every equip from that
    /// panel silently refuse - confirmed as the actual cause of "evidence already in
    /// inventory can't reliably be equipped". Left null (default) for callers with no
    /// gate token of their own (e.g. EvidenceBagTool/SwabTool's own post-seal
    /// convenience equip), which keeps the plain, stricter IsBlocked check for them.
    /// </summary>
    public bool EquipEvidence(string evidenceId, Transform handAnchor, object requester = null)
    {
        if (!ProcedureInterlock.AllowsHandling(evidenceId)) return false;
        // Once inventory is acknowledged, keep custody stable for survey/report/release.
        if (SceneTurnover.IsInvestigationBlocked) return false;
        bool blocked = requester != null ? PlayerUIGate.IsBlockedByOther(requester) : PlayerUIGate.IsBlocked;
        if (blocked || EvidenceInventory.Instance == null || (EvidenceInventory.Instance.RequiresExplicitStorage
            && EvidenceInventory.Instance.CurrentHeldEvidenceId != evidenceId))
        {
            return false;
        }

        PlayerToolRegistry.HolsterCurrent();
        return EvidenceInventory.Instance.EquipHeld(evidenceId, handAnchor);
    }

    /// <summary>Returns the currently held evidence item to inventory - what EvidenceInventoryUI's own "Unequip" action calls. Never touches tools.</summary>
    public void UnequipEvidence()
    {
        if (EvidenceInventory.Instance?.RequiresExplicitStorage == true) TryStoreHeldEvidence();
        else EvidenceInventory.Instance?.UnequipHeld();
    }

    public string HoldContainedSample(string sourceId, GameObject prefab, Transform handAnchor)
    {
        if (PlayerUIGate.IsBlocked || EvidenceInventory.Instance == null || handAnchor == null) return null;
        string itemId = EvidenceInventory.Instance.RegisterContainedSample(sourceId, prefab);
        return itemId != null && EquipEvidence(itemId, handAnchor) ? itemId : null;
    }

    public bool HoldBaggedEvidence(string evidenceId, Transform handAnchor)
    {
        if (PlayerUIGate.IsBlocked || ToolInputLock.IsLocked || EvidenceInventory.Instance == null || handAnchor == null) return false;
        if (!EvidenceInventory.Instance.RegisterBaggedEvidence(evidenceId)) return false;
        return EquipEvidence(evidenceId, handAnchor);
    }

    public bool HoldLiftedFingerprint(string sourceId, GameObject prefab, Transform handAnchor)
    {
        if (PlayerUIGate.IsBlocked || ToolInputLock.IsLocked || handAnchor == null || EvidenceInventory.Instance == null) return false;
        var inventory = EvidenceInventory.Instance;
        string itemId = inventory.RegisterLiftedFingerprint(sourceId, prefab);
        if (itemId == null) return false;
        if (EquipEvidence(itemId, handAnchor)) return true;
        inventory.CancelUnheldFingerprint(itemId);
        return false;
    }

    public bool TryStoreHeldEvidence()
    {
        var inventory = EvidenceInventory.Instance;
        if (PlayerUIGate.IsBlocked || ToolInputLock.HasUIOwners || SceneTurnover.IsInvestigationBlocked || inventory?.CurrentHeldEvidenceId == null
            || !ProcedureInterlock.AllowsHandling(inventory.CurrentHeldEvidenceId)) return false;
        string itemId = inventory.CurrentHeldEvidenceId;
        if (inventory.GetSampleSource(itemId) != null)
        {
            // B closes the contained sample through the existing sealing action before custody storage.
            if (!inventory.IsFingerprintSample(itemId) && !inventory.IsSampleSealed(itemId)
                && (SwabTool.Instance == null || SwabTool.Instance.TrySeal() == TransitionResult.Violation)) return false;
            if (!inventory.ConfirmSampleStorage()) return false;
        }
        if (inventory.IsBaggedEvidence(itemId) && !inventory.ConfirmBagStorage()) return false;
        inventory.UnequipHeld();
        return inventory.CurrentHeldEvidenceId == null;
    }
}
