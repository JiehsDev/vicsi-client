// Assets/_Project/Scripts/RoleSystem/ToolInputLock.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Suspends held-tool and world-interaction input while a pure review panel is open, so
/// a trigger pull meant for the panel can never fire the tool in hand at evidence behind
/// it. Deliberately separate from PlayerUIGate: tools already open their own screens
/// while holding that gate (the Recorder's report, for example) and still need their
/// input then, so PlayerUIGate cannot mean "tools off". Only panels that want tools fully
/// inert take this lock (today: DialogueHistoryUI). Holding it never changes what is
/// equipped or held - tools just skip their Update until it is released.
/// </summary>
public static class ToolInputLock
{
    private static readonly HashSet<object> owners = new();
    private static InputAction modalButtons;
    private static bool waitForRelease;
    private static int releasedFrame = -1;
    public static event System.Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        owners.Clear(); modalButtons?.Dispose(); modalButtons = null;
        waitForRelease = false; releasedFrame = -1;
        OnChanged = null;
    }
    private static bool ReleaseGestureBlocked
    {
        get
        {
            if (waitForRelease && (modalButtons == null || !modalButtons.IsPressed()))
            { waitForRelease = false; releasedFrame = Time.frameCount; }
            return waitForRelease || releasedFrame == Time.frameCount;
        }
    }

    public static bool IsLocked => IsLockedFor(PlayerToolRegistry.CurrentRole);
    public static bool IsLockedFor(ToolType role) => HasUIOwners || SceneTurnover.IsInvestigationBlocked || !ProcedureInterlock.AllowsTool(role);
    public static bool HasUIOwners => owners.Count > 0 || ReleaseGestureBlocked;

    public static void Enter(object owner)
    {
        if (owner != null)
        {
            if (modalButtons == null)
            {
                modalButtons = new InputAction("ViCSI_ModalGestureGuard", InputActionType.Button);
                modalButtons.AddBinding("<XRController>/triggerPressed");
                modalButtons.AddBinding("<XRController>/gripPressed");
                modalButtons.AddBinding("<XRController>{RightHand}/secondaryButton");
                modalButtons.AddBinding("<XRController>{LeftHand}/primaryButton");
                modalButtons.AddBinding("<XRController>{LeftHand}/secondaryButton");
                modalButtons.Enable();
            }
            if (owners.Add(owner)) OnChanged?.Invoke();
        }
    }

    public static void Exit(object owner)
    {
        if (owner != null)
        {
            if (!owners.Remove(owner)) return;
            if (owners.Count == 0 && modalButtons != null && modalButtons.IsPressed()) waitForRelease = true;
            OnChanged?.Invoke();
        }
    }
}
