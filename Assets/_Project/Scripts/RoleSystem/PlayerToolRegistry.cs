// Assets/_Project/Scripts/RoleSystem/PlayerToolRegistry.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static directory of every PlayerTool currently in the scene, keyed by RoleId.
/// This is what makes "one character, many roles" work in code: instead of a
/// separate playable character per role, any system asks this registry for
/// "the Photographer tool" or "whatever tool is currently in hand" and always gets
/// a safe answer - null plus a warning, never an exception - even for roles that
/// don't have a tool built yet. New tools register themselves; nothing here needs
/// to be edited when one is added.
/// </summary>
public static class PlayerToolRegistry
{
    private static readonly Dictionary<RoleId, PlayerTool> tools = new();

    public static RoleId CurrentRole { get; private set; } = RoleId.None;
    public static PlayerTool CurrentTool { get; private set; }

    /// <summary>The role currently attached to a hand via ToggleEquip (the tool wheel), or None if hands are empty.</summary>
    public static RoleId VirtuallyEquippedRole { get; private set; } = RoleId.None;

    /// <summary>Every PlayerTool currently registered, keyed by role - e.g. for a tool wheel to list what's available.</summary>
    public static IReadOnlyDictionary<RoleId, PlayerTool> AllTools => tools;

    /// <summary>Fired whenever a tool becomes the character's current tool (grabbed, or RequestEquip()'d).</summary>
    public static event Action<RoleId, PlayerTool> OnToolGrabbed;

    /// <summary>Fired whenever a virtually-equipped tool is holstered (empty hands) via ToggleEquip/HolsterCurrent.</summary>
    public static event Action<RoleId, PlayerTool> OnToolHolstered;

    /// <summary>Fired whenever a tool registers itself (e.g. a HUD wants to list available roles).</summary>
    public static event Action<PlayerTool> OnToolRegistered;

    // Unity can keep static fields/subscribers alive across scene loads (or Play
    // sessions, with domain reload disabled), which would otherwise leak stale
    // destroyed-tool references. Clearing at the start of every Play session keeps
    // lookups honest.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlaySessionStart()
    {
        tools.Clear();
        CurrentRole = RoleId.None;
        CurrentTool = null;
        VirtuallyEquippedRole = RoleId.None;
        OnToolGrabbed = null;
        OnToolHolstered = null;
        OnToolRegistered = null;
    }

    public static void Register(PlayerTool tool)
    {
        if (tool == null || tool.ToolRole == RoleId.None)
        {
            return;
        }

        if (tools.TryGetValue(tool.ToolRole, out var existing) && existing != null && existing != tool)
        {
            Debug.LogWarning($"[PlayerToolRegistry] Multiple tools registered for role {tool.ToolRole} " +
                $"('{existing.name}' and '{tool.name}'); lookups will keep returning '{existing.name}'.", tool);
            return;
        }

        tools[tool.ToolRole] = tool;
        OnToolRegistered?.Invoke(tool);
    }

    public static void Unregister(PlayerTool tool)
    {
        if (tool == null)
        {
            return;
        }

        if (tools.TryGetValue(tool.ToolRole, out var existing) && existing == tool)
        {
            tools.Remove(tool.ToolRole);
        }

        if (CurrentTool == tool)
        {
            CurrentTool = null;
            CurrentRole = RoleId.None;
        }

        if (VirtuallyEquippedRole == tool.ToolRole)
        {
            VirtuallyEquippedRole = RoleId.None;
        }
    }

    /// <summary>Returns the tool registered for a role, or null if none exists yet - never throws.</summary>
    public static PlayerTool GetTool(RoleId role) => tools.TryGetValue(role, out var tool) ? tool : null;

    /// <summary>Returns the first registered tool assignable to T, or null - never throws.</summary>
    public static T GetTool<T>() where T : PlayerTool
    {
        foreach (var tool in tools.Values)
        {
            if (tool is T match)
            {
                return match;
            }
        }
        return null;
    }

    /// <summary>
    /// Requests that a role become the character's current tool. Returns false (and
    /// logs a warning) instead of throwing when no tool for that role exists yet, so
    /// wiring up a future role's menu entry early can never break the build.
    /// </summary>
    public static bool RequestEquip(RoleId role)
    {
        var tool = GetTool(role);
        if (tool == null)
        {
            Debug.LogWarning($"[PlayerToolRegistry] RequestEquip({role}) - no tool registered for that role yet.");
            return false;
        }

        tool.RequestEquip();
        return true;
    }

    /// <summary>
    /// Equips/holsters a role from the tool wheel: selecting the role already in-hand
    /// holsters it (empty hands, a true on/off toggle); selecting a different one
    /// holsters whatever was equipped first, then attaches the new tool to handAnchor.
    /// Returns false (and logs a warning) instead of throwing when no tool for that
    /// role exists yet.
    /// </summary>
    public static bool ToggleEquip(RoleId role, Transform handAnchor)
    {
        var tool = GetTool(role);
        if (tool == null)
        {
            Debug.LogWarning($"[PlayerToolRegistry] ToggleEquip({role}) - no tool registered for that role yet.");
            return false;
        }

        if (VirtuallyEquippedRole == role)
        {
            HolsterCurrent();
            return true;
        }

        HolsterCurrent();
        tool.EquipToHand(handAnchor);
        VirtuallyEquippedRole = role;
        NotifyToolGrabbed(tool);
        return true;
    }

    /// <summary>Holsters whatever tool is currently virtually equipped, if any. Safe no-op otherwise.</summary>
    public static void HolsterCurrent()
    {
        if (VirtuallyEquippedRole == RoleId.None)
        {
            return;
        }

        var role = VirtuallyEquippedRole;
        var tool = GetTool(role);
        tool?.Holster();
        VirtuallyEquippedRole = RoleId.None;

        if (CurrentTool == tool)
        {
            CurrentTool = null;
            CurrentRole = RoleId.None;
        }

        if (tool != null)
        {
            OnToolHolstered?.Invoke(role, tool);
        }
    }

    internal static void NotifyToolGrabbed(PlayerTool tool)
    {
        if (tool == null)
        {
            return;
        }

        // A physical grab (or a different wheel selection) always wins over whatever
        // was virtually equipped before - holster it so it doesn't sit mid-air
        // attached to a hand anchor while a different tool is now in use.
        if (VirtuallyEquippedRole != RoleId.None && VirtuallyEquippedRole != tool.ToolRole)
        {
            GetTool(VirtuallyEquippedRole)?.Holster();
            VirtuallyEquippedRole = RoleId.None;
        }

        CurrentTool = tool;
        CurrentRole = tool.ToolRole;
        OnToolGrabbed?.Invoke(CurrentRole, tool);
    }
}
