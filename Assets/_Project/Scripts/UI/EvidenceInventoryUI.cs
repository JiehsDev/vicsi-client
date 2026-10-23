// Assets/_Project/Scripts/UI/EvidenceInventoryUI.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The evidence inventory panel - opened as a secondary screen from
/// UtilityMenuController's "Evidence Inventory" entry (see that MenuEntry's onSelect ->
/// Show wiring), exactly the same handoff PhotoAlbumUI's own Album entry already uses:
/// UtilityMenuController exits its own PlayerUIGate token the instant it closes, and
/// this panel enters its own immediately after, in the same synchronous call chain -
/// see UtilityMenuController.CloseMenu.
///
/// Every visual piece (background, header, entry list, entry template) is a normal
/// authored UI GameObject in the scene, same "scene-authored, not built in code"
/// discipline PhotoAlbumUI already established for exactly this kind of secondary
/// panel - this script only wires behavior.
///
/// Right thumbstick highlights an entry (one flick = one step, same debounce as
/// PhotoAlbumUI's own grid navigation); right A on the highlighted entry equips it
/// (auto-unequipping whatever was held/equipped before, via HeldItemCoordinator) or, if
/// it's already the equipped item, unequips it instead; right B always closes the
/// panel. Closing the panel never itself unequips anything - equip state lives in
/// HeldItemCoordinator/EvidenceInventory, completely independent of this panel's own
/// open/closed lifecycle, and independent of UtilityMenuController's lifecycle too:
/// releasing X may close the utility menu underneath this already-open panel, but must
/// never touch what's currently held.
/// </summary>
public class EvidenceInventoryUI : MonoBehaviour
{
    private const float NavDeadzone = 0.5f;

    [Tooltip("The GameObject shown/hidden by Show()/Hide(). Defaults to this GameObject if left unassigned.")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Parent that entry rows are instantiated into - give it a Vertical Layout Group (or similar) to control spacing in the Inspector.")]
    [SerializeField] private RectTransform content;

    [Tooltip("An inactive entry row in the scene used as the template for each inventory item - style this one and every entry will look the same.")]
    [SerializeField] private EvidenceInventoryEntryView entryTemplate;

    [Tooltip("Hand anchor evidence is equipped to. Right hand by default per this feature's own direction, unless a future item needs a different hand. Resolved from OVRCameraRig.rightHandAnchor if left unassigned.")]
    [SerializeField] private Transform equipHandAnchor;

    [SerializeField] private TMP_Text emptyLabel;
    [SerializeField] private string emptyMessage = "No sealed evidence yet.";

    private readonly List<EvidenceInventoryEntryView> rows = new();
    private int selectedIndex = -1;
    private bool stickWasNeutral = true;

    private InputAction navStickAction;
    private InputAction selectAction;
    private InputAction closeAction;

    private OVRCameraRig cameraRig;
    private ResponsivePanelLayout responsiveLayout;
    private WorldPanelPointer pointer;

    private void Awake()
    {
        // NOTE: panelRoot is expected to be explicitly assigned in the Inspector
        // (to this same GameObject) rather than relying on this fallback alone.
        // This component lives on the exact GameObject Show()/Hide() toggle, and
        // that GameObject is meant to start INACTIVE in the saved scene (see
        // Hide()'s own contract) - but Awake() never runs on a component whose
        // GameObject hasn't been activated yet, so a null panelRoot could never
        // be resolved here in time for the first ever Show() call. Confirmed as a
        // real deadlock, not a hypothetical: an earlier attempt at hiding this
        // panel by default via `panelRoot.SetActive(false)` inside this very
        // Awake() made the panel permanently unopenable, since Awake() itself
        // never fires while the GameObject is inactive. Explicit Inspector wiring
        // sidesteps the whole problem; this stays only as a defensive fallback for
        // whatever rare case Awake() does get to run.
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (entryTemplate != null)
        {
            entryTemplate.gameObject.SetActive(false);
        }

        // Same raw XR-controller pattern used throughout this project (PhotographTool,
        // ToolWheelController, PhotoAlbumUI, ...).
        navStickAction = new InputAction("EvidenceInventory_NavStick", InputActionType.Value, "<XRController>{RightHand}/thumbstick", expectedControlType: "Vector2");
        selectAction = PlayerUIGate.CreateConfirmAction(this, "EvidenceInventory_Select", () => panelRoot != null && panelRoot.activeInHierarchy, 300);
        closeAction = new InputAction("EvidenceInventory_Close", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        ConfigureLayout();
    }

    private void ConfigureLayout()
    {
        if (content == null || panelRoot == null || responsiveLayout != null) return;
        var header = panelRoot.transform.Find("Header")?.GetComponentInChildren<TMP_Text>(true);
        var footer = ForensicUIStyle.Text(panelRoot.transform, "InventoryControls", 24, ForensicUIStyle.TextPrimary);
        footer.text = "Right stick: select    Left/right: scroll\nA: equip / return    B: close";
        responsiveLayout = ResponsivePanelLayout.Create((RectTransform)panelRoot.transform, header, content, footer, 240, 740);
        if (emptyLabel != null)
        {
            emptyLabel.fontSize = 28;
            responsiveLayout.AddText(emptyLabel, responsiveLayout.Body, 64);
        }
        pointer = new WorldPanelPointer((RectTransform)panelRoot.transform, "Inventory", this);
        responsiveLayout.RegisterControls(pointer, "inventory-scroll-");
        var closeButton = panelRoot.transform.Find("CloseButton") as RectTransform;
        if (closeButton != null) pointer.Register("inventory-close", closeButton, Vector2.zero, Hide);
    }

    private void RefreshLayout()
    {
        if (responsiveLayout == null) return;
        float width = ((RectTransform)panelRoot.transform).rect.width - 80;
        foreach (var row in rows) row.RefreshLayout(width);
        responsiveLayout.Refresh(false);
    }

    private void LateUpdate() => RefreshLayout();

    private void OnEnable()
    {
        navStickAction.Enable();
        selectAction.Enable();
        closeAction.Enable();
        pointer?.Enable();
        InputSystem.onDeviceChange += OnDeviceChange;

        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.OnInventoryChanged += RefreshEntries;
        }
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        navStickAction.Disable();
        selectAction.Disable();
        closeAction.Disable();
        pointer?.Disable();
        PlayerUIGate.Exit(this);

        if (EvidenceInventory.Instance != null)
        {
            EvidenceInventory.Instance.OnInventoryChanged -= RefreshEntries;
        }
    }

    // These are loose actions (not part of an InputActionAsset), so their binding is
    // resolved once, against whatever devices exist at Enable() time. Confirmed live on
    // a Quest 3S test (see ToolWheelController's own identical fix and comment): the XR
    // controller device can be torn down and recreated mid-session while the OpenXR/OVR
    // session renegotiates - the physical stick/buttons and Input System both keep
    // working, but a loose action's stale resolution can miss the replacement device
    // entirely. Re-enabling on every device add/reconnect forces a fresh resolution so
    // this can't go stale for the rest of the session - this is exactly what "right
    // stick doesn't highlight anything, A does nothing" looks like from a stale binding.
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change != InputDeviceChange.Added && change != InputDeviceChange.Reconnected)
        {
            return;
        }

        navStickAction.Disable();
        navStickAction.Enable();
        selectAction.Disable();
        selectAction.Enable();
        closeAction.Disable();
        closeAction.Enable();
        pointer?.Rebind();
    }

    private void OnDestroy()
    {
        navStickAction?.Dispose();
        selectAction?.Dispose();
        closeAction?.Dispose();
        pointer?.Dispose();
    }

    private void EnsureCameraRig()
    {
        if (cameraRig == null)
        {
            cameraRig = FindFirstObjectByType<OVRCameraRig>();
        }
    }

    private Transform ResolveHandAnchor()
    {
        if (equipHandAnchor != null)
        {
            return equipHandAnchor;
        }

        EnsureCameraRig();
        return cameraRig != null ? cameraRig.rightHandAnchor : null;
    }

    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf)
        {
            stickWasNeutral = true;
            return;
        }

        if (closeAction.WasPressedThisFrame())
        {
            Hide();
            return;
        }

        pointer?.Tick();
        if (!panelRoot.activeSelf) return;

        if (PlayerUIGate.TryConsumeConfirm(this, selectAction))
        {
            HandleSelect();
        }

        Vector2 stick = navStickAction.ReadValue<Vector2>();
        if (Mathf.Abs(stick.x) > NavDeadzone && Mathf.Abs(stick.x) > Mathf.Abs(stick.y))
        {
            responsiveLayout?.ScrollOptions(stick.x * 300 * Time.unscaledDeltaTime);
            return;
        }
        if (stick.sqrMagnitude < NavDeadzone * NavDeadzone)
        {
            stickWasNeutral = true;
            return;
        }

        if (!stickWasNeutral)
        {
            return;
        }
        stickWasNeutral = false;

        MoveSelection(stick.y > 0f ? -1 : 1);
    }

    private void MoveSelection(int delta)
    {
        if (rows.Count == 0)
        {
            return;
        }

        SetSelectedIndex(Mathf.Clamp(selectedIndex + delta, 0, rows.Count - 1));
    }

    private void SetSelectedIndex(int index)
    {
        if (rows.Count == 0)
        {
            selectedIndex = -1;
            return;
        }

        if (selectedIndex >= 0 && selectedIndex < rows.Count)
        {
            rows[selectedIndex].SetHighlighted(false);
        }

        selectedIndex = Mathf.Clamp(index, 0, rows.Count - 1);
        rows[selectedIndex].SetHighlighted(true);
        responsiveLayout?.EnsureVisible((RectTransform)rows[selectedIndex].transform);
    }

    private void HandleSelect()
    {
        if (selectedIndex < 0 || selectedIndex >= rows.Count)
        {
            return;
        }

        var row = rows[selectedIndex];
        string evidenceId = row.EvidenceId;

        if (HeldItemCoordinator.Instance == null || EvidenceInventory.Instance == null)
        {
            return;
        }

        if (EvidenceInventory.Instance.IsHeld(evidenceId))
        {
            HeldItemCoordinator.Instance.UnequipEvidence();
            return;
        }

        if (!row.CanEquip)
        {
            NotificationManager.Notify(row.LockedReason, DialogueKind.Error);
            InteractionFeedback.Blocked();
            return;
        }

        var handAnchor = ResolveHandAnchor();
        if (handAnchor == null)
        {
            Debug.LogWarning("[EvidenceInventoryUI] No hand anchor resolved - cannot equip evidence.", this);
            return;
        }

        HeldItemCoordinator.Instance.EquipEvidence(evidenceId, handAnchor, this);
    }

    private void RefreshEntries()
    {
        string previousId = selectedIndex >= 0 && selectedIndex < rows.Count ? rows[selectedIndex].EvidenceId : null;
        pointer?.Unregister("inventory-item-");
        foreach (var row in rows)
        {
            if (row != null)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
        }
        rows.Clear();

        if (EvidenceInventory.Instance == null || entryTemplate == null || content == null)
        {
            UpdateEmptyLabel();
            return;
        }

        foreach (var evidenceId in EvidenceInventory.Instance.AllEntryIds)
        {
            var definition = EvidenceInventory.Instance.GetDefinition(evidenceId);
            if (definition == null)
            {
                continue;
            }

            var clone = Instantiate(entryTemplate, content);
            clone.gameObject.SetActive(true);

            bool isHeld = EvidenceInventory.Instance.IsHeld(evidenceId);
            // IsBlockedByOther(this), not plain IsBlocked: this panel itself is already
            // holding the gate at this point (Show() enters it before ever calling
            // RefreshEntries) - see HeldItemCoordinator.EquipEvidence's own comment on
            // why plain IsBlocked would make every entry read as locked, always.
            bool canEquip = definition.heldPrefab != null && !PlayerUIGate.IsBlockedByOther(this) && !SceneTurnover.IsInvestigationBlocked;
            string lockedReason = SceneTurnover.IsInvestigationBlocked ? GameDialogue.Text("closeout-custody-fixed") : definition.heldPrefab == null
                ? GameDialogue.Text("inventory-item-not-holdable")
                : (PlayerUIGate.IsBlockedByOther(this) ? GameDialogue.Text("interaction-blocked") : null);

            clone.Bind(evidenceId, definition, isHeld, canEquip, lockedReason);
            rows.Add(clone);
            int index = rows.Count - 1;
            pointer?.Register("inventory-item-" + index, (RectTransform)clone.transform, Vector2.zero, () => SetSelectedIndex(index));
        }

        UpdateEmptyLabel();
        RefreshLayout();
        int previousIndex = rows.FindIndex(r => r.EvidenceId == previousId);
        SetSelectedIndex(rows.Count > 0 ? Mathf.Max(0, previousIndex) : -1);
    }

    private void UpdateEmptyLabel()
    {
        if (emptyLabel == null)
        {
            return;
        }

        emptyLabel.gameObject.SetActive(rows.Count == 0);
        emptyLabel.text = emptyMessage;
    }

    public void Toggle()
    {
        if (panelRoot.activeSelf)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void Show()
    {
        // Defers to whatever else already owns the screen right now - same contract as
        // UtilityMenuController/ToolWheelController/PhotoAlbumUI.
        if (!panelRoot.activeSelf && PlayerUIGate.IsBlocked)
        {
            return;
        }

        if (!panelRoot.activeSelf)
        {
            LocomotionSuspender.Suspend();
            PlayerUIGate.Enter(this);
        }
        HudFollowCamera.ConfigureShared(panelRoot, this, 300);
        panelRoot.SetActive(true);
        stickWasNeutral = true;
        RefreshEntries();
    }

    public void Hide()
    {
        if (panelRoot.activeSelf)
        {
            LocomotionSuspender.Resume();
            PlayerUIGate.Exit(this);
        }
        panelRoot.SetActive(false);
    }
}
