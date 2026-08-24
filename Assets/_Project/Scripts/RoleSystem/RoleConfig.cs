// Assets/_Project/Scripts/RoleSystem/RoleConfig.cs
using System;

public enum RoleId
{
    None,
    Photographer,
    IOC
}

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