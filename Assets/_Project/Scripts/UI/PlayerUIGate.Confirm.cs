using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Raw XR polling and Meta's UnityEvent jump share this owner. No EventSystem dependency.
public static partial class PlayerUIGate
{
    public const int CriticalPriority = 600, ReasoningPriority = 500, DialoguePriority = 400,
        InformationPriority = 300, ProceduralFeedbackPriority = 250, InteractionPriority = 200, AmbientPriority = 100;
    private sealed class ConfirmOwner
    {
        public MonoBehaviour owner;
        public InputAction action;
        public Func<bool> available;
        public int priority;
    }
    private static readonly List<ConfirmOwner> confirmOwners = new();
    private static readonly Dictionary<object, int> activatedFrames = new();
    private static InputAction rawConfirm;
    private static bool confirmHeldConsumed;
    private static int consumedFrame = -1, sampledFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetConfirm()
    {
        rawConfirm?.Dispose(); rawConfirm = null;
        blockers.Clear(); activatedFrames.Clear(); confirmOwners.Clear();
        confirmHeldConsumed = false; consumedFrame = sampledFrame = -1;
        SceneManager.sceneLoaded -= InstallJumpRouting;
        SceneManager.sceneLoaded += InstallJumpRouting;
    }
    private static void InstallJumpRouting(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var wrapper in root.GetComponentsInChildren<Oculus.Interaction.ActiveStateUnityEventWrapper>(true))
                UIConfirmJumpRelay.Install(wrapper);
    }
    private static void EnsureConfirm()
    {
        if (rawConfirm != null) return;
        rawConfirm = new InputAction("ViCSI_ConfirmOwnership", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        rawConfirm.Enable();
    }
    private static bool ConfirmHeld
    {
        get { EnsureConfirm(); return rawConfirm.IsPressed() || OVRInput.Get(OVRInput.RawButton.A); }
    }
    private static void SampleConfirm()
    {
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount;
        if (confirmHeldConsumed && !ConfirmHeld)
        {
            confirmHeldConsumed = false;
            // The release frame is still part of the consumed gesture.
            consumedFrame = Time.frameCount;
        }
    }
    private static void ProtectHeldConfirm()
    {
        SampleConfirm();
        if (ConfirmHeld) { confirmHeldConsumed = true; consumedFrame = Time.frameCount; }
    }
    public static InputAction CreateConfirmAction(MonoBehaviour owner, string name, Func<bool> available, int priority)
    {
        EnsureConfirm();
        var action = new InputAction(name, InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        confirmOwners.Add(new ConfirmOwner { owner = owner, action = action, available = available, priority = priority });
        return action;
    }
    public static object ConfirmOwnerToken
    {
        get
        {
            ConfirmOwner best = null;
            for (int i = confirmOwners.Count - 1; i >= 0; i--)
            {
                var entry = confirmOwners[i];
                if (entry.owner == null) { confirmOwners.RemoveAt(i); continue; }
                if (!entry.owner.isActiveAndEnabled || !entry.action.enabled || !entry.available()) continue;
                if (IsBlocked && !blockers.Contains(entry.owner)) continue;
                if (best == null || entry.priority > best.priority
                    || (entry.priority == best.priority && entry.owner.GetEntityId().GetHashCode() < best.owner.GetEntityId().GetHashCode())) best = entry;
            }
            return best?.owner;
        }
    }
    public static bool IsUIConsumingConfirm => ConfirmOwnerToken != null;
    public static bool CanUsePanel(object owner)
    {
        if (HudFollowCamera.IsOwnerSuppressed(owner)) return false;
        object top = ConfirmOwnerToken;
        return top == null ? !IsBlockedByOther(owner) : Equals(top, owner);
    }
    public static bool TryConsumeConfirm(object owner, InputAction action)
    {
        SampleConfirm();
        if (action == null || !action.WasPressedThisFrame() || !Equals(ConfirmOwnerToken, owner)
            || confirmHeldConsumed || consumedFrame == Time.frameCount || HudFollowCamera.IsOwnerSuppressed(owner)) return false;
        // Opening on A may show the panel before its Update runs. Never select on that press.
        if (activatedFrames.TryGetValue(owner, out int opened) && opened == Time.frameCount)
        { ProtectHeldConfirm(); return false; }
        confirmHeldConsumed = true; consumedFrame = Time.frameCount;
        InteractionSfx.Select();
        return true;
    }
    public static bool SuppressGameplayJump
    {
        get
        {
            SampleConfirm();
            if (IsUIConsumingConfirm) ProtectHeldConfirm();
            return IsUIConsumingConfirm || confirmHeldConsumed || consumedFrame == Time.frameCount;
        }
    }
}
