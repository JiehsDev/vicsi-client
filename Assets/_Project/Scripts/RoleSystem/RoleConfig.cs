// Assets/_Project/Scripts/RoleSystem/RoleConfig.cs
using System;

/// <summary>
/// Identifies a capability/role the single player character can perform - which tool
/// they're using, or which action to attribute a change to - not a separate playable
/// character or scene. A role is free to exist here before any PlayerTool implements
/// it yet; PlayerToolRegistry handles that gracefully everywhere it's looked up.
/// </summary>
public enum RoleId
{
    None,
    Photographer,
    IOC,
    Sketcher,
    EvidenceCollector,
    Recorder,
    TeamLeader,
    CaseAnalyst
}

/// <summary>
/// Records which role the player selected from the menu. Historically this drove
/// RoleSceneLoader's scene pick, back when each role was a separate playable
/// character/scene. Now that one character carries every tool, treat this as the
/// character's starting role - e.g. which tool to start in hand - via
/// PlayerToolRegistry.RequestEquip(SelectedRole).
/// </summary>
public static class RoleConfig
{
    public static RoleId SelectedRole { get; private set; } = RoleId.None;

    public static event Action<RoleId> OnRoleSelected;

    public static void SetRole(RoleId role)
    {
        SelectedRole = role;
        OnRoleSelected?.Invoke(role);
    }
}